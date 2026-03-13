using System;
using System.Collections.Generic;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    [Serializable]
    public class StringTags : Tags
    {
        [SerializeField] private string _tags;

        public override string[] GetTagNames()
        {
            if (string.IsNullOrEmpty(_tags))
                return Array.Empty<string>();

            var tagNames = new List<string>();
            var pairs = _tags.Split(';');

            foreach (var pair in pairs)
            {
                var separatorIndex = pair.IndexOf(':');
                if (separatorIndex < 0)
                    continue;

                var tagName = pair.Substring(0, separatorIndex).Trim();
                tagNames.Add(tagName);
            }

            return tagNames.ToArray();
        }

        public override string GetTag(string tagName)
        {
            if (string.IsNullOrEmpty(_tags))
                return string.Empty;

            var pairs = _tags.Split(';');

            foreach (var pair in pairs)
            {
                var separatorIndex = pair.IndexOf(':');
                if (separatorIndex < 0)
                    continue;

                var currentTagName = pair.Substring(0, separatorIndex).Trim();
                if (currentTagName.Equals(tagName, StringComparison.OrdinalIgnoreCase))
                    return pair.Substring(separatorIndex + 1).Trim();
            }

            return string.Empty;
        }

        public override string SetTag(string tagName, string tagValue)
        {
            var tagsDict = new Dictionary<string, string>();
            var pairs = _tags.Split(';');

            foreach (var pair in pairs)
            {
                var separatorIndex = pair.IndexOf(':');
                if (separatorIndex < 0)
                    continue;

                var currentTagName = pair.Substring(0, separatorIndex).Trim();
                var currentTagValue = pair.Substring(separatorIndex + 1).Trim();
                tagsDict[currentTagName] = currentTagValue;
            }

            tagsDict[tagName] = tagValue;

            _tags = string.Empty;
            foreach (var kvp in tagsDict)
                _tags += $"{kvp.Key}:{kvp.Value};";

            return _tags.TrimEnd(';');
        }
    }
}