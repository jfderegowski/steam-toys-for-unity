using System;
using System.Collections.Generic;
using SteamToys.Runtime.InventorySystem.Converters;

namespace SteamToys.Runtime.InventorySystem
{
    [Serializable]
    public abstract class Tag
    {
        public abstract string GetName();

        public abstract string GetValue();

        public abstract bool SetValue(string value);

        public abstract Type GetValueType();

        public override string ToString() => ToSteamString();
        
        public virtual string ToSteamString() => $"{GetName()}:{GetValue()}";

        /// <summary>
        /// Reads a single Steam tag pair (e.g. <c>"quality:unique"</c>) into this instance.
        /// The inverse of <see cref="ToSteamString"/>. Returns false and leaves the tag
        /// untouched when the string is malformed, names a different tag, or carries a
        /// value this tag cannot represent.
        /// </summary>
        public abstract bool FromSteamString(string value);

        public override bool Equals(object obj)
        {
            if (obj is null) 
                return false;
            
            if (obj is Tag tag)
                return tag.ToSteamString() == ToSteamString();
            
            if (obj is string str)
                return str == ToSteamString();
            
            return false;
        }

        public override int GetHashCode() => HashCode.Combine(GetName(), GetValue());

        public static explicit operator string(Tag tag)
        {
            if (tag is null)
                throw new ArgumentNullException(nameof(tag));

            return tag.ToSteamString();
        }

        public static bool operator ==(Tag tag, string value)
        {
            if (tag is null)
                return value is null;

            return tag.ToSteamString() == value;
        }

        public static bool operator !=(Tag tag, string value) => !(tag == value);

        public static bool operator ==(string value, Tag tag) => tag == value;

        public static bool operator !=(string value, Tag tag) => !(tag == value);
    }

    [Serializable]
    public class Tag<TEnum> : Tag where TEnum : Enum
    {
        public TEnum Value;

        public sealed override Type GetValueType() => typeof(TEnum);

        public override string GetName() => typeof(TEnum).Name.ToSnakeCase();

        public override string GetValue() => Value.ToString().ToSnakeCase();

        public void SetValue(TEnum value) => Value = value;

        public override bool SetValue(string value)
        {
            if (!TryParse(value, out var parsed))
                return false;

            Value = parsed;
            return true;
        }

        public override bool FromSteamString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var separatorIndex = value.IndexOf(':');
            if (separatorIndex < 0)
                return false;

            // A tag only accepts its own pair, so "rarity:rare" is rejected by Tag<Quality>.
            var tagName = value.Substring(0, separatorIndex).Trim();
            if (tagName.ToSnakeCase() != GetName())
                return false;

            return SetValue(value.Substring(separatorIndex + 1).Trim());
        }

        // Redeclared only to satisfy CS0660/CS0661 for the operators below; the value based
        // implementation lives in Tag.
        public override bool Equals(object obj) => base.Equals(obj);

        public override int GetHashCode() => base.GetHashCode();

        public static implicit operator TEnum(Tag<TEnum> tag)
        {
            if (tag is null)
                throw new ArgumentNullException(nameof(tag));

            return tag.Value;
        }

        public static implicit operator Tag<TEnum>(TEnum value) => new Tag<TEnum> { Value = value };

        // Lives here rather than on Tag: only the closed generic knows which enum to build.
        // Returns null when the string does not describe this tag.
        public static explicit operator Tag<TEnum>(string value)
        {
            if (value is null)
                throw new ArgumentNullException(nameof(value));

            var tag = new Tag<TEnum>();

            return tag.FromSteamString(value) ? tag : null;
        }

        // Without this exact-match pair, comparing two tags is a CS0034 ambiguity: both
        // operands can implicitly convert to TEnum, so neither enum overload wins.
        public static bool operator ==(Tag<TEnum> left, Tag<TEnum> right) => ReferenceEquals(left, right);

        public static bool operator !=(Tag<TEnum> left, Tag<TEnum> right) => !ReferenceEquals(left, right);

        public static bool operator ==(Tag<TEnum> tag, TEnum value)
        {
            if (tag is null)
                return false;

            return EqualityComparer<TEnum>.Default.Equals(tag.Value, value);
        }

        public static bool operator !=(Tag<TEnum> tag, TEnum value) => !(tag == value);

        public static bool operator ==(TEnum value, Tag<TEnum> tag) => tag == value;

        public static bool operator !=(TEnum value, Tag<TEnum> tag) => !(tag == value);

        private static bool TryParse(string value, out TEnum result)
        {
            result = default;

            if (string.IsNullOrEmpty(value))
                return false;

            var normalized = value.ToSnakeCase();

            foreach (var name in Enum.GetNames(typeof(TEnum)))
            {
                if (name.ToSnakeCase() != normalized)
                    continue;

                result = (TEnum)Enum.Parse(typeof(TEnum), name);
                return true;
            }

            return false;
        }
    }
}
