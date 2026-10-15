using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.Converters;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// ItemDef of type <c>tag_generator</c>: creates no items, it picks one value of a tag category by weight and
    /// applies it to items created by the bundles and generators that list it in their tag generators.
    /// <see href="https://partner.steamgames.com/doc/features/inventory/itemtags"/>
    /// </summary>
    public abstract class TagGenerator : ItemDef
    {
        #region Properties

        /// <summary>
        /// Category token of the generated tag, e.g. <c>"rarity"</c>.
        /// </summary>
        public string TagGeneratorName => GetTagGeneratorName();

        /// <summary>
        /// Tag values with their relative odds, e.g. <c>"legendary:1;common:9"</c>.
        /// </summary>
        public string TagGeneratorValues => GetTagGeneratorValues();

        #endregion

        #region Getters

        public override ItemType GetItemType() => ItemType.TagGenerator;

        public abstract string GetTagGeneratorName();

        public abstract string GetTagGeneratorValues();

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

    /// <summary>
    /// Tag generator for the <see cref="Tag{TEnum}"/> category of <typeparamref name="TEnum"/>, so the generated
    /// tags match the ones items declare with that enum.
    /// </summary>
    public class TagGenerator<TEnum> : TagGenerator where TEnum : Enum
    {
        #region Constants

        private const char VALUE_SEPARATOR = ';';
        private const char WEIGHT_SEPARATOR = ':';

        #endregion

        #region Properties

        public List<TagWeight<TEnum>> Values
        {
            get => GetValues();
            set => SetValues(value);
        }

        #endregion

        #region Inspector Serialized Fields

        [Header("Tag Generator")]
        [SerializeField, Tooltip("Tag values this generator picks from, with their relative odds.")]
        private List<TagWeight<TEnum>> _values = new();

        #endregion

        #region Getters and Setters

        public virtual List<TagWeight<TEnum>> GetValues() => _values;

        public virtual void SetValues(List<TagWeight<TEnum>> value) => _values = value;

        public override string GetTagGeneratorName() => new Tag<TEnum>().GetName();

        public override string GetTagGeneratorValues()
        {
            if (Values == null)
                return string.Empty;

            var stringBuilder = new StringBuilder();

            foreach (var weight in Values)
            {
                if (weight == null || weight.Weight == 0)
                    continue;

                if (stringBuilder.Length > 0)
                    stringBuilder.Append(VALUE_SEPARATOR);

                stringBuilder.Append(new Tag<TEnum> { Value = weight.Value }.GetValue())
                    .Append(WEIGHT_SEPARATOR)
                    .Append(weight.Weight);
            }

            return stringBuilder.ToString();
        }

        #endregion

        #region Validation

        public override void CollectSchemaWarnings(List<string> warnings)
        {
            base.CollectSchemaWarnings(warnings);

            if (Values == null || Values.Count == 0)
            {
                warnings.Add("Tag generator has no values.");
                return;
            }

            var seen = new HashSet<TEnum>();

            foreach (var weight in Values)
            {
                if (weight == null)
                    continue;

                if (weight.Weight == 0)
                    warnings.Add($"{weight.Value} has a weight of 0 and is skipped.");

                if (!seen.Add(weight.Value))
                    warnings.Add($"{weight.Value} is listed more than once.");
            }
        }

        #endregion
    }

    [Serializable]
    public class TagWeight<TEnum> where TEnum : Enum
    {
        public TEnum Value;

        [Tooltip("Relative odds of this value.")]
        public uint Weight = 1;
    }
}
