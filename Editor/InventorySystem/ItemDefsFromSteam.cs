using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SteamToys.Runtime.InventorySystem;
using SteamToys.Runtime.InventorySystem.Converters;
using SteamToys.Runtime.InventorySystem.ExchangeVariable;
using SteamToys.Runtime.InventorySystem.ExchangeVariable.Materials;
using Steamworks;
using UnityEditor;
using UnityEngine;
using Material = SteamToys.Runtime.InventorySystem.ExchangeVariable.Material;

namespace SteamToys.Editor.InventorySystem
{
    /// <summary>
    /// Makes and updates the definitions of <see cref="SteamItemsDB"/> from the ones published on Steam, as the Steam
    /// client of the signed-in account sees them.
    /// <para>
    /// The client hands out only part of the schema: the presentation, the trading flags, <c>tags</c> (kept as
    /// raw tags where the class has no typed tag for them),
    /// <c>exchange</c> and <c>tag_generators</c>. The contents of bundles and generators, prices, promo rules, drop
    /// settings, the purchase limit and <c>hidden</c> never reach it, so those fields of a definition are left as they
    /// are, and a definition made here starts with them empty.
    /// </para>
    /// </summary>
    internal static class ItemDefsFromSteam
    {
        private const char LIST_SEPARATOR = ';';
        private const char MATERIAL_SEPARATOR = ',';
        private const char ITEM_QUANTITY_SEPARATOR = 'x';
        private const char TAG_QUANTITY_SEPARATOR = '*';
        private const char TAG_SEPARATOR = ':';
        private const uint PROPERTY_BUFFER_SIZE = 8192;

        private static readonly Dictionary<ItemType, Type> BaseTypes = new()
        {
            { ItemType.Item, typeof(Item) },
            { ItemType.Bundle, typeof(Bundle) },
            { ItemType.Generator, typeof(Generator) },
            { ItemType.PlaytimeGenerator, typeof(PlaytimeGenerator) },
            { ItemType.TagGenerator, typeof(TagGenerator) }
        };

        private static readonly Type[] TaggedGenericTypes =
            { typeof(Item<>), typeof(Bundle<>), typeof(Generator<>), typeof(PlaytimeGenerator<>) };

        /// <summary>One definition as the Steam client reports it.</summary>
        internal sealed class SteamDefinition
        {
            public ulong ItemDefId;
            public ItemType Type;
            public Dictionary<string, string> Properties;

            public string Get(string name) => Properties.TryGetValue(name, out var value) ? value ?? string.Empty : string.Empty;

            public string Name => Get("name");
        }

        /// <summary>What a run changed, and what it could not take over.</summary>
        internal sealed class Report
        {
            public readonly List<ItemDef> Created = new();
            public readonly List<ItemDef> Updated = new();
            public readonly List<string> Problems = new();
        }

        #region Reading

        /// <summary>
        /// Every definition the Steam client has loaded for the running app, or null without a Steam session.
        /// </summary>
        internal static List<SteamDefinition> ReadAll()
        {
            if (!Inventory.Initialized)
                return null;

            uint count = 0;

            if (!SteamInventory.GetItemDefinitionIDs(null, ref count) || count == 0)
            {
                // Asks for them, so a second try finds them once Steam answered.
                SteamInventory.LoadItemDefinitions();
                return new List<SteamDefinition>();
            }

            var ids = new SteamItemDef_t[count];
            SteamInventory.GetItemDefinitionIDs(ids, ref count);

            var definitions = new List<SteamDefinition>();

            foreach (var id in ids.Take((int)count))
            {
                var properties = new Dictionary<string, string>();

                foreach (var name in Read(id, null).Split(MATERIAL_SEPARATOR))
                {
                    var trimmed = name.Trim();

                    if (trimmed.Length > 0)
                        properties[trimmed] = Read(id, trimmed);
                }

                definitions.Add(new SteamDefinition
                {
                    ItemDefId = (ulong)id.m_SteamItemDef,
                    Type = ItemTypeConverter.FromString(properties.TryGetValue("type", out var type) ? type : string.Empty),
                    Properties = properties
                });
            }

            return definitions.OrderBy(definition => definition.ItemDefId).ToList();
        }

