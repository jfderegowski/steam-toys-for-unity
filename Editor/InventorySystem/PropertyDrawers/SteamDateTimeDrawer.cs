using System;
using System.Globalization;
using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    /// <summary>
    /// Custom property drawer for <see cref="SteamDateTime"/>.
    /// Displays date and time fields with a clear button,
    /// and shows the resulting Steam-format string as a read-only preview.
    /// Uses Unity's aligned-field USS class so the label and value area
    /// line up identically to a standard <see cref="PropertyField"/>.
    /// </summary>
    [CustomPropertyDrawer(typeof(SteamDateTime))]
    public class SteamDateTimeDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var ticksProp = property.FindPropertyRelative("_ticks");
            var hasValueProp = property.FindPropertyRelative("_hasValue");

            var root = new VisualElement();

            // ── Main row: uses a dummy TextField for label alignment ──
            // The outermost element is a BaseField so Unity's USS aligns
            // the label at the standard Inspector width automatically.
            var wrapper = new TextField(preferredLabel) {
                style = { flexGrow = 1 }
            };
            wrapper.AddToClassList(BaseField<string>.alignedFieldUssClassName);

            // Hide the default text input — we only want the label from it.
            var defaultInput = wrapper.Q("unity-text-input");
            if (defaultInput != null)
                defaultInput.style.display = DisplayStyle.None;

            // Value area: date + time + buttons, placed inside the wrapper
            // so they sit right after the auto-aligned label.
            var valueRow = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexGrow = 1,
                    alignItems = Align.Center,
                }
            };

            var dateField = new TextField {
                tooltip = "Date in YYYY-MM-DD format (UTC)",
                style = { flexGrow = 3, flexShrink = 1 }
            };

            var timeField = new TextField {
                tooltip = "Time in HH:MM:SS format (UTC)",
                style = { flexGrow = 2, flexShrink = 1, marginLeft = 4 }
            };

            var nowBtn = new Button(() => {
                var now = DateTime.UtcNow;
                hasValueProp.boolValue = true;
                ticksProp.longValue = now.Ticks;
                property.serializedObject.ApplyModifiedProperties();
                UpdateFields(ticksProp, hasValueProp, dateField, timeField, null);
            }) {
                text = "Now",
                tooltip = "Set to current UTC time",
                style = { marginLeft = 2, paddingLeft = 6, paddingRight = 6 }
            };

            var clearBtn = new Button(() => {
                hasValueProp.boolValue = false;
                ticksProp.longValue = 0;
                property.serializedObject.ApplyModifiedProperties();
                UpdateFields(ticksProp, hasValueProp, dateField, timeField, null);
            }) {
                text = "✕",
                tooltip = "Clear date/time",
                style = { marginLeft = 2, width = 22 }
            };

            valueRow.Add(dateField);
            valueRow.Add(timeField);
            valueRow.Add(nowBtn);
            valueRow.Add(clearBtn);
            wrapper.Add(valueRow);
            root.Add(wrapper);

            // ── Preview label (aligned with the value area) ──
            var previewLabel = new Label {
                style = {
                    fontSize = 10,
                    color = new Color(0.6f, 0.6f, 0.6f),
                    marginTop = 1,
                    paddingLeft = 3,
                }
            };

            // Wrap in a dummy field so its left padding matches the label width.
            var previewWrapper = new TextField(" ") {
                style = { flexGrow = 1 }
            };
            previewWrapper.AddToClassList(BaseField<string>.alignedFieldUssClassName);

            var previewInput = previewWrapper.Q("unity-text-input");
            if (previewInput != null)
                previewInput.style.display = DisplayStyle.None;

            previewWrapper.Add(previewLabel);
            root.Add(previewWrapper);

            // ── Callbacks ──
            dateField.RegisterValueChangedCallback(_ =>
                TryApplyDateTime(dateField, timeField, ticksProp, hasValueProp, property, previewLabel));

            timeField.RegisterValueChangedCallback(_ =>
                TryApplyDateTime(dateField, timeField, ticksProp, hasValueProp, property, previewLabel));

            // Initial state after binding
            root.RegisterCallback<AttachToPanelEvent>(_ => {
                root.schedule.Execute(() =>
                    UpdateFields(ticksProp, hasValueProp, dateField, timeField, previewLabel));
            });

            return root;
        }

        private static void UpdateFields(
            SerializedProperty ticksProp,
            SerializedProperty hasValueProp,
            TextField dateField,
            TextField timeField,
            Label previewLabel)
        {
            if (!hasValueProp.boolValue)
            {
                dateField.SetValueWithoutNotify("");
                timeField.SetValueWithoutNotify("");

                if (previewLabel == null) return;

                previewLabel.text = "(no date set)";
                previewLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                return;
            }

            var dt = new DateTime(ticksProp.longValue, DateTimeKind.Utc);
            dateField.SetValueWithoutNotify(dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            timeField.SetValueWithoutNotify(dt.ToString("HH:mm:ss", CultureInfo.InvariantCulture));

            if (previewLabel == null) return;

            var steam = dt.ToString(SteamDateTime.STEAM_FORMAT, CultureInfo.InvariantCulture);
            previewLabel.text = $"Steam format: {steam}";
            previewLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
        }

        private static void TryApplyDateTime(
            TextField dateField,
            TextField timeField,
            SerializedProperty ticksProp,
            SerializedProperty hasValueProp,
            SerializedProperty property,
            Label previewLabel)
        {
            var dateStr = dateField.value?.Trim() ?? "";
            var timeStr = timeField.value?.Trim() ?? "";

            if (string.IsNullOrEmpty(dateStr) && string.IsNullOrEmpty(timeStr))
            {
                ApplyClear(ticksProp, hasValueProp, property, previewLabel);
                return;
            }

            if (string.IsNullOrEmpty(timeStr))
                timeStr = "00:00:00";

            if (string.IsNullOrEmpty(dateStr))
                dateStr = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            var combined = $"{dateStr} {timeStr}";

            if (DateTime.TryParseExact(combined, "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var dt))
            {
                hasValueProp.boolValue = true;
                ticksProp.longValue = dt.Ticks;
                property.serializedObject.ApplyModifiedProperties();

                if (previewLabel == null) return;

                var steam = dt.ToString(SteamDateTime.STEAM_FORMAT, CultureInfo.InvariantCulture);
                previewLabel.text = $"Steam format: {steam}";
                previewLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            }
            else
            {
                if (previewLabel == null) return;

                previewLabel.text = "⚠ Invalid format (expected YYYY-MM-DD HH:MM:SS)";
                previewLabel.style.color = new Color(1f, 0.35f, 0.35f);
            }
        }

        private static void ApplyClear(
            SerializedProperty ticksProp,
            SerializedProperty hasValueProp,
            SerializedProperty property,
            Label previewLabel)
        {
            hasValueProp.boolValue = false;
            ticksProp.longValue = 0;
            property.serializedObject.ApplyModifiedProperties();

            if (previewLabel == null) return;

            previewLabel.text = "(no date set)";
            previewLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
        }
    }
}