using SteamToys.Runtime.InventorySystem.NewTagsLogic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers.NewTagsLogic
{
    /// <summary>
    /// Drawer for <see cref="Tag"/> – read-only preview of the name/value pair
    /// that gets sent to Steam. Editing lives in the concrete tag drawers.
    /// </summary>
    [CustomPropertyDrawer(typeof(Tag), true)]
    public class TagDrawer : PropertyDrawer
    {
        private static readonly Color _previewColor = new(0.6f, 0.6f, 0.6f, 1f);
        private static readonly Color _invalidColor = new(1f, 0.35f, 0.35f, 1f);

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return CreateSteamPreview(property);
        }

        /// <summary>
        /// Builds the Steam preview line for a <see cref="Tag"/> property and keeps it
        /// in sync with the serialized data. Reused by the concrete tag drawers so the
        /// preview looks the same wherever a tag is edited.
        /// </summary>
        public static VisualElement CreateSteamPreview(SerializedProperty property)
        {
            var trackedProperty = property.Copy();

            var preview = new Label {
                name = "steamTagPreview",
                style = {
                    fontSize = 10,
                    unityFontStyleAndWeight = FontStyle.Italic,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };

            Refresh();

            // Rebuild the text whenever the tag (or any of its fields) changes.
            preview.TrackPropertyValue(trackedProperty, _ => Refresh());

            return preview;

            void Refresh()
            {
                // [SerializeReference] field left null – no tag to send.
                if (trackedProperty.boxedValue is not Tag tag)
                {
                    preview.text = "no tag assigned";
                    preview.tooltip = "This entry sends nothing to Steam.";
                    preview.style.color = _invalidColor;

                    return;
                }

                // Steam expects tags as name:value pairs.
                preview.text = $"{tag.GetName()}:{tag.GetValue()}";
                preview.tooltip = $"Sent to Steam as name:value ({tag.GetValueType().Name})";
                preview.style.color = _previewColor;
            }
        }
    }
}
