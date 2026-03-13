using System;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem.ExchangeVariable.Materials
{
    [Serializable]
    public class TagMaterial<TTags> : Material where TTags : Tags
    {
        public TTags Tags
        {
            get => GetTags();
            set => SetTags(value);
        }

        [SerializeField] private TTags _tags;
        [SerializeField] private uint _quantity;
        
        public TTags GetTags() => _tags;
        
        public void SetTags(TTags value) => _tags = value;

        public uint Quantity
        {
            get => GetQuantity();
            set => SetQuantity(value);
        }

        public uint GetQuantity() => _quantity;

        public void SetQuantity(uint value) => _quantity = value;

        public override string ToSteamString()
        {
            // Steam expects tag-based materials in the format "tagname:tagvaluexQuantity".
            // Our Tags object can contain multiple tag pairs ("a:1;b:2"); use the first pair for exchange format.
            if (Tags == null)
                return string.Empty;

            var tagsStr = Tags.GetTags();
            if (string.IsNullOrWhiteSpace(tagsStr))
                return string.Empty;

            // Use the first tag pair (separated by ';')
            var firstPair = tagsStr.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            if (string.IsNullOrEmpty(firstPair))
                return string.Empty;

            return $"{firstPair}x{Quantity}";
        }
    }
}