using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem
{
    /// <summary>
    /// Draws the whole <see cref="Item"/> inspector: the default inspector plus the JSON preview / copy button.
    /// </summary>
    public class ItemEditorElement : VisualElement
    {
        private const string COPY_JSON_BUTTON_TEXT = "Get Item JSON";

        private readonly Item _item;

        public ItemEditorElement(SerializedObject serializedObject, UnityEditor.Editor editor)
        {
            _item = serializedObject.targetObject as Item;

            InspectorElement.FillDefaultInspector(this, serializedObject, editor);

            var copyItemJsonButton = new Button(CopyItemJson) {
                text = COPY_JSON_BUTTON_TEXT
            };

            var jsonText = new TextElement {
                text = GetItemJson()
            };

            Add(copyItemJsonButton);
            Add(jsonText);
        }

        private string GetItemJson() => _item == null ? string.Empty : _item.ToJson();

        private void CopyItemJson()
        {
            if (_item == null)
                return;

            CopyToClipboard(_item.ToJson());
        }

        private static void CopyToClipboard(string text)
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
