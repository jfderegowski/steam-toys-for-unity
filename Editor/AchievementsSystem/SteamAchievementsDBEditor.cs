using System;
using System.Collections.Generic;
using System.Linq;
using fefek5.Toys.Editor.VisualElements;
using SteamToys.Editor.Core;
using SteamToys.Editor.StatsSystem;
using SteamToys.Runtime.AchievementsSystem;
using SteamToys.Runtime.StatsSystem;
using Steamworks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SteamToys.Editor.AchievementsSystem
{
    /// <summary>
    /// The inspector of the achievements DB: one table with every achievement it holds and every
    /// achievement Steam has for the App ID in the Steam Settings, marking what is wrong with each, and
    /// the inspector of the selected achievement under it. Built like the inspector of the stats DB.
    /// <para>
    /// Create From Steam makes an achievement for everything Steam has and overwrites the settings of
    /// the ones already there, updating them in place so that references to them survive.
    /// </para>
    /// </summary>
    [CustomEditor(typeof(SteamAchievementsDB))]
    public class SteamAchievementsDBEditor : UnityEditor.Editor
    {
        internal const string MenuPath = "Window/Steam Toys/Steam Achievements DB";

        private const string AchievementsField = "_achievements";

        private const int RowHeight = 20;
        private const int HeaderHeight = 26;
        private const int MaxVisibleRows = 16;

        private static readonly Color WarningTint = new(1f, 0.76f, 0.03f, 0.25f);

        private readonly List<Row> _rows = new();
        private readonly List<Row> _visibleRows = new();
        private readonly Dictionary<SteamAchievement, SerializedObject> _serializedAchievements = new();

        private SchemaLookup _lookup;

        // The achievement of the selected row, or the API Name of an achievement Steam alone has.
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
        public static void Open() => EditorUtility.OpenPropertyEditor(SteamAchievementsDB.Instance);

        #region Assets

        /// <summary>
        /// Names an achievement of the DB after its API Name, which is what the Project window lists it
        /// by. An achievement that is an asset of its own keeps its file name.
        /// </summary>
        internal static void SyncSubAssetName(SteamAchievement achievement)
        {
            if (!AssetDatabase.IsSubAsset(achievement) || string.IsNullOrWhiteSpace(achievement.ApiName) ||
                achievement.name == achievement.ApiName)
                return;

            achievement.name = achievement.ApiName;

            EditorUtility.SetDirty(achievement);
            Save(achievement);
        }

        /// <summary>Writes the asset and imports it again, so the Project window lists its sub-assets.</summary>
        private static void Save(Object asset)
        {
            AssetDatabase.SaveAssetIfDirty(asset);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(asset));
        }

        /// <summary>
        /// Makes an achievement of <paramref name="type"/> and adds it to the DB as a sub-asset, as one
        /// undo step together with the list, which the caller applies.
        /// </summary>
        private SteamAchievement AddAchievement(SerializedProperty achievements, Type type, string apiName)
        {
            var achievement = (SteamAchievement)CreateInstance(type);

            achievement.name = string.IsNullOrWhiteSpace(apiName) ? $"New {ObjectNames.NicifyVariableName(type.Name)}" : apiName;
            achievement.ApiName = apiName;

            AssetDatabase.AddObjectToAsset(achievement, target);
            Undo.RegisterCreatedObjectUndo(achievement, "Add Achievement");

            achievements.arraySize++;
            achievements.GetArrayElementAtIndex(achievements.arraySize - 1).objectReferenceValue = achievement;

            return achievement;
        }

        private void AddEmptyAchievement(Type type)
        {
            serializedObject.Update();

            var achievement = AddAchievement(serializedObject.FindProperty(AchievementsField), type, string.Empty);

            serializedObject.ApplyModifiedProperties();
            Save(target);

            Select(achievement);
        }

        /// <summary>
        /// Makes an achievement for every achievement of <paramref name="definitions"/> the DB does not
        /// have, and overwrites the settings of the ones it has.
        /// </summary>
        private List<SteamAchievement> CreateFromSteam(IEnumerable<AchievementDefinition> definitions)
        {
            Undo.SetCurrentGroupName("Create Achievements From Steam");

            var group = Undo.GetCurrentGroup();

            serializedObject.Update();

            var achievements = serializedObject.FindProperty(AchievementsField);
            var touched = new List<SteamAchievement>();
            int created = 0, updated = 0;
            var missingStats = new SortedSet<string>(StringComparer.Ordinal);
            var withIcons = new List<(SerializedObject, AchievementDefinition)>();

            foreach (var steam in definitions.OrderBy(definition => definition.ApiName, StringComparer.Ordinal))
            {
                var achievement = FindListed(achievements, steam.ApiName);

                if (!achievement)
                {
                    achievement = AddAchievement(achievements, typeof(SteamAchievement), steam.ApiName);
                    created++;
                }
                else
                {
                    var existing = GetSerialized(achievement);

                    existing.Update();

                    if (AchievementSettings.Compare(AchievementSettings.Read(existing), AchievementSettings.Read(steam))
                        .Any(comparison => !comparison.Matches))
                        updated++;
                }

                if (!AchievementSettings.CopyFrom(GetSerialized(achievement), steam))
                    missingStats.Add(steam.ProgressStat);

                touched.Add(achievement);
                withIcons.Add((GetSerialized(achievement), steam));
            }

            serializedObject.ApplyModifiedProperties();

            // After the list is applied: downloading imports the icons, and an import must not find the
            // DB half written.
            var icons = AchievementIcons.Download(_lookup.AppId, withIcons);

            Undo.CollapseUndoOperations(group);
            Save(target);

            Debug.Log($"[SteamToys] Achievements DB: created {created} and updated {updated} from Steam, " +
                      $"downloaded {icons} icons" +
                      (missingStats.Count == 0
                          ? "."
                          : $"; no stat asset has the API Name of the progress stats {string.Join(", ", missingStats)}, " +
                            "so those achievements were left without one. Add the stats to the stats DB and run it again."),
                target);

            return touched;
        }

        private void CreateAllFromSteam()
        {
            if (_lookup.Schema == null)
                return;

            var touched = CreateFromSteam(_lookup.Schema.Achievements.Values);

            Select(_selectedKey as SteamAchievement ?? touched.FirstOrDefault(achievement => Equals(achievement.ApiName, _selectedKey)));
        }

        private void RemoveAchievement(SteamAchievement achievement)
        {
            var achievementName = achievement.name;

            if (!EditorUtility.DisplayDialog("Remove Achievement",
                    $"Remove '{achievementName}' from the achievements DB?\n\nFields that reference it lose it. Undo brings it back. " +
                    $"Its icons stay in {AchievementIcons.Folder}.",
                    "Remove", "Cancel"))
                return;

            Undo.SetCurrentGroupName($"Remove Achievement {achievementName}");

            var group = Undo.GetCurrentGroup();

            serializedObject.Update();

            var achievements = serializedObject.FindProperty(AchievementsField);

            for (var i = achievements.arraySize - 1; i >= 0; i--)
            {
                if (achievements.GetArrayElementAtIndex(i).objectReferenceValue == achievement)
                    DeleteElement(achievements, i);
            }

            serializedObject.ApplyModifiedProperties();
            Undo.DestroyObjectImmediate(achievement);
            Undo.CollapseUndoOperations(group);
            Save(target);

            Select(null);
        }

        /// <summary>
        /// Makes the list match the sub-assets of the DB, for whatever happened to the file behind the
        /// editor's back.
        /// </summary>
        private void RepairAchievementList()
        {
            var path = AssetDatabase.GetAssetPath(target);

            if (string.IsNullOrEmpty(path))
                return;

            var subAssets = AssetDatabase.LoadAllAssetsAtPath(path).OfType<SteamAchievement>().ToList();
            var achievements = serializedObject.FindProperty(AchievementsField);
            var listed = new HashSet<SteamAchievement>();
            var changed = false;

            for (var i = 0; i < achievements.arraySize; i++)
            {
                var achievement = achievements.GetArrayElementAtIndex(i).objectReferenceValue as SteamAchievement;

                if (achievement && subAssets.Contains(achievement) && listed.Add(achievement))
                    continue;

                DeleteElement(achievements, i--);
                changed = true;
            }

            foreach (var achievement in subAssets.Where(achievement => !listed.Contains(achievement)))
            {
                achievements.arraySize++;
                achievements.GetArrayElementAtIndex(achievements.arraySize - 1).objectReferenceValue = achievement;
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

        private static SteamAchievement FindListed(SerializedProperty achievements, string apiName)
        {
            for (var i = 0; i < achievements.arraySize; i++)
            {
                if (achievements.GetArrayElementAtIndex(i).objectReferenceValue is SteamAchievement achievement &&
                    achievement && achievement.ApiName == apiName)
                    return achievement;
            }

            return null;
        }

        private List<SteamAchievement> GetListedAchievements()
        {
            var achievements = serializedObject.FindProperty(AchievementsField);
            var listed = new List<SteamAchievement>(achievements.arraySize);

            for (var i = 0; i < achievements.arraySize; i++)
            {
                if (achievements.GetArrayElementAtIndex(i).objectReferenceValue is SteamAchievement achievement && achievement)
                    listed.Add(achievement);
            }

            return listed;
        }

        private SerializedObject GetSerialized(SteamAchievement achievement)
        {
            if (!_serializedAchievements.TryGetValue(achievement, out var serialized))
                _serializedAchievements[achievement] = serialized = new SerializedObject(achievement);

            return serialized;
        }

        private void OnDisable()
        {
            foreach (var serialized in _serializedAchievements.Values)
                serialized.Dispose();

            _serializedAchievements.Clear();
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
                tooltip = "Makes an achievement for every achievement Steam has and the DB does not, and overwrites " +
                          "the settings of the ones it has with what Steam has, downloading icons that changed. " +
                          "Progress stats are found among the stat assets by their API Name.",
                style = { flexGrow = 1 }
            };

            var addButton = new InspectorButtonElement(ShowAddMenu, "Add Achievement")
            {
                tooltip = "Adds an empty achievement of the chosen class, including classes the game derives from it.",
                style = { flexGrow = 1 }
            };

            _editButton = new InspectorButtonElement(
                () => Application.OpenURL(string.Format(AchievementSettings.EditOnSteamUrl, _lookup.AppId.m_AppId)),
                "Edit on Steam")
            {
                tooltip = "Opens the page the achievements of this app are edited on. A change shows up here once it " +
                          "is published and the game has connected to Steam again.",
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
            // and the states change in play mode.
            root.schedule.Execute(Refresh).Every(1000);
            root.TrackSerializedObjectValue(serializedObject, _ => Refresh());

            return root;
        }

        private VisualElement CreateRuntimeSection()
        {
            var section = new VisualElement { style = { marginTop = 6 } };

            section.Add(new Label("States")
            {
                tooltip = "Need a Steam session: play mode, or Window/Steam Toys/Connect To Steam.",
                style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 2 }
            });

            _runtimeButtons = new VisualElement { style = { flexDirection = FlexDirection.Row } };

            _runtimeButtons.Add(new InspectorButtonElement(() => ((SteamAchievementsDB)target).PullAllFromSteam(), "Pull All From Steam")
            {
                tooltip = "Replaces the cached state of every achievement of the DB with the one the Steam client holds.",
                style = { flexGrow = 1 }
            });

            _runtimeButtons.Add(new InspectorButtonElement(() => SteamStatsDB.StoreStats(), "Store Stats")
            {
                tooltip = "Stores every stat and achievement changed since the last store on the Steam servers, for the " +
                          "signed-in account.",
                style = { flexGrow = 1 }
            });

            _runtimeButtons.Add(new InspectorButtonElement(ClearAllAchievements, "Clear All Achievements")
            {
                tooltip = "Locks every achievement of the signed-in account again, leaving the stats alone.",
                style = { flexGrow = 1 }
            });

            section.Add(_runtimeButtons);

            return section;
        }

        private void ClearAllAchievements()
        {
            if (!EditorUtility.DisplayDialog("Clear All Achievements",
                    "Lock every achievement of the signed-in account again on the Steam servers?\n\n" +
                    "This wipes real progress and cannot be undone. The stats are left alone.",
                    "Clear Achievements", "Cancel"))
                return;

            if (SteamAchievementsDB.ClearAllAchievements())
                ((SteamAchievementsDB)target).PullAllFromSteam();
        }

        private void ShowAddMenu()
        {
            var menu = new GenericMenu();

            foreach (var type in TypeCache.GetTypesDerivedFrom<SteamAchievement>().Append(typeof(SteamAchievement)).OrderBy(type => type.Name))
            {
                if (!type.IsAbstract && !type.IsGenericTypeDefinition)
                    menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(type.Name)), false, () => AddEmptyAchievement(type));
            }

            menu.ShowAsContext();
        }

        private void Refresh()
        {
            if (!target)
                return;

            serializedObject.UpdateIfRequiredOrScript();

            RepairAchievementList();

            _lookup = StatSettings.FindSchema("the achievements");

            BuildRows();

            var withProblems = _rows.Count(row => row.Achievement && row.Severity >= HelpBoxMessageType.Warning);
            var steamOnly = _rows.Count(row => !row.Achievement);

            _status.messageType = withProblems + steamOnly > 0 ? HelpBoxMessageType.Warning : _lookup.MessageType;
            _status.text = $"{_lookup.Message}\n{_rows.Count - steamOnly} achievements in the DB, {withProblems} with problems" +
                           (steamOnly == 0 ? "." : $", {steamOnly} on Steam only.");

            _createButton.SetEnabled(_lookup.Schema != null);
            _editButton.SetEnabled(_lookup.AppId != AppId_t.Invalid);
            _runtimeButtons.SetEnabled(SteamAchievementsDB.Initialized);

            if (_selectedKey is Object selected && !selected)
                Select(null);

            ApplyFilter();
            UpdateDetailProblems();
        }

        private void BuildRows()
        {
            _rows.Clear();

            var achievements = GetListedAchievements();

            foreach (var achievement in _serializedAchievements.Keys.Where(achievement => !achievements.Contains(achievement)).ToList())
            {
                _serializedAchievements[achievement].Dispose();
                _serializedAchievements.Remove(achievement);
            }

            var schema = _lookup.Schema;

            // Whether a stat asset has an API Name, remembered for this refresh: finding out looks
            // through every stat asset of the project.
            var statExists = new Dictionary<string, bool>(StringComparer.Ordinal);

            foreach (var achievement in achievements)
            {
                var serialized = GetSerialized(achievement);

                serialized.UpdateIfRequiredOrScript();

                var values = AchievementSettings.Read(serialized);
                var apiName = achievement.ApiName;
                AchievementDefinition? steam = null;

                if (schema != null && !string.IsNullOrWhiteSpace(apiName) && schema.Achievements.TryGetValue(apiName, out var found))
                    steam = found;

                var row = new Row(achievement, apiName, steam,
                    AchievementSettings.Compare(values, steam.HasValue ? AchievementSettings.Read(steam.Value) : null));

                if (string.IsNullOrWhiteSpace(apiName))
                    row.Add(HelpBoxMessageType.Error, "Set the API Name: without it the achievement cannot reach Steam.");
                else
                {
                    if (apiName.Trim() != apiName)
                        row.Add(HelpBoxMessageType.Warning, "The API Name starts or ends with whitespace.");

                    if (achievements.Count(other => other.ApiName == apiName) > 1)
                        row.Add(HelpBoxMessageType.Error, "Another achievement of the DB has the same API Name, so both unlock the same achievement on Steam.");

                    if (schema != null)
                        AddSteamProblems(row, values, statExists);
                }

                foreach (var problem in AchievementSettings.FindProblems(values))
                    row.Add(HelpBoxMessageType.Warning, problem);

                _rows.Add(row);
            }

            if (schema == null)
                return;

            foreach (var steam in schema.Achievements.Values.OrderBy(definition => definition.ApiName, StringComparer.Ordinal))
            {
                if (achievements.Any(achievement => achievement.ApiName == steam.ApiName))
                    continue;

                var row = new Row(null, steam.ApiName, steam, AchievementSettings.Compare(null, AchievementSettings.Read(steam)));

                row.Add(HelpBoxMessageType.Warning, "Steam has this achievement and the DB does not. Create From Steam adds it.");
                _rows.Add(row);
            }
        }

        private static void AddSteamProblems(Row row, AchievementValues values, Dictionary<string, bool> statExists)
        {
            if (row.Steam is not { } steam)
            {
                row.Add(HelpBoxMessageType.Warning, $"Steam has no achievement '{row.ApiName}'. Add it with Edit on Steam and publish the change.");

                return;
            }

            var differing = row.Settings.Where(comparison => !comparison.Matches)
                .Select(comparison => AchievementSettings.GetLabel(comparison.Setting)).ToList();

            if (differing.Count == 0)
                return;

            if (steam.ProgressStat != null && values.ProgressStat != steam.ProgressStat)
            {
                if (!statExists.TryGetValue(steam.ProgressStat, out var exists))
                    statExists[steam.ProgressStat] = exists = AchievementSettings.FindStat(steam.ProgressStat);

                if (!exists)
                {
                    row.Add(HelpBoxMessageType.Warning,
                        $"Steam ties this achievement to the stat '{steam.ProgressStat}', and no stat asset has that API " +
                        "Name. Add it to the stats DB, then Create From Steam again.");

                    return;
                }
            }

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
                name = "icon",
                width = 24,
                resizable = false,
                makeCell = () => new Image { scaleMode = ScaleMode.ScaleToFit, style = { width = 18, height = 18, alignSelf = Align.Center, marginTop = 1 } },
                bindCell = BindIcon
            });

            table.columns.Add(new Column
            {
                name = "apiName",
                title = "API Name",
                width = 150,
                minWidth = 80,
                stretchable = true,
                makeCell = MakeLabel,
                bindCell = BindApiName
            });

            table.columns.Add(new Column
            {
                name = "displayName",
                title = "Display Name",
                width = 130,
                minWidth = 60,
                stretchable = true,
                makeCell = MakeLabel,
                bindCell = BindDisplayName
            });

            AddSettingColumn(table, AchievementSetting.Hidden, "Hidden", 56);
            AddSettingColumn(table, AchievementSetting.ProgressStat, "Progress Stat", 110);
            AddSettingColumn(table, AchievementSetting.ProgressMin, "Min", 50);
            AddSettingColumn(table, AchievementSetting.ProgressMax, "Max", 50);

            table.columns.Add(new Column
            {
                name = "state",
                title = "State",
                width = 72,
                makeCell = MakeLabel,
                bindCell = BindState
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

        private void AddSettingColumn(MultiColumnListView table, AchievementSetting setting, string title, float width) =>
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

        private void BindIcon(VisualElement element, int index)
        {
            var achievement = _visibleRows[index].Achievement;

            ((Image)element).sprite = achievement ? achievement.Icon : null;
        }

        private void BindApiName(VisualElement element, int index)
        {
            var label = (Label)element;
            var row = _visibleRows[index];

            label.text = string.IsNullOrWhiteSpace(row.ApiName) ? "(no API Name)" : row.ApiName;
            label.style.opacity = row.Achievement ? 1f : 0.55f;
        }

        private void BindDisplayName(VisualElement element, int index)
        {
            var label = (Label)element;
            var row = _visibleRows[index];

            label.text = row.Steam?.DisplayName ?? string.Empty;
            label.tooltip = row.Steam?.Description;
            label.style.opacity = row.Achievement ? 1f : 0.55f;
        }

        private static void BindSetting(Label label, Row row, AchievementSetting setting)
        {
            var comparison = row.Settings.First(candidate => candidate.Setting == setting);

            // An achievement shows what its asset holds, and one Steam alone has what Steam holds.
            label.text = row.Achievement ? comparison.Asset : comparison.Steam;
            label.style.opacity = row.Achievement ? 1f : 0.55f;

            var differs = row.Achievement && !comparison.Matches;

            label.style.backgroundColor = differs ? new StyleColor(WarningTint) : new StyleColor(StyleKeyword.Null);
            label.tooltip = differs ? $"Steam: {comparison.Steam}" : null;
        }

        private void BindState(VisualElement element, int index)
        {
            var label = (Label)element;
            var achievement = _visibleRows[index].Achievement;

            if (!achievement)
            {
                label.text = string.Empty;
                label.tooltip = null;

                return;
            }

            // Read from the cache alone: IsAchieved would pull from Steam on the first read.
            label.text = achievement.IsSynced && achievement.IsAchieved ? "unlocked" : "locked";
            label.tooltip = achievement.IsSynced ? null : "The default: not read from Steam or changed yet.";
        }

        #endregion

        #region Detail

        private void Select(SteamAchievement achievement)
        {
            _selectedKey = achievement;

            Refresh();
            ShowDetail(FindRow(achievement));
        }

        private Row FindRow(object key) => key == null ? null : _rows.FirstOrDefault(row => Equals(row.Key, key));

        /// <summary>
        /// Shows the selected achievement under the table. Built again only when the selection changes,
        /// so that the periodic refresh cannot take the focus away from a field being typed in.
        /// </summary>
        private void ShowDetail(Row row)
        {
            _detail.Clear();
            _detailProblems = null;

            if (row == null)
                return;

            _detail.Add(new Label(row.Achievement ? row.Achievement.name : row.ApiName)
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 13, marginBottom = 2 }
            });

            _detailProblems = new HelpBox();
            _detail.Add(_detailProblems);
            UpdateDetailProblems();

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 6 } };

            _detail.Add(buttons);

            if (row.Achievement)
            {
                var achievement = row.Achievement;

                buttons.Add(new InspectorButtonElement(() => EditorGUIUtility.PingObject(achievement), "Ping")
                {
                    tooltip = "Shows the achievement in the Project window, where it can be dragged into a field.",
                    style = { flexGrow = 1 }
                });

                buttons.Add(new InspectorButtonElement(() => RemoveAchievement(achievement), "Remove From DB")
                {
                    style = { flexGrow = 1 }
                });

                _detail.Add(new InspectorElement(achievement));
            }
            else if (row.Steam is { } steam)
            {
                buttons.Add(new InspectorButtonElement(() => Select(CreateFromSteam(new[] { steam }).FirstOrDefault()), "Create From Steam")
                {
                    tooltip = "Makes this achievement, with the settings Steam has.",
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
        /// One line of the table: an achievement of the DB with the achievement Steam has under its API
        /// Name, or an achievement Steam alone has.
        /// </summary>
        private sealed class Row
        {
            public readonly SteamAchievement Achievement;
            public readonly string ApiName;
            public readonly AchievementDefinition? Steam;
            public readonly List<AchievementComparison> Settings;
            public readonly List<(HelpBoxMessageType type, string text)> Problems = new();

            public Row(SteamAchievement achievement, string apiName, AchievementDefinition? steam, List<AchievementComparison> settings)
            {
                Achievement = achievement;
                ApiName = apiName;
                Steam = steam;
                Settings = settings;
            }

            /// <summary>What the row stands for across refreshes: its achievement, or the API Name of one Steam alone has.</summary>
            public object Key => Achievement ? Achievement : (object)ApiName;

            public HelpBoxMessageType Severity =>
                Problems.Count == 0 ? HelpBoxMessageType.None : Problems.Max(problem => problem.type);

            public void Add(HelpBoxMessageType type, string text) => Problems.Add((type, text));

            public string DescribeProblems() => string.Join("\n", Problems.Select(problem => problem.text));
        }
    }
}
