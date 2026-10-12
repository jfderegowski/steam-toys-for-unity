using System.Collections.Generic;
using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem
{
    /// <summary>
    /// Draws the whole <see cref="ItemDef"/> inspector: the default inspector, the schema warnings and the
    /// JSON preview / copy buttons.
    /// </summary>
    public class ItemEditorElement : VisualElement
    {
        #region Constants

        private const string PREVIEW_TITLE = "Item JSON";

        private const string COPY_ITEM_BUTTON_TEXT = "Copy JSON";
        private const string COPY_ITEM_BUTTON_TOOLTIP = "Copy the item definition on its own.";

        private const string COPY_IMPORT_BUTTON_TEXT = "Copy JSON + AppID";
        private const string COPY_IMPORT_BUTTON_TOOLTIP =
            "Copy the item wrapped in an appid / items envelope, ready to import into the Steam Inventory Service.";

        #endregion

        #region Fields

        private readonly ItemDef _item;
        private readonly VisualElement _warnings;
        private readonly JsonPreviewElement _preview;
        private readonly List<string> _warningMessages = new();

        #endregion

        public ItemEditorElement(SerializedObject serializedObject, UnityEditor.Editor editor)
        {
            _item = serializedObject.targetObject as ItemDef;

            InspectorElement.FillDefaultInspector(this, serializedObject, editor);

            _warnings = new VisualElement();
            Add(_warnings);
            RefreshWarnings();

            _preview = new JsonPreviewElement(PREVIEW_TITLE);

            _preview.AddCopyAction(COPY_ITEM_BUTTON_TEXT, COPY_ITEM_BUTTON_TOOLTIP, GetItemJson);
            _preview.AddCopyAction(COPY_IMPORT_BUTTON_TEXT, COPY_IMPORT_BUTTON_TOOLTIP, GetImportJson);
            _preview.SetJson(GetItemJson());

            Add(_preview);

            // Keep the preview and warnings in sync while the item is being edited.
            this.TrackSerializedObjectValue(serializedObject, _ =>
            {
                RefreshWarnings();
                _preview.SetJson(GetItemJson());
            });
        }

        #region Warnings

        private void RefreshWarnings()
        {
            _warnings.Clear();

            if (_item == null)
                return;

            _warningMessages.Clear();
            _item.CollectSchemaWarnings(_warningMessages);

            foreach (var message in _warningMessages)
                _warnings.Add(new HelpBox(message, HelpBoxMessageType.Warning));
        }

        #endregion

        #region Json

        private string GetItemJson() => _item == null ? string.Empty : _item.ToJson();

        /// <summary>
        /// Wraps the item in the envelope the Steam Inventory Service expects when importing item definitions:
        /// an <c>appid</c> (from the Steam Settings) plus an <c>items</c> array.
        /// </summary>
        private string GetImportJson() => _item == null ? string.Empty : SteamItemsDB.ToImportJson(new[] { _item });

        #endregion
    }
}
