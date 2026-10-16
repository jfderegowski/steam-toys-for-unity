using System;
using System.Collections.Generic;
using System.Linq;
using fefek5.Toys.Runtime;
using fefek5.Toys.Runtime.Attributes;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.Converters;
using SteamToys.Runtime.InventorySystem.ExchangeVariable;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// A Steam Inventory item definition. Steam keeps every property on every definition and lets the <c>type</c>
    /// decide which ones do anything, so each schema property is a field here, once, and its <c>SchemaSupports…</c>
    /// method says whether the type of the definition uses it: only those are shown, exported and imported. Each
    /// subclass is one ItemDef <c>type</c>; <see cref="ItemDef{TTags}"/> adds tags to the types that have them.
    /// <see href="https://partner.steamgames.com/doc/features/inventory/schema"/>
    /// </summary>
    [JsonConverter(typeof(ItemConverter))]
    public abstract class ItemDef : ScriptableObject
    {
        #region Constants

        private const string PRICE_CATEGORY_VERSION = "1;";
        private const string PRICE_CATEGORY_CURRENCY = "VLV";
        private const uint MAX_DISCOUNT = 100;
        private const uint MAX_DROPS_PER_WINDOW = 10;

        #endregion

        #region Properties

        public ulong ItemDefId
        {
            get => GetItemDefId();
            set => SetItemDefId(value);
        }

        /// <summary>
        /// The ItemDef <c>type</c>, fixed by the class of the asset.
        /// </summary>
        public ItemType Type => GetItemType();

        public string Name
        {
            get => GetName();
            set => SetName(value);
        }

        public string Description
        {
            get => GetDescription();
            set => SetDescription(value);
        }

        public bool Hidden
        {
            get => GetHidden();
            set => SetHidden(value);
        }

        public string DisplayType
        {
            get => GetDisplayType();
            set => SetDisplayType(value);
        }

        public Url IconUrl
        {
            get => GetIconUrl();
            set => SetIconUrl(value);
        }

        public Url IconUrlLarge
        {
            get => GetIconUrlLarge();
            set => SetIconUrlLarge(value);
        }

        public Color32 NameColor
        {
            get => GetNameColor();
            set => SetNameColor(value);
        }

        public Color32 BackgroundColor
        {
            get => GetBackgroundColor();
            set => SetBackgroundColor(value);
        }

        /// <summary>
        /// What a bundle or generator creates: a bundle every entry with its quantity, a generator one entry picked
        /// by weight.
        /// </summary>
        public BundleContent Content
        {
            get => GetContents();
            set => SetContents(value);
        }

        public List<TagGenerator> TagGenerators
        {
            get => GetTagGenerators();
            set => SetTagGenerators(value);
        }

        public Price Price
        {
            get => GetPrice();
            set => SetPrice(value);
        }

        /// <summary>
        /// Valve price tier in <c>VLV</c> units, e.g. 100 for about $0.99. Replaces <see cref="Price"/> when set.
        /// </summary>
        public HasValue<uint> PriceCategory
        {
            get => GetPriceCategory();
            set => SetPriceCategory(value);
        }

        public bool UseBundlePrice
        {
            get => GetUseBundlePrice();
            set => SetUseBundlePrice(value);
        }

        /// <summary>
        /// Percentage taken off the summed price of a bundle's contents. Ignored with <see cref="UseBundlePrice"/>.
        /// </summary>
        public uint PurchaseBundleDiscount
        {
            get => GetPurchaseBundleDiscount();
            set => SetPurchaseBundleDiscount(value);
        }

        public List<string> StoreTags
        {
            get => GetStoreTags();
            set => SetStoreTags(value);
        }

        public List<Url> StoreImages
        {
            get => GetStoreImages();
            set => SetStoreImages(value);
        }

        public bool StoreHidden
        {
            get => GetStoreHidden();
            set => SetStoreHidden(value);
        }

        public bool Tradable
        {
            get => GetTradable();
            set => SetTradable(value);
        }

        public bool Marketable
        {
            get => GetMarketable();
            set => SetMarketable(value);
        }

        public bool GameOnly
        {
            get => GetGameOnly();
            set => SetGameOnly(value);
        }

        public bool AutoStack
        {
            get => GetAutoStack();
            set => SetAutoStack(value);
        }

        public uint PurchaseLimit
        {
            get => GetPurchaseLimit();
            set => SetPurchaseLimit(value);
        }

        /// <summary>
        /// Generator whose possible drops are listed in this item's description, for container items.
        /// </summary>
        public ItemDef ContainerContentsGenerator
        {
            get => GetContainerContentsGenerator();
            set => SetContainerContentsGenerator(value);
        }

        public Exchange Exchange
        {
            get => GetExchange();
            set => SetExchange(value);
        }

        public Promo Promo
        {
            get => GetPromo();
            set => SetPromo(value);
        }

        public SteamDateTime DropStartTime
        {
            get => GetDropStartTime();
            set => SetDropStartTime(value);
        }

        public bool GrantedManually
        {
            get => GetGrantedManually();
            set => SetGrantedManually(value);
        }

        /// <summary>
        /// With a manual promo, the minutes before it grants this again, e.g. 10080 for once a week; 0 grants it only
        /// once. On a playtime generator also the minutes of playtime before a drop.
        /// </summary>
        public uint DropInterval
        {
            get => GetDropInterval();
            set => SetDropInterval(value);
        }

        public HasValue<uint> DropWindow
        {
            get => GetDropWindow();
            set => SetDropWindow(value);
        }

        public uint DropMaxPerWindow
        {
            get => GetDropMaxPerWindow();
            set => SetDropMaxPerWindow(value);
        }

        public HasValue<uint> DropLimit
        {
            get => GetDropLimit();
            set => SetDropLimit(value);
        }

        public Tags Tags
        {
            get => GetTags();
            set => SetTags(value);
        }

        #endregion

        #region Inspector Serialized Fields
        
        [Header("Basic Item Data")]
        [SerializeField, Tooltip("Unique identifier for this item definition.")]
        private ulong _itemDefId;
        [SerializeField, Tooltip("Display name of the item.")]
        private string _name;
        [SerializeField, TextArea, Tooltip("Detailed description of the item.")]
        private string _description;

        [Header("Visibility & Restrictions")]
        [SerializeField, Tooltip("Hides the definition from clients and makes it unpurchasable. It can still be granted.")]
        private bool _hidden;

        [Header("Visual & Display")]
        [SerializeField, Tooltip("Category name displayed in the inventory.")]
        private string _displayType;
        [SerializeField, Tooltip("URL to the item's standard icon image.")]
        private Url _iconUrl;
        [SerializeField, Tooltip("URL to the item's large icon image.")]
        private Url _iconUrlLarge;
        [SerializeField, Tooltip("Color of the item's name in inventory.")]
        private Color32 _nameColor;
        [SerializeField, Tooltip("Background color of the item in inventory.")]
        private Color32 _backgroundColor;

        [Header("Contents")]
        [SerializeField, Tooltip("What this creates: a bundle every entry with its quantity, a generator one entry picked by weight.")]
        private BundleContent _bundle = new();
        [SerializeField, Tooltip("Tag generators applied to every item this creates.")]
        private List<TagGenerator> _tagGenerators = new();

        [Header("Pricing")]
        [SerializeField, Tooltip("Price information for the item.")]
        private Price _price;
        [SerializeField, Tooltip("Valve price tier in VLV units (100 is about $0.99). Replaces the price when set.")]
        private HasValue<uint> _priceCategory;
        [SerializeField, Tooltip("If true, uses the bundle's own price instead of the sum of its contents.")]
        private bool _useBundlePrice;
        [SerializeField, Tooltip("Percentage discount on the summed price of the contents. Ignored with Use Bundle Price.")]
        private uint _purchaseBundleDiscount;

        [Header("Item Store")]
        [SerializeField, Tooltip("Tags used to categorize and filter the item in the Item Store.")]
        private List<string> _storeTags = new();
        [SerializeField, Tooltip("Images shown on the item's Item Store detail page.")]
        private List<Url> _storeImages = new();
        [SerializeField, Tooltip("Whether this item is hidden from the Item Store.")]
        private bool _storeHidden;

        [Header("Market & Trading")]
        [SerializeField, Tooltip("Whether this item can be traded between users.")]
        private bool _tradable;
        [SerializeField, Tooltip("Whether this item can be sold on the Steam Community Market.")]
        private bool _marketable;
        [SerializeField, Tooltip("Hides the item from the Steam Backpack, for items consumed right away.")]
        private bool _gameOnly;
        [SerializeField, Tooltip("Whether grants are added to a single stack.")]
        private bool _autoStack;
        [SerializeField, Tooltip("Blocks a purchase when the user already owns this many. 0 means no limit.")]
        private uint _purchaseLimit;
        [SerializeField, Tooltip("Generator whose possible drops are listed in this item's description.")]
        private ItemDef _containerContentsGenerator;

        [Header("Exchange System")]
        [SerializeField, Tooltip("Recipes that consume materials to create this item.")]
        private Exchange _exchange;

        [Header("Promo")]
        [SerializeField, Tooltip("Rules that grant this item automatically.")]
        private Promo _promo;
        [SerializeField, Tooltip("Timestamp before which the manual promo does not grant this item.")]
        private SteamDateTime _dropStartTime;
        [SerializeField, Tooltip("Granted only by AddPromoItem(s) with its explicit ID. Without it GrantPromoItems grants a manual promo too.")]
        private bool _grantedManually;
        [SerializeField, Tooltip("With a manual promo: minutes before it grants this again, e.g. 10080 for once a week; 0 grants it only " +
                 "once. On a playtime generator: minutes of playtime before a drop, 0 uses the application setting.")]
        private uint _dropInterval;

        [Header("Drop System")]
        [SerializeField, Tooltip("Cool-down window in minutes. Drops are limited to the window only when set.")]
        private HasValue<uint> _dropWindow;
        [SerializeField, Tooltip("Drops allowed within the drop window (1-10). 0 uses the application setting.")]
        private uint _dropMaxPerWindow;
        [SerializeField, Tooltip("Maximum number of drops per user. Enforced only when set; 0 stops all future drops.")]
        private HasValue<uint> _dropLimit;

        [Header("Tags")]
        [SerializeReference, SelectType, Tooltip("Tags of this definition; a generator copies them to the item it creates.")]
        private Tags _tags;

        #endregion

        #region Getters and Setters

        public virtual ulong GetItemDefId() => _itemDefId;

        public virtual void SetItemDefId(ulong value) => _itemDefId = value;

        public abstract ItemType GetItemType();

        public virtual string GetName() => _name;

        public virtual void SetName(string value) => _name = value;

        public virtual string GetDescription() => _description;

        public virtual void SetDescription(string value) => _description = value;

        public virtual bool GetHidden() => _hidden;

        public virtual void SetHidden(bool value) => _hidden = value;

        public virtual string GetDisplayType() => _displayType;

        public virtual void SetDisplayType(string value) => _displayType = value;

        public virtual Url GetIconUrl() => _iconUrl;

        public virtual void SetIconUrl(Url value) => _iconUrl = value;

        public virtual Url GetIconUrlLarge() => _iconUrlLarge;

        public virtual void SetIconUrlLarge(Url value) => _iconUrlLarge = value;

        public virtual Color32 GetNameColor() => _nameColor;

        public virtual void SetNameColor(Color32 value) => _nameColor = value;

        public virtual Color32 GetBackgroundColor() => _backgroundColor;

        public virtual void SetBackgroundColor(Color32 value) => _backgroundColor = value;

        public virtual BundleContent GetContents() => _bundle;

        public virtual void SetContents(BundleContent value) => _bundle = value;

        public virtual List<TagGenerator> GetTagGenerators() => _tagGenerators;

        public virtual void SetTagGenerators(List<TagGenerator> value) => _tagGenerators = value;

        public virtual Price GetPrice() => _price;

        public virtual void SetPrice(Price value) => _price = value;

        public virtual HasValue<uint> GetPriceCategory() => _priceCategory;

        public virtual void SetPriceCategory(HasValue<uint> value) => _priceCategory = value;

        public virtual bool GetUseBundlePrice() => _useBundlePrice;

        public virtual void SetUseBundlePrice(bool value) => _useBundlePrice = value;

        public virtual uint GetPurchaseBundleDiscount() => _purchaseBundleDiscount;

        public virtual void SetPurchaseBundleDiscount(uint value) => _purchaseBundleDiscount = value;

        public virtual List<string> GetStoreTags() => _storeTags;

        public virtual void SetStoreTags(List<string> value) => _storeTags = value;

        public virtual List<Url> GetStoreImages() => _storeImages;

        public virtual void SetStoreImages(List<Url> value) => _storeImages = value;

        public virtual bool GetStoreHidden() => _storeHidden;

        public virtual void SetStoreHidden(bool value) => _storeHidden = value;

        public virtual bool GetTradable() => _tradable;

        public virtual void SetTradable(bool value) => _tradable = value;

        public virtual bool GetMarketable() => _marketable;

        public virtual void SetMarketable(bool value) => _marketable = value;

        public virtual bool GetGameOnly() => _gameOnly;

        public virtual void SetGameOnly(bool value) => _gameOnly = value;

        public virtual bool GetAutoStack() => _autoStack;

        public virtual void SetAutoStack(bool value) => _autoStack = value;

        public virtual uint GetPurchaseLimit() => _purchaseLimit;

        public virtual void SetPurchaseLimit(uint value) => _purchaseLimit = value;

        public virtual ItemDef GetContainerContentsGenerator() => _containerContentsGenerator;

        public virtual void SetContainerContentsGenerator(ItemDef value) => _containerContentsGenerator = value;

        public virtual Exchange GetExchange() => _exchange;

        public virtual void SetExchange(Exchange value) => _exchange = value;

        public virtual Promo GetPromo() => _promo;

        public virtual void SetPromo(Promo value) => _promo = value;

        public virtual SteamDateTime GetDropStartTime() => _dropStartTime;

        public virtual void SetDropStartTime(SteamDateTime value) => _dropStartTime = value;

        public virtual bool GetGrantedManually() => _grantedManually;

        public virtual void SetGrantedManually(bool value) => _grantedManually = value;

        public virtual uint GetDropInterval() => _dropInterval;

        public virtual void SetDropInterval(uint value) => _dropInterval = value;

        public virtual HasValue<uint> GetDropWindow() => _dropWindow;

        public virtual void SetDropWindow(HasValue<uint> value) => _dropWindow = value;

        public virtual uint GetDropMaxPerWindow() => _dropMaxPerWindow;

        public virtual void SetDropMaxPerWindow(uint value) => _dropMaxPerWindow = value;

        public virtual HasValue<uint> GetDropLimit() => _dropLimit;

        public virtual void SetDropLimit(HasValue<uint> value) => _dropLimit = value;
        
        public virtual Tags GetTags() => _tags;

        public virtual void SetTags(Tags tags)
        {
            foreach (var (tagName, tagValue) in tags)
                SetTag(tagName, tagValue);
        }

        public virtual bool TryGetTag(string tagName, out string tagValue)
        {
            if (!Tags.ContainsKey(tagName))
            {
                tagValue = string.Empty;
                return false;
            }

            tagValue = GetTag(tagName);
            
            return true;
        }
        
        public virtual string GetTag(string tagName)
        {
            if (!Tags.ContainsKey(tagName))
                throw new Exception($"The tag {tagName} does not exist.");
            
            return Tags[tagName];
        }
        
        public virtual void SetTag(string tagName, string tagValue) => 
            Tags[tagName] = tagValue;

        #endregion

        #region Schema

        // Which schema properties Steam acts on for this definition: Steam keeps any property on any type, so each type
        // says what it was measured to act on (Tests/SchemaProbes), not what it accepts. Only the supported properties
        // are shown, exported and imported.

        /// <summary>Whether this has tags; <see cref="ItemDef{TTags}"/> does.</summary>
        public abstract bool SchemaSupportsTags();

        // Granted with the definition or the items it creates.
        public abstract bool SchemaSupportsDescription();
        public abstract bool SchemaSupportsHidden();
        public abstract bool SchemaSupportsExchange();
        public abstract bool SchemaSupportsPromo();
        public abstract bool SchemaSupportsDropStartTime();
        public abstract bool SchemaSupportsGrantedManually();
        public abstract bool SchemaSupportsDropInterval();

        // Creates items.
        public abstract bool SchemaSupportsContent();
        public abstract bool SchemaSupportsTagGenerators();

        // Priced, and shown in an inventory or the Item Store.
        public abstract bool SchemaSupportsDisplayType();
        public abstract bool SchemaSupportsIconUrl();
        public abstract bool SchemaSupportsIconUrlLarge();
        public abstract bool SchemaSupportsNameColor();
        public abstract bool SchemaSupportsBackgroundColor();
        public abstract bool SchemaSupportsStoreTags();
        public abstract bool SchemaSupportsStoreImages();
        public abstract bool SchemaSupportsStoreHidden();
        public abstract bool SchemaSupportsPrice();
        public abstract bool SchemaSupportsPriceCategory();

        // Priced from its contents.
        public abstract bool SchemaSupportsUseBundlePrice();
        public abstract bool SchemaSupportsPurchaseBundleDiscount();

        // Acts on the item itself, not on what a bundle or generator creates.
        public abstract bool SchemaSupportsTradable();
        public abstract bool SchemaSupportsMarketable();
        public abstract bool SchemaSupportsGameOnly();
        public abstract bool SchemaSupportsAutoStack();
        public abstract bool SchemaSupportsPurchaseLimit();
        public abstract bool SchemaSupportsContainerContentsGenerator();

        // Dropped by the application.
        public abstract bool SchemaSupportsDropWindow();
        public abstract bool SchemaSupportsDropMaxPerWindow();
        public abstract bool SchemaSupportsDropLimit();

        /// <summary>
        /// Whether the serialized field <paramref name="fieldName"/> holds a property this definition supports. A field
        /// of no schema property, such as the itemdefid, always does.
        /// </summary>
        public virtual bool SchemaSupportsField(string fieldName) => fieldName switch
        {
            nameof(_description) => SchemaSupportsDescription(),
            nameof(_hidden) => SchemaSupportsHidden(),
            nameof(_displayType) => SchemaSupportsDisplayType(),
            nameof(_iconUrl) => SchemaSupportsIconUrl(),
            nameof(_iconUrlLarge) => SchemaSupportsIconUrlLarge(),
            nameof(_nameColor) => SchemaSupportsNameColor(),
            nameof(_backgroundColor) => SchemaSupportsBackgroundColor(),
            nameof(_bundle) => SchemaSupportsContent(),
            nameof(_tagGenerators) => SchemaSupportsTagGenerators(),
            nameof(_price) => SchemaSupportsPrice(),
            nameof(_priceCategory) => SchemaSupportsPriceCategory(),
            nameof(_useBundlePrice) => SchemaSupportsUseBundlePrice(),
            nameof(_purchaseBundleDiscount) => SchemaSupportsPurchaseBundleDiscount(),
            nameof(_storeTags) => SchemaSupportsStoreTags(),
            nameof(_storeImages) => SchemaSupportsStoreImages(),
            nameof(_storeHidden) => SchemaSupportsStoreHidden(),
            nameof(_tradable) => SchemaSupportsTradable(),
            nameof(_marketable) => SchemaSupportsMarketable(),
            nameof(_gameOnly) => SchemaSupportsGameOnly(),
            nameof(_autoStack) => SchemaSupportsAutoStack(),
            nameof(_purchaseLimit) => SchemaSupportsPurchaseLimit(),
            nameof(_containerContentsGenerator) => SchemaSupportsContainerContentsGenerator(),
            nameof(_exchange) => SchemaSupportsExchange(),
            nameof(_promo) => SchemaSupportsPromo(),
            nameof(_dropStartTime) => SchemaSupportsDropStartTime(),
            nameof(_grantedManually) => SchemaSupportsGrantedManually(),
            nameof(_dropInterval) => SchemaSupportsDropInterval(),
            nameof(_dropWindow) => SchemaSupportsDropWindow(),
            nameof(_dropMaxPerWindow) => SchemaSupportsDropMaxPerWindow(),
            nameof(_dropLimit) => SchemaSupportsDropLimit(),
            nameof(_tags) => SchemaSupportsTags(),
            _ => true
        };

        /// <summary>Each schema property by its Steam key, with whether it is supported and holds anything.</summary>
        private IEnumerable<(string key, bool supported, bool set)> GetPropertyUse()
        {
            yield return ("description", SchemaSupportsDescription(), !string.IsNullOrEmpty(Description));
            yield return ("hidden", SchemaSupportsHidden(), Hidden);
            yield return ("tags", SchemaSupportsTags(), GetTags() is { Count: > 0 });
            yield return ("display_type", SchemaSupportsDisplayType(), !string.IsNullOrEmpty(DisplayType));
            yield return ("icon_url", SchemaSupportsIconUrl(), IconUrl is { HasValue: true });
            yield return ("icon_url_large", SchemaSupportsIconUrlLarge(), IconUrlLarge is { HasValue: true });
            yield return ("name_color", SchemaSupportsNameColor(), NameColor.a > 0);
            yield return ("background_color", SchemaSupportsBackgroundColor(), BackgroundColor.a > 0);
            yield return ("bundle", SchemaSupportsContent(), Content is { Count: > 0 });
            yield return ("tag_generators", SchemaSupportsTagGenerators(), TagGenerators is { Count: > 0 });
            yield return ("price", SchemaSupportsPrice(), !string.IsNullOrEmpty(Price?.ToSteamString()));
            yield return ("price_category", SchemaSupportsPriceCategory(), PriceCategory.hasValue);
            yield return ("use_bundle_price", SchemaSupportsUseBundlePrice(), UseBundlePrice);
            yield return ("purchase_bundle_discount", SchemaSupportsPurchaseBundleDiscount(), PurchaseBundleDiscount > 0);
            yield return ("store_tags", SchemaSupportsStoreTags(), StoreTags is { Count: > 0 });
            yield return ("store_images", SchemaSupportsStoreImages(), StoreImages is { Count: > 0 });
            yield return ("store_hidden", SchemaSupportsStoreHidden(), StoreHidden);
            yield return ("tradable", SchemaSupportsTradable(), Tradable);
            yield return ("marketable", SchemaSupportsMarketable(), Marketable);
            yield return ("game_only", SchemaSupportsGameOnly(), GameOnly);
            yield return ("auto_stack", SchemaSupportsAutoStack(), AutoStack);
            yield return ("purchase_limit", SchemaSupportsPurchaseLimit(), PurchaseLimit > 0);
            yield return ("container_contents_generator", SchemaSupportsContainerContentsGenerator(), ContainerContentsGenerator != null);
            yield return ("exchange", SchemaSupportsExchange(), Exchange is { Count: > 0 });
            yield return ("promo", SchemaSupportsPromo(), Promo is { Count: > 0 });
            yield return ("drop_start_time", SchemaSupportsDropStartTime(), DropStartTime is { HasValue: true });
            yield return ("granted_manually", SchemaSupportsGrantedManually(), GrantedManually);
            yield return ("drop_interval", SchemaSupportsDropInterval(), DropInterval > 0);
            yield return ("drop_window", SchemaSupportsDropWindow(), DropWindow.hasValue);
            yield return ("drop_max_per_window", SchemaSupportsDropMaxPerWindow(), DropMaxPerWindow > 0);
            yield return ("drop_limit", SchemaSupportsDropLimit(), DropLimit.hasValue);
        }

        #endregion

        #region Inventory

        /// <summary>
        /// Grants <paramref name="quantity"/> of this definition to the signed-in account through
        /// <see cref="Inventory.GenerateItems"/>. Works only for accounts of the Steamworks partner group and only once
        /// the definition is published on Steam, so it is for testing, never for the game itself.
        /// </summary>
        public bool GenerateItem(int quantity = 1)
        {
            if (quantity < 1)
            {
                Debug.LogWarning($"{name}: Generate Item needs a quantity of at least 1.");
                return false;
            }

            return Inventory.GenerateItems(new[] { (this, (uint)quantity) }, succeeded => {
                if (succeeded)
                    Debug.Log($"Generated {quantity} x {name} (itemdefid {ItemDefId}).");
                else
                    Debug.LogWarning($"Steam did not generate {name} (itemdefid {ItemDefId}).");
            });
        }

        #endregion

        #region Validation

        /// <summary>
        /// Adds a message for every setting Steam would reject or silently ignore. Overrides call base first.
        /// </summary>
        public virtual void CollectSchemaWarnings(List<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(Name))
                warnings.Add("Name is empty: Steam rejects the definition, and an upload stops there.");

            foreach (var (key, supported, set) in GetPropertyUse())
            {
                if (!supported && set)
                    warnings.Add($"{key} is set, but a {Type} does not use it, so it is not exported.");
            }

            if (SchemaSupportsContent())
                BundleContent.CollectSchemaWarnings(this, Content, warnings);

            if (SchemaSupportsPrice())
                CollectPriceWarnings(warnings);

            if (SchemaSupportsPromo())
                CollectPromoWarnings(warnings);

            if (SchemaSupportsPurchaseLimit() && PurchaseLimit > 0 && (Tradable || Marketable))
                warnings.Add("Purchase Limit does not block repurchasing tradable or marketable items.");

            if (SchemaSupportsDropMaxPerWindow() && DropMaxPerWindow > MAX_DROPS_PER_WINDOW)
                warnings.Add($"Drop Max Per Window cannot exceed {MAX_DROPS_PER_WINDOW}.");

            if (SchemaSupportsDropWindow() && DropWindow.hasValue && DropWindow.value == 0)
                warnings.Add("Drop Window is enabled with 0 minutes, so it is not exported.");
        }

        private void CollectPriceWarnings(List<string> warnings)
        {
            if (PriceCategory.hasValue && !string.IsNullOrEmpty(Price?.ToSteamString()))
                warnings.Add("Price and Price Category are both set: only the price category is exported.");

            if (Price != null && Price.RegularPrices.Count == 0 && Price.Promotions.Count > 0)
                warnings.Add("Price has sales but no regular price, which Steam requires first.");

            if (!SchemaSupportsPurchaseBundleDiscount())
                return;

            if (PurchaseBundleDiscount > MAX_DISCOUNT)
                warnings.Add($"Purchase Bundle Discount is a percentage and cannot exceed {MAX_DISCOUNT}.");

            if (PurchaseBundleDiscount > 0 && UseBundlePrice)
                warnings.Add("Purchase Bundle Discount is ignored when Use Bundle Price is on.");
        }

        private void CollectPromoWarnings(List<string> warnings)
        {
            var manual = HasManualPromo();

            if (DropStartTime is { HasValue: true } && !manual)
                warnings.Add("Drop Start Time only applies to a manual promo; add a Manual promo rule or clear it.");

            // On a playtime generator the interval also paces its drops.
            if (DropInterval > 0 && !manual && Type != ItemType.PlaytimeGenerator)
                warnings.Add("Drop Interval only applies to a manual promo; add a Manual promo rule or clear it.");

            if (Promo == null)
                return;

            for (var i = 0; i < Promo.Count; i++)
            {
                if (string.IsNullOrEmpty(Promo[i]?.ToSteamString()))
                    warnings.Add($"Promo rule {i} is incomplete and is left out.");
            }
        }

        private bool HasManualPromo()
        {
            if (Promo == null)
                return false;

            foreach (var rule in Promo)
            {
                if (rule is ManualPromoRule)
                    return true;
            }

            return false;
        }

        #endregion

        #region Json

        public virtual string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented);

        /// <summary>
        /// Writes this definition's schema properties into the already opened JSON object: the ones its type
        /// supports. Overrides call base first, so the shared properties come before their own.
        /// </summary>
        public virtual void WriteJsonProperties(JsonWriter writer)
        {
            writer.WritePropertyName("itemdefid");
            writer.WriteValue(ItemDefId);

            writer.WritePropertyName("type");
            writer.WriteValue(ItemTypeConverter.ToString(Type));

            writer.WriteIfNotEmpty("name", Name);

            if (SchemaSupportsDescription())
                writer.WriteIfNotEmpty("description", Description);

            if (SchemaSupportsTags())
                writer.WriteIfNotEmpty("tags", TagsConverter.ToString(Tags));

            if (SchemaSupportsHidden())
                writer.WriteBoolIfTrue("hidden", Hidden);

            if (SchemaSupportsExchange())
                writer.WriteExchangeIfNotEmpty("exchange", Exchange);

            WriteStore(writer);

            if (SchemaSupportsPromo())
                writer.WritePromoIfNotEmpty("promo", Promo);

            if (SchemaSupportsDropStartTime())
                writer.WriteSteamDateTimeIfHasValue("drop_start_time", DropStartTime);

            if (SchemaSupportsGrantedManually())
                writer.WriteBoolIfTrue("granted_manually", GrantedManually);

            if (SchemaSupportsDropInterval())
                writer.WriteUintIfNotZero("drop_interval", DropInterval);

            WriteContents(writer);
            WriteItem(writer);
            WriteDrops(writer);
        }

        private void WriteStore(JsonWriter writer)
        {
            if (SchemaSupportsDisplayType())
                writer.WriteIfNotEmpty("display_type", DisplayType);

            if (SchemaSupportsNameColor())
                writer.WriteColorIfSet("name_color", NameColor);

            if (SchemaSupportsBackgroundColor())
                writer.WriteColorIfSet("background_color", BackgroundColor);

            if (SchemaSupportsIconUrl())
                writer.WriteUrlIfNotEmpty("icon_url", IconUrl);

            if (SchemaSupportsIconUrlLarge())
                writer.WriteUrlIfNotEmpty("icon_url_large", IconUrlLarge);

            if (SchemaSupportsStoreTags())
                writer.WriteListIfNotEmpty("store_tags", StoreTags);

            if (SchemaSupportsStoreImages())
                writer.WriteListIfNotEmpty("store_images", StoreImages);

            if (SchemaSupportsStoreHidden())
                writer.WriteBoolIfTrue("store_hidden", StoreHidden);

            // Steam keeps both and charges the price, so only one is written: the category, which replaces the price.
            if (SchemaSupportsPriceCategory() && PriceCategory.hasValue)
                writer.WriteIfNotEmpty("price_category", PRICE_CATEGORY_VERSION + PRICE_CATEGORY_CURRENCY + PriceCategory.value);
            else if (SchemaSupportsPrice())
                writer.WritePriceIfNotEmpty("price", Price);
        }

        private void WriteContents(JsonWriter writer)
        {
            if (SchemaSupportsContent())
                writer.WriteBundleIfNotEmpty("bundle", Content);

            if (SchemaSupportsTagGenerators())
                writer.WriteItemDefIdsIfNotEmpty("tag_generators", TagGenerators);

            if (SchemaSupportsUseBundlePrice())
                writer.WriteBoolIfTrue("use_bundle_price", UseBundlePrice);

            if (SchemaSupportsPurchaseBundleDiscount())
                writer.WriteUintIfNotZero("purchase_bundle_discount", PurchaseBundleDiscount);
        }

        private void WriteItem(JsonWriter writer)
        {
            if (SchemaSupportsTradable())
                writer.WriteBoolIfTrue("tradable", Tradable);

            if (SchemaSupportsMarketable())
                writer.WriteBoolIfTrue("marketable", Marketable);

            if (SchemaSupportsGameOnly())
                writer.WriteBoolIfTrue("game_only", GameOnly);

            if (SchemaSupportsAutoStack())
                writer.WriteBoolIfTrue("auto_stack", AutoStack);

            if (SchemaSupportsPurchaseLimit())
                writer.WriteUintIfNotZero("purchase_limit", PurchaseLimit);

            if (SchemaSupportsContainerContentsGenerator())
                writer.WriteItemDefIdIfSet("container_contents_generator", ContainerContentsGenerator);
        }

        private void WriteDrops(JsonWriter writer)
        {
            if (SchemaSupportsDropWindow())
            {
                writer.WriteBoolIfTrue("use_drop_window", DropWindow.hasValue);

                if (DropWindow.hasValue)
                    writer.WriteUintIfNotZero("drop_window", DropWindow.value);
            }

            if (SchemaSupportsDropMaxPerWindow())
                writer.WriteUintIfNotZero("drop_max_per_window", DropMaxPerWindow);

            // A drop limit of 0 is meaningful here: it stops all future drops.
            if (SchemaSupportsDropLimit())
            {
                writer.WriteBoolIfTrue("use_drop_limit", DropLimit.hasValue);

                if (DropLimit.hasValue)
                {
                    writer.WritePropertyName("drop_limit");
                    writer.WriteValue(DropLimit.value);
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// An ItemDef with tags of <typeparamref name="TTags"/>: <see cref="DefaultTags"/> for any category, or a
    /// <see cref="Tags"/> of the game.
    /// </summary>
    public abstract class ItemDef<TTags> : ItemDef where TTags : Tags, new()
    {
        #region Properties

        public TTags FixedTags
        {
            get => GetFixedTags();
            set => SetFixedTags(value);
        }

        #endregion
        
        #region Inspector Serialized Field

        [SerializeField, Tooltip("Tags of this definition; a generator copies them to the item it creates.")]
        private TTags _fixedTags = new();

        #endregion

        #region Getters and Setters

        public virtual TTags GetFixedTags() => _fixedTags;

        public virtual void SetFixedTags(TTags tags)
        {
            foreach (var (tagName, tagValue) in tags) 
                FixedTags.TrySetValue(tagName, tagValue);
        }

        public override string GetTag(string tagName)
        {
            if (FixedTags.TryGetValue(tagName, out var tagValue))
                return tagValue;
            
            return base.GetTag(tagName);
        }

        public override void SetTag(string tagName, string tagValue)
        {
            if (FixedTags.TrySetValue(tagName, tagValue))
                return;
            
            base.SetTag(tagName, tagValue);
        }

        #endregion
    }
}
