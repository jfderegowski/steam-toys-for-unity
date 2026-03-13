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

        #endregion

        #region Inspector Serialized Fields
        
        [Header("Steam Inventory Item Data")]
        [SerializeField] private uint _appId;
        [SerializeField] private ulong _itemDefId;
        [SerializeField] private ItemType _type;
        [SerializeField] private string _name;
        [SerializeField, TextArea] private string _description;
        [SerializeField] private string _displayType;
        [SerializeField] private Url _iconUrl;
        [SerializeField] private Url _iconUrlLarge;
        [SerializeField] private Color32 _backgroundColor;
        [SerializeField] private Color32 _nameColor;
        [SerializeField] private bool _marketable;
        [SerializeField] private bool _tradable;
        [SerializeField] private Bundle _bundle;
        [SerializeField] private Promo _promo;
        [SerializeField] private SteamDateTime _dropStartTime;
        [SerializeField] private Exchange _exchange;
        [SerializeField] private Price _price;

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