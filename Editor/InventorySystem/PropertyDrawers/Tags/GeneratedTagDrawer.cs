using System;
using System.Linq;
using SteamToys.Runtime.InventorySystem;
using SteamToys.Runtime.InventorySystem.Converters;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    /// <summary>
    /// Drawer for <see cref="GeneratedTag{TEnum}"/> – no value to edit, since a tag generator picks it for each
    /// created item; shows that the tag is generated and left out of the ItemDef, with the values it can take
    /// in the tooltip.
    /// </summary>
    [CustomPropertyDrawer(typeof(GeneratedTag<>), true)]
    public class GeneratedTagDrawer : PropertyDrawer
    {
        private const string GENERATED_TEXT = "set by a tag generator";
        private const string GENERATED_TOOLTIP =
            "A tag generator picks this value for each created item, so the ItemDef does not send it to Steam. " +
            "It is still read from an item's tags.";
        private const string VALUES_TOOLTIP_PREFIX = "\n\nPossible values: ";

        private static readonly Color _previewColor = new(0.6f, 0.6f, 0.6f, 1f);

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var tag = property.boxedValue as Tag;
            var name = tag != null ? tag.GetName() : preferredLabel;

            var field = new TextField(preferredLabel) {
                value = $"{name}: {GENERATED_TEXT}",
                isReadOnly = true,
                tooltip = tag != null ? GENERATED_TOOLTIP + VALUES_TOOLTIP_PREFIX + GetValuesText(tag.GetValueType()) : GENERATED_TOOLTIP
            };

            field.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            field.Q(TextField.textInputUssName).style.color = _previewColor;

            return field;
        }

        /// <summary>
        /// The enum's values as Steam writes them, e.g. "common, rare, legendary".
        /// </summary>
        private static string GetValuesText(Type enumType) =>
            string.Join(", ", Enum.GetNames(enumType).Select(valueName => valueName.ToSnakeCase()));
    }
}
