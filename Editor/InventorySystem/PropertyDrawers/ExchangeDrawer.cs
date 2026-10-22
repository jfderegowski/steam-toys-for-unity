using fefek5.Toys.Editor.Icons;
using fefek5.Toys.Editor.VisualElements;
using SteamToys.Runtime.InventorySystem.ExchangeVariable;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    /// <summary>
    /// Drawer for <see cref="Exchange"/>: a section under the label of the field holding it, with its recipes.
    /// </summary>
    [CustomPropertyDrawer(typeof(Exchange))]
    public class ExchangeDrawer : PropertyDrawer
    {
        private const string ICON_NAME = "section-exchange-icon";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            EditorIconsDatabase.TryGetIcon<Texture2D>(ICON_NAME, out var icon);

            return new SectionElement(property, icon);
        }
    }
}
