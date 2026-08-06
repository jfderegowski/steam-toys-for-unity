using System;
using System.Collections.Generic;
using SteamToys.Runtime.InventorySystem.Converters;

namespace SteamToys.Runtime.InventorySystem.NewTagsLogic
{
    [Serializable]
    public abstract class Tag
    {
        public abstract string GetName();

        public abstract string GetValue();

        public abstract bool SetValue(string value);

        public abstract Type GetValueType();
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

    [Serializable]
    public abstract class Tags
    {
        public string TagsValue
        {
            get => GetTagsString();
            set => SetTagsString(value);
        }

        public abstract HashSet<Tag> GetTags();

        public TEnum GetValue<TEnum>() where TEnum : Enum
        {
            var tags = GetTags();

            foreach (var tag in tags)
            {
                if (tag != null && tag.GetValueType() == typeof(TEnum))
                    return ((Tag<TEnum>)tag).Value;
            }

            return default;
        }

        public string GetValue(string tagName)
        {
            var tags = GetTags();

            foreach (var tag in tags)
            {
                if (tag != null && tag.GetName() == tagName)
                    return tag.GetValue();
            }

            return null;
        }

        public bool SetValue<TEnum>(TEnum value) where TEnum : Enum
        {
            var tags = GetTags();

            foreach (var tag in tags)
            {
                if (tag == null || tag.GetValueType() != typeof(TEnum))
                    continue;

                ((Tag<TEnum>)tag).Value = value;
                return true;
            }

            return false;
        }

        public bool SetValue(string tagName, string value)
        {
            var tags = GetTags();

            foreach (var tag in tags)
            {
                if (tag != null && tag.GetName() == tagName)
                    return tag.SetValue(value);
            }

            return false;
        }

        public string GetTagsString()
        {
            var tags = GetTags();

            var result = string.Empty;

            foreach (var tag in tags)
            {
                if (tag == null)
                    continue;

                var tagValue = tag.GetValue();
                if (!string.IsNullOrEmpty(tagValue))
                    result += $"{tag.GetName()}:{tagValue};";
            }

            return result.TrimEnd(';');
        }

        public void SetTagsString(string tags)
        {
            if (string.IsNullOrEmpty(tags))
                return;

            var pairs = tags.Split(';');

            foreach (var pair in pairs)
            {
                var separatorIndex = pair.IndexOf(':');
                if (separatorIndex < 0)
                    continue;

                var tagName = pair.Substring(0, separatorIndex).Trim();
                var tagValue = pair.Substring(separatorIndex + 1).Trim();

                SetValue(tagName, tagValue);
            }
        }
    }
}