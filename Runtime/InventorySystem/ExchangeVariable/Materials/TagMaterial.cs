using System;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem.ExchangeVariable.Materials
{
    /// <summary>
    /// Exchange material matching any item that carries one tag value, written as <c>"tagname:tagvalue*quantity"</c>,
    /// e.g. <c>"rarity:legendary*3"</c>. Works for generated tags too, since it matches what items carry.
    /// Subclass it per enum (e.g. <c>RarityMaterial : TagMaterial&lt;Rarity&gt;</c>) to offer it in recipes.
    /// </summary>
    [Serializable]
    public class TagMaterial<TEnum> : Material where TEnum : Enum
    {
        public Tag<TEnum> Tag
        {
            get => GetTag();
            set => SetTag(value);
        }

        public uint Quantity
        {
            get => GetQuantity();
            set => SetQuantity(value);
        }

        [SerializeField] private Tag<TEnum> _tag = new();
        [SerializeField] private uint _quantity = 1;

        public Tag<TEnum> GetTag() => _tag;

        public void SetTag(Tag<TEnum> value) => _tag = value;

        public uint GetQuantity() => _quantity;

        public void SetQuantity(uint value) => _quantity = value;

        public override string ToSteamString()
        {
            if (Tag is null)
                throw new InvalidOperationException("Tag cannot be null for TagMaterial.");

            if (Quantity == 0)
                throw new InvalidOperationException("Quantity must be greater than zero for TagMaterial.");

            return $"{Tag}*{Quantity}";
        }
    }
}
