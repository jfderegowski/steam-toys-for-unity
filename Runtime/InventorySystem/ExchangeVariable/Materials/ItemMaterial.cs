using System;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem.ExchangeVariable.Materials
{
    [Serializable]
    public class ItemMaterial : Material
    {
        public ItemDef Item
        {
            get => GetItem();
            set => SetItem(value);
        }

        public uint Quantity
        {
            get => GetQuantity();
            set => SetQuantity(value);
        }
        
        [SerializeField] private ItemDef _item;
        [SerializeField] private uint _quantity;

        public ItemDef GetItem() => _item;

        public void SetItem(ItemDef value) => _item = value;
        
        public override uint GetQuantity() => _quantity;
        
        public void SetQuantity(uint value) => _quantity = value;

        /// <summary>Empty: it is one item, not a tag.</summary>
        public override string GetTagName() => string.Empty;

        /// <summary>The ID of the item.</summary>
        public override string GetTagValue()
        {
            if (Item == null)
                throw new InvalidOperationException("Item cannot be null for ItemMaterial.");

            return Item.ItemDefId.ToString();
        }

        /// <summary>The item and how many, e.g. <c>Atom x1</c>.</summary>
        public override string ToString() => $"{(Item ? Item.name : "—")} x{Quantity}";
    }
}