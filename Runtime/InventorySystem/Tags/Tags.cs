using System;
using fefek5.Toys.Runtime;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// Tags of any category, edited as a dictionary of category and value. The tags of every definition that has
    /// them unless its class names another <see cref="TagsDef"/>.
    /// </summary>
    [Serializable]
    public class Tags : TagsDef
    {
        [SerializeField, Tooltip("Steam tags by category, e.g. rarity: legendary. Each value is sent as category:value.")]
        private SerializableDictionary<string, string> _tags = new();

        public override SerializableDictionary<string, string> GetTags() => _tags ??= new SerializableDictionary<string, string>();
    }
}