        private static string Read(SteamItemDef_t id, string name)
        {
            var size = PROPERTY_BUFFER_SIZE;

            return SteamInventory.GetItemDefinitionProperty(id, name, out var value, ref size) ? value ?? string.Empty : string.Empty;
        }

        #endregion

        #region Creating

        /// <summary>
        /// Makes a definition in the folder of the DB for every one of <paramref name="definitions"/> it lacks, and
        /// overwrites the fields Steam reports of the ones it has. References between definitions are resolved after
        /// all of them exist, so their order does not matter.
        /// </summary>
        internal static Report Apply(SteamItemsDB db, IReadOnlyList<SteamDefinition> definitions)
        {
            var report = new Report();
            var assets = new List<(ItemDef asset, SteamDefinition steam)>();
            var existing = ById(db);

            EnsureFolder(SteamItemsDB.AssetsFolder);

            Undo.SetCurrentGroupName("Create Item Definitions From Steam");
            var group = Undo.GetCurrentGroup();

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var steam in definitions)
                {
                    existing.TryGetValue(steam.ItemDefId, out var asset);

                    if (asset && asset.Type != steam.Type)
                    {
                        report.Problems.Add($"{asset.name} (itemdefid {steam.ItemDefId}) is a {asset.Type} here and a {steam.Type} " +
                                            "on Steam, so it was left alone.");
                        continue;
                    }

                    if (asset)
                    {
                        Undo.RecordObject(asset, "Create Item Definitions From Steam");
                        report.Updated.Add(asset);
                    }
                    else
                    {
                        asset = CreateAsset(steam, report);

                        if (!asset)
                            continue;

                        report.Created.Add(asset);
                    }

                    ApplyFields(asset, steam, report);
                    assets.Add((asset, steam));
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            db.Reload();

            var all = ById(db);

            // The second pass: every definition exists now, so references find their targets.
            foreach (var (asset, steam) in assets)
                ApplyReferences(asset, steam, all, report);

            foreach (var (asset, _) in assets)
                EditorUtility.SetDirty(asset);

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);

            db.Reload();

            return report;
        }

        private static ItemDef CreateAsset(SteamDefinition steam, Report report)
        {
            var type = ChooseClass(steam, report);

            if (type == null)
                return null;

            var asset = (ItemDef)ScriptableObject.CreateInstance(type);
            asset.ItemDefId = steam.ItemDefId;

            var name = string.IsNullOrWhiteSpace(steam.Name) ? $"Item {steam.ItemDefId}" : steam.Name;
            var path = AssetDatabase.GenerateUniqueAssetPath($"{SteamItemsDB.AssetsFolder}/{SanitizeFileName(name)}.asset");

            AssetDatabase.CreateAsset(asset, path);
            Undo.RegisterCreatedObjectUndo(asset, "Create Item Definitions From Steam");

            return asset;
        }

        /// <summary>
        /// The class for a new definition: of its type, preferring a class of the game whose tags can hold the
        /// categories the definition has, and the plain class of the package otherwise.
        /// </summary>
        private static Type ChooseClass(SteamDefinition steam, Report report)
        {
            var baseType = BaseTypes[steam.Type];

            if (steam.Type == ItemType.TagGenerator)
            {
                var category = steam.Get("tag_generator_name");
                var tagGenerator = GetConcreteTypes(baseType).FirstOrDefault(type => GetTagGeneratorName(type) == category);

                if (tagGenerator == null)
                    report.Problems.Add($"Tag generator {steam.ItemDefId} ({steam.Name}) was skipped: no TagGenerator<TEnum> class " +
                                        $"of the game has the category \"{category}\".");

                return tagGenerator;
            }

            var categories = GetCategories(steam.Get("tags"));

            if (categories.Count == 0)
                return baseType;

            var best = GetConcreteTypes(baseType)
                .Select(type => (type, covered: GetTagCategories(type).Count(categories.Contains)))
                .Where(candidate => candidate.covered > 0)
                .OrderByDescending(candidate => candidate.covered)
                .ThenBy(candidate => candidate.type.Name)
                .FirstOrDefault();

            return best.type ?? baseType;
        }

