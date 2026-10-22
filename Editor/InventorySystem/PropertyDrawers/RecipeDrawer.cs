using fefek5.Toys.Editor.VisualElements;
using SteamToys.Runtime.InventorySystem.ExchangeVariable;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    /// <summary>
    /// Drawer for <see cref="Recipe"/>: a foldout of its materials. In a list its header is <see cref="Recipe.ToString"/>,
    /// the materials it takes; as a field it is the name of the field.
    /// </summary>
    [CustomPropertyDrawer(typeof(Recipe))]
    public class RecipeDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var foldout = new FoldoutElement(property);

            if (!property.propertyPath.EndsWith("]"))
            {
                foldout.text = preferredLabel;
                return foldout;
            }

            var path = property.propertyPath;

            Refresh(property);

            // The materials are managed references, whose changes the property of the recipe does not report.
            foldout.TrackSerializedObjectValue(property.serializedObject, changed => Refresh(changed.FindProperty(path)));

            return foldout;

            void Refresh(SerializedProperty changed) => foldout.text = changed?.boxedValue?.ToString() ?? string.Empty;
        }
    }
}
