using fefek5.Toys.Editor.Icons;
using fefek5.Toys.Editor.VisualElements;
using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    /// <summary>
    /// Drawer for <see cref="Tags"/> and its subclasses: a section under the label of the field holding the tags, with
    /// their fields one after another.
    /// </summary>
    [CustomPropertyDrawer(typeof(Tags), true)]
    public class TagsDrawer : PropertyDrawer
    {
        private const string ICON_NAME = "section-tags-icon";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            EditorIconsDatabase.TryGetIcon<Texture2D>(ICON_NAME, out var icon);

            return new SectionElement(property, icon);
        }
    }
}
