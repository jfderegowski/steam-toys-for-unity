using System;
using System.Collections.Generic;
using System.Linq;
using fefek5.Toys.Editor.Icons;
using fefek5.Toys.Editor.VisualElements;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem
{
    /// <summary>
    /// Draws the whole <see cref="ItemDef"/> inspector: the fields of the schema properties its type uses, each with
    /// its tooltip and framed with its warnings when it needs fixing, and the JSON preview / copy buttons. With several
    /// definitions selected, a field shows when all of them use it, and the warnings, the JSON and Generate Item cover
    /// all of them.
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

        private const string SCRIPT_PROPERTY = "m_Script";
        private const string SECTION_SAVE_KEY = "SteamToys.ItemDef.Section.";

        // Serialized fields of ItemDef and the definitions of this package.
        private const string ITEM_DEF_ID = "_itemDefId";
        private const string NAME = "_name";
        private const string DESCRIPTION = "_description";
        private const string TAGS = "_tags";
        private const string HIDDEN = "_hidden";
        private const string DISPLAY_TYPE = "_displayType";
        private const string ICON_URL = "_iconUrl";
        private const string ICON_URL_LARGE = "_iconUrlLarge";
        private const string NAME_COLOR = "_nameColor";
        private const string BACKGROUND_COLOR = "_backgroundColor";
        private const string CONTENT = "_bundle";
        private const string TAG_GENERATORS = "_tagGenerators";
        private const string PRICE = "_price";
        private const string PRICE_CATEGORY = "_priceCategory";
        private const string USE_BUNDLE_PRICE = "_useBundlePrice";
        private const string PURCHASE_BUNDLE_DISCOUNT = "_purchaseBundleDiscount";
        private const string STORE_TAGS = "_storeTags";
        private const string STORE_IMAGES = "_storeImages";
        private const string STORE_HIDDEN = "_storeHidden";
        private const string TRADABLE = "_tradable";
        private const string MARKETABLE = "_marketable";
        private const string GAME_ONLY = "_gameOnly";
        private const string AUTO_STACK = "_autoStack";
        private const string PURCHASE_LIMIT = "_purchaseLimit";
        private const string CONTAINER_CONTENTS_GENERATOR = "_containerContentsGenerator";
        private const string EXCHANGE = "_exchange";
        private const string PROMO = "_promo";
        private const string DROP_START_TIME = "_dropStartTime";
        private const string GRANTED_MANUALLY = "_grantedManually";
        private const string DROP_INTERVAL = "_dropInterval";
        private const string DROP_WINDOW = "_dropWindow";
        private const string DROP_MAX_PER_WINDOW = "_dropMaxPerWindow";
        private const string DROP_LIMIT = "_dropLimit";
        private const string TAG_NAME = "_tagName";
        private const string TAG_VALUES_AND_CHANCES = "_tagValuesAndChances";

        #endregion

        #region Fields

        private readonly ItemDef[] _items;
        private readonly SerializedObject _serializedObject;
        private readonly HashSet<string> _drawnFields = new() { SCRIPT_PROPERTY };
        private readonly Dictionary<string, FieldFrame> _frames = new();
        private readonly VisualElement _warnings;
        private readonly JsonPreviewElement _preview;
        private readonly List<(string Field, string Message)> _itemWarnings = new();

        private static readonly List<string> NoWarnings = new();

        #endregion

        public ItemDefElement(SerializedObject serializedObject)
        {
            _items = serializedObject.targetObjects.OfType<ItemDef>().ToArray();
            _serializedObject = serializedObject;

            AddFields();

            // The warnings of fields that are not drawn, e.g. of a field only some of the selected definitions use.
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
            this.TrackSerializedObjectValue(serializedObject, _ => {
                RefreshWarnings();
                _preview.SetJson(GetItemJson());
            });
        }

        #region Fields

        private void AddFields()
        {
            AddScriptField();

            var basic = AddSection("Basic Item Data", "section-basic-icon");
            AddField(basic, ITEM_DEF_ID, "Unique identifier for this item definition.");
            AddField(basic, NAME, "Display name of the item.");
            AddField(basic, DESCRIPTION, "Detailed description of the item.");

            var tags = AddSection("Tags", "section-tags-icon");
            AddField(tags, TAGS, "Tags of this definition; a generator copies them to the item it creates.");

            var visibility = AddSection("Visibility & Restrictions", "section-visibility-icon");
            AddField(visibility, HIDDEN, "Hides the definition from clients and makes it unpurchasable. It can still be granted.");

            var display = AddSection("Visual & Display", "section-display-icon");
            AddField(display, DISPLAY_TYPE, "Category name displayed in the inventory.");
            AddField(display, ICON_URL, "URL to the item's standard icon image.");
            AddField(display, ICON_URL_LARGE, "URL to the item's large icon image.");
            AddField(display, NAME_COLOR, "Color of the item's name in inventory.");
            AddField(display, BACKGROUND_COLOR, "Background color of the item in inventory.");

            var contents = AddSection("Contents", "section-contents-icon");
            AddField(contents, CONTENT, "What this creates: a bundle every entry with its quantity, a generator one entry picked by weight.");
            AddField(contents, TAG_GENERATORS, "Tag generators applied to every item this creates.");

            var tagGenerator = AddSection("Tag Generator", "section-tag-generator-icon");
            AddField(tagGenerator, TAG_NAME, "Steam name of the generated tag, e.g. rarity.");
            AddField(tagGenerator, TAG_VALUES_AND_CHANCES, "Steam values of the tag with their relative odds, e.g. legendary: 1.");

            var pricing = AddSection("Pricing", "section-pricing-icon");
            AddField(pricing, PRICE, "Price information for the item.");
            AddField(pricing, PRICE_CATEGORY, "Valve price tier in VLV units (100 is about $0.99). Replaces the price when set.");
            AddField(pricing, USE_BUNDLE_PRICE, "If true, uses the bundle's own price instead of the sum of its contents.");
            AddField(pricing, PURCHASE_BUNDLE_DISCOUNT, "Percentage discount on the summed price of the contents. Ignored with Use Bundle Price.");

            var store = AddSection("Item Store", "section-store-icon");
            AddField(store, STORE_TAGS, "Tags used to categorize and filter the item in the Item Store.");
            AddField(store, STORE_IMAGES, "Images shown on the item's Item Store detail page.");
            AddField(store, STORE_HIDDEN, "Whether this item is hidden from the Item Store.");

            var market = AddSection("Market & Trading", "section-market-icon");
            AddField(market, TRADABLE, "Whether this item can be traded between users.");
            AddField(market, MARKETABLE, "Whether this item can be sold on the Steam Community Market.");
            AddField(market, GAME_ONLY, "Hides the item from the Steam Backpack, for items consumed right away.");
            AddField(market, AUTO_STACK, "Whether grants are added to a single stack.");
            AddField(market, PURCHASE_LIMIT, "Blocks a purchase when the user already owns this many. 0 means no limit.");
            AddField(market, CONTAINER_CONTENTS_GENERATOR, "Generator whose possible drops are listed in this item's description.");

            var exchange = AddSection("Exchange System", "section-exchange-icon");
            AddField(exchange, EXCHANGE, "Recipes that consume materials to create this item.");

            var promo = AddSection("Promo", "section-promo-icon");
            AddField(promo, PROMO, "Rules that grant this item automatically.");
            AddField(promo, DROP_START_TIME, "Timestamp before which the manual promo does not grant this item.");
            AddField(promo, GRANTED_MANUALLY, "Granted only by AddPromoItem(s) with its explicit ID. Without it GrantPromoItems grants a manual promo too.");
            AddField(promo, DROP_INTERVAL, "With a manual promo: minutes before it grants this again, e.g. 10080 for once a week; 0 grants it only " +
                                           "once. On a playtime generator: minutes of playtime before a drop, 0 uses the application setting.");

            var drops = AddSection("Drop System", "section-drops-icon");
            AddField(drops, DROP_WINDOW, "Cool-down window in minutes. Drops are limited to the window only when set.");
            AddField(drops, DROP_MAX_PER_WINDOW, "Drops allowed within the drop window (1-10). 0 uses the application setting.");
            AddField(drops, DROP_LIMIT, "Maximum number of drops per user. Enforced only when set; 0 stops all future drops.");

            AddSubclassFields();

            this.Bind(_serializedObject);
        }

        private void AddScriptField()
        {
            var script = _serializedObject.FindProperty(SCRIPT_PROPERTY);

            if (script == null)
                return;

            var field = new PropertyField(script);

            field.SetEnabled(false);
            Add(field);
        }

        /// <summary>A section with its fields, hidden until one of them is added.</summary>
        private VisualElement AddSection(string title, string iconName)
        {
            EditorIconsDatabase.TryGetIcon<Texture2D>(iconName, out var icon);

            var section = new SectionElement(title, icon, SECTION_SAVE_KEY + title) {
                style = { display = DisplayStyle.None }
            };

            Add(section);

            return section;
        }

        /// <summary>
        /// Adds the field to the section, unless the definition does not have it or its type does not use it.
        /// </summary>
        private void AddField(VisualElement section, string fieldName, string tooltip)
        {
            _drawnFields.Add(fieldName);

            var property = _serializedObject.FindProperty(fieldName);

            if (property == null || !IsShown(fieldName))
                return;

            section.style.display = DisplayStyle.Flex;
            section.Add(CreateFrame(property, tooltip));
        }

        /// <summary>The fields a subclass of the game adds, drawn with its own attributes.</summary>
        private void AddSubclassFields()
        {
            var property = _serializedObject.GetIterator();

            if (!property.NextVisible(true))
                return;

            do
            {
                if (!_drawnFields.Contains(property.name) && IsShown(property.name))
                    Add(CreateFrame(property.Copy(), null));
            } while (property.NextVisible(false));
        }

        private FieldFrame CreateFrame(SerializedProperty property, string tooltip)
        {
            var field = new PropertyField(property);

            if (tooltip != null)
            {
                // PropertyField builds its label once bound: the tooltip goes on it, so it does not cover the whole
                // field, e.g. every entry of a list.
                field.RegisterCallback<GeometryChangedEvent>(_ => {
                    var label = field.Q<Label>();

                    if (label != null)
                        label.tooltip = tooltip;
                });
            }

            var frame = new FieldFrame(field);

            _frames[property.name] = frame;

            return frame;
        }

        private bool IsShown(string fieldName) =>
            _items.All(item => !item || item.SchemaSupportsField(fieldName));

        /// <summary>A field, framed with its warnings when it has any.</summary>
        private sealed class FieldFrame : VisualElement
        {
            private const float BORDER = 1;
            private const float PADDING = 2;

            private static readonly Color BorderColor = new(0.95f, 0.73f, 0.1f);
            private static readonly Color BackgroundColor = new(0.95f, 0.73f, 0.1f, 0.06f);

            private readonly VisualElement _messages = new();

            public FieldFrame(VisualElement field)
            {
                style.borderTopColor = BorderColor;
                style.borderRightColor = BorderColor;
                style.borderBottomColor = BorderColor;
                style.borderLeftColor = BorderColor;

                style.borderTopLeftRadius = 3;
                style.borderTopRightRadius = 3;
                style.borderBottomLeftRadius = 3;
                style.borderBottomRightRadius = 3;

                Add(field);
                Add(_messages);
            }

            public void SetWarnings(IReadOnlyCollection<string> messages)
            {
                _messages.Clear();

                foreach (var message in messages)
                    _messages.Add(new HelpBox(message, HelpBoxMessageType.Warning));

                var framed = messages.Count > 0;
                var border = framed ? BORDER : 0;
                var padding = framed ? PADDING : 0;

                style.borderTopWidth = border;
                style.borderRightWidth = border;
                style.borderBottomWidth = border;
                style.borderLeftWidth = border;

                style.paddingTop = padding;
                style.paddingRight = padding;
                style.paddingBottom = padding;
                style.paddingLeft = padding;

                // The frame grows outwards, so the field keeps its place.
                style.marginLeft = -(border + padding);
                style.marginRight = -(border + padding);
                style.marginTop = framed ? 2 : 0;
                style.marginBottom = framed ? 2 : 0;

                style.backgroundColor = framed ? BackgroundColor : Color.clear;
            }
        }

        #endregion

        #region Warnings

        private void RefreshWarnings()
        {
            _warnings.Clear();

            var named = _items.Length > 1;
            var byField = new Dictionary<string, List<string>>();

            foreach (var item in _items)
            {
                if (!item)
                    continue;

                _itemWarnings.Clear();
                CollectWarnings(item, _itemWarnings);

                foreach (var (field, message) in _itemWarnings)
                {
                    var text = named ? $"{item.name}: {message}" : message;

                    if (!_frames.ContainsKey(field))
                        _warnings.Add(new HelpBox(text, HelpBoxMessageType.Warning));
                    else if (byField.TryGetValue(field, out var messages))
                        messages.Add(text);
                    else
                        byField[field] = new List<string> { text };
                }
            }

            foreach (var (field, frame) in _frames)
                frame.SetWarnings(byField.TryGetValue(field, out var messages) ? messages : NoWarnings);
        }

        /// <summary>
        /// Adds a message for every setting of <paramref name="item"/> Steam would reject or silently ignore, with the
        /// serialized field that needs fixing.
        /// </summary>
        internal static void CollectWarnings(ItemDef item, List<(string Field, string Message)> warnings)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
                warnings.Add((NAME, "Name is empty: Steam rejects the definition, and an upload stops there."));

            if (item.SchemaSupportsContent())
                CollectContentWarnings(item, warnings);

            if (item.SchemaSupportsPrice())
                CollectPriceWarnings(item, warnings);

            if (item.SchemaSupportsPromo())
                CollectPromoWarnings(item, warnings);

            if (item.SchemaSupportsExchange())
                CollectExchangeWarnings(item, warnings);

            if (item.SchemaSupportsPurchaseLimit() && item.PurchaseLimit > 0 && (item.Tradable || item.Marketable))
                warnings.Add((PURCHASE_LIMIT,
                    "Purchase Limit does not block repurchasing tradable or marketable items."));

            if (item.SchemaSupportsDropMaxPerWindow() && item.DropMaxPerWindow > MAX_DROPS_PER_WINDOW)
                warnings.Add((DROP_MAX_PER_WINDOW, $"Drop Max Per Window cannot exceed {MAX_DROPS_PER_WINDOW}."));

            if (item.SchemaSupportsDropWindow() && item.DropWindow.hasValue && item.DropWindow.value == 0)
                warnings.Add((DROP_WINDOW, "Drop Window is enabled with 0 minutes, so it is not exported."));

            if (item is TagGeneratorDef tagGenerator)
                CollectTagGeneratorWarnings(tagGenerator, warnings);
        }

        private static void CollectContentWarnings(ItemDef item, List<(string Field, string Message)> warnings)
        {
            var content = item.Content;
            var amountName = item.Type == ItemType.Bundle ? "quantity" : "weight";

            if (content == null || content.Count == 0)
            {
                warnings.Add((CONTENT, item.Type == ItemType.Bundle
                    ? "Bundle has no contents: Steam rejects it, and an upload stops there."
                    : $"{item.Type} has no contents, so it grants nothing."));
                return;
            }

            for (var i = 0; i < content.Count; i++)
            {
                var element = content[i];

                if (element is not { HasItem: true })
                    warnings.Add((CONTENT, $"Contents entry {i} has no item and is skipped."));
                else if (element.Item == item)
                    warnings.Add((CONTENT, $"Contents entry {i} references this {item.Type} itself."));

                if (element != null && element.Quantity == 0)
                    warnings.Add((CONTENT, $"Contents entry {i} has a {amountName} of 0."));
            }
        }

        /// <summary>
        /// The recipe entries the export cannot write. It stops on them rather than leaving them out, since a recipe
        /// without one of its materials would be cheaper than meant.
        /// </summary>
        private static void CollectExchangeWarnings(ItemDef item, List<(string Field, string Message)> warnings)
        {
            var exchange = item.Exchange;

            if (exchange == null)
                return;

            for (var i = 0; i < exchange.Count; i++)
            {
                var recipe = exchange[i];

                if (recipe == null)
                {
                    warnings.Add((EXCHANGE, $"Exchange recipe {i} is empty, so the definition cannot be exported."));
                    continue;
                }

                for (var j = 0; j < recipe.Count; j++)
                {
                    if (recipe[j] == null)
                    {
                        warnings.Add((EXCHANGE,
                            $"Exchange recipe {i}, material {j} is empty, so the definition cannot be exported."));
                        continue;
                    }

                    try
                    {
                        recipe[j].ToSteamString();
                    }
                    catch (InvalidOperationException exception)
                    {
                        warnings.Add((EXCHANGE,
                            $"Exchange recipe {i}, material {j}: {exception.Message} The definition cannot be exported until it is set."));
                    }
                }
            }
        }

        private static void CollectPriceWarnings(ItemDef item, List<(string Field, string Message)> warnings)
        {
            // On the price, which is the one left out.
            if (item.PriceCategory.hasValue && !string.IsNullOrEmpty(item.Price?.ToSteamString()))
                warnings.Add((PRICE, "Price and Price Category are both set: only the price category is exported."));

            if (item.Price != null && item.Price.RegularPrices.Count == 0 && item.Price.Promotions.Count > 0)
                warnings.Add((PRICE, "Price has sales but no regular price, which Steam requires first."));

            if (!item.SchemaSupportsPurchaseBundleDiscount())
                return;

            if (item.PurchaseBundleDiscount > MAX_DISCOUNT)
                warnings.Add((PURCHASE_BUNDLE_DISCOUNT,
                    $"Purchase Bundle Discount is a percentage and cannot exceed {MAX_DISCOUNT}."));

            if (item.PurchaseBundleDiscount > 0 && item.UseBundlePrice)
                warnings.Add((PURCHASE_BUNDLE_DISCOUNT,
                    "Purchase Bundle Discount is ignored when Use Bundle Price is on."));
        }

        private static void CollectPromoWarnings(ItemDef item, List<(string Field, string Message)> warnings)
        {
            var manual = item.Promo != null && item.Promo.Any(rule => rule is ManualPromoRule);

            if (item.DropStartTime is { HasValue: true } && !manual)
                warnings.Add((DROP_START_TIME,
                    "Drop Start Time only applies to a manual promo; add a Manual promo rule or clear it."));

            // On a playtime generator the interval also paces its drops.
            if (item.DropInterval > 0 && !manual && item.Type != ItemType.PlaytimeGenerator)
                warnings.Add((DROP_INTERVAL,
                    "Drop Interval only applies to a manual promo; add a Manual promo rule or clear it."));

            if (item.Promo == null)
                return;

            for (var i = 0; i < item.Promo.Count; i++)
            {
                if (string.IsNullOrEmpty(item.Promo[i]?.ToSteamString()))
                    warnings.Add((PROMO, $"Promo rule {i} is incomplete and is left out."));
            }
        }

        private static void CollectTagGeneratorWarnings(TagGeneratorDef generatorDef,
            List<(string Field, string Message)> warnings)
        {
            var tagValuesAndChances = generatorDef.TagValuesAndChances;

            if (tagValuesAndChances.Count == 0)
            {
                warnings.Add((TAG_VALUES_AND_CHANCES, "Tag generator has no values."));
                return;
            }

            foreach (var (tagValue, chance) in tagValuesAndChances)
            {
                if (chance == 0)
                    warnings.Add((TAG_VALUES_AND_CHANCES, $"{tagValue} has a chance of 0 and is skipped."));
            }
        }

        #endregion

        #region Grant

        private static VisualElement CreateGrantSection(object[] items)
        {
            var generate = new InspectorButtonElement(items, nameof(ItemDef.GenerateItem)) {
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

            return TryWriteJson(() => items.Length switch {
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
        /// material, must still open here so it can be fixed. The warnings beside its field say which entry it is.
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