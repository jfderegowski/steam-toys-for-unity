using System.Collections.Generic;
using fefek5.Toys.Runtime;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.Converters;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// An ItemDef players see and can buy: an item or a bundle. Holds the presentation and Item Store properties;
    /// Steam never prices a generator.
    /// </summary>
    public abstract class StoreItemDef : GrantableItemDef
    {
        #region Constants

        private const string PRICE_CATEGORY_VERSION = "1;";
        private const string PRICE_CATEGORY_CURRENCY = "VLV";

        #endregion

        #region Properties

        public string DisplayType
        {
            get => GetDisplayType();
            set => SetDisplayType(value);
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

        public bool StoreHidden
        {
            get => GetStoreHidden();
            set => SetStoreHidden(value);
        }

        #endregion

        #region Inspector Serialized Fields

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

        [Header("Item Store")]
        [SerializeField, Tooltip("Tags used to categorize and filter the item in the Item Store.")]
        private List<string> _storeTags = new();
        [SerializeField, Tooltip("Images shown on the item's Item Store detail page.")]
        private List<Url> _storeImages = new();
        [SerializeField, Tooltip("Whether this item is hidden from the Item Store.")]
        private bool _storeHidden;

        [Header("Pricing")]
        [SerializeField, Tooltip("Price information for the item.")]
        private Price _price;
        [SerializeField, Tooltip("Valve price tier in VLV units (100 is about $0.99). Replaces the price when set.")]
        private HasValue<uint> _priceCategory;

        #endregion

        #region Getters and Setters

        public virtual string GetDisplayType() => _displayType;

        public virtual void SetDisplayType(string value) => _displayType = value;

        public virtual Color32 GetNameColor() => _nameColor;

        public virtual void SetNameColor(Color32 value) => _nameColor = value;

        public virtual Color32 GetBackgroundColor() => _backgroundColor;

        public virtual void SetBackgroundColor(Color32 value) => _backgroundColor = value;

        public virtual Url GetIconUrl() => _iconUrl;

        public virtual void SetIconUrl(Url value) => _iconUrl = value;

        public virtual Url GetIconUrlLarge() => _iconUrlLarge;

        public virtual void SetIconUrlLarge(Url value) => _iconUrlLarge = value;

        public virtual List<string> GetStoreTags() => _storeTags;

        public virtual void SetStoreTags(List<string> value) => _storeTags = value;

        public virtual List<Url> GetStoreImages() => _storeImages;

        public virtual void SetStoreImages(List<Url> value) => _storeImages = value;

        public virtual Price GetPrice() => _price;

        public virtual void SetPrice(Price value) => _price = value;

        public virtual HasValue<uint> GetPriceCategory() => _priceCategory;

        public virtual void SetPriceCategory(HasValue<uint> value) => _priceCategory = value;

        public virtual bool GetStoreHidden() => _storeHidden;

        public virtual void SetStoreHidden(bool value) => _storeHidden = value;

        #endregion

        #region Validation

        public override void CollectSchemaWarnings(List<string> warnings)
        {
            base.CollectSchemaWarnings(warnings);

            if (PriceCategory.hasValue && !string.IsNullOrEmpty(Price?.ToSteamString()))
                warnings.Add("Price and Price Category are both set: only the price category is exported.");

            if (Price != null && Price.RegularPrices.Count == 0 && Price.Promotions.Count > 0)
                warnings.Add("Price has sales but no regular price, which Steam requires first.");
        }

        #endregion

        #region Json

        public override void WriteJsonProperties(JsonWriter writer)
        {
            base.WriteJsonProperties(writer);

            writer.WriteIfNotEmpty("display_type", DisplayType);
            writer.WriteColorIfSet("name_color", NameColor);
            writer.WriteColorIfSet("background_color", BackgroundColor);
            writer.WriteUrlIfNotEmpty("icon_url", IconUrl);
            writer.WriteUrlIfNotEmpty("icon_url_large", IconUrlLarge);
            writer.WriteListIfNotEmpty("store_tags", StoreTags);
            writer.WriteListIfNotEmpty("store_images", StoreImages);
            writer.WriteBoolIfTrue("store_hidden", StoreHidden);

            // Steam keeps both and charges the price, so only one is written: the category, which replaces the price.
            if (PriceCategory.hasValue)
                writer.WriteIfNotEmpty("price_category", PRICE_CATEGORY_VERSION + PRICE_CATEGORY_CURRENCY + PriceCategory.value);
            else
                writer.WritePriceIfNotEmpty("price", Price);
        }

        #endregion
    }
}
