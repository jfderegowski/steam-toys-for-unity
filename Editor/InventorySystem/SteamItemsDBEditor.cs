using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using fefek5.Toys.Editor.VisualElements;
using SteamToys.Runtime.InventorySystem;
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

            // Counted against the loaded list: a lookup by ID reloads the folder on every miss.
            var ids = new HashSet<ulong>(db.Items.Where(item => item).Select(item => item.ItemDefId));
            var existing = definitions.Count(definition => ids.Contains(definition.ItemDefId));

            if (!EditorUtility.DisplayDialog("Create From Steam",
                    $"Steam has {definitions.Count} item definitions for app {SteamItemsDB.AppId}.\n\n" +
                    $"Create {definitions.Count - existing} in {SteamItemsDB.AssetsFolder} and update {existing} the DB has?\n\n" +
                    "Only these fields are written, each for the types that use it: names, descriptions, icons, colors, " +
                    "trading flags, tags, exchange recipes and tag generators. The others, such as contents, prices, " +
                    "promo rules and drop settings, are left as they are.",
                    "Create And Update", "Cancel"))
                return;

            var report = ItemDefsFromSteam.Apply(db, definitions);

            foreach (var problem in report.Problems)
                Debug.LogWarning($"[SteamToys] Create From Steam: {problem}");

            EditorUtility.DisplayDialog("Create From Steam",
                $"Created {report.Created.Count} and updated {report.Updated.Count} item definitions." +
                (report.Problems.Count == 0 ? string.Empty : $"\n\n{report.Problems.Count} could not be taken over in full; the console lists them."),
                "OK");

            Refresh();
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
                item.CollectSchemaWarnings(row.Problems);

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
