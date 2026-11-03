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
        public override IEnumerable<string> GetTagNames() => null;

        public override bool TryGetTagValue(string tagName, out string tagValue)
        {
            tagValue = null;
            return false;
        }
        
        public override bool TrySetTagValue(string tagName, string tagValue) => false;
    }
}