using System;
using System.Collections.Generic;
using System.Globalization;
using fefek5.Toys.Editor.VisualElements;
using SteamToys.Editor.Core;
using SteamToys.Runtime.StatsSystem;
using Steamworks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.StatsSystem
{
    /// <summary>
    /// The default inspector of a stat, with its current value, the one Steam holds and buttons that
    /// move it at the top, and how it compares with the same stat on Steam at the bottom, as the Steam
    /// client downloaded it for the App ID in the Steam Settings.
    /// <para>
    /// Steam has no API that writes stat settings, so the way back is by hand: Edit on Steam opens the
    /// page they are edited on, and Pull copies what Steam has into the asset.
    /// </para>
    /// </summary>
    [CustomEditor(typeof(SteamStat), true)]
    public class SteamStatEditor : UnityEditor.Editor
    {
        internal const string EditOnSteamUrl = "https://partner.steamgames.com/apps/stats/{0}";

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            InspectorElement.FillDefaultInspector(root, serializedObject, this);

            // Right under the Script field, which the default inspector puts first.
            var scriptField = root.Q<PropertyField>("PropertyField:m_Script");

            root.Insert(scriptField == null ? 0 : root.IndexOf(scriptField) + 1, CreateValueSection());
            root.Insert(0, CreateDuplicateNotice());
            root.Add(CreateSteamSection());

            // A stat of the DB is named after its API Name once the field is left rather than while
            // typing: renaming writes the DB and imports it again.
            root.Q<PropertyField>($"PropertyField:{StatSettings.ApiNameField}")?.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (target is SteamStat stat && stat)
                    SteamStatsDBEditor.SyncSubAssetName(stat);
            });

            return root;
        }

        /// <summary>
        /// Says which other stat assets use the same API Name, and so read and write the same stat on
        /// Steam. Only a notice: a second asset for one stat can be deliberate.
        /// <para>
        /// Checked again once typing has paused, because it looks through every stat asset of the project.
        /// </para>
        /// </summary>
        private VisualElement CreateDuplicateNotice()
        {
            var notice = new HelpBox { messageType = HelpBoxMessageType.Info, style = { display = DisplayStyle.None } };

            var apiName = serializedObject.FindProperty(StatSettings.ApiNameField);
            var check = notice.schedule.Execute(Check);

            notice.TrackPropertyValue(apiName, _ => check.ExecuteLater(500));

            return notice;

            void Check()
            {
                // Gone when a stat of the DB was removed while its inspector was open.
                if (target is not SteamStat stat || !stat)
                    return;

                var duplicates = FindOtherStatsWithApiName(stat);

                notice.style.display = duplicates.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
                notice.text = $"Another stat asset uses the API Name '{stat.ApiName}', so both read and write the " +
                              $"same stat on Steam:\n{string.Join("\n", duplicates)}";
            }
        }

        /// <summary>
        /// The other stat assets of the project with the API Name of <paramref name="stat"/>, by path,
        /// with a stat of the DB followed by its name.
        /// </summary>
        private static List<string> FindOtherStatsWithApiName(SteamStat stat)
        {
            var duplicates = new List<string>();

            if (string.IsNullOrWhiteSpace(stat.ApiName))
                return duplicates;

            // Searched class by class, which finds sub-assets and classes games derive from stats alike.
            var paths = new SortedSet<string>();

            foreach (var type in TypeCache.GetTypesDerivedFrom<SteamStat>())
            {
                if (type.IsAbstract || type.IsGenericTypeDefinition)
                    continue;

                foreach (var guid in AssetDatabase.FindAssets($"t:{type.Name}"))
                    paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            foreach (var path in paths)
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is SteamStat other && other != stat && other.ApiName == stat.ApiName)
                        duplicates.Add(AssetDatabase.IsSubAsset(other) ? $"{path} > {other.name}" : path);
                }
            }

            return duplicates;
        }

        private VisualElement CreateValueSection()
        {
            var stat = (SteamStat)target;

            var section = new VisualElement { style = { marginTop = 2, marginBottom = 6 } };

            var current = CreateReadOnlyField("Current Value",
                "The value cached in the asset, which the game reads and writes. Shown without touching Steam.");
            var steam = CreateReadOnlyField("Steam Value",
                "The value the Steam client holds for the signed-in user. Needs a Steam session: play mode, or " +
                "Window/Steam Toys/Connect To Steam.");

            section.Add(current);
            section.Add(steam);

            // An average rate stat takes slices of play instead of a value.
            section.Add(stat is AvgRateStat
                ? new InspectorButtonElement(stat, nameof(AvgRateStat.AddSession), "Add Session")
                {
                    tooltip = "Reports how much of the counted thing happened over how many seconds, and hands it " +
                              "to the Steam client. Push Value To Steam stores it on the Steam servers."
                }
                : new InspectorButtonElement(stat, nameof(SteamStat<int>.TrySetValue), "Set Value")
                {
                    tooltip = "Checks the value against the constraints, caches it and hands it to the Steam client. " +
                              "Push Value To Steam stores it on the Steam servers."
                });

            var push = new InspectorButtonElement(Push, "Push Value To Steam")
            {
                tooltip = "Hands the cached value to the Steam client and stores every changed stat on the Steam " +
                          "servers, for the signed-in account.",
                style = { flexGrow = 1 }
            };

            var pull = new InspectorButtonElement(() => stat.TryPullFromSteam(), "Pull Value From Steam")
            {
                tooltip = "Replaces the cached value with the one the Steam client holds.",
                style = { flexGrow = 1 }
            };

            var sync = new VisualElement { style = { flexDirection = FlexDirection.Row } };

            sync.Add(push);
            sync.Add(pull);

            section.Add(sync);

            Refresh();

            // Polled, and more often than the comparison: in play mode the values move all the time.
            section.schedule.Execute(Refresh).Every(200);

            return section;

            void Refresh()
            {
                current.SetValueWithoutNotify(stat.IsSynced
                    ? stat.GetValueString()
                    : $"{stat.GetValueString()} (not synced yet)");

                steam.SetValueWithoutNotify(ReadSteamValue(stat));

                // Setting works offline too, but these two only mean anything with Steam to talk to.
                var canSync = SteamStatsDB.Initialized && !string.IsNullOrWhiteSpace(stat.ApiName);

                push.SetEnabled(canSync);
                pull.SetEnabled(canSync);
            }

            void Push()
            {
                // Handing the value to the client is not enough: it reaches the servers once stored.
                if (stat.TryPushToSteam())
                    SteamStatsDB.StoreStats();
            }
        }

        /// <summary>
        /// Reads the value straight from the Steam client, leaving the cache of the stat alone, which a
        /// pull through the stat itself would overwrite.
        /// </summary>
        private static string ReadSteamValue(SteamStat stat)
        {
            if (string.IsNullOrWhiteSpace(stat.ApiName))
                return "no API Name";

            // The raw calls throw rather than fail while the API is down.
            if (!SteamStatsDB.Initialized)
                return "not connected to Steam";

            // Float and average rate stats both read through the float overload.
            if (stat.GetValueType() == typeof(int))
                return SteamUserStats.GetStat(stat.ApiName, out int intValue)
                    ? intValue.ToString(CultureInfo.InvariantCulture)
                    : "not on Steam, or of another type";

            return SteamUserStats.GetStat(stat.ApiName, out float floatValue)
                ? floatValue.ToString(CultureInfo.InvariantCulture)
                : "not on Steam, or of another type";
        }

        private static TextField CreateReadOnlyField(string label, string tooltip)
        {
            var field = new TextField(label) { isReadOnly = true, tooltip = tooltip };

            field.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            field.SetEnabled(false);

            return field;
        }

        private VisualElement CreateSteamSection()
        {
            var apiName = serializedObject.FindProperty(StatSettings.ApiNameField);

            var section = new VisualElement { style = { marginTop = 12 } };

            section.Add(new Label("Steam") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 2 } });

            var status = new HelpBox();

            section.Add(status);

            var table = new VisualElement { style = { marginTop = 2, marginBottom = 2 } };
            var header = new ComparisonRow(string.Empty);

            header.SetHeader("Asset", "Steam");
            table.Add(header);

            // Only an AvgRateStat has a window, and only the rows the stat has get made.
            var hasWindowSize = serializedObject.FindProperty(StatSettings.WindowSizeField) != null;
            var rows = new Dictionary<StatSetting, ComparisonRow>();

            foreach (StatSetting setting in Enum.GetValues(typeof(StatSetting)))
            {
                if (setting != StatSetting.WindowSize || hasWindowSize)
                    table.Add(rows[setting] = new ComparisonRow(StatSettings.GetLabel(setting)));
            }

            section.Add(table);

            // The stat found on Steam by the last refresh, for Pull to copy from.
            StatDefinition? steamStat = null;
            var appId = AppId_t.Invalid;

            // Declared ahead of the buttons: Pull refreshes the section, which enables and disables both.
            InspectorButtonElement pullButton = null;
            InspectorButtonElement editButton = null;

            pullButton = new InspectorButtonElement(Pull, "Pull Stat Settings From Steam")
            {
                tooltip = "Copies the settings Steam has for this stat into the asset.",
                style = { flexGrow = 1 }
            };

            editButton = new InspectorButtonElement(
                () => Application.OpenURL(string.Format(EditOnSteamUrl, appId.m_AppId)), "Edit on Steam")
            {
                tooltip = "Opens the page the stats of this app are edited on. A change shows up here once it is " +
                          "published and the game has connected to Steam again.",
                style = { flexGrow = 1 }
            };

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row } };

            buttons.Add(pullButton);
            buttons.Add(editButton);

            section.Add(buttons);

            Refresh();

            // Polled, because what changes the file is the Steam client, and nothing tells the editor.
            section.schedule.Execute(Refresh).Every(1000);
            section.TrackSerializedObjectValue(serializedObject, _ => Refresh());

            return section;

            void Refresh()
            {
                // Gone when a stat of the DB was removed while its inspector was open.
                if (!target)
                    return;

                serializedObject.UpdateIfRequiredOrScript();

                var lookup = StatSettings.FindSchema("this stat");

                appId = lookup.AppId;
                steamStat = null;

                editButton.SetEnabled(appId != AppId_t.Invalid);
                pullButton.SetEnabled(false);
                table.style.display = DisplayStyle.None;

                if (string.IsNullOrWhiteSpace(apiName.stringValue))
                {
                    SetStatus(HelpBoxMessageType.Info, "Set the API Name to compare this stat with Steam.");

                    return;
                }

                if (lookup.Schema is not { } schema)
                {
                    SetStatus(lookup.MessageType, lookup.Message);

                    return;
                }

                if (!schema.Stats.TryGetValue(apiName.stringValue, out var steam))
                {
                    SetStatus(HelpBoxMessageType.Warning,
                        $"Steam has no stat '{apiName.stringValue}'. Add it with Edit on Steam and publish the change.\n{lookup.Message}");

                    return;
                }

                steamStat = steam;
                table.style.display = DisplayStyle.Flex;

                var typeMatches = true;
                var otherMatches = true;

                foreach (var comparison in StatSettings.Compare(StatSettings.Read(serializedObject), StatSettings.Read(steam)))
                {
                    rows[comparison.Setting].Set(comparison.Asset, comparison.Steam, comparison.Matches);

                    if (comparison.Setting == StatSetting.Type)
                        typeMatches = comparison.Matches;
                    else
                        otherMatches &= comparison.Matches;
                }

                // The type is the class of the asset, which no value copied into it can change.
                pullButton.SetEnabled(typeMatches && !otherMatches);

                if (!typeMatches)
                    SetStatus(HelpBoxMessageType.Warning,
                        $"Steam has this stat as {steam.Type}, and the type comes from the class of the asset, " +
                        $"so Pull cannot change it: use a {StatSettings.GetAssetName(steam.Type)} asset instead.\n{lookup.Message}");
                else if (!otherMatches)
                    SetStatus(HelpBoxMessageType.Warning, $"The settings of this stat differ from Steam.\n{lookup.Message}");
                else
                    SetStatus(HelpBoxMessageType.Info, $"This stat matches Steam.\n{lookup.Message}");
            }

            void SetStatus(HelpBoxMessageType type, string text)
            {
                status.messageType = type;
                status.text = text;
            }

            void Pull()
            {
                if (steamStat is not { } steam)
                    return;

                StatSettings.CopyFrom(serializedObject, steam);

                Refresh();
            }
        }

        /// <summary>One line of the comparison: the setting, its value in the asset and on Steam, and whether they agree.</summary>
        private sealed class ComparisonRow : VisualElement
        {
            private readonly Label _asset;
            private readonly Label _steam;
            private readonly Image _mark;

            public ComparisonRow(string setting)
            {
                style.flexDirection = FlexDirection.Row;
                style.alignItems = Align.Center;
                style.minHeight = 18;

                Add(new Label(setting) { style = { width = Length.Percent(40) } });
                Add(_asset = new Label { style = { width = Length.Percent(25) } });
                Add(_steam = new Label { style = { width = Length.Percent(25) } });
                Add(_mark = new Image { style = { width = 16, height = 16 } });
            }

            public void SetHeader(string asset, string steam)
            {
                _asset.text = asset;
                _steam.text = steam;
                _asset.style.unityFontStyleAndWeight = FontStyle.Bold;
                _steam.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            /// <summary>Shows both values and returns whether they match.</summary>
            public bool Set(string asset, string steam, bool matches)
            {
                _asset.text = asset;
                _steam.text = steam;
                _mark.image = EditorGUIUtility.IconContent(matches ? "GreenCheckmark" : "console.warnicon.sml").image;
                _mark.tooltip = matches ? "Matches Steam" : "Differs from Steam";

                return matches;
            }
        }
    }
}
