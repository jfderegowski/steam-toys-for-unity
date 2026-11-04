using SteamToys.Runtime.InventorySystem.ExchangeVariable;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    /// <summary>
    /// Drawer for <see cref="Recipe"/>: just the list of its materials. In a list its header is <see cref="Recipe.ToString"/>,
    /// the materials it takes, until there are none; as a field it is the name of the field.
    /// </summary>
    [CustomPropertyDrawer(typeof(Recipe))]
    public class RecipeDrawer : PropertyDrawer
    {
        private const string MATERIALS_PROP_NAME = "_materials";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var field = new PropertyField(property.FindPropertyRelative(MATERIALS_PROP_NAME));

            if (!property.propertyPath.EndsWith("]"))
            {
                field.label = preferredLabel;
                return field;
            }

            var path = property.propertyPath;

            Refresh(property);

            // The materials are managed references, whose changes the property of the recipe does not report.
            field.TrackSerializedObjectValue(property.serializedObject, changed => Refresh(changed.FindProperty(path)));

            return field;

            void Refresh(SerializedProperty changed)
            {
                if (changed == null) return;

                var materials = changed.FindPropertyRelative(MATERIALS_PROP_NAME);

                field.label = materials.arraySize > 0 ? changed.boxedValue?.ToString() : materials.displayName;

                // The field hands its label to the list only when it builds it.
                if (field.Q<ListView>() is { } list)
                    list.headerTitle = field.label;
            }
        }
    }
}
