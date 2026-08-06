using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.Converters;
using SteamToys.Runtime.InventorySystem.ExchangeVariable;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    [JsonConverter(typeof(SteamInventoryItemConverter))]
    public abstract class Item : ScriptableObject
    {
        #region Properties

        public uint AppId
        {
            get => GetAppId();
            set => SetAppId(value);
        }
        
        public ulong ItemDefId
        {
            get => GetItemDefId();
            set => SetItemDefId(value);
        }

        public ItemType Type
        {
            get => GetItemType();
            set => SetItemType(value);
        }
        
        public string DisplayType
        {
            get => GetDisplayType();
            set => SetDisplayType(value);
        }

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
        
        public string Tags
        {
            get => GetTags();
            set => SetTags(value);
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

        public Exchange Exchange
        {
            get => GetExchange();
            set => SetExchange(value);
        }

        public Bundle Bundle
        {
            get => GetBundle();
            set => SetBundle(value);
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
        
        public Price Price
        {
            get => GetPrice();
            set => SetPrice(value);
        }

        public bool GameOnly
        {
            get => GetGameOnly();
            set => SetGameOnly(value);
        }

        public bool Hidden
        {
            get => GetHidden();
            set => SetHidden(value);
        }

        public bool StoreHidden
        {
            get => GetStoreHidden();
            set => SetStoreHidden(value);
        }

        public bool GrantedManually
        {
            get => GetGrantedManually();
            set => SetGrantedManually(value);
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

        public bool UseDropLimit
        {
            get => GetUseDropLimit();
            set => SetUseDropLimit(value);
        }

        public uint DropLimit
        {
            get => GetDropLimit();
            set => SetDropLimit(value);
        }

        public uint DropInterval
        {
            get => GetDropInterval();
            set => SetDropInterval(value);
        }

        public bool UseDropWindow
        {
            get => GetUseDropWindow();
            set => SetUseDropWindow(value);
        }

        public uint DropWindow
        {
            get => GetDropWindow();
            set => SetDropWindow(value);
        }

        public uint DropMaxPerWindow
        {
            get => GetDropMaxPerWindow();
            set => SetDropMaxPerWindow(value);
        }

        public bool UseBundlePrice
        {
            get => GetUseBundlePrice();
            set => SetUseBundlePrice(value);
        }

        #endregion

        #region Inspector Serialized Fields
        
        [Header("Basic Item Data")]
        [SerializeField, Tooltip("Steam Application ID for this item.")]
        private uint _appId;
        [SerializeField, Tooltip("Unique identifier for this item definition.")]
        private ulong _itemDefId;
        [SerializeField, Tooltip("Classification type of the item (e.g., Cosmetic, Tool, Consumable).")]
        private ItemType _type;
        [SerializeField, Tooltip("Display name of the item.")]
        private string _name;
        [SerializeField, TextArea, Tooltip("Detailed description of the item.")]
        private string _description;
        
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
        
        [Header("Market & Trading")]
        [SerializeField, Tooltip("Whether this item can be traded between users.")]
        private bool _tradable;
        [SerializeField, Tooltip("Whether this item can be sold on the Steam Community Market.")]
        private bool _marketable;
        
        [Header("Pricing")]
        [SerializeField, Tooltip("Price information for the item.")]
        private Price _price;
        [SerializeField, Tooltip("If true, uses bundle price instead of individual item price.")]
        private bool _useBundlePrice;
        
        [Header("Bundles & Promos")]
        [SerializeField, Tooltip("Bundle configuration if this item contains other items.")]
        private Bundle _bundle;
        [SerializeField, Tooltip("Promotional settings associated with this item.")]
        private Promo _promo;
        
        [Header("Drop System")]
        [SerializeField, Tooltip("Timestamp when this item starts dropping to users.")]
        private SteamDateTime _dropStartTime;
        [SerializeField, Tooltip("Whether to enforce a drop limit for this item.")]
        private bool _useDropLimit;
        [SerializeField, Tooltip("Maximum number of this item that can be obtained by a user.")]
        private uint _dropLimit;
        [SerializeField, Tooltip("Time in seconds between consecutive item drops.")]
        private uint _dropInterval;
        [SerializeField, Tooltip("Whether to limit drops within a specific time window.")]
        private bool _useDropWindow;
        [SerializeField, Tooltip("Duration of the drop window in seconds.")]
        private uint _dropWindow;
        [SerializeField, Tooltip("Maximum drops allowed within the drop window.")]
        private uint _dropMaxPerWindow;
        
        [Header("Exchange System")]
        [SerializeField, Tooltip("Configuration for trading/exchanging this item with other items.")]
        private Exchange _exchange;
        
        [Header("Visibility & Restrictions")]
        [SerializeField, Tooltip("Whether this item is hidden from the inventory UI.")]
        private bool _hidden;
        [SerializeField, Tooltip("Whether this item is hidden from the in-game store.")]
        private bool _storeHidden;
        [SerializeField, Tooltip("Whether this item was granted manually to users.")]
        private bool _grantedManually;
        [SerializeField, Tooltip("Whether this item is only available in-game and not on the market.")]
        private bool _gameOnly;
        
        [Header("Stack Management")]
        [SerializeField, Tooltip("Whether items automatically stack in the inventory.")]
        private bool _autoStack;
        
        [Header("Purchase Limits")]
        [SerializeField, Tooltip("Maximum number of this item a user can purchase.")]
        private uint _purchaseLimit;

        #endregion
        
        #region Getters and Setters
        
        public virtual uint GetAppId() => _appId;
        
        public virtual void SetAppId(uint value) => _appId = value;
        
        public virtual ulong GetItemDefId() => _itemDefId;
        
        public virtual void SetItemDefId(ulong value) => _itemDefId = value;
        
        public virtual ItemType GetItemType() => _type;
        
        public virtual void SetItemType(ItemType value) => _type = value;
        
        public virtual string GetDisplayType() => _displayType;
        
        public virtual void SetDisplayType(string value) => _displayType = value;
        
        public virtual string GetName() => _name;
        
        public virtual void SetName(string value) => _name = value;
        
        public virtual string GetDescription() => _description;
        
        public virtual void SetDescription(string value) => _description = value;
        
        public virtual Color32 GetNameColor() => _nameColor;
        
        public virtual void SetNameColor(Color32 value) => _nameColor = value;
        
        public virtual Color32 GetBackgroundColor() => _backgroundColor;
        
        public virtual void SetBackgroundColor(Color32 value) => _backgroundColor = value;
        
        public virtual Url GetIconUrl() => _iconUrl;
        
        public virtual void SetIconUrl(Url value) => _iconUrl = value;
        
        public virtual Url GetIconUrlLarge() => _iconUrlLarge;
        
        public virtual void SetIconUrlLarge(Url value) => _iconUrlLarge = value;

        public abstract string GetTags();

        public abstract void SetTags(string value);
        
        public virtual bool GetTradable() => _tradable;
        
        public virtual void SetTradable(bool value) => _tradable = value;
        
        public virtual bool GetMarketable() => _marketable;
        
        public virtual void SetMarketable(bool value) => _marketable = value;
        
        public virtual Bundle GetBundle() => _bundle;
        
        public virtual void SetBundle(Bundle value) => _bundle = value;

        public virtual Promo GetPromo() => _promo;
        
        public virtual void SetPromo(Promo value) => _promo = value;
        
        public virtual SteamDateTime GetDropStartTime() => _dropStartTime;
        
        public virtual void SetDropStartTime(SteamDateTime value) => _dropStartTime = value;
        
        public virtual Exchange GetExchange() => _exchange;

        public virtual void SetExchange(Exchange value) => _exchange = value;
        
        public virtual Price GetPrice() => _price;
        
        public virtual void SetPrice(Price value) => _price = value;

        public virtual bool GetGameOnly() => _gameOnly;
        
        public virtual void SetGameOnly(bool value) => _gameOnly = value;

        public virtual bool GetHidden() => _hidden;
        
        public virtual void SetHidden(bool value) => _hidden = value;

        public virtual bool GetStoreHidden() => _storeHidden;
        
        public virtual void SetStoreHidden(bool value) => _storeHidden = value;

        public virtual bool GetGrantedManually() => _grantedManually;
        
        public virtual void SetGrantedManually(bool value) => _grantedManually = value;

        public virtual bool GetAutoStack() => _autoStack;
        
        public virtual void SetAutoStack(bool value) => _autoStack = value;

        public virtual uint GetPurchaseLimit() => _purchaseLimit;
        
        public virtual void SetPurchaseLimit(uint value) => _purchaseLimit = value;

        public virtual bool GetUseDropLimit() => _useDropLimit;
        
        public virtual void SetUseDropLimit(bool value) => _useDropLimit = value;

        public virtual uint GetDropLimit() => _dropLimit;
        
        public virtual void SetDropLimit(uint value) => _dropLimit = value;

        public virtual uint GetDropInterval() => _dropInterval;
        
        public virtual void SetDropInterval(uint value) => _dropInterval = value;

        public virtual bool GetUseDropWindow() => _useDropWindow;
        
        public virtual void SetUseDropWindow(bool value) => _useDropWindow = value;

        public virtual uint GetDropWindow() => _dropWindow;
        
        public virtual void SetDropWindow(uint value) => _dropWindow = value;

        public virtual uint GetDropMaxPerWindow() => _dropMaxPerWindow;
        
        public virtual void SetDropMaxPerWindow(uint value) => _dropMaxPerWindow = value;

        public virtual bool GetUseBundlePrice() => _useBundlePrice;
        
        public virtual void SetUseBundlePrice(bool value) => _useBundlePrice = value;

        #endregion

        #region Json

        public virtual string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented);

        #endregion
    }

    public class Item<TTags> : Item where TTags : Tags, new()
    {
        [SerializeField] private TTags _tags;

        public override string GetTags()
        {
            EnsureTagsInitialized();
            
            return _tags.GetTags();
        }

        public override void SetTags(string value)
        {
            EnsureTagsInitialized();
            
            _tags.SetTags(value);
        }

        private void EnsureTagsInitialized() => _tags ??= new TTags();
    }
}