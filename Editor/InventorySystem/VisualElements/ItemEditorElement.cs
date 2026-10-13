using System.Collections.Generic;
using System.Linq;
using fefek5.Toys.Editor.VisualElements;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem
{
    /// <summary>
    /// Draws the whole <see cref="ItemDef"/> inspector: the default inspector, the schema warnings and the
    /// JSON preview / copy buttons. With several definitions selected, the warnings, the JSON and Generate Item
    /// cover all of them.
    /// </summary>
    public class ItemEditorElement : VisualElement
    {
        #region Constants

        private const string PREVIEW_TITLE = "Item JSON";

        private const string COPY_ITEM_BUTTON_TEXT = "Copy JSON";
        private const string COPY_ITEM_BUTTON_TOOLTIP =
            "Copy the item definition on its own, or an array of the selected definitions.";

        private const string COPY_IMPORT_BUTTON_TEXT = "Copy JSON + AppID";
        private const string COPY_IMPORT_BUTTON_TOOLTIP =
            "Copy the selected items wrapped in an appid / items envelope, ready to import into the Steam Inventory Service.";

        #endregion

        #region Fields

        private readonly ItemDef[] _items;
        private readonly VisualElement _warnings;
        private readonly JsonPreviewElement _preview;
        private readonly List<string> _warningMessages = new();

        #endregion

        public ItemEditorElement(SerializedObject serializedObject, UnityEditor.Editor editor)
        {
            _items = serializedObject.targetObjects.OfType<ItemDef>().ToArray();

            InspectorElement.FillDefaultInspector(this, serializedObject, editor);

            _warnings = new VisualElement();
            Add(_warnings);
            RefreshWarnings();

            // A tag generator cannot be granted, so the button is there only when every selected definition can be.
            if (_items.Length > 0 && _items.All(item => item is GrantableItemDef))
                Add(CreateGrantSection(_items.Cast<object>().ToArray()));

            _preview = new JsonPreviewElement(PREVIEW_TITLE);

            _preview.AddCopyAction(COPY_ITEM_BUTTON_TEXT, COPY_ITEM_BUTTON_TOOLTIP, GetItemJson);
            _preview.AddCopyAction(COPY_IMPORT_BUTTON_TEXT, COPY_IMPORT_BUTTON_TOOLTIP, GetImportJson);
            _preview.SetJson(GetItemJson());

            Add(_preview);

            // Keep the preview and warnings in sync while the items are being edited.
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

            var named = _items.Length > 1;

            foreach (var item in _items)
            {
                if (!item)
                    continue;

                _warningMessages.Clear();
                item.CollectSchemaWarnings(_warningMessages);

                foreach (var message in _warningMessages)
                    _warnings.Add(new HelpBox(named ? $"{item.name}: {message}" : message, HelpBoxMessageType.Warning));
            }
        }

        #endregion

        #region Grant

        private static VisualElement CreateGrantSection(object[] items)
        {
            var generate = new InspectorButtonElement(items, nameof(GrantableItemDef.GenerateItem))
            {
                tooltip = "Grants each selected definition to the signed-in account, for testing. Works only for " +
                          "accounts of the Steamworks partner group, and only once the definition is published on Steam."
            };

            generate.SetEnabled(Inventory.Initialized);

            // Polled: the Steam session starts and stops outside this inspector.
            generate.schedule.Execute(() => generate.SetEnabled(Inventory.Initialized)).Every(500);

            return generate;
        }

        #endregion

        #region Json

        private IEnumerable<ItemDef> LiveItems => _items.Where(item => item);

        /// <summary>The definition on its own, or an array of them with several selected.</summary>
        private string GetItemJson()
        {
            var items = LiveItems.ToArray();

            return items.Length switch
            {
                0 => string.Empty,
                1 => items[0].ToJson(),
                _ => JsonConvert.SerializeObject(items, Formatting.Indented)
            };
        }

        /// <summary>
        /// Wraps the items in the envelope the Steam Inventory Service expects when importing item definitions:
        /// an <c>appid</c> (from the Steam Settings) plus an <c>items</c> array.
        /// </summary>
        private string GetImportJson() => _items.Length == 0 ? string.Empty : SteamItemsDB.ToImportJson(LiveItems);

        #endregion
    }
}
