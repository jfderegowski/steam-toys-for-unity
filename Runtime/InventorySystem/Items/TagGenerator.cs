using System;
using System.Collections.Generic;
using fefek5.Toys.Runtime;
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
    [JsonConverter(typeof(TagGeneratorConverter))]
    public abstract class TagGenerator : ItemDef
    {
        #region Properties

        /// <summary>
        /// Category token of the generated tag, e.g. <c>"rarity"</c>: the one of the first value.
        /// </summary>
        public abstract string TagName { get; }

        /// <summary>
        /// Tag values with their relative odds, e.g. <c>"legendary:1;common:9"</c>.
        /// </summary>
        public abstract Dictionary<string, uint> TagValuesAndChances { get; }

        #endregion

        /// <summary>
        /// Replaces the tag values and their odds with <paramref name="tagValuesAndChances"/>, given as Steam values,
        /// e.g. <c>legendary</c>.
        /// </summary>
        /// <returns>The values left out: not values of this category, or already set.</returns>
        public abstract List<string> SetTagValuesAndChances(IReadOnlyDictionary<string, uint> tagValuesAndChances);

        #region Getters and Setters

        public override ItemType GetItemType() => ItemType.TagGenerator;

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
    }

    public class TagGenerator<TEnum> : TagGenerator where TEnum : Enum
    {
        [SerializeField] private SerializableDictionary<TEnum, uint> _tagValuesAndChances = new();

        public override string TagName => typeof(TEnum).Name.ToSnakeCase();

        public override Dictionary<string, uint> TagValuesAndChances
        {
            get
            {
                var tagValuesAndChances = new Dictionary<string, uint>();

                foreach (var (tagValue, tagChance) in _tagValuesAndChances) 
                    tagValuesAndChances[tagValue.ToString()] = tagChance;
                
                return tagValuesAndChances;
            }
        }

        public override List<string> SetTagValuesAndChances(IReadOnlyDictionary<string, uint> tagValuesAndChances)
        {
            var notHeld = new List<string>();

            _tagValuesAndChances.Clear();

            foreach (var (tagValue, tagChance) in tagValuesAndChances)
            {
                // Tag<TEnum> reads the value, so it takes the same values the tags of items do.
                var tag = new Tag<TEnum>();

                if (!tag.SetValue(tagValue) || !_tagValuesAndChances.TryAdd(tag.Value, tagChance))
                    notHeld.Add(tagValue);
            }

            return notHeld;
        }
    }
}
