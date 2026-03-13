using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem
{
    [CustomEditor(typeof(Item), true), CanEditMultipleObjects]
    public class ItemEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var item = (Item)target;
            
            var root = new VisualElement();
            
            InspectorElement.FillDefaultInspector(root, serializedObject, this);

            var cappyItemJsonButton = new Button(CopyItemJson) {
                text = "Get Item JSON"
            };

            var jsonText = new TextElement {
                text = item.ToJson(),
            };
            
            root.Add(cappyItemJsonButton);
            root.Add(jsonText);
            
            return root;
        }
        
        private void CopyItemJson() => CopyItemJson((Item)target);

        private void CopyItemJson(Item item) => CopyToClipboard(item.ToJson());

        private void CopyToClipboard(string text)
        {
            var te = new TextEditor {
                text = text
            };
            
            te.SelectAll();
            te.Copy();

            Debug.Log($"Copied to clipboard: (Select to show more) \n{text}");
        }
    }
}