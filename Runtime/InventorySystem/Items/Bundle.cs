using System.Collections.Generic;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.Converters;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// ItemDef of type <c>bundle</c>: expands into all of its <see cref="Content"/> when granted or bought. Unlike a
    /// generator it does not pass tags on to the items it creates, so it has no tags of its own; its
    /// <see cref="TagGenerators"/> do apply to them.
    /// </summary>
    [CreateAssetMenu(fileName = "New Bundle", menuName = "SteamToys/Inventory/Bundle", order = 2)]
    public class Bundle : StoreItemDef
    {
        #region Constants

        private const uint MAX_DISCOUNT = 100;

        #endregion

        #region Properties

        /// <summary>
        /// Definitions granted by this bundle, with how many of each.
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

        public bool UseBundlePrice
        {
            get => GetUseBundlePrice();
            set => SetUseBundlePrice(value);
        }

        /// <summary>
        /// Percentage taken off the summed price of the contents. Ignored with <see cref="UseBundlePrice"/>.
        /// </summary>
        public uint PurchaseBundleDiscount
        {
            get => GetPurchaseBundleDiscount();
            set => SetPurchaseBundleDiscount(value);
        }

        #endregion

        #region Inspector Serialized Fields

        [Header("Bundle")]
        [SerializeField, Tooltip("Definitions granted by this bundle, with how many of each.")]
        private BundleContent _bundle = new();
        [SerializeField, Tooltip("Tag generators applied to every item this bundle creates.")]
        private List<TagGenerator> _tagGenerators = new();

        [Header("Bundle Pricing")]
        [SerializeField, Tooltip("If true, uses the bundle's own price instead of the sum of its contents.")]
        private bool _useBundlePrice;
        [SerializeField, Tooltip("Percentage discount on the summed price of the contents.")]
        private uint _purchaseBundleDiscount;

        #endregion

        #region Getters and Setters

        public override ItemType GetItemType() => ItemType.Bundle;

        public virtual BundleContent GetContents() => _bundle;

        public virtual void SetContents(BundleContent value) => _bundle = value;

        public virtual List<TagGenerator> GetTagGenerators() => _tagGenerators;

        public virtual void SetTagGenerators(List<TagGenerator> value) => _tagGenerators = value;

        public virtual bool GetUseBundlePrice() => _useBundlePrice;

        public virtual void SetUseBundlePrice(bool value) => _useBundlePrice = value;

        public virtual uint GetPurchaseBundleDiscount() => _purchaseBundleDiscount;

        public virtual void SetPurchaseBundleDiscount(uint value) => _purchaseBundleDiscount = value;

        #endregion

        #region Validation

        public override void CollectSchemaWarnings(List<string> warnings)
        {
            base.CollectSchemaWarnings(warnings);

            BundleContent.CollectSchemaWarnings(this, Content, warnings);

            if (PurchaseBundleDiscount > MAX_DISCOUNT)
                warnings.Add($"Purchase Bundle Discount is a percentage and cannot exceed {MAX_DISCOUNT}.");

            if (PurchaseBundleDiscount > 0 && UseBundlePrice)
                warnings.Add("Purchase Bundle Discount is ignored when Use Bundle Price is on.");
        }

        #endregion

        #region Json

        public override void WriteJsonProperties(JsonWriter writer)
        {
            base.WriteJsonProperties(writer);

            writer.WriteBundleIfNotEmpty("bundle", Content);
            writer.WriteItemDefIdsIfNotEmpty("tag_generators", TagGenerators);
            writer.WriteBoolIfTrue("use_bundle_price", UseBundlePrice);
            writer.WriteUintIfNotZero("purchase_bundle_discount", PurchaseBundleDiscount);
        }

        #endregion
    }
}
