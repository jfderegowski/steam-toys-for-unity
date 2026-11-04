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
    /// <see cref="DefaultTags"/> holds the categories added to it in the inspector or imported from Steam. A subclass
    /// can also hold a fixed set by overriding <see cref="GetTagNames"/>, <see cref="TryGetTagValue"/> and
    /// <see cref="TrySetTagValue"/>. <see cref="TrySetValue"/> only changes a tag that already exists.
    /// </para>
    /// <see href="https://partner.steamgames.com/doc/features/inventory/itemtags"/>
    /// </summary>
    [Serializable]
    [JsonConverter(typeof(TagsConverter))]
    public abstract class Tags : IReadOnlyDictionary<string, string>
    {
        [SerializeField] private SerializableDictionary<string, string> _stringTags = new();

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
            TryGetTagValue(tagName, out tagValue) || _stringTags.TryGetValue(tagName, out tagValue);

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

        public IEnumerable<string> Keys
        {
            get
            {
                var tagNames = GetTagNames();

                if (tagNames != null)
                    return tagNames.Union(_stringTags.Keys);

                return _stringTags.Keys;
            }
        }

        public IEnumerable<string> Values => Keys.Select(key => this[key]);

        #endregion

        public bool TrySetValue(string tagName, string tagValue)
        {
            if (TrySetTagValue(tagName, tagValue))
                return true;

            if (!_stringTags.ContainsKey(tagName))
                return false;

            _stringTags[tagName] = tagValue;
            return true;
        }

        /// <summary>
        /// Adds a new string tag. For the tags published on Steam only, through
        /// <see cref="TagsConverter.FromString(string, Tags, bool)"/>; everything else changes existing tags with
        /// <see cref="TrySetValue"/>.
        /// </summary>
        /// <returns>False if the tag name is empty or these tags already hold it.</returns>
        internal bool TryAddStringTag(string tagName, string tagValue)
        {
            if (string.IsNullOrEmpty(tagName) || GetTagNames()?.Contains(tagName) == true)
                return false;

            return _stringTags.TryAdd(tagName, tagValue);
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
