using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using fefek5.Toys.Editor.VisualElements;
using SteamToys.Editor.Core;
using SteamToys.Editor.StatsSystem;
using SteamToys.Runtime.AchievementsSystem;
using Steamworks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.AchievementsSystem
{
    /// <summary>
    /// The default inspector of an achievement, with its cached state, the one Steam holds and buttons
    /// that change it at the top, and how it compares with the same achievement on Steam at the bottom,
    /// as the Steam client downloaded it for the App ID in the Steam Settings.
    /// <para>
    /// Steam has no API that writes achievement settings, so the way back is by hand: Edit on Steam
    /// opens the page they are edited on, and Pull copies what Steam has into the asset.
    /// </para>
    /// </summary>
    [CustomEditor(typeof(SteamAchievement), true)]
    public class SteamAchievementEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            InspectorElement.FillDefaultInspector(root, serializedObject, this);

            // Right under the Script field, which the default inspector puts first.
            var scriptField = root.Q<PropertyField>("PropertyField:m_Script");

            root.Insert(scriptField == null ? 0 : root.IndexOf(scriptField) + 1, CreateStateSection());
            root.Insert(0, CreateDuplicateNotice());
            root.Add(CreateSteamSection());

            // Named after its API Name once the field is left, like a stat of the stats DB.
            root.Q<PropertyField>($"PropertyField:{AchievementSettings.ApiNameField}")?.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (target is not SteamAchievement achievement || !achievement)
                    return;

                SteamAchievementsDBEditor.SyncSubAssetName(achievement);
                AchievementIcons.SyncNames(achievement);
            });

            return root;
        }

        /// <summary>
        /// Says which other achievement assets use the same API Name, and so unlock the same achievement
        /// on Steam. Only a notice: a second asset for one achievement can be deliberate.
        /// </summary>
        private VisualElement CreateDuplicateNotice()
        {
            var notice = new HelpBox { messageType = HelpBoxMessageType.Info, style = { display = DisplayStyle.None } };

            var apiName = serializedObject.FindProperty(AchievementSettings.ApiNameField);
            var check = notice.schedule.Execute(Check);

            notice.TrackPropertyValue(apiName, _ => check.ExecuteLater(500));

            return notice;

            void Check()
            {
                // Gone when an achievement of the DB was removed while its inspector was open.
                if (target is not SteamAchievement achievement || !achievement)
                    return;

                var duplicates = FindOtherAchievementsWithApiName(achievement);

                notice.style.display = duplicates.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
                notice.text = $"Another achievement asset uses the API Name '{achievement.ApiName}', so both " +
                              $"unlock the same achievement on Steam:\n{string.Join("\n", duplicates)}";
            }
        }

        private static List<string> FindOtherAchievementsWithApiName(SteamAchievement achievement)
        {
            var duplicates = new List<string>();

            if (string.IsNullOrWhiteSpace(achievement.ApiName))
                return duplicates;

            var paths = new SortedSet<string>();

            foreach (var type in TypeCache.GetTypesDerivedFrom<SteamAchievement>().Append(typeof(SteamAchievement)))
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
                    if (asset is SteamAchievement other && other != achievement && other.ApiName == achievement.ApiName)
                        duplicates.Add(AssetDatabase.IsSubAsset(other) ? $"{path} > {other.name}" : path);
                }
            }

            return duplicates;
        }

        private VisualElement CreateStateSection()
        {
            var achievement = (SteamAchievement)target;

            var section = new VisualElement { style = { marginTop = 2, marginBottom = 6 } };

            var current = CreateReadOnlyField("Current State",
                "The state cached in the asset, which the game reads. Shown without touching Steam.");
            var steam = CreateReadOnlyField("Steam State",
                "The state the Steam client holds for the signed-in user. Needs a Steam session: play mode, or " +
                "Window/Steam Toys/Connect To Steam.");
            var progress = CreateReadOnlyField("Progress",
                "The cached value of the progress stat between Progress Min and Progress Max. Shown without " +
                "touching Steam, so it reads the default until the stat is read or written.");

            section.Add(current);
            section.Add(steam);
            section.Add(progress);

            var icons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4, marginBottom = 4 } };
            var icon = CreateIconPreview("Icon");
            var lockedIcon = CreateIconPreview("Locked Icon");

            icons.Add(icon);
            icons.Add(lockedIcon);
            section.Insert(0, icons);

            var unlock = new InspectorButtonElement(() => achievement.Unlock(), "Unlock")
            {
                tooltip = "Unlocks the achievement for the signed-in account and stores it on the Steam servers, " +
                          "which shows the notification.",
                style = { flexGrow = 1 }
            };

            var clear = new InspectorButtonElement(() => achievement.Clear(), "Clear")
            {
                tooltip = "Locks the achievement again for the signed-in account and stores the change. For testing.",
                style = { flexGrow = 1 }
            };

            var indicate = new InspectorButtonElement(() => achievement.IndicateProgress(), "Indicate Progress")
            {
                tooltip = "Shows the notification with the progress read from the progress stat. Steam shows " +
                          "nothing for an unlocked achievement or a stat at or past Progress Max.",
                style = { flexGrow = 1 }
            };

            var pull = new InspectorButtonElement(() => achievement.TryPullFromSteam(), "Pull State From Steam")
            {
                tooltip = "Replaces the cached state with the one the Steam client holds.",
                style = { flexGrow = 1 }
            };

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row } };

            buttons.Add(unlock);
            buttons.Add(clear);
            buttons.Add(indicate);
            buttons.Add(pull);

            section.Add(buttons);

            Refresh();

            // Polled: in play mode the state changes all the time.
            section.schedule.Execute(Refresh).Every(200);

            return section;

            void Refresh()
            {
                if (!achievement)
                    return;

                current.SetValueWithoutNotify(achievement.IsSynced
                    ? DescribeState(achievement.IsAchieved, achievement.UnlockTime)
                    : $"{DescribeState(false, null)} (not synced yet)");

                steam.SetValueWithoutNotify(ReadSteamState(achievement));

                progress.style.display = achievement.HasProgress ? DisplayStyle.Flex : DisplayStyle.None;

                icon.Q<Image>().sprite = achievement.Icon;
                lockedIcon.Q<Image>().sprite = achievement.LockedIcon;
                icons.style.display = achievement.Icon || achievement.LockedIcon ? DisplayStyle.Flex : DisplayStyle.None;

                if (achievement.HasProgress)
                    progress.SetValueWithoutNotify(DescribeProgress(achievement));

                var canSync = SteamAchievementsDB.Initialized && !string.IsNullOrWhiteSpace(achievement.ApiName);

                unlock.SetEnabled(canSync);
                clear.SetEnabled(canSync);
                indicate.SetEnabled(canSync && achievement.HasProgress);
                pull.SetEnabled(canSync);
            }
        }

        /// <summary>One icon at the size Steam shows it, 64 pixels, with its name under it.</summary>
        private static VisualElement CreateIconPreview(string label)
        {
            var preview = new VisualElement { style = { alignItems = Align.Center, marginRight = 8 } };

            preview.Add(new Image
            {
                scaleMode = ScaleMode.ScaleToFit,
                style = { width = 64, height = 64 }
            });

            preview.Add(new Label(label) { style = { fontSize = 10, opacity = 0.7f } });

            return preview;
        }

        private static string DescribeState(bool achieved, DateTime? unlockTime) =>
            !achieved ? "locked"
            : unlockTime is { } time ? $"unlocked {time.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}"
            : "unlocked";

        /// <summary>
        /// The progress from the cache of the stat alone: reading its value would pull it from Steam,
        /// which an inspector redrawing five times a second must not do.
        /// </summary>
        private static string DescribeProgress(SteamAchievement achievement)
        {
            var stat = achievement.ProgressStat;
            var range = $"{AchievementSettings.Format(achievement.ProgressMin)} .. {AchievementSettings.Format(achievement.ProgressMax)}";

            if (!stat.IsSynced)
                return $"{stat.GetValueString()} of {range} (stat not synced yet)";

            return achievement.Progress is { } fraction
                ? $"{stat.GetValueString()} of {range} ({fraction.ToString("P0", CultureInfo.InvariantCulture)})"
                : $"{stat.GetValueString()} of {range}";
        }

        /// <summary>
        /// Reads the state straight from the Steam client, leaving the cache of the achievement alone,
        /// which a pull through the achievement itself would overwrite.
        /// </summary>
        private static string ReadSteamState(SteamAchievement achievement)
        {
            if (string.IsNullOrWhiteSpace(achievement.ApiName))
                return "no API Name";

            // The raw calls throw rather than fail while the API is down.
            if (!SteamAchievementsDB.Initialized)
                return "not connected to Steam";

            if (!SteamUserStats.GetAchievementAndUnlockTime(achievement.ApiName, out var achieved, out var unlockTime))
                return "not on Steam, or not published";

            return DescribeState(achieved, achieved && unlockTime != 0
                ? DateTimeOffset.FromUnixTimeSeconds(unlockTime).LocalDateTime
                : null);
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
            var apiName = serializedObject.FindProperty(AchievementSettings.ApiNameField);

            var section = new VisualElement { style = { marginTop = 12 } };

            section.Add(new Label("Steam") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 2 } });

            var status = new HelpBox();

            section.Add(status);

            // What Steam shows the user, in English; a mirror of neither side, so not compared.
            var texts = new Label { style = { whiteSpace = WhiteSpace.Normal, marginTop = 2, marginBottom = 2 } };

            section.Add(texts);

            var table = new VisualElement { style = { marginTop = 2, marginBottom = 2 } };
            var header = new SteamStatEditor.ComparisonRow(string.Empty);

            header.SetHeader("Asset", "Steam");
            table.Add(header);

            var rows = new Dictionary<AchievementSetting, SteamStatEditor.ComparisonRow>();

            foreach (AchievementSetting setting in Enum.GetValues(typeof(AchievementSetting)))
                table.Add(rows[setting] = new SteamStatEditor.ComparisonRow(AchievementSettings.GetLabel(setting)));

            section.Add(table);

            // The achievement found on Steam by the last refresh, for Pull to copy from.
            AchievementDefinition? steamAchievement = null;
            var appId = AppId_t.Invalid;

            InspectorButtonElement pullButton = null;
            InspectorButtonElement editButton = null;

            pullButton = new InspectorButtonElement(Pull, "Pull Achievement Settings From Steam")
            {
                tooltip = "Copies the settings Steam has for this achievement into the asset, and downloads its icons " +
                          "into sub-assets of it. The progress stat is found among the stat assets by its API Name.",
                style = { flexGrow = 1 }
            };

            editButton = new InspectorButtonElement(
                () => Application.OpenURL(string.Format(AchievementSettings.EditOnSteamUrl, appId.m_AppId)), "Edit on Steam")
            {
                tooltip = "Opens the page the achievements of this app are edited on. A change shows up here once it " +
                          "is published and the game has connected to Steam again.",
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
                if (!target)
                    return;

                serializedObject.UpdateIfRequiredOrScript();

                var lookup = StatSettings.FindSchema("this achievement");
                var values = AchievementSettings.Read(serializedObject);
                var problems = AchievementSettings.FindProblems(values).ToList();

                appId = lookup.AppId;
                steamAchievement = null;

                editButton.SetEnabled(appId != AppId_t.Invalid);
                pullButton.SetEnabled(false);
                table.style.display = DisplayStyle.None;
                texts.style.display = DisplayStyle.None;

                if (string.IsNullOrWhiteSpace(apiName.stringValue))
                {
                    SetStatus(HelpBoxMessageType.Info, "Set the API Name to compare this achievement with Steam.");

                    return;
                }

                if (lookup.Schema is not { } schema)
                {
                    SetStatus(lookup.MessageType, lookup.Message);

                    return;
                }

                if (!schema.Achievements.TryGetValue(apiName.stringValue, out var steam))
                {
                    SetStatus(HelpBoxMessageType.Warning,
                        $"Steam has no achievement '{apiName.stringValue}'. Add it with Edit on Steam and publish the change.\n{lookup.Message}");

                    return;
                }

                steamAchievement = steam;
                table.style.display = DisplayStyle.Flex;
                texts.style.display = DisplayStyle.Flex;
                texts.text = $"<b>{steam.DisplayName}</b>\n{steam.Description}";

                var matches = true;

                foreach (var comparison in AchievementSettings.Compare(values, AchievementSettings.Read(steam)))
                    matches &= rows[comparison.Setting].Set(comparison.Asset, comparison.Steam, comparison.Matches);

                pullButton.SetEnabled(!matches);

                // Searched only while the asset points elsewhere, since it looks through every stat asset.
                if (steam.ProgressStat != null && values.ProgressStat != steam.ProgressStat && !AchievementSettings.FindStat(steam.ProgressStat))
                    problems.Insert(0, $"Steam ties this achievement to the stat '{steam.ProgressStat}', and no stat asset " +
                                       "has that API Name. Add it to the stats DB, then pull again.");

                if (!matches)
                    problems.Insert(0, "The settings of this achievement differ from Steam.");

                SetStatus(problems.Count > 0 ? HelpBoxMessageType.Warning : HelpBoxMessageType.Info,
                    problems.Count > 0
                        ? $"{string.Join("\n", problems)}\n{lookup.Message}"
                        : $"This achievement matches Steam.\n{lookup.Message}");
            }

            void SetStatus(HelpBoxMessageType type, string text)
            {
                status.messageType = type;
                status.text = text;
            }

            void Pull()
            {
                if (steamAchievement is not { } steam)
                    return;

                Undo.SetCurrentGroupName("Pull Achievement Settings From Steam");

                var group = Undo.GetCurrentGroup();

                AchievementSettings.CopyFrom(serializedObject, steam);
                AchievementIcons.Download(appId, new[] { (serializedObject, steam) });

                Undo.CollapseUndoOperations(group);

                Refresh();
            }
        }
    }
}
