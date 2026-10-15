using System;
using System.Collections.Generic;
using System.Linq;
using fefek5.Toys.Runtime;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// The tags of an ItemDef or an owned item: Steam <c>category:value</c> pairs, one value per category, kept as
    /// strings. An enum is only a helper for them: <see cref="GetValue{TEnum}"/> and <see cref="SetValue{TEnum}"/>
    /// read and write the category named after the enum, as <see cref="Tag{TEnum}"/> names it, e.g.
    /// <c>Rarity.Legendary</c> as <c>rarity:legendary</c>.
    /// <para>
    /// <see cref="Tags"/> holds any category. A subclass can hold a fixed set instead by overriding
    /// <see cref="CanHold"/>.
    /// </para>
    /// <see href="https://partner.steamgames.com/doc/features/inventory/itemtags"/>
    /// </summary>
    [Serializable]
    public abstract class TagsDef
    {
        #region Constants

        private const char PAIR_SEPARATOR = ';';
        private const char CATEGORY_SEPARATOR = ':';

        #endregion

        /// <summary>
        /// The tags by category.
        /// </summary>
        public abstract SerializableDictionary<string, string> GetTags();

        /// <summary>
        /// Whether this tags type takes the category. Any non-empty category by default.
        /// </summary>
        public virtual bool CanHold(string category) => !string.IsNullOrWhiteSpace(category);

        #region Values

        public string this[string category]
        {
            get => GetValue(category);
            set => SetValue(category, value);
        }

        public int Count => GetTags().Count;

        /// <summary>The value of the category, or null without one.</summary>
        public string GetValue(string category) =>
            category != null && GetTags().TryGetValue(category, out var value) ? value : null;

        public bool TryGetValue(string category, out string value) => (value = GetValue(category)) != null;

        /// <summary>
        /// Sets the value of the category, or removes the category for a null or empty value. False when this tags
        /// type does not take the category.
        /// </summary>
        public virtual bool SetValue(string category, string value)
        {
            if (!CanHold(category))
                return false;

            if (string.IsNullOrEmpty(value))
                GetTags().Remove(category);
            else
                GetTags()[category] = value;

            return true;
        }

        public bool Remove(string category) => category != null && GetTags().Remove(category);

        public void Clear() => GetTags().Clear();

        public bool Has(string category, string value) => GetValue(category) == value && value != null;

        #endregion

        #region Enum Helpers

        /// <summary>The category <typeparamref name="TEnum"/> stands for, e.g. <c>rarity</c> for <c>Rarity</c>.</summary>
        public static string GetCategory<TEnum>() where TEnum : Enum => new Tag<TEnum>().GetName();

        public bool TryGetValue<TEnum>(out TEnum value) where TEnum : Enum
        {
            var tag = new Tag<TEnum>();
            var found = tag.SetValue(GetValue(GetCategory<TEnum>()));

            value = tag.Value;
            return found;
        }

        /// <summary>The value of the <typeparamref name="TEnum"/> category, or its default without one.</summary>
        public TEnum GetValue<TEnum>() where TEnum : Enum => TryGetValue<TEnum>(out var value) ? value : default;

        public bool SetValue<TEnum>(TEnum value) where TEnum : Enum =>
            SetValue(GetCategory<TEnum>(), new Tag<TEnum> { Value = value }.GetValue());

        public bool Remove<TEnum>() where TEnum : Enum => Remove(GetCategory<TEnum>());

        public bool Has<TEnum>(TEnum value) where TEnum : Enum =>
            Has(GetCategory<TEnum>(), new Tag<TEnum> { Value = value }.GetValue());

        #endregion

        #region Steam String

        /// <summary>The Steam <c>tags</c> string, e.g. <c>"rarity:legendary;wear:field_tested"</c>.</summary>
        public string ToSteamString() =>
            string.Join(PAIR_SEPARATOR, GetTags()
                .Where(tag => !string.IsNullOrEmpty(tag.Key) && !string.IsNullOrEmpty(tag.Value))
                .Select(tag => tag.Key + CATEGORY_SEPARATOR + tag.Value));

        /// <summary>Replaces every tag with the ones of a Steam <c>tags</c> string.</summary>
        /// <returns>The pairs left out: malformed, or of a category this tags type does not take.</returns>
        public List<string> SetSteamString(string tags)
        {
            Clear();

            return MergeSteamString(tags);
        }

        /// <summary>
        /// Takes the pairs of a Steam <c>tags</c> string over the tags held, e.g. an owned item's own tags over the ones
        /// of its definition.
        /// </summary>
        /// <returns>The pairs left out: malformed, or of a category this tags type does not take.</returns>
        public List<string> MergeSteamString(string tags)
        {
            var notHeld = new List<string>();

            foreach (var pair in (tags ?? string.Empty).Split(PAIR_SEPARATOR, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = pair.Trim();
                var separator = trimmed.IndexOf(CATEGORY_SEPARATOR);

                var held = separator > 0 && separator < trimmed.Length - 1 &&
                           SetValue(trimmed[..separator].Trim(), trimmed[(separator + 1)..].Trim());

                if (!held && trimmed.Length > 0)
                    notHeld.Add(trimmed);
            }

            return notHeld;
        }

        public override string ToString() => ToSteamString();

        #endregion
    }
}
