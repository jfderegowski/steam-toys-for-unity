using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using fefek5.Toys.Editor.VisualElements;
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
    /// The inspector of the items DB: one table with every definition in its Resources folder, marking what Steam
    /// would reject in each, and the inspector of the selected definition under it. Built like the inspectors of the
    /// stats and achievements DBs, without the comparison with Steam. It works on the definitions only; the items
    /// a player owns are in the Steam Inventory window.
    /// </summary>
    [CustomEditor(typeof(SteamItemsDB))]
    public class SteamItemsDBEditor : UnityEditor.Editor
    {
        internal const string MenuPath = "Window/Steam Toys/Steam Items DB";

        private const int RowHeight = 20;
        private const string JsonFileName = "items.json";

        private readonly List<Row> _rows = new();
        private readonly List<Row> _visibleRows = new();
        private readonly List<string> _dbWarnings = new();

        private ItemDef _selected;

        // Set while the table is refilled, which may report a selection change of its own.
        private bool _refilling;

        private HelpBox _status;
        private MultiColumnListView _table;
        private Toggle _problemsOnly;
        private ToolbarSearchField _search;
        private VisualElement _detail;
        private HelpBox _detailProblems;
        private InspectorButtonElement _createButton;

        [MenuItem(MenuPath)]
        public static void Open() => EditorUtility.OpenPropertyEditor(SteamItemsDB.Instance);

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

            Refresh();
            ShowDetail(FindRow(_selected));

            // Polled, because definitions are files that change without the DB being told.
            root.schedule.Execute(Refresh).Every(1000);

            return root;
        }

        private void CreateFromSteam()
        {
            var db = (SteamItemsDB)target;
            var definitions = ItemDefsFromSteam.ReadAll();

            if (definitions == null)
                return;

            if (definitions.Count == 0)
            {
                EditorUtility.DisplayDialog("Create From Steam",
                    "The Steam client has no item definitions for this app yet. They were asked for; try again in a moment.", "OK");
                return;
            }

            if (EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog("Create From Steam", "Scripts are compiling; try again once they have.", "OK");
                return;
            }

            if (!ProposeDefinitions(definitions) || !ProposeGeneration(definitions, out var packageClasses))
                return;

            // Counted against the loaded list: a lookup by ID reloads the folder on every miss.
            var ids = new HashSet<ulong>(db.Items.Where(item => item).Select(item => item.ItemDefId));
            var existing = definitions.Count(definition => ids.Contains(definition.ItemDefId));

            if (!EditorUtility.DisplayDialog("Create From Steam",
                    $"Steam has {definitions.Count} item definitions for app {SteamItemsDB.AppId}.\n\n" +
                    $"Create {definitions.Count - existing} in {SteamItemsDB.AssetsFolder} and update {existing} the DB has?\n\n" +
                    "Only these fields are written, each for the types that use it: names, descriptions, icons, colors, " +
                    "trading flags, tags, exchange recipes and tag generators. The others, such as contents, prices, " +
                    "promo rules and drop settings, are left as they are." +
                    (packageClasses ? "\n\nNew definitions get the Default classes of the package." : string.Empty),
                    "Create And Update", "Cancel"))
                return;

            var report = ItemDefsFromSteam.Apply(db, definitions, packageClasses);

            foreach (var problem in report.Problems)
                Debug.LogWarning($"[SteamToys] Create From Steam: {problem}");

            EditorUtility.DisplayDialog("Create From Steam",
                $"Created {report.Created.Count} and updated {report.Updated.Count} item definitions." +
                (report.Problems.Count == 0 ? string.Empty : $"\n\n{report.Problems.Count} could not be taken over in full; the console lists them."),
                "OK");

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

            _dbWarnings.Clear();
            db.CollectSchemaWarnings(_dbWarnings);

            BuildRows(db);

            var withProblems = _rows.Count(row => row.Problems.Count > 0);

            _createButton.SetEnabled(Inventory.Initialized);

            _status.messageType = withProblems > 0 || _dbWarnings.Count > 0 ? HelpBoxMessageType.Warning : HelpBoxMessageType.Info;
            _status.text = $"{_rows.Count} definitions in {SteamItemsDB.AssetsFolder}, {withProblems} with problems. " +
                           $"App ID {SteamItemsDB.AppId}, from the Steam Settings." +
                           (_dbWarnings.Count == 0 ? string.Empty : "\n" + string.Join("\n", _dbWarnings));

            if (_selected is Object selected && !selected)
                Select(null);

            ApplyFilter();
            UpdateDetailProblems();
        }

        private void BuildRows(SteamItemsDB db)
        {
            _rows.Clear();

            foreach (var item in db.Items)
            {
                if (!item)
                    continue;

                var row = new Row(item);
                ItemDefElement.CollectWarnings(item, row.Problems);

                _rows.Add(row);
            }
        }

        private void ApplyFilter()
        {
            var search = _search.value?.Trim() ?? string.Empty;

            _visibleRows.Clear();
            _visibleRows.AddRange(_rows.Where(row =>
                (!_problemsOnly.value || row.Problems.Count > 0) &&
                (search.Length == 0 || row.Matches(search))));

            _refilling = true;

            _table.RefreshItems();

            var index = _visibleRows.FindIndex(row => row.Item == _selected);

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

            AddLabelColumn(table, "id", "ID", 70, row => row.Item.ItemDefId.ToString());
            AddLabelColumn(table, "type", "Type", 110, row => row.Item.Type.ToString());
            AddLabelColumn(table, "name", "Name", 160, row => string.IsNullOrEmpty(row.Item.Name) ? $"({row.Item.name})" : row.Item.Name, true);
            AddLabelColumn(table, "class", "Class", 130, row => row.Item.GetType().Name, true);

            table.selectionChanged += _ =>
            {
                if (_refilling)
                    return;

                var row = _table.selectedItem as Row;

                _selected = row?.Item;
                ShowDetail(row);
            };

            return table;
        }

        private void AddLabelColumn(MultiColumnListView table, string name, string title, float width, Func<Row, string> text,
            bool stretchable = false) =>
            table.columns.Add(new Column
            {
                name = name,
                title = title,
                width = width,
                minWidth = 40,
                stretchable = stretchable,
                makeCell = MakeLabel,
                bindCell = (element, index) => ((Label)element).text = text(_visibleRows[index])
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

            image.image = row.Problems.Count > 0 ? EditorGUIUtility.IconContent("console.warnicon.sml").image : null;
            image.tooltip = row.Problems.Count > 0 ? string.Join("\n", row.Problems) : null;
        }

        #endregion

        #region Detail

        private void Select(ItemDef item)
        {
            _selected = item;

            Refresh();
            ShowDetail(FindRow(item));
        }

        private Row FindRow(ItemDef item) => item ? _rows.FirstOrDefault(row => row.Item == item) : null;

        /// <summary>
        /// Shows the selected definition under the table. Built again only when the selection changes, so that the
        /// periodic refresh cannot take the focus away from a field being typed in.
        /// </summary>
        private void ShowDetail(Row row)
        {
            _detail.Clear();
            _detailProblems = null;

            if (row == null)
                return;

            var item = row.Item;

            _detail.Add(new Label(item.name)
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 13, marginBottom = 2 }
            });

            _detailProblems = new HelpBox();
            _detail.Add(_detailProblems);
            UpdateDetailProblems();

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 6 } };

            buttons.Add(new InspectorButtonElement(() => EditorGUIUtility.PingObject(item), "Ping")
            {
                tooltip = "Shows the definition in the Project window, where it can be dragged into a field.",
                style = { flexGrow = 1 }
            });

            buttons.Add(new InspectorButtonElement(() => RemoveItem(item), "Move To Trash")
            {
                style = { flexGrow = 1 }
            });

            _detail.Add(buttons);
            _detail.Add(new InspectorElement(item));
        }

        private void UpdateDetailProblems()
        {
            if (_detailProblems == null)
                return;

            var row = FindRow(_selected);
            var hasProblems = row is { Problems: { Count: > 0 } };

            _detailProblems.style.display = hasProblems ? DisplayStyle.Flex : DisplayStyle.None;
            _detailProblems.messageType = HelpBoxMessageType.Warning;
            _detailProblems.text = hasProblems ? string.Join("\n", row.Problems) : string.Empty;
        }

        #endregion

        private sealed class Row
        {
            public readonly ItemDef Item;
            public readonly List<string> Problems = new();

            public Row(ItemDef item) => Item = item;

            public bool Matches(string search) =>
                Item.ItemDefId.ToString().Contains(search) ||
                Item.name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (Item.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                Item.Type.ToString().Contains(search, StringComparison.OrdinalIgnoreCase);
        }
    }
}
