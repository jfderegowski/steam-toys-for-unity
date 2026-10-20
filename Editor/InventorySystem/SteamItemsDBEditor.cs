using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using fefek5.Toys.Editor.VisualElements;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SteamToys.Editor.InventorySystem.CodeGenerators;
using SteamToys.Runtime.InventorySystem;
using SteamToys.Runtime.InventorySystem.Converters;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SteamToys.Editor.InventorySystem
{
    /// <summary>
    /// The inspector of the items DB: one table with every definition in its Resources folder and every definition
    /// the Steam client reports for the app, marking what Steam would reject in each and where a definition differs
    /// from the one on Steam, and the inspector of the selected definition under it. Built like the inspectors of the
    /// stats and achievements DBs. It works on the definitions only; the items a player owns are in the Steam
    /// Inventory window.
    /// <para>
    /// The definitions on Steam are read through the Steam client, so they need a Steam session; the last ones read
    /// stay for the comparison after it ends. The client never lists bundles, tag generators and playtime
    /// generators, so those are looked up by itemdefid and may not be compared at all.
    /// </para>
    /// </summary>
    [CustomEditor(typeof(SteamItemsDB))]
    public class SteamItemsDBEditor : UnityEditor.Editor
    {
        internal const string MenuPath = "Window/Steam Toys/Steam Items DB";

        private const int RowHeight = 20;
        private const string JsonFileName = "items.json";

        private static readonly Color WarningTint = new(1f, 0.76f, 0.03f, 0.25f);
        private static readonly Color ErrorTint = new(1f, 0.25f, 0.25f, 0.3f);

        // How long to wait before asking a Steam client that had no definitions yet again.
        private static readonly TimeSpan SteamRetryInterval = TimeSpan.FromSeconds(5);

        // The properties compared with Steam: the ones Create From Steam takes over.
        private static readonly string[] ComparedProperties =
        {
            "type", "name", "description", "tags", "exchange", "tag_generators", "tag_generator_name",
            "tag_generator_values", "display_type", "icon_url", "icon_url_large", "name_color", "background_color",
            "store_hidden", "tradable", "marketable", "game_only", "auto_stack"
        };

        private static readonly HashSet<string> BoolProperties = new() { "store_hidden", "tradable", "marketable", "game_only", "auto_stack" };
        private static readonly HashSet<string> ColorProperties = new() { "name_color", "background_color" };
        private static readonly HashSet<string> ListProperties = new() { "tags", "exchange", "tag_generators", "tag_generator_values" };

        // What the Steam client reported, kept for every inspector of the DB and after the session ends. Null until
        // it was read; empty while the client has not loaded the definitions yet.
        private static List<ItemDefsFromSteam.SteamDefinition> _steamDefinitions;
        private static DateTime _steamReadAt;
        private static bool _steamStale = true;

        private readonly List<Row> _rows = new();
        private readonly List<Row> _visibleRows = new();
        private readonly List<string> _dbWarnings = new();

        // The definition of the selected row, or the itemdefid of a definition Steam alone has. Kept apart from the
        // selection of the table, which a filter can hide the row from.
        private object _selectedKey;

        // Set while the table is refilled, which may report a selection change of its own.
        private bool _refilling;

        private HelpBox _status;
        private MultiColumnListView _table;
        private Toggle _problemsOnly;
        private ToolbarSearchField _search;
        private VisualElement _detail;
        private HelpBox _detailProblems;
        private VisualElement _comparison;

        // What the comparison shows, so the periodic refresh builds it again only when that changes.
        private string _comparisonShown;
        private InspectorButtonElement _createButton;

        [MenuItem(MenuPath)]
        public static void Open() => EditorUtility.OpenPropertyEditor(SteamItemsDB.Instance);

        [InitializeOnLoadMethod]
        private static void WatchSteamDefinitions() => Inventory.OnDefinitionsChanged += () => _steamStale = true;

        #region Assets

        private void ShowAddMenu()
        {
            var menu = new GenericMenu();

            foreach (var type in TypeCache.GetTypesDerivedFrom<ItemDef>().OrderBy(type => type.Name))
            {
                if (!type.IsAbstract && !type.IsGenericTypeDefinition)
                    menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(type.Name)), false, () => AddItem(type));
            }

            menu.ShowAsContext();
        }

        /// <summary>
        /// Makes a definition of <paramref name="type"/> in the folder of the DB, with the next free itemdefid.
        /// </summary>
        private void AddItem(Type type)
        {
            var db = (SteamItemsDB)target;

            ItemDefsFromSteam.EnsureFolder(SteamItemsDB.AssetsFolder);

            var item = (ItemDef)CreateInstance(type);
            item.ItemDefId = db.GetNextItemDefId();

            var path = AssetDatabase.GenerateUniqueAssetPath(
                $"{SteamItemsDB.AssetsFolder}/New {ObjectNames.NicifyVariableName(type.Name)}.asset");

            AssetDatabase.CreateAsset(item, path);
            Undo.RegisterCreatedObjectUndo(item, "Add Item Definition");
            AssetDatabase.SaveAssets();

            db.Reload();
            Select(item);
        }

        /// <summary>
        /// Moves the definition to the trash of the operating system, where it can still be restored from.
        /// </summary>
        private void RemoveItem(ItemDef item)
        {
            if (!EditorUtility.DisplayDialog("Remove Item Definition",
                    $"Move {item.name} (itemdefid {item.ItemDefId}) to the trash?\n\n" +
                    "References to it in other definitions become empty.",
                    "Move To Trash", "Cancel"))
                return;

            AssetDatabase.MoveAssetToTrash(AssetDatabase.GetAssetPath(item));

            ((SteamItemsDB)target).Reload();
            Select(null);
        }

        #endregion

        #region Inspector

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            _status = new HelpBox();
            root.Add(_status);

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 2 } };

            _createButton = new InspectorButtonElement(CreateFromSteam, "Create From Steam")
            {
                tooltip = "Makes a definition for every one published on Steam that the DB does not have, and overwrites " +
                          "the fields Steam reports of the ones it has. The Steam client does not hand out bundle and " +
                          "generator contents, prices, promo rules, drop settings, the purchase limit or hidden, so those " +
                          "are left as they are. Needs a Steam session.",
                style = { flexGrow = 1 }
            };

            buttons.Add(_createButton);

            buttons.Add(new InspectorButtonElement(ShowAddMenu, "Add Item Definition")
            {
                tooltip = $"Adds a definition of the chosen class to {SteamItemsDB.AssetsFolder}, including classes " +
                          "the game derives, with the next free itemdefid.",
                style = { flexGrow = 1 }
            });

            buttons.Add(new InspectorButtonElement(CopyJson, "Copy All As JSON")
            {
                tooltip = "Copies every definition of the DB in the file the Steam Inventory Service imports, with the " +
                          "App ID of the Steam Settings.",
                style = { flexGrow = 1 }
            });

            buttons.Add(new InspectorButtonElement(SaveJson, "Save JSON...")
            {
                tooltip = "Saves every definition of the DB as the file to upload on the Inventory Service page of Steamworks.",
                style = { flexGrow = 1 }
            });

            root.Add(buttons);

            var filters = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 6 } };

            _problemsOnly = new Toggle { text = "Problems Only", style = { marginRight = 8 } };
            _problemsOnly.RegisterValueChangedCallback(_ => ApplyFilter());

            _search = new ToolbarSearchField { style = { flexGrow = 1, width = StyleKeyword.Auto } };
            _search.RegisterValueChangedCallback(_ => ApplyFilter());

            filters.Add(_problemsOnly);
            filters.Add(_search);
            root.Add(filters);

            _table = CreateTable();
            root.Add(_table);

            _detail = new VisualElement { style = { marginTop = 10 } };
            root.Add(_detail);

            // Read again on opening, for a change the Steam client did not report.
            _steamStale = true;

            Refresh();
            ShowDetail(FindRow(_selectedKey));

            // Polled, because definitions are files that change without the DB being told.
            root.schedule.Execute(Refresh).Every(1000);

            return root;
        }

        private void CreateFromSteam()
        {
            var definitions = ItemDefsFromSteam.ReadAll();

            if (definitions == null)
                return;

            if (definitions.Count == 0)
            {
                EditorUtility.DisplayDialog("Create From Steam",
                    "The Steam client has no item definitions for this app yet. They were asked for; try again in a moment.", "OK");
                return;
            }

            CreateFromSteam(definitions);
        }

        /// <summary>
        /// Makes a definition for each of <paramref name="definitions"/> the DB lacks and updates the ones it has,
        /// after offering what the Steam Item Definitions lack for them. Needs no Steam session of its own.
        /// </summary>
        private void CreateFromSteam(IReadOnlyList<ItemDefsFromSteam.SteamDefinition> definitions)
        {
            var db = (SteamItemsDB)target;

            if (EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog("Create From Steam", "Scripts are compiling; try again once they have.", "OK");
                return;
            }

            if (!ProposeDefinitions(definitions) || !ProposeGeneration(definitions, out var packageClasses))
                return;

            // Counted against the loaded list: a lookup by ID reloads the folder on every miss.
            var ids = new HashSet<ulong>(db.Items.Where(item => item).Select(item => item.ItemDefId));
            var created = definitions.Where(definition => !ids.Contains(definition.ItemDefId)).ToList();
            var message = $"Of the item definitions Steam has for app {SteamItemsDB.AppId}, create {created.Count} " +
                          $"in {SteamItemsDB.AssetsFolder} and update {definitions.Count - created.Count} the DB has?\n\n" +
                          "Only these fields are written, each for the types that use it: names, descriptions, icons, colors, " +
                          "trading flags, tags, exchange recipes and tag generators. The others, such as contents, prices, " +
                          "promo rules and drop settings, are left as they are.";

            if (packageClasses || created.Count == 0)
            {
                if (EditorUtility.DisplayDialog("Create From Steam",
                        message + (packageClasses ? "\n\nNew definitions get the Default classes of the package." : string.Empty),
                        "Create And Update", "Cancel"))
                    Apply(definitions, packageClasses, null);

                return;
            }

            var groups = GroupByClasses(created, definitions);

            if (!CreateFromSteamWindow.Show(message + "\n\nThe new definitions are made with the class chosen for their group: " +
                                            "the classes of their type that hold their tags.", groups))
                return;

            Apply(definitions, false, groups
                .SelectMany(group => group.Definitions.Select(definition => (definition.ItemDefId, group.Choice)))
                .ToDictionary(pair => pair.ItemDefId, pair => pair.Choice));
        }

        /// <summary>
        /// The new definitions in groups of the same type and tag categories, which the same classes hold, each with
        /// the class Create From Steam picks for it chosen.
        /// </summary>
        private static List<CreateFromSteamWindow.Group> GroupByClasses(IEnumerable<ItemDefsFromSteam.SteamDefinition> created,
            IReadOnlyList<ItemDefsFromSteam.SteamDefinition> definitions)
        {
            var groups = new Dictionary<string, CreateFromSteamWindow.Group>();

            foreach (var steam in created)
            {
                var classes = ItemDefsFromSteam.FindClasses(steam, definitions, out var tags, out var ownedTags);

                // None holds it: Apply picks a class the way it always did, and reports what it could not take over.
                if (classes.Count == 0)
                    continue;

                var tagList = string.Join(", ", tags.OrderBy(tag => tag, StringComparer.Ordinal));
                var ownedList = string.Join(", ", ownedTags.OrderBy(tag => tag, StringComparer.Ordinal));
                var key = $"{steam.Type}|{tagList}|{ownedList}";

                if (!groups.TryGetValue(key, out var group))
                {
                    var type = ObjectNames.NicifyVariableName(steam.Type.ToString());

                    groups.Add(key, group = new CreateFromSteamWindow.Group
                    {
                        Label = steam.Type == ItemType.TagGenerator
                            ? $"{type} {tagList}"
                            : $"{type}, " + (tagList.Length == 0 ? "no tags" : $"tags {tagList}") +
                              (ownedList.Length == 0 ? string.Empty : $", owned item {ownedList}"),
                        Classes = classes,
                        Choice = classes[0]
                    });
                }

                group.Definitions.Add(steam);
            }

            return groups.Values.OrderBy(group => group.Definitions[0].Type).ThenBy(group => group.Label, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// Makes the definition Steam alone has with the class chosen for it, without the offers of the run over all
        /// of them: the class is picked already.
        /// </summary>
        private void CreateFromSteam(ItemDefsFromSteam.SteamDefinition steam, Type type)
        {
            if (EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog("Create From Steam", "Scripts are compiling; try again once they have.", "OK");
                return;
            }

            Apply(new[] { steam }, false, new Dictionary<ulong, Type> { { steam.ItemDefId, type } });
        }

        private void Apply(IReadOnlyList<ItemDefsFromSteam.SteamDefinition> definitions, bool packageClasses,
            IReadOnlyDictionary<ulong, Type> classes)
        {
            var report = ItemDefsFromSteam.Apply((SteamItemsDB)target, definitions, packageClasses, classes);

            foreach (var problem in report.Problems)
                Debug.LogWarning($"[SteamToys] Create From Steam: {problem}");

            EditorUtility.DisplayDialog("Create From Steam",
                $"Created {report.Created.Count} and updated {report.Updated.Count} item definitions." +
                (report.Problems.Count == 0 ? string.Empty : $"\n\n{report.Problems.Count} could not be taken over in full; the console lists them."),
                "OK");

            // A definition Steam alone had is a definition of the DB now, and stays selected as one.
            var made = _selectedKey is ulong id ? report.Created.Concat(report.Updated).FirstOrDefault(item => item.ItemDefId == id) : null;

            if (made)
                Select(made);
            else
                Refresh();
        }

        /// <summary>
        /// Offers to add to <see cref="SteamItemDefinitions"/> what the definitions on Steam need of it: the tag
        /// categories and values Steam uses, and a kind of item for each set of tags no kind of item takes.
        /// <see cref="ProposeGeneration"/> then offers to generate them. False when cancelled.
        /// </summary>
        private static bool ProposeDefinitions(IReadOnlyList<ItemDefsFromSteam.SteamDefinition> definitions)
        {
            var settings = SteamItemDefinitions.instance;
            var tags = FindMissingTags(settings, definitions);
            var kinds = FindMissingKinds(settings, definitions);

            if (tags.Count == 0 && kinds.Count == 0)
                return true;

            var lines = new List<string>();

            if (tags.Count > 0)
            {
                lines.Add("Tags:");
                lines.AddRange(tags.Select(entry => entry.tag == null
                    ? $"  {entry.category} (new): {string.Join(", ", entry.values)}"
                    : $"  {entry.category}: + {string.Join(", ", entry.values)}"));
            }

            if (kinds.Count > 0)
            {
                lines.Add("Item Defs:");
                lines.AddRange(kinds.Select(kind => "  " + kind.Describe()));
            }

            var choice = EditorUtility.DisplayDialogComplex("Create From Steam",
                $"The {SteamItemDefinitionsWindow.TITLE} lack what the definitions on Steam use:\n\n{string.Join("\n", lines)}\n\n" +
                "Add them? Skip goes on without them.",
                "Add", "Cancel", "Skip");

            if (choice != 0)
                return choice == 2;

            Undo.RecordObject(settings, "Add Steam Item Definitions From Steam");

            foreach (var (category, tag, values) in tags)
            {
                var target = tag;

                if (target == null)
                {
                    target = new TagCodeGenerator { ClassName = category.ToPascalCase() };
                    settings.PlaceNextToOthers(target);
                    settings.Tags.Add(target);
                }

                foreach (var value in values)
                    target.AddValue(value.ToPascalCase());
            }

            // After the tags, which the kinds of items refer to by id.
            foreach (var kind in kinds)
            {
                var itemDefs = new ItemDefsCodeGenerator
                {
                    ClassName = kind.ClassName,
                    GenerateItemDef = kind.Types.Contains(ItemType.Item),
                    GenerateGeneratorDef = kind.Types.Contains(ItemType.Generator),
                    GeneratePlaytimeGeneratorDef = kind.Types.Contains(ItemType.PlaytimeGenerator)
                };

                itemDefs.Tags.AddRange(kind.Tags.Select(category =>
                    new TagsEntry { TagId = settings.FindTagBySteamName(category).Id }));
                itemDefs.Tags.AddRange(kind.InventoryItemTags.Select(category =>
                    new TagsEntry { TagId = settings.FindTagBySteamName(category).Id, OnInventoryItem = true }));

                settings.PlaceNextToOthers(itemDefs);
                settings.ItemDefs.Add(itemDefs);
            }

            settings.SaveDefinitions();

            return true;
        }

        /// <summary>
        /// Offers to generate the scripts of the tags Steam uses and of the kinds of items with them that are not
        /// generated as they are now, or to make the new definitions with the Default classes of the package instead,
        /// set in <paramref name="packageClasses"/>. False when the run stops: cancelled, or scripts were generated,
        /// which are to compile before the definitions are made with their classes.
        /// </summary>
        private static bool ProposeGeneration(IReadOnlyList<ItemDefsFromSteam.SteamDefinition> definitions, out bool packageClasses)
        {
            packageClasses = false;

            var settings = SteamItemDefinitions.instance;
            var categories = ItemDefsFromSteam.CollectTags(definitions).Keys;
            var tags = settings.Tags.Where(tag => tag != null && categories.Contains(tag.SteamName)).ToList();
            var itemDefs = settings.ItemDefs.Where(kind => kind != null && tags.Any(kind.Uses)).ToList();
            var stale = tags.Cast<CodeGenerator>().Concat(itemDefs).Where(generator => !CodeGeneration.IsUpToDate(generator)).ToList();

            if (stale.Count == 0)
                return true;

            var lines = stale.Select(generator =>
            {
                var problems = CodeGeneration.GetProblems(generator).Count;

                return $"  {generator.Kind} {generator.DisplayName}" +
                       (problems > 0 ? $" (has {problems} problem{(problems == 1 ? string.Empty : "s")}, fix it in the window)" : string.Empty);
            });

            var choice = EditorUtility.DisplayDialogComplex("Create From Steam",
                $"These {SteamItemDefinitionsWindow.TITLE} are not generated as they are now:\n\n{string.Join("\n", lines)}\n\n" +
                "Generate them? Run Create From Steam again once the scripts compiled, so the definitions are made with " +
                "their classes. Use Default Classes makes the new definitions with DefaultItemDef, DefaultGeneratorDef, " +
                "DefaultPlaytimeGeneratorDef and DefaultTagGeneratorDef instead.",
                "Generate", "Cancel", "Use Default Classes");

            if (choice == 2)
                packageClasses = true;

            if (choice != 0)
                return choice == 2;

            // The tags first, so the kinds of items find their enums.
            CodeGeneration.Generate(stale.OfType<TagCodeGenerator>());
            CodeGeneration.Generate(stale.OfType<ItemDefsCodeGenerator>());

            if (stale.Any(generator => !CodeGeneration.IsUpToDate(generator)))
                SteamItemDefinitionsWindow.Open();

            return false;
        }

        /// <summary>The tag categories Steam uses that have no tag, and the values the tags lack.</summary>
        private static List<(string category, TagCodeGenerator tag, List<string> values)> FindMissingTags(
            SteamItemDefinitions settings, IReadOnlyList<ItemDefsFromSteam.SteamDefinition> definitions)
        {
            var missing = new List<(string category, TagCodeGenerator tag, List<string> values)>();

            foreach (var (category, values) in ItemDefsFromSteam.CollectTags(definitions))
            {
                var tag = settings.FindTagBySteamName(category);
                var known = tag == null
                    ? new HashSet<string>()
                    : new HashSet<string>(tag.Values.Where(value => value != null).Select(value => value.Name.ToSnakeCase()));
                var lacking = values.Where(value => !known.Contains(value)).ToList();

                if (tag == null || lacking.Count > 0)
                    missing.Add((category, tag, lacking));
            }

            return missing;
        }

        /// <summary>
        /// A kind of item for each set of tags of an item or generator on Steam that no kind of item takes: the tags of
        /// an item go to its ItemDef tags and those of its tag generators to its InventoryItem tags, while a generator
        /// copies its tags to the item it makes, so its classes take them as InventoryItem tags. Named after the tags.
        /// </summary>
        private static List<MissingKind> FindMissingKinds(SteamItemDefinitions settings,
            IReadOnlyList<ItemDefsFromSteam.SteamDefinition> definitions)
        {
            var kinds = new Dictionary<string, MissingKind>();

            foreach (var (type, tags, generatedTags) in ItemDefsFromSteam.CollectTagCategories(definitions))
            {
                var kind = type == ItemType.Item
                    ? new MissingKind(tags, generatedTags.Except(tags))
                    : new MissingKind(Enumerable.Empty<string>(), tags.Concat(generatedTags));

                if (settings.ItemDefs.Any(itemDefs => itemDefs != null && kind.IsTakenBy(itemDefs, type, settings)))
                    continue;

                if (kinds.TryGetValue(kind.Key, out var same))
                    kind = same;
                else
                    kinds.Add(kind.Key, kind);

                kind.Types.Add(type);
            }

            var taken = new HashSet<string>(settings.ItemDefs.Where(itemDefs => itemDefs != null).Select(itemDefs => itemDefs.ClassName));

            foreach (var kind in kinds.Values)
            {
                var name = string.Concat(kind.Tags.Concat(kind.InventoryItemTags).Distinct().Select(category => category.ToPascalCase()));
                kind.ClassName = name;

                for (var number = 2; !taken.Add(kind.ClassName); number++)
                    kind.ClassName = name + number;
            }

            return kinds.Values.ToList();
        }

        private sealed class MissingKind
        {
            public readonly SortedSet<string> Tags;
            public readonly SortedSet<string> InventoryItemTags;
            public readonly HashSet<ItemType> Types = new();
            public string ClassName;

            public MissingKind(IEnumerable<string> tags, IEnumerable<string> inventoryItemTags)
            {
                Tags = new SortedSet<string>(tags);
                InventoryItemTags = new SortedSet<string>(inventoryItemTags);
            }

            public string Key => $"{string.Join(",", Tags)}|{string.Join(",", InventoryItemTags)}";

            /// <summary>Whether <paramref name="itemDefs"/> has a class for <paramref name="type"/> that holds these tags.</summary>
            public bool IsTakenBy(ItemDefsCodeGenerator itemDefs, ItemType type, SteamItemDefinitions settings)
            {
                var generates = type switch
                {
                    ItemType.Item => itemDefs.GenerateItemDef,
                    ItemType.Generator => itemDefs.GenerateGeneratorDef,
                    ItemType.PlaytimeGenerator => itemDefs.GeneratePlaytimeGeneratorDef,
                    _ => false
                };

                if (!generates)
                    return false;

                var held = new HashSet<(string, bool)>(itemDefs.Tags
                    .Where(entry => entry != null)
                    .Select(entry => (settings.FindTag(entry.TagId)?.SteamName, entry.OnInventoryItem)));

                return Tags.All(category => held.Contains((category, false))) &&
                       InventoryItemTags.All(category => held.Contains((category, true)));
            }

            public string Describe() =>
                $"{ClassName} ({string.Join(", ", Types.OrderBy(type => type).Select(type => ObjectNames.NicifyVariableName(type.ToString())))}): " +
                string.Join("; ", new[]
                {
                    Tags.Count > 0 ? $"tags {string.Join(", ", Tags)}" : null,
                    InventoryItemTags.Count > 0 ? $"on the owned item {string.Join(", ", InventoryItemTags)}" : null
                }.Where(part => part != null));
        }

        private void CopyJson() => EditorGUIUtility.systemCopyBuffer = ((SteamItemsDB)target).ToJson();

        private void SaveJson()
        {
            var path = EditorUtility.SaveFilePanel("Save Steam Item Definitions", string.Empty, JsonFileName, "json");

            if (!string.IsNullOrEmpty(path))
                File.WriteAllText(path, ((SteamItemsDB)target).ToJson());
        }

        private void Refresh()
        {
            if (!target)
                return;

            var db = (SteamItemsDB)target;

            db.Reload();

            if (Inventory.Initialized && (_steamStale ||
                                          _steamDefinitions is { Count: 0 } && DateTime.Now - _steamReadAt > SteamRetryInterval))
                ReadSteam(db);

            _dbWarnings.Clear();
            db.CollectSchemaWarnings(_dbWarnings);

            BuildRows(db);

            var withProblems = _rows.Count(row => row.Item && row.Severity >= HelpBoxMessageType.Warning);
            var steamOnly = _rows.Count(row => !row.Item);

            _createButton.SetEnabled(Inventory.Initialized);

            _status.messageType = withProblems + steamOnly > 0 || _dbWarnings.Count > 0 ? HelpBoxMessageType.Warning : HelpBoxMessageType.Info;
            _status.text = $"{_rows.Count - steamOnly} definitions in {SteamItemsDB.AssetsFolder}, {withProblems} with problems" +
                           (steamOnly == 0 ? ". " : $", {steamOnly} on Steam only. ") +
                           $"App ID {SteamItemsDB.AppId}, from the Steam Settings.\n{DescribeSteam()}" +
                           (_dbWarnings.Count == 0 ? string.Empty : "\n" + string.Join("\n", _dbWarnings));

            if (_selectedKey is Object selected && !selected)
                Select(null);

            ApplyFilter();
            UpdateDetailProblems();
        }

        /// <summary>
        /// Reads the definitions the Steam client lists, and those of the DB it does not list but knows by itemdefid.
        /// </summary>
        private static void ReadSteam(SteamItemsDB db)
        {
            var definitions = ItemDefsFromSteam.ReadAll();

            if (definitions == null)
                return;

            _steamStale = false;
            _steamReadAt = DateTime.Now;

            // Empty means the client has not loaded them yet, and was asked to.
            if (definitions.Count > 0)
            {
                var listed = new HashSet<ulong>(definitions.Select(definition => definition.ItemDefId));

                foreach (var item in db.Items)
                {
                    if (item && listed.Add(item.ItemDefId) && ItemDefsFromSteam.Read(item.ItemDefId) is { } unlisted)
                        definitions.Add(unlisted);
                }
            }

            _steamDefinitions = definitions;
        }

        private static string DescribeSteam()
        {
            if (_steamDefinitions == null)
                return "Not compared with Steam, which needs a Steam session: play mode, or Window/Steam Toys/Connect To Steam.";

            if (_steamDefinitions.Count == 0)
                return "Not compared with Steam: the Steam client has no item definitions for this app yet.";

            return $"Compared with the {_steamDefinitions.Count} definitions the Steam client had at {_steamReadAt:HH:mm:ss}" +
                   (Inventory.Initialized ? "." : "; the Steam session has ended since.");
        }

        private void BuildRows(SteamItemsDB db)
        {
            _rows.Clear();

            var steamById = new Dictionary<ulong, ItemDefsFromSteam.SteamDefinition>();

            foreach (var steam in _steamDefinitions ?? Enumerable.Empty<ItemDefsFromSteam.SteamDefinition>())
                steamById.TryAdd(steam.ItemDefId, steam);

            var compared = steamById.Count > 0;
            var inDb = new HashSet<ulong>();

            foreach (var item in db.Items)
            {
                if (!item)
                    continue;

                inDb.Add(item.ItemDefId);
                steamById.TryGetValue(item.ItemDefId, out var steam);

                var row = new Row(item, steam);
                var warnings = new List<string>();

                ItemDefElement.CollectWarnings(item, warnings);

                foreach (var warning in warnings)
                    row.Add(HelpBoxMessageType.Warning, warning);

                if (compared)
                    AddSteamProblems(row);

                _rows.Add(row);
            }

            foreach (var steam in steamById.Values.Where(steam => !inDb.Contains(steam.ItemDefId)))
            {
                var row = new Row(null, steam);

                row.Add(HelpBoxMessageType.Warning, "Steam has this definition and the DB does not. Create From Steam adds it.");
                _rows.Add(row);
            }

            // One list by itemdefid; OrderBy keeps definitions that share one in the order of the DB.
            var ordered = _rows.OrderBy(row => row.ItemDefId).ToList();

            _rows.Clear();
            _rows.AddRange(ordered);
        }

        /// <summary>Compares a definition of the DB with the one Steam has under its itemdefid.</summary>
        private static void AddSteamProblems(Row row)
        {
            var item = row.Item;

            if (row.Steam == null)
            {
                if (item.Type is ItemType.Item or ItemType.Generator)
                    row.Add(HelpBoxMessageType.Warning,
                        $"Steam has no definition with itemdefid {item.ItemDefId}. Save the JSON and upload it on the " +
                        "Inventory Service page of Steamworks.");
                else
                    row.Add(HelpBoxMessageType.Info,
                        $"The Steam client does not hand out this {item.Type}, so it cannot be compared with Steam.");

                return;
            }

            JObject json;

            try
            {
                json = row.Json = JObject.FromObject(item);
            }
            catch (Exception exception) when (exception is InvalidOperationException or NullReferenceException)
            {
                // An exchange entry that is not filled in, which the export stops on too.
                row.Add(HelpBoxMessageType.Error, $"Cannot be written as JSON, so it is not compared with Steam: {exception.Message}");

                return;
            }

            foreach (var property in ComparedProperties)
            {
                var here = Normalize(property, json.TryGetValue(property, out var token) ? token.ToString() : null);
                var steam = Normalize(property, row.Steam.Get(property));

                if (here == steam)
                    continue;

                row.Differences.Add(property, steam);

                row.Add(property == "type" ? HelpBoxMessageType.Error : HelpBoxMessageType.Warning, property == "type"
                    ? $"Steam has this definition as a {row.Steam.Type}, and the type comes from the class of the asset, " +
                      "so Create From Steam leaves it alone."
                    : $"{property} differs from Steam: \"{here}\" here, \"{steam}\" on Steam.");
            }
        }

        /// <summary>
        /// A property as the schema means it, so the JSON of the DB and what the client reports compare equal: the
        /// client writes flags as 1 and 0 and leaves out false ones, a color may have a leading #, and the order of
        /// list entries means nothing.
        /// </summary>
        private static string Normalize(string property, string value)
        {
            value = value?.Trim() ?? string.Empty;

            if (BoolProperties.Contains(property))
                return value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ? "true" : "false";

            if (ColorProperties.Contains(property))
                return value.TrimStart('#').ToUpperInvariant();

            if (ListProperties.Contains(property))
                return string.Join(";", value
                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(entry => string.Join(",", entry
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(part => part.Trim())
                        .OrderBy(part => part, StringComparer.Ordinal)))
                    .Where(entry => entry.Length > 0)
                    .OrderBy(entry => entry, StringComparer.Ordinal));

            return value;
        }

        private void ApplyFilter()
        {
            var search = _search.value?.Trim() ?? string.Empty;

            _visibleRows.Clear();
            _visibleRows.AddRange(_rows.Where(row =>
                (!_problemsOnly.value || row.Severity >= HelpBoxMessageType.Warning) &&
                (search.Length == 0 || row.Matches(search))));

            _refilling = true;

            _table.RefreshItems();

            var index = _visibleRows.FindIndex(row => Equals(row.Key, _selectedKey));

            if (index >= 0)
                _table.SetSelectionWithoutNotify(new[] { index });
            else
                _table.ClearSelection();

            _refilling = false;
        }

        #endregion

        #region Table

        private MultiColumnListView CreateTable()
        {
            var table = new MultiColumnListView
            {
                itemsSource = _visibleRows,
                fixedItemHeight = RowHeight,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                showBorder = true,
                horizontalScrollingEnabled = true,
                style = { marginTop = 2, maxHeight = 360 }
            };

            table.columns.Add(new Column
            {
                name = "status",
                width = 24,
                resizable = false,
                makeCell = () => new Image { style = { width = 16, height = 16, alignSelf = Align.Center, marginTop = 2 } },
                bindCell = BindStatus
            });

            AddLabelColumn(table, "id", "ID", 70, null, row => row.ItemDefId.ToString());
            AddLabelColumn(table, "type", "Type", 110, "type", row => row.Item ? row.Item.Type.ToString() : row.Steam.Type.ToString());
            AddLabelColumn(table, "name", "Name", 160, "name", row => !row.Item ? row.Steam.Name :
                string.IsNullOrEmpty(row.Item.Name) ? $"({row.Item.name})" : row.Item.Name, true);
            AddLabelColumn(table, "class", "Class", 130, null, row => row.Item ? row.Item.GetType().Name : "(on Steam only)", true);

            table.selectionChanged += _ =>
            {
                if (_refilling)
                    return;

                var row = _table.selectedItem as Row;

                _selectedKey = row?.Key;
                ShowDetail(row);
            };

            return table;
        }

        /// <summary>
        /// A column of text, tinted where the definition differs from Steam in <paramref name="property"/>. A
        /// definition Steam alone has is shown faded.
        /// </summary>
        private void AddLabelColumn(MultiColumnListView table, string name, string title, float width, string property,
            Func<Row, string> text, bool stretchable = false) =>
            table.columns.Add(new Column
            {
                name = name,
                title = title,
                width = width,
                minWidth = 40,
                stretchable = stretchable,
                makeCell = MakeLabel,
                bindCell = (element, index) =>
                {
                    var label = (Label)element;
                    var row = _visibleRows[index];
                    string steam = null;
                    var differs = property != null && row.Differences.TryGetValue(property, out steam);

                    label.text = text(row);
                    label.style.opacity = row.Item ? 1f : 0.55f;
                    label.style.backgroundColor = differs
                        ? new StyleColor(property == "type" ? ErrorTint : WarningTint)
                        : new StyleColor(StyleKeyword.Null);
                    label.tooltip = differs ? $"Steam: {steam}" : null;
                }
            });

        private static VisualElement MakeLabel() => new Label
        {
            style =
            {
                flexGrow = 1,
                paddingLeft = 4,
                unityTextAlign = TextAnchor.MiddleLeft,
                overflow = Overflow.Hidden,
                textOverflow = TextOverflow.Ellipsis
            }
        };

        private void BindStatus(VisualElement element, int index)
        {
            var image = (Image)element;
            var row = _visibleRows[index];

            var icon = row.Severity switch
            {
                HelpBoxMessageType.Error => "console.erroricon.sml",
                HelpBoxMessageType.Warning => "console.warnicon.sml",
                HelpBoxMessageType.Info => "console.infoicon.sml",
                _ => row.Steam != null ? "GreenCheckmark" : null
            };

            image.image = icon == null ? null : EditorGUIUtility.IconContent(icon).image;
            image.tooltip = row.Problems.Count > 0 ? row.DescribeProblems() : row.Steam != null ? "Matches Steam" : null;
        }

        #endregion

        #region Detail

        private void Select(ItemDef item)
        {
            _selectedKey = item ? item : null;

            Refresh();
            ShowDetail(FindRow(_selectedKey));
        }

        private Row FindRow(object key) => key == null ? null : _rows.FirstOrDefault(row => Equals(row.Key, key));

        /// <summary>
        /// Shows the selected definition under the table, or what Steam has for a definition only Steam has. Built
        /// again only when the selection changes, so that the periodic refresh cannot take the focus away from a
        /// field being typed in.
        /// </summary>
        private void ShowDetail(Row row)
        {
            _detail.Clear();
            _detailProblems = null;

            if (row == null)
                return;

            var item = row.Item;

            _detail.Add(new Label(item ? item.name : row.Steam.Name)
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 13, marginBottom = 2 }
            });

            _detailProblems = new HelpBox();
            _detail.Add(_detailProblems);
            UpdateDetailProblems();

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 6 } };

            _detail.Add(buttons);

            if (item)
            {
                buttons.Add(new InspectorButtonElement(() => EditorGUIUtility.PingObject(item), "Ping")
                {
                    tooltip = "Shows the definition in the Project window, where it can be dragged into a field.",
                    style = { flexGrow = 1 }
                });

                buttons.Add(new InspectorButtonElement(() => RemoveItem(item), "Move To Trash")
                {
                    style = { flexGrow = 1 }
                });
            }
            else
            {
                var steam = row.Steam;
                var classes = ItemDefsFromSteam.FindClasses(steam, _steamDefinitions, out var tags, out var ownedTags);
                var missing = DescribeMissingClass(steam, classes, tags, ownedTags);

                if (missing != null)
                {
                    _detail.Insert(_detail.IndexOf(buttons), new HelpBox(missing, HelpBoxMessageType.Info));
                    _detail.Insert(_detail.IndexOf(buttons), new InspectorButtonElement(SteamItemDefinitionsWindow.Open,
                        $"Open {SteamItemDefinitionsWindow.TITLE}") { style = { marginBottom = 4 } });
                }

                PopupField<Type> classField = null;

                if (classes.Count > 0)
                {
                    classField = new PopupField<Type>("Class", classes, 0, CreateFromSteamWindow.FormatClass,
                        CreateFromSteamWindow.FormatClass)
                    {
                        tooltip = "The class this definition is made with: every one of its type with exactly its tags.",
                        style = { flexGrow = 1 }
                    };

                    // Shown with one class too, so it is clear which one it is.
                    classField.SetEnabled(classes.Count > 1);
                    buttons.Add(classField);
                }

                var createButton = new InspectorButtonElement(() => CreateFromSteam(steam, classField.value), "Create From Steam")
                {
                    tooltip = "Makes this definition from what the Steam client reported for it, with the chosen class.",
                    style = { flexGrow = 1 }
                };

                createButton.SetEnabled(classField != null);
                buttons.Add(createButton);
            }

            if (item)
                _detail.Add(new InspectorElement(item));

            // After the inspector, so it sits right under the JSON preview of the definition to compare with.
            if (row.Steam != null)
                _detail.Add(CreateSteamPreview(row.Steam));

            _comparison = null;
            _comparisonShown = null;

            if (item && row.Steam != null)
            {
                _comparison = new VisualElement { style = { marginTop = 8 } };
                _detail.Add(_comparison);
                UpdateComparison(row);
            }
        }

        /// <summary>
        /// The JSON of the definition next to what Steam has, property by property: the ones that differ tinted, and
        /// the ones that are not compared faded.
        /// </summary>
        private void UpdateComparison(Row row)
        {
            if (_comparison == null || row == null || !row.Item || row.Steam == null)
                return;

            var lines = new List<(string property, string here, string steam, string state)>();

            if (row.Json != null)
            {
                var steamOnly = row.Steam.Properties.Keys.Where(property => !row.Json.ContainsKey(property));

                foreach (var property in row.Json.Properties().Select(property => property.Name).Concat(steamOnly))
                {
                    var here = row.Json.TryGetValue(property, out var token) ? token.ToString(Formatting.None) : null;
                    var steam = row.Steam.Properties.TryGetValue(property, out var value) ? JsonConvert.ToString(value) : null;
                    var state = row.Differences.ContainsKey(property) ? property == "type" ? "error" : "differs"
                        // The itemdefid is what pairs the two, so it always matches.
                        : ComparedProperties.Contains(property) || property == "itemdefid" ? "matches" : "not compared";

                    lines.Add((property, here, steam, state));
                }
            }

            var shown = string.Join("\n", lines.Select(line => $"{line.property}\t{line.here}\t{line.steam}\t{line.state}"));

            if (shown == _comparisonShown && _comparison.childCount > 0)
                return;

            _comparisonShown = shown;
            _comparison.Clear();

            _comparison.Add(new Label("Project And Steam")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 2 }
            });

            if (row.Json == null)
            {
                _comparison.Add(new HelpBox("The definition cannot be written as JSON, so there is nothing to compare.", HelpBoxMessageType.Warning));
                return;
            }

            var theme = EditorGUIUtility.isProSkin ? JsonSyntaxHighlighter.Theme.Dark : JsonSyntaxHighlighter.Theme.Light;
            var font = JsonPreviewElement.GetMonospaceFont();
            var table = new VisualElement
            {
                style =
                {
                    borderTopWidth = 1, borderBottomWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
                    borderTopColor = Color.gray, borderBottomColor = Color.gray, borderLeftColor = Color.gray, borderRightColor = Color.gray
                }
            };

            table.Add(CreateComparisonLine("Property", "Project", "Steam", font, header: true));

            foreach (var (property, here, steam, state) in lines)
            {
                var line = CreateComparisonLine(property, Highlight(here), Highlight(steam), font);

                switch (state)
                {
                    case "error":
                        line.style.backgroundColor = ErrorTint;
                        line.tooltip = "Differs from Steam.";
                        break;
                    case "differs":
                        line.style.backgroundColor = WarningTint;
                        line.tooltip = "Differs from Steam.";
                        break;
                    case "not compared":
                        line.style.opacity = 0.55f;
                        line.tooltip = here == null ? "Only on Steam, and not compared." :
                            steam == null ? "Only in the project: the Steam client does not hand it out, or Steam does not have it." :
                            "Not compared: Create From Steam does not take it over.";
                        break;
                }

                table.Add(line);
            }

            _comparison.Add(table);

            string Highlight(string value) => value == null ? string.Empty : JsonSyntaxHighlighter.Highlight(value, theme);
        }

        private static VisualElement CreateComparisonLine(string property, string here, string steam, Font font, bool header = false)
        {
            var line = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.5f, 0.5f, 0.5f, 0.3f)
                }
            };

            line.Add(CreateComparisonCell(property, font, header, 150, 0));
            line.Add(CreateComparisonCell(here, font, header, 0, 1));
            line.Add(CreateComparisonCell(steam, font, header, 0, 1));

            return line;
        }

        private static Label CreateComparisonCell(string text, Font font, bool header, float width, float grow)
        {
            var cell = new Label(text)
            {
                style =
                {
                    flexGrow = grow,
                    flexBasis = grow > 0 ? new StyleLength(0f) : new StyleLength(StyleKeyword.Auto),
                    width = width > 0 ? new StyleLength(width) : new StyleLength(StyleKeyword.Auto),
                    flexShrink = width > 0 ? 0 : 1,
                    paddingLeft = 4, paddingRight = 4, paddingTop = 1, paddingBottom = 1,
                    whiteSpace = WhiteSpace.Normal,
                    unityTextGenerator = TextGeneratorType.Standard
                }
            };

            if (header)
                cell.style.unityFontStyleAndWeight = FontStyle.Bold;
            else if (font != null)
                cell.style.unityFontDefinition = FontDefinition.FromFont(font);

            return cell;
        }

        /// <summary>
        /// What to add to the Steam Item Definitions for a class of the game that holds <paramref name="steam"/>, or
        /// null when it needs none: it has no tags, or <paramref name="classes"/> has a class of the game.
        /// </summary>
        private static string DescribeMissingClass(ItemDefsFromSteam.SteamDefinition steam, IReadOnlyList<Type> classes,
            ICollection<string> tags, ICollection<string> ownedTags)
        {
            if (tags.Count + ownedTags.Count == 0 || classes.Any(type => !ItemDefsFromSteam.IsPackageClass(type)))
                return null;

            var settings = SteamItemDefinitions.instance;
            var missingTags = tags.Concat(ownedTags).Distinct().Where(category => settings.FindTagBySteamName(category) == null).ToList();
            var fallback = classes.Count > 0 ? $" Until then it can be made as a {classes[0].Name}, which takes any tag." : string.Empty;
            var addTags = missingTags.Count == 0
                ? string.Empty
                : $" It has no tag {string.Join(", ", missingTags)} yet, so add that first.";

            if (steam.Type == ItemType.TagGenerator)
                return $"No tag generator class of the game has the category {tags.First()}. In the {SteamItemDefinitionsWindow.TITLE}, " +
                       $"generate the tag {tags.First().ToPascalCase()} with its tag generator class.{addTags}{fallback}";

            var parts = new List<string>();

            if (tags.Count > 0)
                parts.Add($"the tags {string.Join(", ", tags.OrderBy(tag => tag))} on the definition");

            if (ownedTags.Count > 0)
                parts.Add($"{string.Join(", ", ownedTags.OrderBy(tag => tag))} on the owned item");

            return $"No {steam.Type} class of the game has exactly its tags. In the {SteamItemDefinitionsWindow.TITLE}, add Item Defs " +
                   $"that generate the {ObjectNames.NicifyVariableName(steam.Type.ToString())} class, with {string.Join(" and ", parts)}, " +
                   $"and generate them.{addTags}{fallback}";
        }

        /// <summary>
        /// Every property the Steam client reported for the definition, as JSON in the order it lists them. The
        /// client reports every value as a string, flags as 1 and 0 included.
        /// </summary>
        private static VisualElement CreateSteamPreview(ItemDefsFromSteam.SteamDefinition steam)
        {
            var json = JsonConvert.SerializeObject(steam.Properties, Formatting.Indented);
            var preview = new JsonPreviewElement("On Steam") { style = { marginTop = 6 } };

            preview.AddCopyAction("Copy JSON", "Copies what the Steam client reported for this definition.", () => json);
            preview.SetJson(json);

            return preview;
        }

        private void UpdateDetailProblems()
        {
            if (_detailProblems == null)
                return;

            var row = FindRow(_selectedKey);

            _detailProblems.style.display = row is { Problems: { Count: > 0 } } ? DisplayStyle.Flex : DisplayStyle.None;

            if (row == null)
                return;

            _detailProblems.messageType = row.Severity;
            _detailProblems.text = row.DescribeProblems();

            UpdateComparison(row);
        }

        #endregion

        /// <summary>
        /// One line of the table: a definition of the DB with the one Steam has under its itemdefid, or a definition
        /// Steam alone has.
        /// </summary>
        private sealed class Row
        {
            public readonly ItemDef Item;
            public readonly ItemDefsFromSteam.SteamDefinition Steam;
            public readonly List<(HelpBoxMessageType type, string text)> Problems = new();

            /// <summary>The properties that differ from Steam, with the value Steam has.</summary>
            public readonly Dictionary<string, string> Differences = new();

            /// <summary>The JSON of the definition, once compared with Steam; null when it cannot be written.</summary>
            public JObject Json;

            public Row(ItemDef item, ItemDefsFromSteam.SteamDefinition steam)
            {
                Item = item;
                Steam = steam;
            }

            public ulong ItemDefId => Item ? Item.ItemDefId : Steam.ItemDefId;

            /// <summary>What the row stands for across refreshes: its definition, or the itemdefid of one Steam alone has.</summary>
            public object Key => Item ? Item : (object)Steam.ItemDefId;

            public HelpBoxMessageType Severity =>
                Problems.Count == 0 ? HelpBoxMessageType.None : Problems.Max(problem => problem.type);

            public void Add(HelpBoxMessageType type, string text) => Problems.Add((type, text));

            public string DescribeProblems() => string.Join("\n", Problems.Select(problem => problem.text));

            public bool Matches(string search) =>
                ItemDefId.ToString().Contains(search) ||
                (Item && Item.name.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                ((Item ? Item.Name : Steam.Name)?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (Item ? Item.Type : Steam.Type).ToString().Contains(search, StringComparison.OrdinalIgnoreCase);
        }
    }
}
