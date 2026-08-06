using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem
{
    [CustomEditor(typeof(Item), true), CanEditMultipleObjects]
    public class ItemEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI() => 
            new ItemEditorElement(serializedObject, this);
    }
}
