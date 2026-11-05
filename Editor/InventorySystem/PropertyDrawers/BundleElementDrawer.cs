using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    /// <summary>
    /// Drawer for <see cref="BundleElement"/>: one row of the item picker, labelled with the item's name and id,
    /// and its quantity (a weight in a generator).
    /// </summary>
    [CustomPropertyDrawer(typeof(BundleElement))]
    public class BundleElementDrawer : PropertyDrawer
    {
        private const string ITEM_PROP_NAME = "_item";
        private const string QUANTITY_PROP_NAME = "_quantity";

        private const string NO_ITEM_LABEL = "(None)";

        private const string QUANTITY_LABEL = "x";
        private const string QUANTITY_TOOLTIP = "How many are granted.";
        private const string WEIGHT_LABEL = "weight";
        private const string WEIGHT_TOOLTIP = "Relative odds of this entry being picked.";

        private const int QUANTITY_INPUT_WIDTH = 50;
        private const int REFRESH_INTERVAL_MS = 500;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var itemProp = property.FindPropertyRelative(ITEM_PROP_NAME);
            var quantityProp = property.FindPropertyRelative(QUANTITY_PROP_NAME);

            var root = new VisualElement { style = { flexDirection = FlexDirection.Row } };

            // The item picker, labelled with the item itself and aligned like any inspector field.
            var itemField = new ObjectField(BuildInfoText(itemProp.objectReferenceValue)) {
                bindingPath = itemProp.propertyPath,
                objectType = typeof(ItemDef),
                allowSceneObjects = false,
                style = { flexGrow = 1, flexShrink = 1 }
            };
            itemField.AddToClassList(BaseField<Object>.alignedFieldUssClassName);

            // The same contents list holds quantities in a bundle and weights in a generator.
            var isBundle = property.serializedObject.targetObject is ItemDef { Type: ItemType.Bundle };

            var quantityField = new IntegerField(isBundle ? QUANTITY_LABEL : WEIGHT_LABEL) {
                bindingPath = quantityProp.propertyPath,
                tooltip = isBundle ? QUANTITY_TOOLTIP : WEIGHT_TOOLTIP,
                style = { flexGrow = 0, flexShrink = 0 }
            };
            // Only the default label column is too wide for a one-word label next to another field.
            quantityField.labelElement.style.minWidth = StyleKeyword.Auto;
            quantityField.labelElement.style.width = StyleKeyword.Auto;
            quantityField.Q(className: IntegerField.inputUssClassName).style.width = QUANTITY_INPUT_WIDTH;

            root.Add(itemField);
            root.Add(quantityField);

            itemField.RegisterValueChangedCallback(evt => SetInfoText(itemField, evt.newValue));

            // A bound field is not notified on undo, and the item's own name can change elsewhere.
            root.RegisterCallback<AttachToPanelEvent>(_ =>
                root.schedule.Execute(() => SetInfoText(itemField, itemField.value)).Every(REFRESH_INTERVAL_MS));

            return root;
        }

        private static void SetInfoText(ObjectField field, Object item)
        {
            var text = BuildInfoText(item);
            if (field.label == text)
                return;

            field.label = text;
            field.labelElement.tooltip = text;
        }

        private static string BuildInfoText(Object itemObj)
        {
            var item = itemObj as ItemDef;
            if (item == null)
                return NO_ITEM_LABEL;

            var name = string.IsNullOrEmpty(item.Name) ? item.name : item.Name;
            return $"{name}  (ID: {item.ItemDefId})";
        }
    }
}
