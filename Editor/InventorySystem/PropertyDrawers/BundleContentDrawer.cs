using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    /// <summary>
    /// Drawer for <see cref="BundleContent"/>: just the list of its elements, under the label of the field holding it,
    /// and below it a treemap of everything it grants, nested generators and bundles included.
    /// </summary>
    [CustomPropertyDrawer(typeof(BundleContent))]
    public class BundleContentDrawer : PropertyDrawer
    {
        private const string ELEMENTS_PROP_NAME = "_elements";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var elements = property.FindPropertyRelative(ELEMENTS_PROP_NAME);
            var treemap = new ContentTreemapElement(elements);

            var root = new VisualElement();
            root.Add(new PropertyField(elements, property.displayName));
            root.Add(treemap);

            // Tracked on a parent, because on a PropertyField itself the callback runs only for the first change.
            root.TrackSerializedObjectValue(property.serializedObject, _ => treemap.Refresh());

            return root;
        }
    }
}
