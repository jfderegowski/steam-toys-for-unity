using System;
using System.Collections.Generic;
using fefek5.Toys.Runtime;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// Tags of any category, edited as a dictionary of category and value. The tags of every definition that has
    /// them unless its class names another <see cref="Tags"/>.
    /// </summary>
    [Serializable]
    public class DefaultTags : Tags
    {
        [SerializeField, Tooltip("Steam tags by category, e.g. rarity: legendary. Each value is sent as category:value.")]
        private SerializableDictionary<string, string> _tags = new();

        #region Tags

        public override IEnumerable<string> Keys => _tags.Keys;

        public override bool TryGetValue(string tagName, out string tagValue) =>
            _tags.TryGetValue(tagName, out tagValue);

        public override bool TrySetValue(string tagName, string tagValue)
        {
            if (!ContainsKey(tagName))
                return false;
            
            _tags[tagName] = tagValue;
            
            return true;
        }

        #endregion
    }
}