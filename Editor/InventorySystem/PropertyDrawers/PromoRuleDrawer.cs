using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    /// <summary>
    /// Drawer for every <see cref="PromoRule"/>: a <see cref="NamedSelectTypeElement"/> with the fields of the rule in
    /// its foldout.
    /// </summary>
    [CustomPropertyDrawer(typeof(PromoRule), true)]
    public class PromoRuleDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) =>
            new NamedSelectTypeElement(property, typeof(PromoRule));
    }
}
