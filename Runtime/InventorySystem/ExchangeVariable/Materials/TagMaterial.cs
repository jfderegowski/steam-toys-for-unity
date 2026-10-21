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
        /// <summary>The Steam category of <typeparamref name="TEnum"/>, e.g. <c>rarity</c>.</summary>
        public string TagName => Tags.GetTagName<TEnum>();

        public TEnum Value
        {
            get => GetValue();
            set => SetValue(value);
        }

        public uint Quantity
        {
            get => GetQuantity();
            set => SetQuantity(value);
        }

        [SerializeField] private TEnum _value;
        [SerializeField] private uint _quantity = 1;

        public TEnum GetValue() => _value;

        public void SetValue(TEnum value) => _value = value;

        /// <summary>Sets <see cref="Value"/> from its Steam value, e.g. <c>legendary</c>.</summary>
        /// <returns>False when <paramref name="tagValue"/> is not a value of <typeparamref name="TEnum"/>.</returns>
        public bool TrySetTagValue(string tagValue)
        {
            if (!Tags.TryParseTagValue(tagValue, out TEnum value))
                return false;

            _value = value;
            return true;
        }

        public uint GetQuantity() => _quantity;

        public void SetQuantity(uint value) => _quantity = value;

        public override string ToSteamString()
        {
            if (Quantity == 0)
                throw new InvalidOperationException("Quantity must be greater than zero for TagMaterial.");

            return $"{TagName}:{Tags.GetTagValue(Value)}*{Quantity}";
        }
    }
}
