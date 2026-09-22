using System;
using System.Collections.Generic;
using System.Linq;
using fefek5.Toys.Editor.VisualElements;
using SteamToys.Editor.Core;
using SteamToys.Runtime.StatsSystem;
using Steamworks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SteamToys.Editor.StatsSystem
{
    /// <summary>
    /// The inspector of the stats DB: one table with every stat it holds and every stat Steam has for
    /// the App ID in the Steam Settings, marking what is wrong with each, and the inspector of the
    /// selected stat under it.
    /// <para>
    /// Create From Steam makes a stat for everything Steam has and overwrites the settings of the ones
    /// already there, which keeps the DB in step with the partner site in one click. A stat is always
    /// updated in place rather than made again, because a reference to a sub-asset breaks once it is
    /// made again.
    /// </para>
    /// </summary>
    [CustomEditor(typeof(SteamStatsDB))]
    public class SteamStatsDBEditor : UnityEditor.Editor
    {
        internal const string MenuPath = "Window/Steam Toys/Steam Stats DB";

        private const string StatsField = "_stats";

        private const int RowHeight = 20;
        private const int HeaderHeight = 26;
        private const int MaxVisibleRows = 16;

        private static readonly Color WarningTint = new(1f, 0.76f, 0.03f, 0.25f);
        private static readonly Color ErrorTint = new(1f, 0.25f, 0.25f, 0.3f);

        private readonly List<Row> _rows = new();
        private readonly List<Row> _visibleRows = new();
        private readonly Dictionary<SteamStat, SerializedObject> _serializedStats = new();

        private SchemaLookup _lookup;

        // The stat of the selected row, or the API Name of a stat Steam alone has. Kept apart from the
        // selection of the table, which a filter can hide the row from.
        private object _selectedKey;

        // Set while the table is refilled, which may report a selection change of its own.
        private bool _refilling;

        private HelpBox _status;
        private MultiColumnListView _table;
        private Toggle _problemsOnly;
        private ToolbarSearchField _search;
        private InspectorButtonElement _createButton;
        private InspectorButtonElement _editButton;
        private VisualElement _runtimeButtons;
        private VisualElement _detail;
        private HelpBox _detailProblems;

        [MenuItem(MenuPath)]
        public static void Open() => EditorUtility.OpenPropertyEditor(SteamStatsDB.Instance);

        #region Assets

        /// <summary>
        /// Names a stat of the DB after its API Name, which is what the Project window lists it by. A
        /// stat that is an asset of its own keeps its file name.
        /// </summary>
        internal static void SyncSubAssetName(SteamStat stat)
        {
            if (!AssetDatabase.IsSubAsset(stat) || string.IsNullOrWhiteSpace(stat.ApiName) || stat.name == stat.ApiName)
                return;

            // No undo of its own: undoing the API Name brings the name back along with it.
            stat.name = stat.ApiName;

            EditorUtility.SetDirty(stat);
            Save(stat);
        }

        /// <summary>
        /// Writes the asset <paramref name="asset"/> belongs to and imports it again. The Project window
        /// lists sub-assets as of the last import, so without it a change shows only once the project is
        /// saved.
        /// </summary>
        private static void Save(Object asset)
        {
            AssetDatabase.SaveAssetIfDirty(asset);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(asset));
        }

        /// <summary>
        /// Makes a stat of <paramref name="type"/> and adds it to the DB as a sub-asset, as one undo
        /// step together with the list, which the caller applies.
        /// </summary>
        private SteamStat AddStat(SerializedProperty stats, Type type, string apiName)
        {
            var stat = (SteamStat)CreateInstance(type);

            stat.name = string.IsNullOrWhiteSpace(apiName) ? $"New {ObjectNames.NicifyVariableName(type.Name)}" : apiName;
            stat.ApiName = apiName;

            AssetDatabase.AddObjectToAsset(stat, target);
            Undo.RegisterCreatedObjectUndo(stat, "Add Stat");

            stats.arraySize++;
            stats.GetArrayElementAtIndex(stats.arraySize - 1).objectReferenceValue = stat;

            return stat;
        }

        private void AddEmptyStat(Type type)
        {
            serializedObject.Update();

            var stat = AddStat(serializedObject.FindProperty(StatsField), type, string.Empty);

            serializedObject.ApplyModifiedProperties();
            Save(target);

            Select(stat);
        }

        /// <summary>
        /// Makes a stat for every stat of <paramref name="definitions"/> the DB does not have, and
        /// overwrites the settings of the ones it has. A stat Steam has as another type is left alone:
        /// its type is its class, and making it again would break every reference to it.
        /// </summary>
        private List<SteamStat> CreateFromSteam(IEnumerable<StatDefinition> definitions)
        {
            Undo.SetCurrentGroupName("Create Stats From Steam");

            var group = Undo.GetCurrentGroup();

            serializedObject.Update();

            var stats = serializedObject.FindProperty(StatsField);
            var touched = new List<SteamStat>();
            int created = 0, updated = 0, skipped = 0;

            foreach (var steam in definitions.OrderBy(definition => definition.ApiName, StringComparer.Ordinal))
            {
                var stat = FindListed(stats, steam.ApiName);

                if (!stat)
                {
                    stat = AddStat(stats, StatSettings.GetStatClass(steam.Type), steam.ApiName);
                    created++;
                }
                else if (stat.StatType != steam.Type)
                {
                    skipped++;

                    continue;
                }
                else
                {
                    var serialized = GetSerialized(stat);

                    serialized.Update();

                    if (StatSettings.Compare(StatSettings.Read(serialized), StatSettings.Read(steam)).Any(comparison => !comparison.Matches))
                        updated++;
                }

                StatSettings.CopyFrom(GetSerialized(stat), steam);
                touched.Add(stat);
            }

            serializedObject.ApplyModifiedProperties();
            Undo.CollapseUndoOperations(group);
            Save(target);

            Debug.Log($"[SteamToys] Stats DB: created {created} and updated {updated} from Steam" +
                      (skipped == 0 ? "." : $"; left {skipped} as they are, because Steam has them as another type."), target);

            return touched;
        }

        private void CreateAllFromSteam()
        {
            if (_lookup.Schema == null)
                return;

            var touched = CreateFromSteam(_lookup.Schema.Stats.Values);

            // A stat Steam alone had is a stat of the DB now, and stays selected as one.
            Select(_selectedKey as SteamStat ?? touched.FirstOrDefault(stat => Equals(stat.ApiName, _selectedKey)));
        }

        private void RemoveStat(SteamStat stat)
        {
            var statName = stat.name;

            if (!EditorUtility.DisplayDialog("Remove Stat",
                    $"Remove '{statName}' from the stats DB?\n\nFields that reference it lose it. Undo brings it back.",
                    "Remove", "Cancel"))
                return;

            Undo.SetCurrentGroupName($"Remove Stat {statName}");

            var group = Undo.GetCurrentGroup();

            serializedObject.Update();

            var stats = serializedObject.FindProperty(StatsField);

            for (var i = stats.arraySize - 1; i >= 0; i--)
            {
                if (stats.GetArrayElementAtIndex(i).objectReferenceValue == stat)
                    DeleteElement(stats, i);
            }

            serializedObject.ApplyModifiedProperties();
            Undo.DestroyObjectImmediate(stat);
            Undo.CollapseUndoOperations(group);
            Save(target);

            Select(null);
        }

        /// <summary>
        /// Makes the list match the sub-assets of the DB: drops entries whose stat is gone or lives
        /// elsewhere, and lists sub-assets it misses. Undo keeps the two in step on its own, so this is
        /// for whatever happened to the file behind the editor's back.
        /// </summary>
        private void RepairStatList()
        {
            var path = AssetDatabase.GetAssetPath(target);

            if (string.IsNullOrEmpty(path))
                return;

            var subAssets = AssetDatabase.LoadAllAssetsAtPath(path).OfType<SteamStat>().ToList();
            var stats = serializedObject.FindProperty(StatsField);
            var listed = new HashSet<SteamStat>();
            var changed = false;

            for (var i = 0; i < stats.arraySize; i++)
            {
                var stat = stats.GetArrayElementAtIndex(i).objectReferenceValue as SteamStat;

                if (stat && subAssets.Contains(stat) && listed.Add(stat))
                    continue;

                DeleteElement(stats, i--);
                changed = true;
            }

            foreach (var stat in subAssets.Where(stat => !listed.Contains(stat)))
            {
                stats.arraySize++;
                stats.GetArrayElementAtIndex(stats.arraySize - 1).objectReferenceValue = stat;
                changed = true;
            }

            if (!changed)
                return;

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            Save(target);
        }

        private static void DeleteElement(SerializedProperty array, int index)
        {
            // Cleared first: deleting an element that still references an object has only cleared it in
            // some versions of Unity.
            array.GetArrayElementAtIndex(index).objectReferenceValue = null;
            array.DeleteArrayElementAtIndex(index);
        }

        private static SteamStat FindListed(SerializedProperty stats, string apiName)
        {
            for (var i = 0; i < stats.arraySize; i++)
            {
                if (stats.GetArrayElementAtIndex(i).objectReferenceValue is SteamStat stat && stat && stat.ApiName == apiName)
                    return stat;
            }

            return null;
        }

        private List<SteamStat> GetListedStats()
        {
            var stats = serializedObject.FindProperty(StatsField);
            var listed = new List<SteamStat>(stats.arraySize);

            for (var i = 0; i < stats.arraySize; i++)
            {
                if (stats.GetArrayElementAtIndex(i).objectReferenceValue is SteamStat stat && stat)
                    listed.Add(stat);
            }

            return listed;
        }

        private SerializedObject GetSerialized(SteamStat stat)
        {
            if (!_serializedStats.TryGetValue(stat, out var serialized))
                _serializedStats[stat] = serialized = new SerializedObject(stat);

            return serialized;
        }

        private void OnDisable()
        {
            foreach (var serialized in _serializedStats.Values)
                serialized.Dispose();

            _serializedStats.Clear();
        }

        #endregion

        #region Inspector

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            _status = new HelpBox();
            root.Add(_status);

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 2 } };

            _createButton = new InspectorButtonElement(CreateAllFromSteam, "Create From Steam")
            {
                tooltip = "Makes a stat for every stat Steam has and the DB does not, and overwrites the settings " +
                          "of the ones it has with what Steam has. A stat Steam has as another type is left alone.",
                style = { flexGrow = 1 }
            };

            var addButton = new InspectorButtonElement(ShowAddMenu, "Add Stat")
            {
                tooltip = "Adds an empty stat of the chosen class, including classes the game derives from the stats.",
                style = { flexGrow = 1 }
            };

            _editButton = new InspectorButtonElement(
                () => Application.OpenURL(string.Format(SteamStatEditor.EditOnSteamUrl, _lookup.AppId.m_AppId)),
                "Edit on Steam")
            {
                tooltip = "Opens the page the stats of this app are edited on. A change shows up here once it is " +
                          "published and the game has connected to Steam again.",
                style = { flexGrow = 1 }
            };

            buttons.Add(_createButton);
            buttons.Add(addButton);
            buttons.Add(_editButton);
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

            root.Add(CreateRuntimeSection());

            _detail = new VisualElement { style = { marginTop = 10 } };
            root.Add(_detail);

            Refresh();
            ShowDetail(FindRow(_selectedKey));

            // Polled, because what changes the schema is the Steam client and nothing tells the editor,
            // and the values move all the time in play mode.
            root.schedule.Execute(Refresh).Every(1000);
            root.TrackSerializedObjectValue(serializedObject, _ => Refresh());

            return root;
        }

        private VisualElement CreateRuntimeSection()
        {
            var section = new VisualElement { style = { marginTop = 6 } };

            section.Add(new Label("Values")
            {
                tooltip = "Need a Steam session: play mode, or Window/Steam Toys/Connect To Steam.",
                style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 2 }
            });

            _runtimeButtons = new VisualElement { style = { flexDirection = FlexDirection.Row } };

            _runtimeButtons.Add(new InspectorButtonElement(() => ((SteamStatsDB)target).PullAllFromSteam(), "Pull All From Steam")
            {
                tooltip = "Replaces the cached value of every stat of the DB with the one the Steam client holds.",
                style = { flexGrow = 1 }
            });

            _runtimeButtons.Add(new InspectorButtonElement(() => SteamStatsDB.StoreStats(), "Store Stats")
            {
                tooltip = "Stores every stat changed since the last store on the Steam servers, for the signed-in account.",
                style = { flexGrow = 1 }
            });

            _runtimeButtons.Add(new InspectorButtonElement(ResetAllStats, "Reset All Stats")
            {
                tooltip = "Resets every stat of the signed-in account to its default on the Steam servers.",
                style = { flexGrow = 1 }
            });

            section.Add(_runtimeButtons);

            return section;
        }

        private static void ResetAllStats()
        {
            var choice = EditorUtility.DisplayDialogComplex("Reset All Stats",
                "Reset every stat of the signed-in account to its default on the Steam servers?\n\n" +
                "This wipes real progress and cannot be undone. Pull All From Steam picks up the reset values.",
                "Reset Stats", "Cancel", "Reset Stats And Achievements");

            if (choice != 1)
                SteamStatsDB.ResetAllStats(achievementsToo: choice == 2);
        }

        private void ShowAddMenu()
        {
            var menu = new GenericMenu();

            foreach (var type in TypeCache.GetTypesDerivedFrom<SteamStat>().OrderBy(type => type.Name))
            {
                if (!type.IsAbstract && !type.IsGenericTypeDefinition)
                    menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(type.Name)), false, () => AddEmptyStat(type));
            }

            menu.ShowAsContext();
        }

        private void Refresh()
        {
            if (!target)
                return;

            serializedObject.UpdateIfRequiredOrScript();

            RepairStatList();

            _lookup = StatSettings.FindSchema("the stats");

            BuildRows();

            var withProblems = _rows.Count(row => row.Stat && row.Severity >= HelpBoxMessageType.Warning);
            var steamOnly = _rows.Count(row => !row.Stat);

            _status.messageType = withProblems + steamOnly > 0 ? HelpBoxMessageType.Warning : _lookup.MessageType;
            _status.text = $"{_lookup.Message}\n{_rows.Count - steamOnly} stats in the DB, {withProblems} with problems" +
                           (steamOnly == 0 ? "." : $", {steamOnly} on Steam only.");

            _createButton.SetEnabled(_lookup.Schema != null);
            _editButton.SetEnabled(_lookup.AppId != AppId_t.Invalid);
            _runtimeButtons.SetEnabled(SteamStatsDB.Initialized);

            // A stat that was removed, or undone, takes its inspector with it.
            if (_selectedKey is Object selected && !selected)
                Select(null);

            ApplyFilter();
            UpdateDetailProblems();
        }

        private void BuildRows()
        {
            _rows.Clear();

            var stats = GetListedStats();

            foreach (var stat in _serializedStats.Keys.Where(stat => !stats.Contains(stat)).ToList())
            {
                _serializedStats[stat].Dispose();
                _serializedStats.Remove(stat);
            }

            var schema = _lookup.Schema;

            foreach (var stat in stats)
            {
                var serialized = GetSerialized(stat);

                serialized.UpdateIfRequiredOrScript();

                var values = StatSettings.Read(serialized);
                var apiName = stat.ApiName;
                StatDefinition? steam = null;

                if (schema != null && !string.IsNullOrWhiteSpace(apiName) && schema.Stats.TryGetValue(apiName, out var found))
                    steam = found;

                var row = new Row(stat, apiName, steam, StatSettings.Compare(values, steam.HasValue ? StatSettings.Read(steam.Value) : null));

                if (string.IsNullOrWhiteSpace(apiName))
                    row.Add(HelpBoxMessageType.Error, "Set the API Name: without it the stat cannot reach Steam.");
                else
                {
                    if (apiName.Trim() != apiName)
                        row.Add(HelpBoxMessageType.Warning, "The API Name starts or ends with whitespace.");

                    if (stats.Count(other => other.ApiName == apiName) > 1)
                        row.Add(HelpBoxMessageType.Error, "Another stat of the DB has the same API Name, so both read and write the same stat on Steam.");

                    if (schema != null)
                        AddSteamProblems(row);
                }

                foreach (var problem in StatSettings.FindProblems(values))
                    row.Add(HelpBoxMessageType.Warning, problem);

                _rows.Add(row);
            }

            if (schema == null)
                return;

            foreach (var steam in schema.Stats.Values.OrderBy(definition => definition.ApiName, StringComparer.Ordinal))
            {
                if (stats.Any(stat => stat.ApiName == steam.ApiName))
                    continue;

                var row = new Row(null, steam.ApiName, steam, StatSettings.Compare(null, StatSettings.Read(steam)));

                row.Add(HelpBoxMessageType.Warning, "Steam has this stat and the DB does not. Create From Steam adds it.");
                _rows.Add(row);
            }
        }

        private static void AddSteamProblems(Row row)
        {
            if (row.Steam is not { } steam)
            {
                row.Add(HelpBoxMessageType.Warning, $"Steam has no stat '{row.ApiName}'. Add it with Edit on Steam and publish the change.");

                return;
            }

            if (!row.Find(StatSetting.Type).Matches)
            {
                row.Add(HelpBoxMessageType.Error,
                    $"Steam has this stat as {steam.Type}, and the type comes from the class of the asset, so Create From " +
                    $"Steam leaves it alone: it takes a {StatSettings.GetAssetName(steam.Type)} instead.");

                return;
            }

            var differing = row.Settings.Where(comparison => !comparison.Matches).Select(comparison => StatSettings.GetLabel(comparison.Setting)).ToList();

            if (differing.Count > 0)
                row.Add(HelpBoxMessageType.Warning, $"Differs from Steam in {string.Join(", ", differing)}. Create From Steam overwrites it.");
        }

        private void ApplyFilter()
        {
            _visibleRows.Clear();

            var search = _search.value?.Trim();

            foreach (var row in _rows)
            {
                if (_problemsOnly.value && row.Severity < HelpBoxMessageType.Warning)
                    continue;

                if (!string.IsNullOrEmpty(search) &&
                    row.ApiName?.IndexOf(search, StringComparison.OrdinalIgnoreCase) is null or < 0 &&
                    row.Steam?.DisplayName?.IndexOf(search, StringComparison.OrdinalIgnoreCase) is null or < 0)
                    continue;

                _visibleRows.Add(row);
            }

            // Sized to its rows: an inspector gives a list no height of its own to fill.
            _table.style.height = HeaderHeight + RowHeight * Mathf.Clamp(_visibleRows.Count, 1, MaxVisibleRows);

            var index = _visibleRows.FindIndex(row => Equals(row.Key, _selectedKey));

            _refilling = true;

            try
            {
                _table.RefreshItems();
                _table.SetSelectionWithoutNotify(index < 0 ? Array.Empty<int>() : new[] { index });
            }
            finally
            {
                _refilling = false;
            }
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
                style = { marginTop = 2 }
            };

            table.columns.Add(new Column
            {
                name = "status",
                width = 24,
                resizable = false,
                makeCell = () => new Image { style = { width = 16, height = 16, alignSelf = Align.Center, marginTop = 2 } },
                bindCell = BindStatus
            });

            table.columns.Add(new Column
            {
                name = "apiName",
                title = "API Name",
                width = 160,
                minWidth = 80,
                stretchable = true,
                makeCell = MakeLabel,
                bindCell = BindApiName
            });

            AddSettingColumn(table, StatSetting.Type, "Type", 64);
            AddSettingColumn(table, StatSetting.DefaultValue, "Default", 60);
            AddSettingColumn(table, StatSetting.MinValue, "Min", 56);
            AddSettingColumn(table, StatSetting.MaxValue, "Max", 56);
            AddSettingColumn(table, StatSetting.MaxChange, "Max Change", 76);
            AddSettingColumn(table, StatSetting.IncrementOnly, "Increment Only", 94);
            AddSettingColumn(table, StatSetting.WindowSize, "Window", 60);

            table.columns.Add(new Column
            {
                name = "value",
                title = "Value",
                width = 72,
                makeCell = MakeLabel,
                bindCell = BindValue
            });

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

        private void AddSettingColumn(MultiColumnListView table, StatSetting setting, string title, float width) =>
            table.columns.Add(new Column
            {
                name = setting.ToString(),
                title = title,
                width = width,
                makeCell = MakeLabel,
                bindCell = (element, index) => BindSetting((Label)element, _visibleRows[index], setting)
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
                _ => row.Steam.HasValue ? "GreenCheckmark" : null
            };

            image.image = icon == null ? null : EditorGUIUtility.IconContent(icon).image;
            image.tooltip = row.Problems.Count > 0 ? row.DescribeProblems() : row.Steam.HasValue ? "Matches Steam" : null;
        }

        private void BindApiName(VisualElement element, int index)
        {
            var label = (Label)element;
            var row = _visibleRows[index];

            label.text = string.IsNullOrWhiteSpace(row.ApiName) ? "(no API Name)" : row.ApiName;
            label.tooltip = row.Steam?.DisplayName;
            label.style.opacity = row.Stat ? 1f : 0.55f;
        }

        private static void BindSetting(Label label, Row row, StatSetting setting)
        {
            var comparison = row.Settings.FirstOrDefault(candidate => candidate.Setting == setting);
            var has = row.Settings.Any(candidate => candidate.Setting == setting);

            // A stat shows what its asset holds, and a stat Steam alone has what Steam holds.
            label.text = !has ? string.Empty : row.Stat ? comparison.Asset : comparison.Steam;
            label.style.opacity = row.Stat ? 1f : 0.55f;

            var differs = has && row.Stat && !comparison.Matches;

            label.style.backgroundColor = differs
                ? new StyleColor(setting == StatSetting.Type ? ErrorTint : WarningTint)
                : new StyleColor(StyleKeyword.Null);
            label.tooltip = differs ? $"Steam: {comparison.Steam}" : null;
        }

        private void BindValue(VisualElement element, int index)
        {
            var label = (Label)element;
            var stat = _visibleRows[index].Stat;

            label.text = stat ? stat.GetValueString() : string.Empty;
            label.tooltip = stat && !stat.IsSynced ? "The default: not read from Steam or written yet." : null;
        }

        #endregion

        #region Detail

        private void Select(SteamStat stat)
        {
            _selectedKey = stat;

            Refresh();
            ShowDetail(FindRow(stat));
        }

        private Row FindRow(object key) => key == null ? null : _rows.FirstOrDefault(row => Equals(row.Key, key));

        /// <summary>
        /// Shows the selected stat under the table. Built again only when the selection changes, so that
        /// the periodic refresh cannot take the focus away from a field being typed in.
        /// </summary>
        private void ShowDetail(Row row)
        {
            _detail.Clear();
            _detailProblems = null;

            if (row == null)
                return;

            _detail.Add(new Label(row.Stat ? row.Stat.name : row.ApiName)
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 13, marginBottom = 2 }
            });

            _detailProblems = new HelpBox();
            _detail.Add(_detailProblems);
            UpdateDetailProblems();

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 6 } };

            _detail.Add(buttons);

            if (row.Stat)
            {
                var stat = row.Stat;

                buttons.Add(new InspectorButtonElement(() => EditorGUIUtility.PingObject(stat), "Ping")
                {
                    tooltip = "Shows the stat in the Project window, where it can be dragged into a field.",
                    style = { flexGrow = 1 }
                });

                buttons.Add(new InspectorButtonElement(() => RemoveStat(stat), "Remove From DB")
                {
                    style = { flexGrow = 1 }
                });

                _detail.Add(new InspectorElement(stat));
            }
            else if (row.Steam is { } steam)
            {
                buttons.Add(new InspectorButtonElement(() => Select(CreateFromSteam(new[] { steam }).FirstOrDefault()), "Create From Steam")
                {
                    tooltip = "Makes this stat, with the settings Steam has.",
                    style = { flexGrow = 1 }
                });
            }
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
        }

        #endregion

        /// <summary>
        /// One line of the table: a stat of the DB with the stat Steam has under its API Name, or a stat
        /// Steam alone has.
        /// </summary>
        private sealed class Row
        {
            public readonly SteamStat Stat;
            public readonly string ApiName;
            public readonly StatDefinition? Steam;
            public readonly List<SettingComparison> Settings;
            public readonly List<(HelpBoxMessageType type, string text)> Problems = new();

            public Row(SteamStat stat, string apiName, StatDefinition? steam, List<SettingComparison> settings)
            {
                Stat = stat;
                ApiName = apiName;
                Steam = steam;
                Settings = settings;
            }

            /// <summary>What the row stands for across refreshes: its stat, or the API Name of a stat Steam alone has.</summary>
            public object Key => Stat ? Stat : (object)ApiName;

            public HelpBoxMessageType Severity =>
                Problems.Count == 0 ? HelpBoxMessageType.None : Problems.Max(problem => problem.type);

            public void Add(HelpBoxMessageType type, string text) => Problems.Add((type, text));

            public SettingComparison Find(StatSetting setting) => Settings.First(comparison => comparison.Setting == setting);

            public string DescribeProblems() => string.Join("\n", Problems.Select(problem => problem.text));
        }
    }
}
