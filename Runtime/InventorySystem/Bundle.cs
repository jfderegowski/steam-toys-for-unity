using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.Converters;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    [Serializable]
    [JsonConverter(typeof(BundleElementConverter))]
    public class BundleElement
    {
        public Item Item
        {
            get => GetItem();
            set => SetItem(value);
        }
        
        public ulong ItemDefId
        {
            get => GetItemDefId();
            set => SetItemDefId(value);
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

        public ulong GetItemDefId() => Item.ItemDefId;

        public void SetItemDefId(ulong value) => Item.ItemDefId = value;

        public uint GetQuantity() => _quantity;

        public void SetQuantity(uint value) => _quantity = value;
    }

    [Serializable]
    [JsonConverter(typeof(BundleConverter))]
    public class Bundle : IList<BundleElement>
    {
        [SerializeField] private List<BundleElement> _elements = new();

        #region IList Implementation

        public IEnumerator<BundleElement> GetEnumerator() => _elements.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(BundleElement item) => _elements.Add(item);

        public void Clear() => _elements.Clear();

        public bool Contains(BundleElement item) => _elements.Contains(item);

        public void CopyTo(BundleElement[] array, int arrayIndex) => _elements.CopyTo(array, arrayIndex);

        public bool Remove(BundleElement item) => _elements.Remove(item);

        public int Count => _elements.Count;
        
        public bool IsReadOnly => false;

        public int IndexOf(BundleElement item) => _elements.IndexOf(item);

        public void Insert(int index, BundleElement item) => _elements.Insert(index, item);

        public void RemoveAt(int index) => _elements.RemoveAt(index);

        public BundleElement this[int index]
        {
            get => _elements[index];
            set => _elements[index] = value;
        }

        #endregion
    }
}