using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    [CustomPropertyDrawer(typeof(PriceValue))]
    public class PriceValueDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var priceValue = property.boxedValue as PriceValue;
            
            // Root container with horizontal layout
            var root = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                },
                name = "priceValueRoot"
            };

            // Find serialized fields
            var amountProp = property.FindPropertyRelative("_amount");
            var currencyProp = property.FindPropertyRelative("_currency");

            var priceText = priceValue != null
                ? $"{priceValue.GetFloatAmount()} {priceValue.Currency}"
                : "Invalid PriceValue";

            var steamString = priceValue != null 
                ? priceValue.ToSteamString() 
                : "Invalid PriceValue";

            // Create PropertyFields which will bind automatically when the parent is bound
            var amountField = new PropertyField(amountProp,$"{priceText} ({steamString})");
            var currencyField = new PropertyField(currencyProp, "");

            // Styling: amount grows, currency has fixed basis
            amountField.style.flexGrow = 1;
            amountField.style.marginRight = 4;
            currencyField.style.width = 140;

            // Add to root
            root.Add(amountField);
            root.Add(currencyField);

            // Bind the root to the SerializedObject so PropertyFields work
            root.Bind(property.serializedObject);

            return root;
        }
    }
}