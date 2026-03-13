using System;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem.ExchangeVariable.Materials
{
    [Serializable]
    public class ItemMaterial : Material
    {
        public Item Item
        {
            get => GetItem();
            set => SetItem(value);
        }

        public uint Quantity
        {
            get => GetQuantity();
            set => SetQuantity(value);
        }
        
        [SerializeField] private Item _item;
        [SerializeField] private uint _quantity;

        public Item GetItem() => _item;

        public void SetItem(Item value) => _item = value;
        
        public uint GetQuantity() => _quantity;
        
        public void SetQuantity(uint value) => _quantity = value;

        public override string ToSteamString()
        {
            if (Item == null)
                throw new InvalidOperationException("Item cannot be null for ItemMaterial.");
            
            if (Quantity == 0)
                throw new InvalidOperationException("Quantity must be greater than zero for ItemMaterial.");
            
            return $"{Item.ItemDefId}x{Quantity}";
        }
    }
}