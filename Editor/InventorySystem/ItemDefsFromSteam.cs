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
    /// This takes the presentation, the trading flags, <c>tags</c>, <c>exchange</c>, <c>tag_generators</c> and the
    /// values of a tag generator, each only for the types that use it. Tags a class cannot hold are reported, as are
    /// the tags of a bundle, which Steam does not pass on. The client never gets <c>hidden</c>, the contents of
    /// generators or
    /// <c>container_contents_generator</c>; the other fields are left as they are, and a definition made here starts
    /// with them empty.
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

        // The class of the package for each type, taken when no class of the game is a better fit.
        private static readonly Dictionary<ItemType, Type> PackageTypes = new() {
            { ItemType.Item, typeof(DefaultItemDef) },
            { ItemType.Bundle, typeof(BundleDef) },
            { ItemType.Generator, typeof(DefaultGeneratorDef) },
            { ItemType.PlaytimeGenerator, typeof(DefaultPlaytimeGeneratorDef) }
        };

        private static readonly Dictionary<Type, ItemType> ItemTypesOfClasses = new();

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
        /// The class for a new definition: of its type, and with tags, the class of the game whose tags type takes
        /// only a fixed set of categories that holds every one the definition has, by name when several do; the class
        /// of the package otherwise, whose <see cref="DefaultTags"/> takes any category. A tag generator takes the class
        /// of its category, or <see cref="DefaultTagGeneratorDef"/> when the game has none.
        /// </summary>
        private static Type ChooseClass(SteamDefinition steam, Report report)
        {
            if (steam.Type == ItemType.TagGenerator)
            {
                var category = steam.Get("tag_generator_name");
                // A class of the game for the category, or the one of the package that takes any category.
                return GetConcreteTypes(ItemType.TagGenerator).FirstOrDefault(type => GetTagName(type) == category) ??
                       typeof(DefaultTagGeneratorDef);
            }

            var packageType = PackageTypes[steam.Type];
            var categories = GetCategories(steam.Get("tags"));

            if (categories.Count == 0 || CreateTags(packageType) == null)
                return packageType;

            return GetConcreteTypes(steam.Type)
                .Select(type => (type, tags: CreateTags(type)))
                .Where(candidate => candidate.tags != null && HoldsFixedCategories(candidate.tags) && categories.All(candidate.tags.ContainsKey))
                .OrderBy(candidate => candidate.type.Name)
                .Select(candidate => candidate.type)
                .FirstOrDefault() ?? packageType;
        }

        // A tags type that turns away a category nobody would name takes a fixed set, so holding the categories of a
        // definition says something about its class.
        private static bool HoldsFixedCategories(Tags tags) => !tags.ContainsKey("\u0001");

        private static void ApplyFields(ItemDef asset, SteamDefinition steam, Report report)
        {
            asset.Name = steam.Name;

            if (asset.SchemaSupportsDescription())
                asset.Description = steam.Get("description");

            var steamTags = steam.Get("tags");
            var tags = asset.SchemaSupportsTags() ? CreateTags(asset.GetType()) : null;
            var notHeld = tags != null
                ? TagsConverter.FromString(steamTags, tags)
                : new List<string>(steamTags.Split(LIST_SEPARATOR, StringSplitOptions.RemoveEmptyEntries));

            // if (tags != null)
            //     asset.Tags = tags;

            if (notHeld.Count > 0)
                report.Problems.Add(asset.SchemaSupportsTags()
                    ? $"{asset.name}: its tags type does not take {string.Join(";", notHeld)}, so they were left out."
                    : $"{asset.name}: Steam has tags on this {asset.Type} ({string.Join(";", notHeld)}), which it does not pass on, so they were left out.");

            if (asset is DefaultTagGeneratorDef defaultTagGenerator)
                defaultTagGenerator.SetTagName(steam.Get("tag_generator_name"));

            if (asset is TagGeneratorDef tagGenerator)
                ApplyTagValues(tagGenerator, steam.Get("tag_generator_values"), report);

            if (asset.SchemaSupportsDisplayType())
            {
                asset.DisplayType = steam.Get("display_type");
                asset.IconUrl = new Url(steam.Get("icon_url"));
                asset.IconUrlLarge = new Url(steam.Get("icon_url_large"));
                asset.NameColor = ParseColor(steam.Get("name_color"));
                asset.BackgroundColor = ParseColor(steam.Get("background_color"));
                asset.StoreHidden = ParseBool(steam.Get("store_hidden"));
            }

            if (asset.SchemaSupportsTradable())
            {
                asset.Tradable = ParseBool(steam.Get("tradable"));
                asset.Marketable = ParseBool(steam.Get("marketable"));
                asset.GameOnly = ParseBool(steam.Get("game_only"));
                asset.AutoStack = ParseBool(steam.Get("auto_stack"));
            }
        }

        private static void ApplyReferences(ItemDef asset, SteamDefinition steam, IReadOnlyDictionary<ulong, ItemDef> db, Report report)
        {
            if (asset.SchemaSupportsExchange())
            {
                var exchange = ParseExchange(steam.Get("exchange"), db, out var problem);

                if (problem != null)
                    report.Problems.Add($"{asset.name}: its exchange was left as it was, since {problem}");
                else
                    asset.Exchange = exchange;
            }

            var tagGeneratorIds = steam.Get("tag_generators");

            if (!asset.SchemaSupportsTagGenerators())
                return;

            var tagGenerators = new List<TagGeneratorDef>();

            foreach (var id in tagGeneratorIds.Split(LIST_SEPARATOR, StringSplitOptions.RemoveEmptyEntries))
            {
                if (ulong.TryParse(id.Trim(), out var itemDefId) && db.TryGetValue(itemDefId, out var found) && found is TagGeneratorDef tagGenerator)
                    tagGenerators.Add(tagGenerator);
                else
                    report.Problems.Add($"{asset.name}: tag generator {id.Trim()} is not in the DB, so it was left out.");
            }

            asset.TagGenerators = tagGenerators;
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

        /// <summary>
        /// Sets the values of a tag generator and their odds from Steam, e.g. <c>"legendary:1;common:9"</c>. Values it
        /// does not take, e.g. ones its enum does not have, and repeats, are reported and left out.
        /// </summary>
        private static void ApplyTagValues(TagGeneratorDef tagGeneratorDef, string values, Report report)
        {
            var tagValuesAndChances = new Dictionary<string, uint>();

            foreach (var pair in values.Split(LIST_SEPARATOR, StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = pair.IndexOf(TAG_SEPARATOR);
                var tagValue = (separator < 0 ? pair : pair[..separator]).Trim();
                var chance = separator < 0 ? 1u : uint.TryParse(pair[(separator + 1)..].Trim(), out var parsed) ? parsed : 0u;

                if (!tagValuesAndChances.TryAdd(tagValue, chance))
                    report.Problems.Add($"{tagGeneratorDef.name}: Steam lists {tagValue} more than once; the first chance was kept.");
            }

            if (tagValuesAndChances.Count == 0)
                report.Problems.Add($"{tagGeneratorDef.name}: Steam reported no values for this tag generator.");

            var notHeld = tagGeneratorDef.SetTagValuesAndChances(tagValuesAndChances);

            if (notHeld.Count > 0)
                report.Problems.Add($"{tagGeneratorDef.name}: {string.Join(", ", notHeld)} are not values the " +
                                    $"{tagGeneratorDef.TagName} tag generator takes, so they were left out.");
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

            if (!ulong.TryParse(idText, out var itemDefId) || !db.TryGetValue(itemDefId, out var found) || found is not DefaultItemDef item)
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

        private static string GetTagName(Type type)
        {
            var instance = (TagGeneratorDef)ScriptableObject.CreateInstance(type);

            try
            {
                return instance.TagName;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// The classes a definition of <paramref name="type"/> can be made with: every concrete ItemDef class of that
        /// type, those of the package included.
        /// </summary>
        private static IEnumerable<Type> GetConcreteTypes(ItemType type) =>
            TypeCache.GetTypesDerivedFrom<ItemDef>()
                .Where(candidate => !candidate.IsAbstract && !candidate.IsGenericTypeDefinition && GetItemType(candidate) == type);

        private static ItemType GetItemType(Type type)
        {
            if (ItemTypesOfClasses.TryGetValue(type, out var itemType))
                return itemType;

            var instance = (ItemDef)ScriptableObject.CreateInstance(type);

            try
            {
                return ItemTypesOfClasses[type] = instance.Type;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        /// <summary>A new tags object of the type <paramref name="type"/> keeps its tags in, or null without tags.</summary>
        private static Tags CreateTags(Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(ItemDef<>))
                    return (Tags)Activator.CreateInstance(current.GetGenericArguments()[0]);
            }

            return null;
        }

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
