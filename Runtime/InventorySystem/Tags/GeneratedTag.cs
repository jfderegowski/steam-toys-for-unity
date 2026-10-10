using System;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// A tag whose value a <see cref="TagGenerator{TEnum}"/> picks for each created item. It is left out of the
    /// ItemDef's <c>tags</c>, but reads from an item's tags like any <see cref="Tag{TEnum}"/>, so the value only
    /// means something on a <see cref="Tags"/> filled from an item instance, never on the ItemDef asset.
    /// </summary>
    [Serializable]
    public class GeneratedTag<TEnum> : Tag<TEnum> where TEnum : Enum
    {
        public override bool IsGenerated => true;

        // Redeclared only to satisfy CS0660/CS0661 alongside the inherited operators.
        public override bool Equals(object obj) => base.Equals(obj);

        public override int GetHashCode() => base.GetHashCode();

        public static implicit operator GeneratedTag<TEnum>(TEnum value) => new() { Value = value };

        // Returns null when the string does not describe this tag.
        public static explicit operator GeneratedTag<TEnum>(string value)
        {
            if (value is null)
                throw new ArgumentNullException(nameof(value));

            return FromSteamJson(value);
        }

        /// <summary>
        /// Reads a single Steam tag pair (e.g. <c>"rarity:legendary"</c>) into a new generated tag.
        /// Returns null when the string is malformed, names a different tag, or carries a
        /// value this enum cannot represent.
        /// </summary>
        public new static GeneratedTag<TEnum> FromSteamJson(string value)
        {
            var tag = Tag<TEnum>.FromSteamJson(value);

            return tag == null ? null : new GeneratedTag<TEnum> { Value = tag.Value };
        }
    }
}
