using fefek5.Toys.Editor.VisualElements;
using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    /// <summary>
    /// Drawer for <see cref="Promo"/>: a foldout of its rules whose header is <see cref="Promo.ToString"/>, the rules
    /// joined. In a list that is the whole header; as a field it follows the name of the field.
    /// </summary>
    [CustomPropertyDrawer(typeof(Promo))]
    public class PromoDrawer : PropertyDrawer
    {
        private static readonly Color _summaryColor = new(0.6f, 0.6f, 0.6f, 1f);

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var isListElement = property.propertyPath.EndsWith("]");
            var path = property.propertyPath;

            var summary = new Label { style = { color = _summaryColor, marginLeft = 8 } };
            var foldout = isListElement ? new FoldoutElement(property) : new FoldoutElement(property, summary);

            if (!isListElement)
                foldout.text = preferredLabel;

            Refresh(property);

            // The rules are managed references, whose changes the property of the promo does not report.
            foldout.TrackSerializedObjectValue(property.serializedObject, changed => Refresh(changed.FindProperty(path)));

            return foldout;

            void Refresh(SerializedProperty changed)
            {
                var text = changed?.boxedValue?.ToString() ?? string.Empty;

                if (isListElement)
                    foldout.text = text;
                else
                    summary.text = text;
            }
        }
    }
}
