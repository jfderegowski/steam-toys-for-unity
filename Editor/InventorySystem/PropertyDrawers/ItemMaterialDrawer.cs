using SteamToys.Runtime.InventorySystem;
using SteamToys.Runtime.InventorySystem.ExchangeVariable.Materials;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    [CustomPropertyDrawer(typeof(ItemMaterial))]
    public class ItemMaterialDrawer : MaterialDrawer
    {
        private const string NO_ITEM_LABEL = "(None)";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var baseElement = base.CreatePropertyGUI(property);
            
            var itemProp = property.FindPropertyRelative("_item");
            var quantityProp = property.FindPropertyRelative("_quantity");

            var root = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginTop = 2,
                    marginBottom = 2
                }
            };

            // Use the object reference value directly for the initial text to avoid keeping a live
            // SerializedProperty reference around (it becomes invalid after the iterator moves).
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

            var itemField = new ObjectField {
                bindingPath = itemProp.propertyPath,
                objectType = typeof(Item),
                allowSceneObjects = false,
                label = "",
                style = {
                    flexGrow = 1,
                    flexShrink = 1
                }
            };
            itemField.labelElement.style.display = DisplayStyle.None;

            var qtyRow = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexShrink = 0,
                    flexGrow = 0,
                    marginLeft = 4
                }
            };

            var xLabel = new Label("x") {
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

            baseElement.Add(root);
            
            // Update info label when the ObjectField value changes — use the event's newValue
            itemField.RegisterValueChangedCallback(evt => { infoLabel.text = BuildInfoText(evt.newValue); });

            // Refresh periodically (undo/external changes). Read the value from the ObjectField
            // instead of the SerializedProperty to avoid InvalidOperationException.
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
            
            return baseElement;
        }

        // Accept an Object (or null) instead of a SerializedProperty to avoid
        // keeping the SerializedProperty alive for scheduled callbacks.
        private static string BuildInfoText(Object itemObj)
        {
            var item = itemObj as Item;
            if (item == null)
                return NO_ITEM_LABEL;

            var name = string.IsNullOrEmpty(item.Name) ? item.name : item.Name;
            return $"{name}  (ID: {item.ItemDefId})";
        }
    }
}