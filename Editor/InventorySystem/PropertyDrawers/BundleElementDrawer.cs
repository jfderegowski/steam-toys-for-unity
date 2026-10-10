using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    [CustomPropertyDrawer(typeof(BundleElement))]
    public class BundleElementDrawer : PropertyDrawer
    {
        private const string NO_ITEM_LABEL = "(None)";

        private const string QUANTITY_LABEL = "x";
        private const string QUANTITY_TOOLTIP = "How many are granted.";
        private const string WEIGHT_LABEL = "weight";
        private const string WEIGHT_TOOLTIP = "Relative odds of this entry being picked.";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var itemProp = property.FindPropertyRelative("_item");
            var quantityProp = property.FindPropertyRelative("_quantity");

            // Root container – horizontal row
            var root = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginTop = 2,
                    marginBottom = 2
                }
            };

            // Info label: "Name (ID: 123)"
            // Use the object reference value directly for the initial text to avoid keeping a SerializedProperty reference alive
            var infoLabel = new Label(BuildInfoText(itemProp.objectReferenceValue)) {
                style = {
                    minWidth = 120,
                    flexShrink = 0,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis,
                    whiteSpace = WhiteSpace.NoWrap,
                    marginRight = 6,
                    color = new Color(0.85f, 0.85f, 0.85f)
                }
            };

            // Item object field – just the reference picker, no label
            var itemField = new ObjectField {
                bindingPath = itemProp.propertyPath,
                objectType = typeof(GrantableItemDef),
                allowSceneObjects = false,
                label = "",
                style = {
                    flexGrow = 1,
                    flexShrink = 1
                }
            };
            
            // Hide the built-in label so only the object picker is visible
            itemField.labelElement.style.display = DisplayStyle.None;

            // Quantity: custom row with "x" label + integer input (no built-in label gap)
            var qtyRow = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexShrink = 0,
                    flexGrow = 0,
                    marginLeft = 4
                }
            };

            // The same contents list holds quantities in a bundle and weights in a generator.
            var isBundle = property.serializedObject.targetObject is Bundle;

            var xLabel = new Label(isBundle ? QUANTITY_LABEL : WEIGHT_LABEL) {
                tooltip = isBundle ? QUANTITY_TOOLTIP : WEIGHT_TOOLTIP,
                style = {
                    marginRight = 2,
                    paddingRight = 0,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };

            var quantityField = new IntegerField {
                bindingPath = quantityProp.propertyPath,
                label = "",
                style = {
                    minWidth = 40,
                    width = 40,
                    flexShrink = 0,
                    flexGrow = 0
                }
            };
            quantityField.labelElement.style.display = DisplayStyle.None;

            qtyRow.Add(xLabel);
            qtyRow.Add(quantityField);

            root.Add(infoLabel);
            root.Add(itemField);
            root.Add(qtyRow);

            // Bind so that changes reflect automatically
            // Use the ObjectField's value from the change event instead of capturing the SerializedProperty
            itemField.RegisterValueChangedCallback(evt => {
                infoLabel.text = BuildInfoText(evt.newValue);
            });

            // Also refresh on undo / external changes
            // Use the ObjectField.value inside the scheduled callback to avoid accessing a possibly disposed SerializedProperty
            root.RegisterCallback<AttachToPanelEvent>(_ => {
                root.schedule.Execute(() => {
                    try {
                        infoLabel.text = BuildInfoText(itemField.value);
                    }
                    catch {
                        // Swallow exceptions (element may be destroyed) to avoid repeated errors
                    }
                }).Every(500);
            });

            return root;
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
