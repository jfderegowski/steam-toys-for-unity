using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    /// <summary>
    /// Drawer for <see cref="BundleContent"/>: just the list of its elements, under the label of the field holding it,
    /// and in a generator a bar of the odds of its entries below.
    /// </summary>
    [CustomPropertyDrawer(typeof(BundleContent))]
    public class BundleContentDrawer : PropertyDrawer
    {
        private const string ELEMENTS_PROP_NAME = "_elements";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var elements = property.FindPropertyRelative(ELEMENTS_PROP_NAME);
            var weightBar = new WeightBarElement(elements);

            var root = new VisualElement();
            root.Add(new PropertyField(elements, property.displayName));
            root.Add(weightBar);

            // Tracked on a parent, because on a PropertyField itself the callback runs only for the first change.
            root.TrackSerializedObjectValue(property.serializedObject, _ => weightBar.Refresh());

            return root;
        }
    }
}
