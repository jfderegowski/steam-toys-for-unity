using System;

namespace SteamToys.Runtime.InventorySystem
{
    [Serializable]
    public abstract class Tags
    {
        public string TagsValue
        {
            get => GetTags();
            set => SetTags(value);
        }

        public string GetTags()
        {
            var tagNames = GetTagNames();
            
            var tags = string.Empty;
            
            foreach (var tagName in tagNames)
            {
                var tagValue = GetTag(tagName);
                if (!string.IsNullOrEmpty(tagValue))
                    tags += $"{tagName}:{tagValue};";
            }
            
            return tags.TrimEnd(';');
        }
        
        public void SetTags(string tags)
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

                SetTag(tagName, tagValue);
            }
        }
        
        public abstract string[] GetTagNames();

        public abstract string GetTag(string tagName);
        
        public abstract string SetTag(string tagName, string tagValue);
    }
}