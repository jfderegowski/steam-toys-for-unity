using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    /// <summary>
    /// Drawer for <see cref="BundleContent"/>: just the list of its elements, under the label of the field holding it.
    /// </summary>
    [CustomPropertyDrawer(typeof(BundleContent))]
    public class BundleContentDrawer : PropertyDrawer
    {
        private const string ELEMENTS_PROP_NAME = "_elements";

        public override VisualElement CreatePropertyGUI(SerializedProperty property) =>
            new PropertyField(property.FindPropertyRelative(ELEMENTS_PROP_NAME), property.displayName);
    }
}
