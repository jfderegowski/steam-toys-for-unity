using System;
using SteamToys.Runtime.InventorySystem.ExchangeVariable.Materials;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Material = SteamToys.Runtime.InventorySystem.ExchangeVariable.Material;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    /// <summary>
    /// Drawer for every <see cref="TagMaterial{TEnum}"/>: under the type of <see cref="MaterialDrawer"/>, one row of the
    /// name of the tag, e.g. <c>Item Rarity</c> with the Steam name <c>item_rarity</c> as its tooltip, in place of the
    /// label of the value, the value and the quantity, as <see cref="ItemMaterialDrawer"/> draws an item.
    /// </summary>
    [CustomPropertyDrawer(typeof(TagMaterial<>), true)]
    public class TagMaterialDrawer : MaterialDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var baseElement = base.CreatePropertyGUI(property);

            var material = property.managedReferenceValue as Material;
            var enumType = GetEnumType(material?.GetType());

            var root = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginTop = 2,
                    marginBottom = 2
                }
            };

            var tagLabel = new Label(enumType != null ? ObjectNames.NicifyVariableName(enumType.Name) : string.Empty) {
                tooltip = material?.GetTagName(),
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

            var valueField = new PropertyField(property.FindPropertyRelative("_value"), string.Empty) {
                style = {
                    flexGrow = 1,
                    flexShrink = 1
                }
            };

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
                bindingPath = property.FindPropertyRelative("_quantity").propertyPath,
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

            root.Add(tagLabel);
            root.Add(valueField);
            root.Add(qtyRow);

            baseElement.Add(root);

            return baseElement;
        }

        /// <summary>The enum of the tag, the TEnum of the <see cref="TagMaterial{TEnum}"/> it derives from.</summary>
        private static Type GetEnumType(Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(TagMaterial<>))
                    return current.GetGenericArguments()[0];
            }

            return null;
        }
    }
}
