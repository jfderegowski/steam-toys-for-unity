using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    /// <summary>
    /// Drawer for <see cref="DefaultTags"/>: its dictionary under the label of the field holding it, rather than a foldout
    /// around a second one.
    /// </summary>
    [CustomPropertyDrawer(typeof(DefaultTags))]
    public class TagsDrawer : PropertyDrawer
    {
        // Name of the serialized dictionary declared by DefaultTags.
        private const string TAGS_FIELD = "_tags";

        public override VisualElement CreatePropertyGUI(SerializedProperty property) =>
            new PropertyField(property.FindPropertyRelative(TAGS_FIELD), preferredLabel);
    }
}
