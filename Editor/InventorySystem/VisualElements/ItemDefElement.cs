using System;
using System.Collections.Generic;
using System.Linq;
using fefek5.Toys.Editor.Icons;
using fefek5.Toys.Editor.VisualElements;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem;
using SteamToys.Runtime.InventorySystem.Converters;
using SteamToys.Runtime.InventorySystem.Converters.ExchangeVariable;
using SteamToys.Runtime.InventorySystem.Converters.Price;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem
{
    /// <summary>
    /// Draws the whole <see cref="ItemDef"/> inspector: its warnings listed at the top, the fields of the schema
    /// properties its type uses, each with its tooltip and framed with its warnings when it needs fixing, and the JSON
    /// preview / copy buttons. With several
    /// definitions selected, a field shows when all of them use it, and the warnings, the JSON and Generate Item cover
    /// all of them.
    /// </summary>
    public class ItemDefElement : VisualElement
    {
        #region Constants

        private const uint MAX_DISCOUNT = 100;
        private const uint MAX_DROPS_PER_WINDOW = 10;

        private const string PREVIEW_TITLE = "Item JSON";

        private const string FIX_BUTTON_TEXT = "Fix";
        private const string FIX_BUTTON_TOOLTIP = "Changes the definition so this warning goes away. Undo reverts it.";
        private const string COPY_ITEM_BUTTON_TEXT = "Copy JSON";

        private const string COPY_ITEM_BUTTON_TOOLTIP =
            "Copy the item definition on its own, or an array of the selected definitions.";

        private const string COPY_IMPORT_BUTTON_TEXT = "Copy JSON + AppID";

        private const string COPY_IMPORT_BUTTON_TOOLTIP =
            "Copy the selected items wrapped in an appid / items envelope, ready to import into the Steam Inventory Service.";

        private const string DOWNLOAD_ICONS_BUTTON_TEXT = "Download Icons";

        private const string DOWNLOAD_ICONS_BUTTON_TOOLTIP =
            "Downloads the icons of the selected definitions from their URLs into " + ItemIconDownloader.ROOT +
            ", in folders following each URL, and sets Icon and Icon Large to them.";

        // The shapes Steam asks for: 200x200 for the icon, 350x200 for the large one, until a sprite is set.
        private const float ICON_ASPECT = 1;
        private const float ICON_LARGE_ASPECT = 350f / 200f;
        private const float ICON_GAP = 6;

        private const string HELP_BOX_LABEL_CLASS = "unity-help-box__label";
        private const string HELP_BOX_BOTTOM_CLASS = "unity-help-box__bottom-container";

        private const string SCRIPT_PROPERTY = "m_Script";
        private const string SECTION_SAVE_KEY = "SteamToys.ItemDef.Section.";

        // Serialized fields of ItemDef and the definitions of this package.
        internal const string ITEM_DEF_ID = "_itemDefId";
        internal const string NAME = "_name";
        internal const string DESCRIPTION = "_description";
        internal const string TAGS = "_tags";
        internal const string HIDDEN = "_hidden";
        internal const string DISPLAY_TYPE = "_displayType";
        internal const string ICON_URL = "_iconUrl";
        internal const string ICON_URL_LARGE = "_iconUrlLarge";
        internal const string ICON = "_icon";
        internal const string ICON_LARGE = "_iconLarge";
        internal const string NAME_COLOR = "_nameColor";
        internal const string BACKGROUND_COLOR = "_backgroundColor";
        internal const string CONTENT = "_bundle";
        internal const string TAG_GENERATORS = "_tagGenerators";
        internal const string PRICE = "_price";
        internal const string PRICE_CATEGORY = "_priceCategory";
        internal const string USE_BUNDLE_PRICE = "_useBundlePrice";
        internal const string PURCHASE_BUNDLE_DISCOUNT = "_purchaseBundleDiscount";
        internal const string STORE_TAGS = "_storeTags";
        internal const string STORE_IMAGES = "_storeImages";
        internal const string STORE_HIDDEN = "_storeHidden";
        internal const string TRADABLE = "_tradable";
        internal const string MARKETABLE = "_marketable";
        internal const string GAME_ONLY = "_gameOnly";
        internal const string AUTO_STACK = "_autoStack";
        internal const string PURCHASE_LIMIT = "_purchaseLimit";
        internal const string CONTAINER_CONTENTS_GENERATOR = "_containerContentsGenerator";
        internal const string EXCHANGE = "_exchange";
        internal const string PROMO = "_promo";
        internal const string DROP_START_TIME = "_dropStartTime";
        internal const string GRANTED_MANUALLY = "_grantedManually";
        internal const string DROP_INTERVAL = "_dropInterval";
        internal const string DROP_WINDOW = "_dropWindow";
        internal const string DROP_MAX_PER_WINDOW = "_dropMaxPerWindow";
        internal const string DROP_LIMIT = "_dropLimit";
        private const string TAG_NAME = "_tagName";
        private const string TAG_VALUES_AND_CHANCES = "_tagValuesAndChances";

        // How a SerializableDictionary serializes: a list of entries, each a key and a value.
        private const string TAG_VALUES_ENTRIES_PATH = ".entries";
        private const string TAG_VALUE_KEY = "key";
        private const string TAG_VALUE_CHANCE = "value";

        // Serialized paths inside those fields, for the fixes of the warnings.
        private const string HAS_VALUE_PATH = ".hasValue";
        private const string DATE_HAS_VALUE_PATH = "._hasValue";
        private const string CONTENT_ELEMENTS_PATH = CONTENT + "._elements";
        private const string EXCHANGE_RECIPES_PATH = EXCHANGE + "._recipes";
        private const string RECIPE_MATERIALS_PATH = "._materials";
        private const string PROMO_RULES_PATH = PROMO + "._rules";
        private const string REGULAR_PRICES = "_regularPrices";
        private const string PROMOTIONS = "_promotions";

        #endregion

        #region Fields

        private readonly ItemDef[] _items;
        private readonly SerializedObject _serializedObject;
        private readonly HashSet<string> _drawnFields = new() { SCRIPT_PROPERTY };
        private readonly Dictionary<string, FieldFrame> _frames = new();
        private readonly VisualElement _warnings;
        private readonly JsonPreviewElement _preview;
        private readonly List<ItemDefWarnign> _itemWarnings = new();
        private readonly Func<ItemDef, IEnumerable<ItemDefWarnign>> _getAdditionalWarnings;

        private static readonly List<ItemDefWarnign> NoWarnings = new();
        private static readonly Color IconPreviewBackground = new(0, 0, 0, 0.15f);

        #endregion

        /// <param name="serializedObject">The selected definitions.</param>
        /// <param name="getAdditionalWarnings">
        /// Warnings of its own, after the built-in ones, which are always collected. The field of each is the name
        /// of a serialized field, framed with it when the inspector draws that field, and one with a fix gets a Fix
        /// button. Called again on every change.
        /// </param>
        public ItemDefElement(SerializedObject serializedObject,
                              Func<ItemDef, IEnumerable<ItemDefWarnign>> getAdditionalWarnings = null)
        {
            _items = serializedObject.targetObjects.OfType<ItemDef>().ToArray();
            _serializedObject = serializedObject;
            _getAdditionalWarnings = getAdditionalWarnings;

            // Every warning, one under another at the top, also when its field is drawn framed with it below.
            _warnings = new VisualElement();
            Add(_warnings);

            AddFields();
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

            // The tags draw their own section.
            AddField(this, TAGS, "Tags of this definition; a generator copies them to the item it creates.");

            var visibility = AddSection("Visibility & Restrictions", "section-visibility-icon");
            AddField(visibility, HIDDEN, "Hides the definition from clients and makes it unpurchasable. It can still be granted.");

            var display = AddSection("Visual & Display", "section-display-icon");
            AddField(display, DISPLAY_TYPE, "Category name displayed in the inventory.");
            AddField(display, ICON_URL, "URL to the item's standard icon image.");
            AddField(display, ICON_URL_LARGE, "URL to the item's large icon image.");
            AddField(display, NAME_COLOR, "Color of the item's name in inventory.");
            AddField(display, BACKGROUND_COLOR, "Background color of the item in inventory.");
            AddIconFields(display);

            if (IsShown(ICON_URL) || IsShown(ICON_URL_LARGE))
                display.Add(CreateDownloadIconsButton());

            var contents = AddSection("Contents", "section-contents-icon");
            AddField(contents, CONTENT, "What this creates: a bundle every entry with its quantity, a generator one entry picked by weight.");
            AddField(contents, TAG_GENERATORS, "Tag generators applied to every item this creates.");

            var tagGenerator = AddSection("Tag Generator", "section-tag-generator-icon");
            AddField(tagGenerator, TAG_NAME, "Steam name of the generated tag, e.g. rarity.");
            AddField(tagGenerator, TAG_VALUES_AND_CHANCES, "Steam values of the tag with their relative odds, e.g. legendary: 1.");
            AddTagValuesTreemap(tagGenerator);

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

            // The exchange draws its own section.
            AddField(this, EXCHANGE, "Recipes that consume materials to create this item.");

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

        /// <summary>The odds of the tag values as a treemap, under their field, editable like a generator's.</summary>
        private void AddTagValuesTreemap(VisualElement section)
        {
            var entries = _serializedObject.FindProperty(TAG_VALUES_AND_CHANCES + TAG_VALUES_ENTRIES_PATH);

            if (entries == null || !IsShown(TAG_VALUES_AND_CHANCES))
                return;

            var treemap = new ContentTreemapElement(entries, TAG_VALUE_KEY, TAG_VALUE_CHANCE);

            section.Add(treemap);
            treemap.TrackSerializedObjectValue(_serializedObject, _ => treemap.Refresh());
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

            var frame = new FieldFrame(this, field);

            _frames[property.name] = frame;

            return frame;
        }

        /// <summary>
        /// Icon and Icon Large side by side across the whole section, each a preview over its sprite field, framed with
        /// its warnings like any field. Their widths follow the shapes of the icons, so both previews are as tall.
        /// </summary>
        private void AddIconFields(VisualElement section)
        {
            var row = new VisualElement {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Stretch, marginTop = 2, marginBottom = 2 }
            };

            AddIconField(row, ICON, ICON_ASPECT,
                "The standard icon downloaded from Icon Url. Kept in the project only, never exported.");
            AddIconField(row, ICON_LARGE, ICON_LARGE_ASPECT,
                "The large icon downloaded from Icon Url Large. Kept in the project only, never exported.");

            if (row.childCount == 0)
                return;

            // The gap only between the two.
            for (var i = 0; i < row.childCount - 1; i++)
                row[i].style.marginRight = ICON_GAP;

            section.style.display = DisplayStyle.Flex;
            section.Add(row);
        }

        private void AddIconField(VisualElement row, string fieldName, float defaultAspect, string tooltip)
        {
            _drawnFields.Add(fieldName);

            var property = _serializedObject.FindProperty(fieldName);

            if (property == null || !IsShown(fieldName))
                return;

            var aspect = defaultAspect;
            var column = new VisualElement { tooltip = tooltip };

            column.Add(new Label(property.displayName) { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 2 } });

            var preview = new Image {
                scaleMode = ScaleMode.ScaleToFit,
                style = {
                    backgroundColor = IconPreviewBackground,
                    borderTopLeftRadius = 3, borderTopRightRadius = 3, borderBottomLeftRadius = 3, borderBottomRightRadius = 3,
                    justifyContent = Justify.Center
                }
            };

            var empty = new Label("No icon") { style = { unityTextAlign = TextAnchor.MiddleCenter, opacity = 0.5f } };

            preview.Add(empty);
            column.Add(preview);

            // Bound with the rest by Bind; it takes a sprite dropped on it, and shows the one Download Icons sets.
            var field = new ObjectField { objectType = typeof(Sprite), allowSceneObjects = false, bindingPath = fieldName };

            field.style.marginLeft = 0;
            field.style.marginRight = 0;
            field.style.marginTop = 2;
            column.Add(field);

            var frame = new FieldFrame(this, column) { style = { flexGrow = aspect, flexShrink = 1, flexBasis = 0, minWidth = 0 } };

            // As tall as the width allows for the shape of the icon.
            preview.RegisterCallback<GeometryChangedEvent>(change => {
                var height = change.newRect.width / aspect;

                if (Mathf.Abs(change.newRect.height - height) > 0.5f)
                    preview.style.height = height;
            });

            preview.RegisterCallback<ClickEvent>(_ => {
                if (field.value)
                    EditorGUIUtility.PingObject(field.value);
            });

            field.RegisterValueChangedCallback(change => {
                var sprite = change.newValue as Sprite;

                preview.sprite = sprite;
                empty.style.display = sprite ? DisplayStyle.None : DisplayStyle.Flex;

                aspect = sprite && sprite.rect.height > 0 ? sprite.rect.width / sprite.rect.height : defaultAspect;
                frame.style.flexGrow = aspect;

                if (preview.resolvedStyle.width > 0)
                    preview.style.height = preview.resolvedStyle.width / aspect;
            });

            _frames[fieldName] = frame;
            row.Add(frame);
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

            private readonly ItemDefElement _owner;
            // Takes the height left when the frame is stretched next to a taller one.
            private readonly VisualElement _messages = new() { style = { flexGrow = 1 } };

            public FieldFrame(ItemDefElement owner, VisualElement field)
            {
                _owner = owner;

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

            public void SetWarnings(IReadOnlyCollection<ItemDefWarnign> warnings, bool named)
            {
                _messages.Clear();

                foreach (var warning in warnings)
                    _messages.Add(_owner.CreateWarningBox(warning, named));

                var framed = warnings.Count > 0;
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

                // Kept inside the section: growing outwards clipped the frame's right edge.
                style.marginTop = framed ? 2 : 0;
                style.marginBottom = framed ? 2 : 0;

                style.backgroundColor = framed ? BackgroundColor : Color.clear;
            }
        }

        #endregion

        #region Icons

        private VisualElement CreateDownloadIconsButton()
        {
            InspectorButtonElement button = null;

            button = new InspectorButtonElement(Download, DOWNLOAD_ICONS_BUTTON_TEXT) {
                tooltip = DOWNLOAD_ICONS_BUTTON_TOOLTIP,
                style = { marginLeft = 0, marginRight = 0, marginTop = 4 }
            };

            return button;

            async void Download()
            {
                button.SetEnabled(false);

                try
                {
                    await ItemIconDownloader.DownloadAsync(LiveItems.ToArray());
                }
                finally
                {
                    button.SetEnabled(true);
                    _serializedObject.Update();
                }
            }
        }

        #endregion

        #region Warnings

        private void RefreshWarnings()
        {
            _warnings.Clear();

            var named = _items.Length > 1;
            var byField = new Dictionary<string, List<ItemDefWarnign>>();

            foreach (var item in _items)
            {
                if (!item)
                    continue;

                _itemWarnings.Clear();
                _itemWarnings.AddRange(GetWarnings(item));

                if (_getAdditionalWarnings != null)
                    _itemWarnings.AddRange(_getAdditionalWarnings(item));

                foreach (var warning in _itemWarnings)
                {
                    // Repeated at the top, so a warning still shows while its section is folded.
                    _warnings.Add(CreateWarningBox(warning, named));

                    if (warning.Field == null || !_frames.ContainsKey(warning.Field))
                        continue;

                    if (byField.TryGetValue(warning.Field, out var fieldWarnings))
                        fieldWarnings.Add(warning);
                    else
                        byField[warning.Field] = new List<ItemDefWarnign> { warning };
                }
            }

            foreach (var (field, frame) in _frames)
                frame.SetWarnings(byField.TryGetValue(field, out var fieldWarnings) ? fieldWarnings : NoWarnings, named);
        }

        /// <summary>
        /// The message of <paramref name="warning"/>, after the name of its definition when <paramref name="named"/>,
        /// with a Fix button when it has a fix.
        /// </summary>
        private HelpBox CreateWarningBox(ItemDefWarnign warning, bool named)
        {
            var text = named && warning.ItemDef ? $"{warning.ItemDef.name}: {warning.Message}" : warning.Message;
            // Grows to fill its frame, so the warnings of fields side by side are as tall; the text stays at the top
            // and the Fix button at the bottom.
            var box = new HelpBox(text, HelpBoxMessageType.Warning) { style = { flexGrow = 1 } };

            FitTextHeight(box.Q<Label>(className: HELP_BOX_LABEL_CLASS));

            if (!warning.CanFix)
                return box;

            var bottom = box.Q(className: HELP_BOX_BOTTOM_CLASS) ?? box;

            bottom.style.flexDirection = FlexDirection.Column;
            bottom.style.alignItems = Align.Stretch;
            bottom.style.alignSelf = Align.Stretch;

            bottom.Add(new InspectorButtonElement(() => ApplyFix(warning), FIX_BUTTON_TEXT) {
                tooltip = FIX_BUTTON_TOOLTIP,
                style = { marginLeft = 0, marginRight = 0, marginTop = 2, marginBottom = 2, alignSelf = Align.Stretch }
            });

            return box;
        }

        /// <summary>
        /// Keeps <paramref name="label"/> as tall as its text at the width it ends up with. Unity measures wrapped
        /// text at the width before it is rounded to whole pixels, so a line that needs exactly the final width is
        /// measured as wrapping once more and leaves an empty line under the text.
        /// </summary>
        private static void FitTextHeight(Label label)
        {
            if (label == null)
                return;

            label.RegisterCallback<GeometryChangedEvent>(change => {
                var height = label.MeasureTextSize(label.text, change.newRect.width, MeasureMode.Exactly, 0,
                    MeasureMode.Undefined).y;

                if (!float.IsNaN(height) && Mathf.Abs(change.newRect.height - height) > 0.5f)
                    label.style.height = height;
            });
        }

        /// <summary>Fixes the definition, then shows the fields, warnings and JSON as they are now.</summary>
        private void ApplyFix(ItemDefWarnign warning)
        {
            warning.Fix();

            _serializedObject.Update();
            RefreshWarnings();
            _preview.SetJson(GetItemJson());
        }

        /// <summary>
        /// Every serialized field the built-in checks look at, with the method returning its warnings: the settings Steam
        /// would reject or silently ignore.
        /// </summary>
        private static readonly (string Field, Func<ItemDef, IEnumerable<Warning>> Get)[] FieldWarnings = {
            (NAME, GetNameWarnings),
            (CONTENT, GetContentWarnings),
            (PRICE, GetPriceWarnings),
            (ICON, item => GetIconWarnings(item, item.IconUrl, item.Icon, "Icon")),
            (ICON_LARGE, item => GetIconWarnings(item, item.IconUrlLarge, item.IconLarge, "Icon Large")),
            (PURCHASE_BUNDLE_DISCOUNT, GetPurchaseBundleDiscountWarnings),
            (PURCHASE_LIMIT, GetPurchaseLimitWarnings),
            (EXCHANGE, GetExchangeWarnings),
            (PROMO, GetPromoWarnings),
            (DROP_START_TIME, GetDropStartTimeWarnings),
            (DROP_INTERVAL, GetDropIntervalWarnings),
            (DROP_WINDOW, GetDropWindowWarnings),
            (DROP_MAX_PER_WINDOW, GetDropMaxPerWindowWarnings),
            (TAG_VALUES_AND_CHANCES, GetTagValuesAndChancesWarnings)
        };

        /// <summary>The built-in warnings of <paramref name="item"/>, each with the serialized field that needs fixing.</summary>
        internal static IEnumerable<ItemDefWarnign> GetWarnings(ItemDef item) => GetFieldWarnings(item, FieldWarnings);

        /// <summary>
        /// The warnings of each of <paramref name="fieldWarnings"/> the type of <paramref name="item"/> uses, about
        /// <paramref name="item"/> and that field.
        /// </summary>
        internal static IEnumerable<ItemDefWarnign> GetFieldWarnings(ItemDef item,
            IEnumerable<(string Field, Func<ItemDef, IEnumerable<Warning>> Get)> fieldWarnings)
        {
            foreach (var (field, get) in fieldWarnings)
            {
                if (!item.SchemaSupportsField(field))
                    continue;

                foreach (var warning in get(item))
                    yield return new ItemDefWarnign(item, field, warning);
            }
        }

        private static IEnumerable<Warning> GetNameWarnings(ItemDef item)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
                yield return new Warning("Name is empty: Steam rejects the definition, and an upload stops there.");
        }

        /// <summary>A local icon that is missing, or is not the file of its URL.</summary>
        private static IEnumerable<Warning> GetIconWarnings(ItemDef item, Url url, Sprite icon, string label)
        {
            var hasUrl = url is not null && url.IsValid();

            if (!hasUrl)
            {
                if (icon)
                    yield return new Warning($"{label} is set, but its URL is empty, so Steam shows no icon there.");

                yield break;
            }

            Action download = () => _ = ItemIconDownloader.DownloadAsync(new[] { item });

            if (!icon)
                yield return new Warning($"{label} is not downloaded yet; Fix downloads it from its URL.", download);
            else if (!ItemIconDownloader.IsDownloadOf(icon, url))
                yield return new Warning($"{label} is not the file of its URL, so it may differ from what Steam shows; " +
                                         "Fix downloads it from the URL.", download);
        }

        private static IEnumerable<Warning> GetContentWarnings(ItemDef item)
        {
            var content = item.Content;
            var amountName = item.Type == ItemType.Bundle ? "quantity" : "weight";

            if (content == null || content.Count == 0)
            {
                yield return new Warning(item.Type == ItemType.Bundle
                    ? "Bundle has no contents: Steam rejects it, and an upload stops there."
                    : $"{item.Type} has no contents, so it grants nothing.");
                yield break;
            }

            for (var i = 0; i < content.Count; i++)
            {
                var element = content[i];

                if (element is not { HasItem: true })
                    yield return new Warning($"Contents entry {i} has no item and is skipped.",
                        RemoveElement(item, CONTENT_ELEMENTS_PATH, i));
                else if (element.Item == item)
                    yield return new Warning($"Contents entry {i} references this {item.Type} itself.",
                        RemoveElement(item, CONTENT_ELEMENTS_PATH, i));

                if (element != null && element.Quantity == 0)
                    yield return new Warning($"Contents entry {i} has a {amountName} of 0.");
            }
        }

        private static IEnumerable<Warning> GetPriceWarnings(ItemDef item)
        {
            // On the price, which is the one left out.
            if (item.PriceCategory.hasValue && !string.IsNullOrEmpty(PriceConverter.ToString(item.Price)))
                yield return new Warning("Price and Price Category are both set: only the price category is exported.",
                    FixProperty(item, PRICE, property => {
                        property.FindPropertyRelative(REGULAR_PRICES).ClearArray();
                        property.FindPropertyRelative(PROMOTIONS).ClearArray();
                    }));

            if (item.Price != null && item.Price.RegularPrices.Count == 0 && item.Price.Promotions.Count > 0)
                yield return new Warning("Price has sales but no regular price, which Steam requires first.");

            for (var i = 0; i < (item.Price?.Promotions.Count ?? 0); i++)
            {
                var promotion = item.Price.Promotions[i];

                if (string.IsNullOrEmpty(PromotionConverter.ToString(promotion)))
                    yield return new Warning($"Sale {i} needs a start, an end and a price; it is left out.",
                        RemoveElement(item, PRICE + "." + PROMOTIONS, i));
                else if (promotion.EndTime.CompareTo(promotion.StartTime) <= 0)
                    yield return new Warning($"Sale {i} ends before it starts.");
            }
        }

        private static IEnumerable<Warning> GetPurchaseBundleDiscountWarnings(ItemDef item)
        {
            if (item.PurchaseBundleDiscount > MAX_DISCOUNT)
                yield return new Warning($"Purchase Bundle Discount is a percentage and cannot exceed {MAX_DISCOUNT}.",
                    FixProperty(item, PURCHASE_BUNDLE_DISCOUNT, property => property.uintValue = MAX_DISCOUNT));

            if (item.PurchaseBundleDiscount > 0 && item.UseBundlePrice)
                yield return new Warning("Purchase Bundle Discount is ignored when Use Bundle Price is on.",
                    FixProperty(item, PURCHASE_BUNDLE_DISCOUNT, property => property.uintValue = 0));
        }

        private static IEnumerable<Warning> GetPurchaseLimitWarnings(ItemDef item)
        {
            if (item.PurchaseLimit > 0 && (item.Tradable || item.Marketable))
                yield return new Warning("Purchase Limit does not block repurchasing tradable or marketable items.");
        }

        /// <summary>
        /// The recipe entries the export cannot write. It stops on them rather than leaving them out, since a recipe
        /// without one of its materials would be cheaper than meant.
        /// </summary>
        private static IEnumerable<Warning> GetExchangeWarnings(ItemDef item)
        {
            var exchange = item.Exchange;

            if (exchange == null)
                yield break;

            for (var i = 0; i < exchange.Count; i++)
            {
                var recipe = exchange[i];

                if (recipe == null)
                {
                    yield return new Warning($"Exchange recipe {i} is empty, so the definition cannot be exported.",
                        RemoveElement(item, EXCHANGE_RECIPES_PATH, i));
                    continue;
                }

                for (var j = 0; j < recipe.Count; j++)
                {
                    if (recipe[j] == null)
                    {
                        yield return new Warning(
                            $"Exchange recipe {i}, material {j} is empty, so the definition cannot be exported.",
                            RemoveElement(item, $"{EXCHANGE_RECIPES_PATH}.Array.data[{i}]{RECIPE_MATERIALS_PATH}", j));
                        continue;
                    }

                    var problem = GetMaterialProblem(recipe[j]);

                    if (problem != null)
                        yield return new Warning(
                            $"Exchange recipe {i}, material {j}: {problem} The definition cannot be exported until it is set.");
                }
            }
        }

        /// <summary>Why the export cannot write <paramref name="material"/>; null when it can.</summary>
        private static string GetMaterialProblem(SteamToys.Runtime.InventorySystem.ExchangeVariable.Material material)
        {
            // An iterator cannot yield inside a try with a catch, so the check is in a method of its own.
            try
            {
                MaterialConverter.ToString(material);

                return null;
            }
            catch (InvalidOperationException exception)
            {
                return exception.Message;
            }
        }

        private static IEnumerable<Warning> GetPromoWarnings(ItemDef item)
        {
            if (item.Promo == null)
                yield break;

            for (var i = 0; i < item.Promo.Count; i++)
            {
                if (string.IsNullOrEmpty(PromoRuleConverter.ToString(item.Promo[i])))
                    yield return new Warning($"Promo rule {i} is incomplete and is left out.",
                        RemoveElement(item, PROMO_RULES_PATH, i));
            }
        }

        private static IEnumerable<Warning> GetDropStartTimeWarnings(ItemDef item)
        {
            if (item.DropStartTime is { HasValue: true } && !HasManualPromo(item))
                yield return new Warning("Drop Start Time only applies to a manual promo; add a Manual promo rule or clear it.",
                    FixProperty(item, DROP_START_TIME + DATE_HAS_VALUE_PATH, property => property.boolValue = false));
        }

        private static IEnumerable<Warning> GetDropIntervalWarnings(ItemDef item)
        {
            // On a playtime generator the interval also paces its drops.
            if (item.DropInterval > 0 && !HasManualPromo(item) && item.Type != ItemType.PlaytimeGenerator)
                yield return new Warning("Drop Interval only applies to a manual promo; add a Manual promo rule or clear it.",
                    FixProperty(item, DROP_INTERVAL, property => property.uintValue = 0));
        }

        private static bool HasManualPromo(ItemDef item) =>
            item.Promo != null && item.Promo.Any(rule => rule is ManualPromoRule);

        private static IEnumerable<Warning> GetDropWindowWarnings(ItemDef item)
        {
            if (item.DropWindow.hasValue && item.DropWindow.value == 0)
                yield return new Warning("Drop Window is enabled with 0 minutes, so it is not exported.",
                    FixProperty(item, DROP_WINDOW + HAS_VALUE_PATH, property => property.boolValue = false));
        }

        private static IEnumerable<Warning> GetDropMaxPerWindowWarnings(ItemDef item)
        {
            if (item.DropMaxPerWindow > MAX_DROPS_PER_WINDOW)
                yield return new Warning($"Drop Max Per Window cannot exceed {MAX_DROPS_PER_WINDOW}.",
                    FixProperty(item, DROP_MAX_PER_WINDOW, property => property.uintValue = MAX_DROPS_PER_WINDOW));
        }

        /// <summary>A field of <see cref="TagGeneratorDef"/>, so the other definitions have none.</summary>
        private static IEnumerable<Warning> GetTagValuesAndChancesWarnings(ItemDef item)
        {
            if (item is not TagGeneratorDef generatorDef)
                yield break;

            var tagValuesAndChances = generatorDef.TagValuesAndChances;

            if (tagValuesAndChances.Count == 0)
            {
                yield return new Warning("Tag generator has no values.");
                yield break;
            }

            foreach (var (tagValue, chance) in tagValuesAndChances)
            {
                if (chance == 0)
                    yield return new Warning($"{tagValue} has a chance of 0 and is skipped.", () => {
                        var values = new Dictionary<string, uint>(generatorDef.TagValuesAndChances);

                        values.Remove(tagValue);
                        generatorDef.SetTagValuesAndChances(values);
                    });
            }
        }

        /// <summary>A fix that changes the serialized property at <paramref name="path"/> of <paramref name="item"/>.</summary>
        private static Action FixProperty(ItemDef item, string path, Action<SerializedProperty> change) => () => {
            using var serializedItem = new SerializedObject(item);
            var property = serializedItem.FindProperty(path);

            if (property == null)
                return;

            change(property);

            // The undo step is recorded by ItemDefWarnign.Fix.
            serializedItem.ApplyModifiedPropertiesWithoutUndo();
        };

        /// <summary>A fix that removes element <paramref name="index"/> of the serialized list at <paramref name="path"/>.</summary>
        private static Action RemoveElement(ItemDef item, string path, int index) =>
            FixProperty(item, path, list => {
                if (index >= list.arraySize)
                    return;

                var count = list.arraySize;

                list.DeleteArrayElementAtIndex(index);

                // An object reference is only cleared by the first delete.
                if (list.arraySize == count)
                    list.DeleteArrayElementAtIndex(index);
            });

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
        /// material, must still open here so it can be fixed. The warnings at the top say which entry it is.
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