        private static void ApplyFields(ItemDef asset, SteamDefinition steam, Report report)
        {
            asset.Name = steam.Name;

            if (asset is GrantableItemDef grantable)
            {
                grantable.Description = steam.Get("description");

                // Tags its class has no typed tag for are kept as raw tags, so none is lost.
                grantable.Tags = steam.Get("tags");
            }

            if (asset is StoreItemDef store)
            {
                store.DisplayType = steam.Get("display_type");
                store.IconUrl = new Url(steam.Get("icon_url"));
                store.IconUrlLarge = new Url(steam.Get("icon_url_large"));
                store.NameColor = ParseColor(steam.Get("name_color"));
                store.BackgroundColor = ParseColor(steam.Get("background_color"));
                store.StoreHidden = ParseBool(steam.Get("store_hidden"));
            }

            if (asset is Item item)
            {
                item.Tradable = ParseBool(steam.Get("tradable"));
                item.Marketable = ParseBool(steam.Get("marketable"));
                item.GameOnly = ParseBool(steam.Get("game_only"));
                item.AutoStack = ParseBool(steam.Get("auto_stack"));
            }
        }

        private static void ApplyReferences(ItemDef asset, SteamDefinition steam, IReadOnlyDictionary<ulong, ItemDef> db, Report report)
        {
            if (asset is CraftableItemDef craftable)
            {
                var exchange = ParseExchange(steam.Get("exchange"), db, out var problem);

                if (problem != null)
                    report.Problems.Add($"{asset.name}: its exchange was left as it was, since {problem}");
                else
                    craftable.Exchange = exchange;
            }

            var tagGeneratorIds = steam.Get("tag_generators");

            if (asset is not (Bundle or Generator))
                return;

            var tagGenerators = new List<TagGenerator>();

            foreach (var id in tagGeneratorIds.Split(LIST_SEPARATOR, StringSplitOptions.RemoveEmptyEntries))
            {
                if (ulong.TryParse(id.Trim(), out var itemDefId) && db.TryGetValue(itemDefId, out var found) && found is TagGenerator tagGenerator)
                    tagGenerators.Add(tagGenerator);
                else
                    report.Problems.Add($"{asset.name}: tag generator {id.Trim()} is not in the DB, so it was left out.");
            }

            switch (asset)
            {
                case Bundle bundle:
                    bundle.TagGenerators = tagGenerators;
                    break;
                case Generator generator:
                    generator.TagGenerators = tagGenerators;
                    break;
            }
        }

        #endregion

        #region Exchange

        /// <summary>
        /// Reads an exchange string, e.g. <c>"100,101;rarity:common*5"</c>. Returns null with <paramref name="problem"/>
        /// set when a material cannot be represented, so a half-read exchange never replaces a whole one.
        /// </summary>
        private static Exchange ParseExchange(string value, IReadOnlyDictionary<ulong, ItemDef> db, out string problem)
        {
            problem = null;

            var exchange = new Exchange();

            foreach (var recipeText in value.Split(LIST_SEPARATOR, StringSplitOptions.RemoveEmptyEntries))
            {
                var recipe = new Recipe();

                foreach (var materialText in recipeText.Split(MATERIAL_SEPARATOR, StringSplitOptions.RemoveEmptyEntries))
                {
                    var material = ParseMaterial(materialText.Trim(), db, out problem);

                    if (material == null)
                        return null;

                    recipe.Add(material);
                }

                if (recipe.Count > 0)
                    exchange.Add(recipe);
            }

            return exchange;
        }

