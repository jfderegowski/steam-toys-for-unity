using SteamToys.Runtime.InventorySystem;
using SteamToys.Runtime.InventorySystem.Converters.Price;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    /// <summary>
    /// Drawer for <see cref="PriceValue"/>: one <c>Price</c> row with the amount, the currency and the price as
    /// <see cref="PriceValue.ToString"/> tells it, e.g. <c>4.99 PLN ≈ 1.35 $</c>.
    /// </summary>
    [CustomPropertyDrawer(typeof(PriceValue))]
    public class PriceValueDrawer : PropertyDrawer
    {
        private const string LABEL = "Price";
        private const string AMOUNT_FIELD = "_amount";
        private const string CURRENCY_FIELD = "_currency";

        private static readonly Color _previewColor = new(0.6f, 0.6f, 0.6f, 1f);

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            // The amount field carries the aligned Price label; the currency and the price follow its input.
            var root = new UnsignedIntegerField(LABEL)
            {
                bindingPath = property.FindPropertyRelative(AMOUNT_FIELD).propertyPath,
                tooltip = "Amount in the smallest unit of the currency, e.g. cents: 499 is 4.99."
            };

            root.AddToClassList(BaseField<uint>.alignedFieldUssClassName);

            var currency = new EnumField
            {
                bindingPath = property.FindPropertyRelative(CURRENCY_FIELD).propertyPath,
                style = { width = 100, marginLeft = 4 }
            };

            var preview = new Label
            {
                style =
                {
                    marginLeft = 6,
                    minWidth = 120,
                    color = _previewColor,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };

            root.Add(currency);
            root.Add(preview);

            Refresh(property, preview);
            preview.TrackPropertyValue(property, changed => Refresh(changed, preview));

            return root;
        }

        private static void Refresh(SerializedProperty property, Label preview)
        {
            var price = property.boxedValue as PriceValue;

            preview.text = price?.ToString() ?? string.Empty;
            preview.tooltip = price == null ? string.Empty : $"Sent to Steam as {PriceValueConverter.ToString(price)}.";
        }
    }
}
