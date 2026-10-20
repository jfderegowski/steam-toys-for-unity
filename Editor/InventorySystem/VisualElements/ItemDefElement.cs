using System;
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
    /// Draws the whole <see cref="ItemDef"/> inspector: the fields of the schema properties its type uses, the schema
    /// warnings and the JSON preview / copy buttons. With several definitions selected, a field shows when all of them
    /// use it, and the warnings, the JSON and Generate Item cover all of them.
    /// </summary>
    public class ItemDefElement : VisualElement
    {
        #region Constants

        private const uint MAX_DISCOUNT = 100;
        private const uint MAX_DROPS_PER_WINDOW = 10;

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

        public ItemDefElement(SerializedObject serializedObject)
        {
            _items = serializedObject.targetObjects.OfType<ItemDef>().ToArray();

            AddFields(serializedObject);

            _warnings = new VisualElement();
            Add(_warnings);
            RefreshWarnings();

            // A tag generator creates no items, so the button is there only when every selected definition does.
            if (_items.Length > 0 && _items.All(item => item.Type != ItemType.TagGenerator))
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

        #region Fields

        private const string SCRIPT_PROPERTY = "m_Script";

        /// <summary>The default inspector, without the fields of schema properties the type does not use.</summary>
        private void AddFields(SerializedObject serializedObject)
        {
            var property = serializedObject.GetIterator();

            if (!property.NextVisible(true))
                return;

            do
            {
                if (!IsShown(property.name))
                    continue;

                var field = new PropertyField(property.Copy());

                if (property.propertyPath == SCRIPT_PROPERTY)
                    field.SetEnabled(false);

                Add(field);
            }
            while (property.NextVisible(false));

            this.Bind(serializedObject);
        }

        private bool IsShown(string fieldName) =>
            _items.All(item => !item || item.SchemaSupportsField(fieldName));

        #endregion

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
                CollectWarnings(item, _warningMessages);

                foreach (var message in _warningMessages)
                    _warnings.Add(new HelpBox(named ? $"{item.name}: {message}" : message, HelpBoxMessageType.Warning));
            }
        }

        /// <summary>
        /// Adds a message for every setting of <paramref name="item"/> Steam would reject or silently ignore.
        /// </summary>
        internal static void CollectWarnings(ItemDef item, List<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
                warnings.Add("Name is empty: Steam rejects the definition, and an upload stops there.");

            if (item.SchemaSupportsContent())
                CollectContentWarnings(item, warnings);

            if (item.SchemaSupportsPrice())
                CollectPriceWarnings(item, warnings);

            if (item.SchemaSupportsPromo())
                CollectPromoWarnings(item, warnings);

            if (item.SchemaSupportsExchange())
                CollectExchangeWarnings(item, warnings);

            if (item.SchemaSupportsPurchaseLimit() && item.PurchaseLimit > 0 && (item.Tradable || item.Marketable))
                warnings.Add("Purchase Limit does not block repurchasing tradable or marketable items.");

            if (item.SchemaSupportsDropMaxPerWindow() && item.DropMaxPerWindow > MAX_DROPS_PER_WINDOW)
                warnings.Add($"Drop Max Per Window cannot exceed {MAX_DROPS_PER_WINDOW}.");

            if (item.SchemaSupportsDropWindow() && item.DropWindow.hasValue && item.DropWindow.value == 0)
                warnings.Add("Drop Window is enabled with 0 minutes, so it is not exported.");

            if (item is TagGeneratorDef tagGenerator)
                CollectTagGeneratorWarnings(tagGenerator, warnings);
        }

        private static void CollectContentWarnings(ItemDef item, List<string> warnings)
        {
            var content = item.Content;
            var amountName = item.Type == ItemType.Bundle ? "quantity" : "weight";

            if (content == null || content.Count == 0)
            {
                warnings.Add(item.Type == ItemType.Bundle
                    ? "Bundle has no contents: Steam rejects it, and an upload stops there."
                    : $"{item.Type} has no contents, so it grants nothing.");
                return;
            }

            for (var i = 0; i < content.Count; i++)
            {
                var element = content[i];

                if (element is not { HasItem: true })
                    warnings.Add($"Contents entry {i} has no item and is skipped.");
                else if (element.Item == item)
                    warnings.Add($"Contents entry {i} references this {item.Type} itself.");

                if (element != null && element.Quantity == 0)
                    warnings.Add($"Contents entry {i} has a {amountName} of 0.");
            }
        }

        /// <summary>
        /// The recipe entries the export cannot write. It stops on them rather than leaving them out, since a recipe
        /// without one of its materials would be cheaper than meant.
        /// </summary>
        private static void CollectExchangeWarnings(ItemDef item, List<string> warnings)
        {
            var exchange = item.Exchange;

            if (exchange == null)
                return;

            for (var i = 0; i < exchange.Count; i++)
            {
                var recipe = exchange[i];

                if (recipe == null)
                {
                    warnings.Add($"Exchange recipe {i} is empty, so the definition cannot be exported.");
                    continue;
                }

                for (var j = 0; j < recipe.Count; j++)
                {
                    if (recipe[j] == null)
                    {
                        warnings.Add($"Exchange recipe {i}, material {j} is empty, so the definition cannot be exported.");
                        continue;
                    }

                    try
                    {
                        recipe[j].ToSteamString();
                    }
                    catch (InvalidOperationException exception)
                    {
                        warnings.Add($"Exchange recipe {i}, material {j}: {exception.Message} The definition cannot be exported until it is set.");
                    }
                }
            }
        }

        private static void CollectPriceWarnings(ItemDef item, List<string> warnings)
        {
            if (item.PriceCategory.hasValue && !string.IsNullOrEmpty(item.Price?.ToSteamString()))
                warnings.Add("Price and Price Category are both set: only the price category is exported.");

            if (item.Price != null && item.Price.RegularPrices.Count == 0 && item.Price.Promotions.Count > 0)
                warnings.Add("Price has sales but no regular price, which Steam requires first.");

            if (!item.SchemaSupportsPurchaseBundleDiscount())
                return;

            if (item.PurchaseBundleDiscount > MAX_DISCOUNT)
                warnings.Add($"Purchase Bundle Discount is a percentage and cannot exceed {MAX_DISCOUNT}.");

            if (item.PurchaseBundleDiscount > 0 && item.UseBundlePrice)
                warnings.Add("Purchase Bundle Discount is ignored when Use Bundle Price is on.");
        }

        private static void CollectPromoWarnings(ItemDef item, List<string> warnings)
        {
            var manual = item.Promo != null && item.Promo.Any(rule => rule is ManualPromoRule);

            if (item.DropStartTime is { HasValue: true } && !manual)
                warnings.Add("Drop Start Time only applies to a manual promo; add a Manual promo rule or clear it.");

            // On a playtime generator the interval also paces its drops.
            if (item.DropInterval > 0 && !manual && item.Type != ItemType.PlaytimeGenerator)
                warnings.Add("Drop Interval only applies to a manual promo; add a Manual promo rule or clear it.");

            if (item.Promo == null)
                return;

            for (var i = 0; i < item.Promo.Count; i++)
            {
                if (string.IsNullOrEmpty(item.Promo[i]?.ToSteamString()))
                    warnings.Add($"Promo rule {i} is incomplete and is left out.");
            }
        }

        private static void CollectTagGeneratorWarnings(TagGeneratorDef generatorDef, List<string> warnings)
        {
            var tagValuesAndChances = generatorDef.TagValuesAndChances;

            if (tagValuesAndChances.Count == 0)
            {
                warnings.Add("Tag generator has no values.");
                return;
            }

            foreach (var (tagValue, chance) in tagValuesAndChances)
            {
                if (chance == 0)
                    warnings.Add($"{tagValue} has a chance of 0 and is skipped.");
            }
        }

        #endregion

        #region Grant

        private static VisualElement CreateGrantSection(object[] items)
        {
            var generate = new InspectorButtonElement(items, nameof(ItemDef.GenerateItem))
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

            return TryWriteJson(() => items.Length switch
            {
                0 => string.Empty,
                1 => items[0].ToJson(),
                _ => JsonConvert.SerializeObject(items, Formatting.Indented)
            });
        }

        /// <summary>
        /// Wraps the items in the envelope the Steam Inventory Service expects when importing item definitions:
        /// an <c>appid</c> (from the Steam Settings) plus an <c>items</c> array.
        /// </summary>
        private string GetImportJson() =>
            _items.Length == 0 ? string.Empty : TryWriteJson(() => SteamItemsDB.ToImportJson(LiveItems));

        /// <summary>
        /// The JSON, or why it cannot be written: a definition the export stops on, e.g. over an empty exchange
        /// material, must still open here so it can be fixed. The warnings above say which entry it is.
        /// </summary>
        private static string TryWriteJson(Func<string> write)
        {
            try
            {
                return write();
            }
            catch (Exception exception) when (exception is InvalidOperationException or NullReferenceException)
            {
                return $"Cannot be written as JSON: {exception.Message}";
            }
        }

        #endregion
    }
}
