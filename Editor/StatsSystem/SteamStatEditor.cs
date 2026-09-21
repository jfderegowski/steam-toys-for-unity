using System;
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
        private const string EditOnSteamUrl = "https://partner.steamgames.com/apps/stats/{0}";

        // The serialized fields of SteamStat, SteamStat<TValue> and AvgRateStat.
        private const string ApiNameField = "_apiName";
        private const string DefaultValueField = "_defaultValue";
        private const string MinValueField = "_minValue";
        private const string MaxValueField = "_maxValue";
        private const string MaxChangeField = "_maxChange";
        private const string IncrementOnlyField = "_incrementOnly";
        private const string WindowSizeField = "_windowSize";

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            InspectorElement.FillDefaultInspector(root, serializedObject, this);

            // Right under the Script field, which the default inspector puts first.
            var scriptField = root.Q<PropertyField>("PropertyField:m_Script");

            root.Insert(scriptField == null ? 0 : root.IndexOf(scriptField) + 1, CreateValueSection());
            root.Add(CreateSteamSection());

            return root;
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
                var canSync = SteamStats.Initialized && !string.IsNullOrWhiteSpace(stat.ApiName);

                push.SetEnabled(canSync);
                pull.SetEnabled(canSync);
            }

            void Push()
            {
                // Handing the value to the client is not enough: it reaches the servers once stored.
                if (stat.TryPushToSteam())
                    SteamStats.StoreStats();
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
            if (!SteamStats.Initialized)
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
            var apiName = serializedObject.FindProperty(ApiNameField);
            var defaultValue = serializedObject.FindProperty(DefaultValueField);
            var minValue = serializedObject.FindProperty(MinValueField);
            var maxValue = serializedObject.FindProperty(MaxValueField);
            var maxChange = serializedObject.FindProperty(MaxChangeField);
            var incrementOnly = serializedObject.FindProperty(IncrementOnlyField);

            // Only an AvgRateStat has one.
            var windowSize = serializedObject.FindProperty(WindowSizeField);

            // Int stats hold ints and the other two floats, which are compared at float precision so
            // that e.g. 0.1 on Steam matches 0.1 in the asset.
            var isFloat = defaultValue.propertyType == SerializedPropertyType.Float;

            var section = new VisualElement { style = { marginTop = 12 } };

            section.Add(new Label("Steam") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 2 } });

            var status = new HelpBox();

            section.Add(status);

            var table = new VisualElement { style = { marginTop = 2, marginBottom = 2 } };
            var header = new ComparisonRow(string.Empty);

            header.SetHeader("Asset", "Steam");

            var typeRow = new ComparisonRow("Type");
            var defaultRow = new ComparisonRow("Default Value");
            var minRow = new ComparisonRow("Min Value");
            var maxRow = new ComparisonRow("Max Value");
            var maxChangeRow = new ComparisonRow("Max Change");
            var incrementOnlyRow = new ComparisonRow("Increment Only");
            var windowSizeRow = new ComparisonRow("Window Size")
            {
                style = { display = windowSize == null ? DisplayStyle.None : DisplayStyle.Flex }
            };

            table.Add(header);
            table.Add(typeRow);
            table.Add(defaultRow);
            table.Add(minRow);
            table.Add(maxRow);
            table.Add(maxChangeRow);
            table.Add(incrementOnlyRow);
            table.Add(windowSizeRow);

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
                serializedObject.UpdateIfRequiredOrScript();

                var settings = Runtime.Core.SteamSettings.Instance;

                appId = settings ? settings.AppId : AppId_t.Invalid;
                steamStat = null;

                editButton.SetEnabled(appId != AppId_t.Invalid);
                pullButton.SetEnabled(false);
                table.style.display = DisplayStyle.None;

                if (string.IsNullOrWhiteSpace(apiName.stringValue))
                {
                    SetStatus(HelpBoxMessageType.Info, "Set the API Name to compare this stat with Steam.");

                    return;
                }

                if (appId == AppId_t.Invalid)
                {
                    SetStatus(HelpBoxMessageType.Info, "Choose the App ID in Window/Steam Toys/Steam Settings to compare this stat with Steam.");

                    return;
                }

                if (SteamAppCache.SteamPath == null)
                {
                    SetStatus(HelpBoxMessageType.Warning, "Steam is not installed on this machine, so there is nothing to compare this stat with.");

                    return;
                }

                if (!SteamAppCache.TryGetStatSchema(appId, out var schema))
                {
                    SetStatus(HelpBoxMessageType.Info,
                        $"No stat schema of app {appId} could be read on this machine. The Steam client downloads it " +
                        "when the game connects to Steam, e.g. through Window/Steam Toys/Connect To Steam.");

                    return;
                }

                var source = $"Schema version {schema.Version} of app {appId}, downloaded " +
                             $"{schema.DownloadedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}.";

                if (!schema.Stats.TryGetValue(apiName.stringValue, out var steam))
                {
                    SetStatus(HelpBoxMessageType.Warning,
                        $"Steam has no stat '{apiName.stringValue}'. Add it with Edit on Steam and publish the change.\n{source}");

                    return;
                }

                steamStat = steam;
                table.style.display = DisplayStyle.Flex;

                var statType = ((SteamStat)target).StatType;
                var typeMatches = typeRow.Set(statType.ToString(), steam.Type.ToString(), statType == steam.Type);

                // & rather than &&, so that every row is filled in. Settings are compared as written, ticked
                // or not: a ticked max change of 0 differs from none on Steam, even though the runtime treats
                // both as no limit.
                var otherMatches =
                    defaultRow.Set(ReadNumber(defaultValue), steam.Default ?? 0, isFloat) &
                    minRow.Set(ReadOptional(minValue), steam.Min, isFloat) &
                    maxRow.Set(ReadOptional(maxValue), steam.Max, isFloat) &
                    maxChangeRow.Set(ReadOptional(maxChange), steam.MaxChange, isFloat) &
                    incrementOnlyRow.Set(incrementOnly.boolValue.ToString(), steam.IncrementOnly.ToString(),
                        incrementOnly.boolValue == steam.IncrementOnly) &
                    (windowSize == null || windowSizeRow.Set(windowSize.floatValue, steam.WindowSize, true));

                // The type is the class of the asset, which no value copied into it can change.
                pullButton.SetEnabled(typeMatches && !otherMatches);

                if (!typeMatches)
                    SetStatus(HelpBoxMessageType.Warning,
                        $"Steam has this stat as {steam.Type}, and the type comes from the class of the asset, " +
                        $"so Pull cannot change it: use a {GetAssetName(steam.Type)} asset instead.\n{source}");
                else if (!otherMatches)
                    SetStatus(HelpBoxMessageType.Warning, $"The settings of this stat differ from Steam.\n{source}");
                else
                    SetStatus(HelpBoxMessageType.Info, $"This stat matches Steam.\n{source}");
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

                serializedObject.Update();

                WriteNumber(defaultValue, steam.Default ?? 0);
                WriteOptional(minValue, steam.Min);
                WriteOptional(maxValue, steam.Max);
                WriteOptional(maxChange, steam.MaxChange);
                incrementOnly.boolValue = steam.IncrementOnly;

                if (windowSize != null && steam.WindowSize is { } window)
                    windowSize.floatValue = (float)window;

                serializedObject.ApplyModifiedProperties();

                Refresh();
            }
        }

        private static string GetAssetName(SteamStatType type) => type switch
        {
            SteamStatType.Int => "Int Stat",
            SteamStatType.Float => "Float Stat",
            _ => "Avg Rate Stat"
        };

        private static double ReadNumber(SerializedProperty property) =>
            property.propertyType == SerializedPropertyType.Float ? (double)property.floatValue : property.intValue;

        // Reads a HasValue<T>, whose value only counts while hasValue is ticked.
        private static double? ReadOptional(SerializedProperty property) =>
            property.FindPropertyRelative("hasValue").boolValue
                ? ReadNumber(property.FindPropertyRelative("value"))
                : null;

        private static void WriteNumber(SerializedProperty property, double value)
        {
            if (property.propertyType == SerializedPropertyType.Float)
                property.floatValue = (float)value;
            else
                property.intValue = (int)Math.Clamp(Math.Round(value), int.MinValue, int.MaxValue);
        }

        // Unticks hasValue for a setting Steam leaves unset, and keeps the value in the asset as it was.
        private static void WriteOptional(SerializedProperty property, double? value)
        {
            property.FindPropertyRelative("hasValue").boolValue = value.HasValue;

            if (value.HasValue)
                WriteNumber(property.FindPropertyRelative("value"), value.Value);
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

            public bool Set(double? asset, double? steam, bool isFloat) =>
                Set(Format(asset, isFloat), Format(steam, isFloat), asset.HasValue == steam.HasValue &&
                    (!asset.HasValue || (isFloat ? (float)asset.Value == (float)steam.Value : asset.Value == steam.Value)));

            private static string Format(double? value, bool isFloat) =>
                value is not { } number ? "not set"
                : isFloat ? ((float)number).ToString(CultureInfo.InvariantCulture)
                : number.ToString(CultureInfo.InvariantCulture);
        }
    }
}
