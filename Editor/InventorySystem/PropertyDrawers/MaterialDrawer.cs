using System;
using fefek5.Toys.Editor.Extensions;
using fefek5.Toys.Editor.VisualElements;
using SteamToys.Runtime.InventorySystem;
using SteamToys.Runtime.InventorySystem.ExchangeVariable.Materials;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Material = SteamToys.Runtime.InventorySystem.ExchangeVariable.Material;
using Object = UnityEngine.Object;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    /// <summary>
    /// Drawer for every <see cref="Material"/>: a <see cref="SelectTypeElement"/> with one row in its foldout. An item
    /// and a tag get the name of what they match, e.g. <c>Item Rarity</c> with the Steam name <c>item_rarity</c> as its
    /// tooltip, then the value and the quantity; any other material gets its fields.
    /// </summary>
    [CustomPropertyDrawer(typeof(Material), true)]
    public class MaterialDrawer : PropertyDrawer
    {
        private const string NO_ITEM_LABEL = "(None)";

        public override VisualElement CreatePropertyGUI(SerializedProperty property) =>
            new SelectTypeElement(property, typeof(Material), CreateContent);

        private static VisualElement CreateContent(SerializedProperty property)
        {
            var material = property.managedReferenceValue as Material;

            if (material is ItemMaterial)
                return CreateItemRow(property);

            if (GetEnumType(material?.GetType()) is { } enumType)
                return CreateTagRow(property, material, enumType);

            var fields = new VisualElement();

            foreach (var child in property.GetVisibleChildren())
                fields.Add(new PropertyField(child));

            return fields;
        }

        #region Rows

        private static VisualElement CreateItemRow(SerializedProperty property)
        {
            var itemProperty = property.FindPropertyRelative("_item");

            var itemField = new ObjectField {
                bindingPath = itemProperty.propertyPath,
                objectType = typeof(ItemDef),
                allowSceneObjects = false,
                style = {
                    flexGrow = 1,
                    flexShrink = 1
                }
            };
            itemField.AddToClassList(ObjectField.alignedFieldUssClassName);

            SetName(itemProperty.objectReferenceValue);

            itemField.RegisterValueChangedCallback(evt => SetName(evt.newValue));

            // The name of the item can change without the reference changing.
            itemField.RegisterCallback<AttachToPanelEvent>(_ =>
                itemField.schedule.Execute(() => SetName(itemField.value)).Every(500));

            return CreateRow(itemField, property);

            // The label is too narrow for a long name, so the tooltip has all of it.
            void SetName(Object item) => itemField.label = itemField.tooltip = BuildItemText(item);
        }

        private static VisualElement CreateTagRow(SerializedProperty property, Material material, Type enumType)
        {
            var label = ObjectNames.NicifyVariableName(enumType.Name);

            var valueField = new PropertyField(property.FindPropertyRelative("_value"), label) {
                tooltip = material.GetTagName(),
                style = {
                    flexGrow = 1,
                    flexShrink = 1
                }
            };

            return CreateRow(valueField, property);
        }

        private static VisualElement CreateRow(VisualElement valueField, SerializedProperty property)
        {
            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginTop = 2,
                    marginBottom = 2
                }
            };

            var quantityRow = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexShrink = 0,
                    flexGrow = 0,
                    marginLeft = 4
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

            quantityRow.Add(new Label("x") {
                style = {
                    marginRight = 2,
                    paddingRight = 0,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            });
            quantityRow.Add(quantityField);

            row.Add(valueField);
            row.Add(quantityRow);

            return row;
        }

        #endregion

        private static string BuildItemText(Object itemObject)
        {
            var item = itemObject as ItemDef;

            if (item == null)
                return NO_ITEM_LABEL;

            var name = string.IsNullOrEmpty(item.Name) ? item.name : item.Name;

            return $"{name}  (ID: {item.ItemDefId})";
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