        private static Material ParseMaterial(string text, IReadOnlyDictionary<ulong, ItemDef> db, out string problem)
        {
            problem = null;

            if (text.Contains(TAG_SEPARATOR))
            {
                var quantityIndex = text.IndexOf(TAG_QUANTITY_SEPARATOR);
                var pair = quantityIndex < 0 ? text : text[..quantityIndex];
                var quantity = quantityIndex < 0 ? 1u : uint.TryParse(text[(quantityIndex + 1)..], out var parsed) ? parsed : 0u;

                var category = pair[..pair.IndexOf(TAG_SEPARATOR)].Trim();
                var tagValue = pair[(pair.IndexOf(TAG_SEPARATOR) + 1)..].Trim();

                foreach (var type in GetTagMaterialTypes())
                {
                    var material = (Material)Activator.CreateInstance(type);
                    var tag = (Tag)type.GetProperty("Tag")!.GetValue(material);

                    if (tag == null || tag.GetName() != category)
                        continue;

                    if (!tag.SetValue(tagValue))
                    {
                        problem = $"\"{tagValue}\" is not a value of the {category} enum.";
                        return null;
                    }

                    type.GetProperty("Quantity")!.SetValue(material, quantity);

                    return material;
                }

                problem = $"no TagMaterial<TEnum> class of the game has the category \"{category}\" (for \"{text}\").";
                return null;
            }

            var separator = text.IndexOf(ITEM_QUANTITY_SEPARATOR);
            var idText = separator < 0 ? text : text[..separator];
            var itemQuantity = separator < 0 ? 1u : uint.TryParse(text[(separator + 1)..], out var parsedQuantity) ? parsedQuantity : 0u;

            if (!ulong.TryParse(idText, out var itemDefId) || !db.TryGetValue(itemDefId, out var found) || found is not Item item)
            {
                problem = $"item {idText} is not an item in the DB.";
                return null;
            }

            return new ItemMaterial { Item = item, Quantity = itemQuantity };
        }

        #endregion

        #region Helpers

        private static Dictionary<ulong, ItemDef> ById(SteamItemsDB db)
        {
            var byId = new Dictionary<ulong, ItemDef>();

            foreach (var item in db.Items)
            {
                if (item)
                    byId.TryAdd(item.ItemDefId, item);
            }

            return byId;
        }

        private static string GetTagGeneratorName(Type type)
        {
            var instance = (TagGenerator)ScriptableObject.CreateInstance(type);

            try
            {
                return instance.TagGeneratorName;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// The classes a definition of <paramref name="baseType"/> can be made with, leaving out those under a more
        /// specific base type, such as playtime generators under <see cref="Generator"/>.
        /// </summary>
        private static IEnumerable<Type> GetConcreteTypes(Type baseType) =>
            TypeCache.GetTypesDerivedFrom(baseType).Append(baseType)
                .Where(type => !type.IsAbstract && !type.IsGenericTypeDefinition)
                .Where(type => !BaseTypes.Values.Any(other =>
                    other != baseType && baseType.IsAssignableFrom(other) && other.IsAssignableFrom(type)));

        private static IEnumerable<Type> GetTagMaterialTypes() =>
            TypeCache.GetTypesDerivedFrom<Material>()
                .Where(type => !type.IsAbstract && !type.IsGenericTypeDefinition && IsTagMaterial(type));

        private static bool IsTagMaterial(Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(TagMaterial<>))
                    return true;
            }

            return false;
        }

        /// <summary>The tag categories a definition class can hold, from the tags type it is built on.</summary>
        private static HashSet<string> GetTagCategories(Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (!current.IsGenericType || !TaggedGenericTypes.Contains(current.GetGenericTypeDefinition()))
                    continue;

                var tags = (Tags)Activator.CreateInstance(current.GetGenericArguments()[0]);

                return new HashSet<string>(tags.GetTags().Where(tag => tag != null).Select(tag => tag.GetName()));
            }

            return new HashSet<string>();
        }

        private static HashSet<string> GetCategories(string tags) =>
            new(tags.Split(LIST_SEPARATOR, StringSplitOptions.RemoveEmptyEntries)
                .Select(pair => pair.Split(TAG_SEPARATOR)[0].Trim())
                .Where(category => category.Length > 0));

        // Six hex digits, as in the schema; anything else stays unset.
        private static Color32 ParseColor(string hex) =>
            hex is { Length: 6 } && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb)
                ? new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 0xFF)
                : default;

        // The client reports flags as "1" / "0"; the schema writes them as "true" / "false".
        private static bool ParseBool(string value) =>
            value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

        private static string SanitizeFileName(string name)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');

            return name.Trim();
        }

        internal static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            var parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        #endregion
    }
}
