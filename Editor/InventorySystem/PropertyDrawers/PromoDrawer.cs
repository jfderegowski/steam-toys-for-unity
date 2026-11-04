using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    /// <summary>
    /// Drawer for <see cref="Promo"/>: just the list of its rules.
    /// </summary>
    [CustomPropertyDrawer(typeof(Promo))]
    public class PromoDrawer : PropertyDrawer
    {
        private const string RULES_PROP_NAME = "_rules";

        public override VisualElement CreatePropertyGUI(SerializedProperty property) =>
            new PropertyField(property.FindPropertyRelative(RULES_PROP_NAME));
    }
}
