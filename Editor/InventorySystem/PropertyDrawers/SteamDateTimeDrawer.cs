using System;
using System.Globalization;
using fefek5.Toys.Editor.Icons;
using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    /// <summary>
    /// Drawer for <see cref="SteamDateTime"/>: one row with the date and time in UTC and buttons to set it to now or
    /// clear it. A warning shows under it while the text typed is not a date.
    /// </summary>
    [CustomPropertyDrawer(typeof(SteamDateTime))]
    public class SteamDateTimeDrawer : PropertyDrawer
    {
        private const string TICKS_FIELD = "_ticks";
        private const string HAS_VALUE_FIELD = "_hasValue";
        private const string NOW_ICON = "now-icon";
        private const string CLEAR_ICON = "clear-icon";
        private const string DATE_FORMAT = "yyyy-MM-dd";
        private const string TIME_FORMAT = "HH:mm:ss";

        private const float BUTTON_SIZE = 18;
        private const float ICON_SIZE = 12;
        private const float GAP = 2;

        private static readonly Color _invalidColor = new(1f, 0.35f, 0.35f, 1f);

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var ticksProp = property.FindPropertyRelative(TICKS_FIELD);
            var hasValueProp = property.FindPropertyRelative(HAS_VALUE_FIELD);

            var root = new VisualElement();

            var inputs = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            var row = new RowField(preferredLabel, inputs);
            row.AddToClassList(BaseField<string>.alignedFieldUssClassName);

            var dateField = new TextField { tooltip = "Date in YYYY-MM-DD format (UTC)" };
            SetMargins(dateField, 0);
            dateField.style.flexGrow = 1;
            dateField.style.flexShrink = 1;

            var timeField = new TextField { tooltip = "Time in HH:MM:SS format (UTC)" };
            SetMargins(timeField, GAP);
            timeField.style.width = 70;
            timeField.style.flexShrink = 0;

            var warning = new Label("Invalid format (expected YYYY-MM-DD HH:MM:SS)")
            {
                style =
                {
                    display = DisplayStyle.None,
                    color = _invalidColor,
                    fontSize = 10,
                    marginLeft = EditorGUIUtility.labelWidth + 3
                }
            };

            var nowButton = CreateIconButton(NOW_ICON, "Set to the current UTC time", () => Set(DateTime.UtcNow.Ticks, true));
            var clearButton = CreateIconButton(CLEAR_ICON, "Clear the date and time", () => Set(0, false));

            inputs.Add(dateField);
            inputs.Add(timeField);
            inputs.Add(nowButton);
            inputs.Add(clearButton);
            root.Add(row);
            root.Add(warning);

            dateField.RegisterValueChangedCallback(_ => Apply());
            timeField.RegisterValueChangedCallback(_ => Apply());

            Refresh();
            root.TrackPropertyValue(property, _ => Refresh());

            return root;

            void Set(long ticks, bool hasValue)
            {
                ticksProp.longValue = ticks;
                hasValueProp.boolValue = hasValue;
                property.serializedObject.ApplyModifiedProperties();
                Refresh();
            }

            void Refresh()
            {
                warning.style.display = DisplayStyle.None;

                if (!hasValueProp.boolValue)
                {
                    dateField.SetValueWithoutNotify(string.Empty);
                    timeField.SetValueWithoutNotify(string.Empty);
                    return;
                }

                var dateTime = new DateTime(ticksProp.longValue, DateTimeKind.Utc);

                dateField.SetValueWithoutNotify(dateTime.ToString(DATE_FORMAT, CultureInfo.InvariantCulture));
                timeField.SetValueWithoutNotify(dateTime.ToString(TIME_FORMAT, CultureInfo.InvariantCulture));
            }

            void Apply()
            {
                var date = dateField.value?.Trim() ?? string.Empty;
                var time = timeField.value?.Trim() ?? string.Empty;

                if (date.Length == 0 && time.Length == 0)
                {
                    Set(0, false);
                    return;
                }

                if (time.Length == 0)
                    time = "00:00:00";

                if (date.Length == 0)
                    date = DateTime.UtcNow.ToString(DATE_FORMAT, CultureInfo.InvariantCulture);

                var valid = DateTime.TryParseExact($"{date} {time}", $"{DATE_FORMAT} {TIME_FORMAT}",
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var dateTime);

                warning.style.display = valid ? DisplayStyle.None : DisplayStyle.Flex;

                if (!valid)
                    return;

                ticksProp.longValue = dateTime.Ticks;
                hasValueProp.boolValue = true;
                property.serializedObject.ApplyModifiedProperties();
            }
        }

        private static Button CreateIconButton(string iconName, string tooltip, Action onClick)
        {
            EditorIconsDatabase.TryGetIcon<Texture2D>(iconName, out var icon);

            var button = new Button(onClick)
            {
                tooltip = tooltip,
                style =
                {
                    width = BUTTON_SIZE,
                    height = BUTTON_SIZE,
                    flexShrink = 0,
                    paddingLeft = 0,
                    paddingRight = 0,
                    paddingTop = 0,
                    paddingBottom = 0,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center
                }
            };

            SetMargins(button, GAP);
            button.Add(new Image { image = icon, style = { width = ICON_SIZE, height = ICON_SIZE } });

            return button;
        }

        // Every element of the row but the first is GAP after the one before it, and none has other margins.
        private static void SetMargins(VisualElement element, float left)
        {
            element.style.marginLeft = left;
            element.style.marginRight = 0;
            element.style.marginTop = 0;
            element.style.marginBottom = 0;
        }

        /// <summary>
        /// A field only for its aligned label. Its input is the row of the date, the time and the buttons, so hovering
        /// one of them highlights that one alone.
        /// </summary>
        private class RowField : BaseField<string>
        {
            public RowField(string label, VisualElement input) : base(label, input) { }
        }
    }
}
