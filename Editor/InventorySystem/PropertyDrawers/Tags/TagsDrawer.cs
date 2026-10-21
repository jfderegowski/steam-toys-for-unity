using System.Collections.Generic;
using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    /// <summary>
    /// Drawer for <see cref="Tags"/> and its subclasses: their fields one after another, rather than a foldout around
    /// them. A class with a single field, e.g. the dictionary of <see cref="DefaultTags"/>, draws it under the label of
    /// the field holding the tags.
    /// </summary>
    [CustomPropertyDrawer(typeof(Tags), true)]
    public class TagsDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var children = GetChildren(property);

            if (children.Count == 1)
                return new PropertyField(children[0], preferredLabel);

            var root = new VisualElement();

            foreach (var child in children)
                root.Add(new PropertyField(child));

            return root;
        }

        private static List<SerializedProperty> GetChildren(SerializedProperty property)
        {
            var children = new List<SerializedProperty>();
            var child = property.Copy();
            var end = property.GetEndProperty();

            if (!child.NextVisible(true))
                return children;

            do
            {
                if (SerializedProperty.EqualContents(child, end))
                    break;

                children.Add(child.Copy());
            }
            while (child.NextVisible(false));

            return children;
        }
    }
}
