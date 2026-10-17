using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem
{
    [CustomEditor(typeof(ItemDef), true), CanEditMultipleObjects]
    public class ItemDefEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI() => 
            new ItemDefElement(serializedObject);
    }
}
