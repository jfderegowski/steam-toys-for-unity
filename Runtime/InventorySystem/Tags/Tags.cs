using System;
using System.Collections.Generic;

namespace SteamToys.Runtime.InventorySystem
{
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