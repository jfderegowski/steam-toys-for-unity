using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using fefek5.Toys.Runtime;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.Converters;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// The tags of an ItemDef or an owned item: Steam <c>category:value</c> pairs, one value per category,
    /// kept as strings.
    /// <c>Rarity.Legendary</c> as <c>rarity:legendary</c>.
    /// <para>
    /// <see cref="DefaultTags"/> holds any category. A subclass can hold a fixed set instead by overriding
    /// </para>
    /// <see href="https://partner.steamgames.com/doc/features/inventory/itemtags"/>
    /// </summary>
    [Serializable]
    [JsonConverter(typeof(TagsConverter))]
    public abstract class Tags : IReadOnlyDictionary<string, string>
    {
        [SerializeField] private SerializableDictionary<string, string> _stringTags;

        #region IReadOnlyDictionary

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            foreach (var tagName in Keys)
                if (TryGetValue(tagName, out var tagValue))
                    yield return new KeyValuePair<string, string>(tagName, tagValue);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int Count => Keys.Count();

        public bool ContainsKey(string tagName) => Keys.Contains(tagName);

        public bool TryGetValue(string tagName, out string tagValue) =>
            _stringTags.TryGetValue(tagName, out tagValue) || TryGetTagValue(tagName, out tagValue);

        public string this[string tagName]
        {
            get =>
                TryGetValue(tagName, out var tagValue)
                    ? tagValue
                    : throw new KeyNotFoundException(tagName);
            set
            {
                if (!TrySetValue(tagName, value))
                    throw new ArgumentException($"{tagName}:{value} is not held by {GetType().Name}.");
            }
        }

        public IEnumerable<string> Keys => _stringTags.Keys.Concat(GetTagNames());

        public IEnumerable<string> Values => Keys.Select(key => this[key]);

        #endregion

        public bool TrySetValue(string tagName, string tagValue)
        {
            if (!_stringTags.ContainsKey(tagName))
                return TrySetTagValue(tagName, tagValue);
            
            _stringTags[tagName] = tagValue;
            
            return true;
        }

        /// <summary>Whether these tags hold the pair <c>tagName:tagValue</c>, e.g. <c>rarity:legendary</c>.</summary>
        public bool Contains(string tagName, string tagValue) =>
            TryGetValue(tagName, out var held) && held == tagValue;

        /// <summary>Whether these tags hold <paramref name="value"/>, e.g. <c>Rarity.Legendary</c> as <c>rarity:legendary</c>.</summary>
        public bool Contains<TEnum>(TEnum value) where TEnum : Enum =>
            Contains(GetTagName<TEnum>(), GetTagValue(value));

        #region Enum Tags

        /// <summary>The Steam category of the enum <typeparamref name="TEnum"/>, e.g. <c>rarity</c> for <c>Rarity</c>.</summary>
        public static string GetTagName<TEnum>() where TEnum : Enum => typeof(TEnum).Name.ToSnakeCase();

        /// <summary>The Steam value of <paramref name="value"/>, e.g. <c>legendary</c> for <c>Rarity.Legendary</c>.</summary>
        public static string GetTagValue<TEnum>(TEnum value) where TEnum : Enum => value.ToString().ToSnakeCase();

        /// <summary>
        /// Reads the Steam value <paramref name="tagValue"/>, e.g. <c>legendary</c>, into the value of
        /// <typeparamref name="TEnum"/> it names.
        /// </summary>
        public static bool TryParseTagValue<TEnum>(string tagValue, out TEnum value) where TEnum : Enum
        {
            value = default;

            if (string.IsNullOrEmpty(tagValue))
                return false;

            var normalized = tagValue.ToSnakeCase();

            foreach (var name in Enum.GetNames(typeof(TEnum)))
            {
                if (name.ToSnakeCase() != normalized)
                    continue;

                value = (TEnum)Enum.Parse(typeof(TEnum), name);
                return true;
            }

            return false;
        }

        #endregion

        #region Abstract

        public abstract IEnumerable<string> GetTagNames();
        
        public abstract bool TryGetTagValue(string tagName, out string tagValue);
        
        public abstract bool TrySetTagValue(string tagName, string tagValue);

        #endregion
    }
}
