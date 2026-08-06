using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    /// <summary>
    /// Drawer for <see cref="Tag"/> – draws the enum value as a single row,
    /// labelled with the tag name instead of the array element index, followed by
    /// the Steam preview line rendered by <see cref="TagDrawer"/>.
    /// </summary>
    [CustomPropertyDrawer(typeof(Tag<>),  true)]
    public class GenericTagDrawer : PropertyDrawer
    {
        // Name of the serialized field declared by Tag<TEnum>.
        private const string VALUE_FIELD = "Value";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();

            // Steam preview – same element the base Tag drawer renders on its own.
            var preview = TagDrawer.CreateSteamPreview(property);

            var valueProp = property.FindPropertyRelative(VALUE_FIELD);

            // [SerializeReference] field left null – nothing to edit, preview only.
            if (valueProp == null)
            {
                root.Add(preview);

                return root;
            }

            // PropertyField resolves TEnum on its own, so the enum popup comes for free.
            var valueField = new PropertyField(valueProp, preferredLabel);

            valueField.Bind(property.serializedObject);

            // Line the preview up with the value column, under the field it describes.
            preview.style.marginLeft = EditorGUIUtility.labelWidth;

            root.Add(valueField);
            root.Add(preview);

            return root;
        }
    }
}
