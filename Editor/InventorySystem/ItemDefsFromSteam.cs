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

        // What a class holds, read once: each takes making an asset, and a run asks for every definition.
        private static readonly Dictionary<Type, string> TagNamesOfClasses = new();
        private static readonly Dictionary<Type, Tags> OwnedTagsOfClasses = new();

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

            return ids.Take((int)count)
                .Select(id => Read((ulong)id.m_SteamItemDef))
                .Where(definition => definition != null)
                .OrderBy(definition => definition.ItemDefId)
                .ToList();
        }

        /// <summary>
        /// The definition with the given itemdefid as the Steam client has it, or null when it has none. Also finds
        /// definitions the client knows but does not list, e.g. those with an itemdefid in the Workshop range.
        /// Needs a Steam session.
        /// </summary>
        internal static SteamDefinition Read(ulong itemDefId)
        {
            var id = new SteamItemDef_t((int)itemDefId);
            var properties = new Dictionary<string, string>();

            foreach (var name in Read(id, null).Split(MATERIAL_SEPARATOR))
            {
                var trimmed = name.Trim();

                if (trimmed.Length > 0)
                    properties[trimmed] = Read(id, trimmed);
            }

            if (properties.Count == 0)
                return null;

            return new SteamDefinition
            {
                ItemDefId = itemDefId,
                Type = ItemTypeConverter.FromString(properties.TryGetValue("type", out var type) ? type : string.Empty),
                Properties = properties
            };
        }

        private static string Read(SteamItemDef_t id, string name)
        {
            var size = PROPERTY_BUFFER_SIZE;

            return SteamInventory.GetItemDefinitionProperty(id, name, out var value, ref size) ? value ?? string.Empty : string.Empty;
        }

        /// <summary>
        /// Every tag category <paramref name="definitions"/> use, with its values in the order Steam lists them: those
        /// of <c>tags</c>, of tag generators and of the tag materials of <c>exchange</c>.
        /// </summary>
        internal static Dictionary<string, List<string>> CollectTags(IEnumerable<SteamDefinition> definitions)
        {
            var tags = new Dictionary<string, List<string>>();

            foreach (var steam in definitions)
            {
                foreach (var pair in steam.Get("tags").Split(LIST_SEPARATOR, StringSplitOptions.RemoveEmptyEntries))
                    AddTag(tags, pair);

                if (steam.Type == ItemType.TagGenerator)
                {
                    var category = steam.Get("tag_generator_name");

                    foreach (var pair in steam.Get("tag_generator_values").Split(LIST_SEPARATOR, StringSplitOptions.RemoveEmptyEntries))
                        AddTag(tags, $"{category}{TAG_SEPARATOR}{pair.Split(TAG_SEPARATOR)[0]}");
                }

                foreach (var material in steam.Get("exchange").Split(new[] { LIST_SEPARATOR, MATERIAL_SEPARATOR }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (material.Contains(TAG_SEPARATOR))
                        AddTag(tags, material.Split(TAG_QUANTITY_SEPARATOR)[0]);
                }
            }

            return tags;
        }

        /// <summary>
        /// The tag categories of each item and generator of <paramref name="definitions"/> that has any: those it sets
        /// itself, and those its tag generators set on the owned item.
        /// </summary>
        internal static IEnumerable<(ItemType type, HashSet<string> tags, HashSet<string> generatedTags)> CollectTagCategories(
            IReadOnlyList<SteamDefinition> definitions)
        {
            var tagGeneratorNames = GetTagGeneratorNames(definitions);

            foreach (var steam in definitions)
            {
                if (steam.Type is ItemType.Bundle or ItemType.TagGenerator)
                    continue;

                var (tags, generatedTags) = GetTagCategories(steam, tagGeneratorNames);

                if (tags.Count > 0 || generatedTags.Count > 0)
                    yield return (steam.Type, tags, generatedTags);
            }
        }

        private static Dictionary<ulong, string> GetTagGeneratorNames(IEnumerable<SteamDefinition> definitions)
        {
            var names = new Dictionary<ulong, string>();

            foreach (var steam in definitions.Where(steam => steam.Type == ItemType.TagGenerator))
                names[steam.ItemDefId] = steam.Get("tag_generator_name").Trim();

            return names;
        }

        /// <summary>The tag categories a definition sets itself, and those its tag generators set.</summary>
        private static (HashSet<string> tags, HashSet<string> generatedTags) GetTagCategories(SteamDefinition steam,
            IReadOnlyDictionary<ulong, string> tagGeneratorNames)
        {
            var generatedTags = new HashSet<string>(steam.Get("tag_generators")
                .Split(LIST_SEPARATOR, StringSplitOptions.RemoveEmptyEntries)
                .Select(id => ulong.TryParse(id.Trim(), out var itemDefId) && tagGeneratorNames.TryGetValue(itemDefId, out var name) ? name : string.Empty)
                .Where(name => name.Length > 0));

            return (GetCategories(steam.Get("tags")), generatedTags);
        }

        // A "category:value" pair; a value already listed is not listed again.
        private static void AddTag(Dictionary<string, List<string>> tags, string pair)
        {
            var separator = pair.IndexOf(TAG_SEPARATOR);
            var category = (separator < 0 ? pair : pair[..separator]).Trim();
            var value = separator < 0 ? string.Empty : pair[(separator + 1)..].Trim();

            if (category.Length == 0)
                return;

            if (!tags.TryGetValue(category, out var values))
                tags.Add(category, values = new List<string>());

            if (value.Length > 0 && !values.Contains(value))
                values.Add(value);
        }

        #endregion

        #region Classes

        /// <summary>
        /// Every class <paramref name="steam"/> can be made with: of its type, whose tags type holds exactly the
        /// categories in <paramref name="tags"/> and, for an item, whose owned item exactly those in
        /// <paramref name="ownedTags"/>. A class with categories Steam does not use is left out, so is one that lacks
        /// any. A tag generator takes the classes of its category. With tags, the classes of the game come first and
        /// the one of the package, which takes any category, last; without, the other way round.
        /// <para>
        /// <paramref name="tags"/> are the categories the definition sets and <paramref name="ownedTags"/> those its
        /// tag generators set on the owned item; for a tag generator, its category. A bundle asks for none: Steam does
        /// not pass its tags on. <paramref name="definitions"/> are all the ones on Steam, for the tag generators.
        /// </para>
        /// </summary>
        internal static List<Type> FindClasses(SteamDefinition steam, IReadOnlyList<SteamDefinition> definitions,
            out HashSet<string> tags, out HashSet<string> ownedTags)
        {
            IEnumerable<Type> classes;

            if (steam.Type == ItemType.TagGenerator)
            {
                var category = steam.Get("tag_generator_name").Trim();

                tags = new HashSet<string> { category };
                ownedTags = new HashSet<string>();
                classes = GetConcreteTypes(ItemType.TagGenerator)
                    .Where(type => type == typeof(DefaultTagGeneratorDef) || GetTagName(type) == category);
            }
            else
            {
                (tags, ownedTags) = steam.Type == ItemType.Bundle
                    ? (new HashSet<string>(), new HashSet<string>())
                    : GetTagCategories(steam, GetTagGeneratorNames(definitions));

                // A generator is never owned: its tag generators tag the items it makes, which have classes of their own.
                if (steam.Type != ItemType.Item)
                    ownedTags.Clear();

                var required = tags;
                var requiredOwned = ownedTags;

                classes = GetConcreteTypes(steam.Type)
                    .Where(type => HoldsExactly(CreateTags(type), required) && HoldsExactly(GetOwnedTags(type), requiredOwned));
            }

            var hasTags = tags.Count + ownedTags.Count > 0;
            var packageClass = GetPackageClass(steam.Type);

            // Of the package, only the class it makes the type with, e.g. not BundleDef next to DefaultBundleDef.
            return classes
                .Where(type => !IsPackageClass(type) || type == packageClass)
                .OrderBy(type => IsPackageClass(type) == hasTags)
                .ThenBy(type => type.Name)
                .ToList();
        }

        /// <summary>Whether <paramref name="type"/> comes with the package, e.g. <see cref="DefaultItemDef"/>.</summary>
        internal static bool IsPackageClass(Type type) => type.Assembly == typeof(ItemDef).Assembly;

        // DefaultTags takes any category, so it holds every set; a class without tags only the empty one.
        private static bool HoldsExactly(Tags tags, ICollection<string> categories) =>
            tags == null ? categories.Count == 0 : tags is DefaultTags || tags.Keys.ToHashSet().SetEquals(categories);

        /// <summary>The tags object the owned items of <paramref name="type"/> read their tags into, to look at only.</summary>
        private static Tags GetOwnedTags(Type type)
        {
            if (OwnedTagsOfClasses.TryGetValue(type, out var tags))
                return tags;

            var instance = (ItemDef)ScriptableObject.CreateInstance(type);

            try
            {
                return OwnedTagsOfClasses[type] = instance.CreateInventoryItem()?.CreateEmptyTags();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        #endregion

        #region Creating

        /// <summary>
        /// Makes a definition in the folder of the DB for every one of <paramref name="definitions"/> it lacks, and
        /// overwrites the fields Steam reports of the ones it has. References between definitions are resolved after
        /// all of them exist, so their order does not matter. With <paramref name="packageClasses"/>, a new definition
        /// takes the class of the package for its type, e.g. <see cref="DefaultItemDef"/>, and never one of the game.
        /// <paramref name="classes"/> names the class of a new definition by its itemdefid, before either.
        /// </summary>
        internal static Report Apply(SteamItemsDB db, IReadOnlyList<SteamDefinition> definitions, bool packageClasses = false,
            IReadOnlyDictionary<ulong, Type> classes = null)
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
                        asset = CreateAsset(steam, classes != null && classes.TryGetValue(steam.ItemDefId, out var type)
                            ? type
                            : packageClasses ? GetPackageClass(steam.Type) : ChooseClass(steam));

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

        private static ItemDef CreateAsset(SteamDefinition steam, Type type)
        {
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
        private static Type ChooseClass(SteamDefinition steam)
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

        private static Type GetPackageClass(ItemType type) =>
            type == ItemType.TagGenerator ? typeof(DefaultTagGeneratorDef) : PackageTypes[type];

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
            if (TagNamesOfClasses.TryGetValue(type, out var tagName))
                return tagName;

            var instance = (TagGeneratorDef)ScriptableObject.CreateInstance(type);

            try
            {
                return TagNamesOfClasses[type] = instance.TagName;
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
        internal static IEnumerable<Type> GetConcreteTypes(ItemType type) =>
            TypeCache.GetTypesDerivedFrom<ItemDef>()
                .Where(candidate => !candidate.IsAbstract && !candidate.IsGenericTypeDefinition && GetItemType(candidate) == type);

        internal static ItemType GetItemType(Type type)
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
            var tagsType = GetTagsType(type);

            return tagsType == null ? null : (Tags)Activator.CreateInstance(tagsType);
        }

        /// <summary>The <c>TTags</c> of the <see cref="ItemDef{TTags}"/> <paramref name="type"/> inherits, or null without tags.</summary>
        internal static Type GetTagsType(Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(ItemDef<>))
                    return current.GetGenericArguments()[0];
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
