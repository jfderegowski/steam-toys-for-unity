using System;
using System.Collections.Generic;
using System.Text;
using fefek5.Toys.Runtime.Attributes;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.Converters;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// ItemDef of type <c>tag_generator</c>: creates no items, it picks one value of a tag category by weight and
    /// applies it to items created by the bundles and generators that list it in their tag generators. Each value is
    /// a <see cref="Tag"/>, so the category is the one its class names and every value has to be of the same one.
    /// <see href="https://partner.steamgames.com/doc/features/inventory/itemtags"/>
    /// </summary>
    [CreateAssetMenu(fileName = "New Tag Generator", menuName = "SteamToys/Inventory/Tag Generator", order = 5)]
    public sealed class TagGenerator : ItemDef
    {
        #region Constants

        private const char VALUE_SEPARATOR = ';';
        private const char WEIGHT_SEPARATOR = ':';

        #endregion

        #region Properties

        /// <summary>
        /// Category token of the generated tag, e.g. <c>"rarity"</c>: the one of the first value.
        /// </summary>
        public string TagGeneratorName => GetTagGeneratorName();

        /// <summary>
        /// Tag values with their relative odds, e.g. <c>"legendary:1;common:9"</c>.
        /// </summary>
        public string TagGeneratorValues => GetTagGeneratorValues();

        public List<TagWeight> Values
        {
            get => GetValues();
            set => SetValues(value);
        }

        #endregion

        #region Inspector Serialized Fields

        [Header("Tag Generator")]
        [SerializeField, Tooltip("Tag values this generator picks from, with their relative odds. All of one category.")]
        private List<TagWeight> _values = new();

        #endregion

        #region Getters and Setters

        public override ItemType GetItemType() => ItemType.TagGenerator;

        public List<TagWeight> GetValues() => _values;

        public void SetValues(List<TagWeight> value) => _values = value;

        public string GetTagGeneratorName()
        {
            if (Values == null)
                return string.Empty;

            foreach (var weight in Values)
            {
                if (weight?.Tag != null)
                    return weight.Tag.GetName();
            }

            return string.Empty;
        }

        public string GetTagGeneratorValues()
        {
            if (Values == null)
                return string.Empty;

            var stringBuilder = new StringBuilder();

            foreach (var weight in Values)
            {
                if (weight?.Tag == null || weight.Weight == 0)
                    continue;

                if (stringBuilder.Length > 0)
                    stringBuilder.Append(VALUE_SEPARATOR);

                stringBuilder.Append(weight.Tag.GetValue())
                    .Append(WEIGHT_SEPARATOR)
                    .Append(weight.Weight);
            }

            return stringBuilder.ToString();
        }

        #endregion

        #region Support

        public override bool SchemaSupportsTags() => false;

        public override bool SchemaSupportsDescription() => false;
        public override bool SchemaSupportsHidden() => false;
        public override bool SchemaSupportsExchange() => false;
        public override bool SchemaSupportsPromo() => false;
        public override bool SchemaSupportsDropStartTime() => false;
        public override bool SchemaSupportsGrantedManually() => false;
        public override bool SchemaSupportsDropInterval() => false;

        public override bool SchemaSupportsContent() => false;
        public override bool SchemaSupportsTagGenerators() => false;

        public override bool SchemaSupportsDisplayType() => false;
        public override bool SchemaSupportsIconUrl() => false;
        public override bool SchemaSupportsIconUrlLarge() => false;
        public override bool SchemaSupportsNameColor() => false;
        public override bool SchemaSupportsBackgroundColor() => false;
        public override bool SchemaSupportsStoreTags() => false;
        public override bool SchemaSupportsStoreImages() => false;
        public override bool SchemaSupportsStoreHidden() => false;
        public override bool SchemaSupportsPrice() => false;
        public override bool SchemaSupportsPriceCategory() => false;

        public override bool SchemaSupportsUseBundlePrice() => false;
        public override bool SchemaSupportsPurchaseBundleDiscount() => false;

        public override bool SchemaSupportsTradable() => false;
        public override bool SchemaSupportsMarketable() => false;
        public override bool SchemaSupportsGameOnly() => false;
        public override bool SchemaSupportsAutoStack() => false;
        public override bool SchemaSupportsPurchaseLimit() => false;
        public override bool SchemaSupportsContainerContentsGenerator() => false;

        public override bool SchemaSupportsDropWindow() => false;
        public override bool SchemaSupportsDropMaxPerWindow() => false;
        public override bool SchemaSupportsDropLimit() => false;

        #endregion

        #region Json

        public override void WriteJsonProperties(JsonWriter writer)
        {
            base.WriteJsonProperties(writer);

            writer.WriteIfNotEmpty("tag_generator_name", TagGeneratorName);
            writer.WriteIfNotEmpty("tag_generator_values", TagGeneratorValues);
        }

        #endregion
    }

    [Serializable]
    public class TagWeight
    {
        [SerializeReference, SelectType, Tooltip("The tag value, e.g. rarity: legendary.")]
        public Tag Tag;

        [Tooltip("Relative odds of this value.")]
        public uint Weight = 1;
    }
}
