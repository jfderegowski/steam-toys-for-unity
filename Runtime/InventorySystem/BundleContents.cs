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
        public GrantableItemDef Item
        {
            get => GetItem();
            set => SetItem(value);
        }
        
        public ulong ItemDefId
        {
            get => GetItemDefId();
            set => SetItemDefId(value);
        }

        /// <summary>
        /// How many are granted in a <see cref="Bundle"/>, or the relative odds in a generator.
        /// </summary>
        public uint Quantity
        {
            get => GetQuantity();
            set => SetQuantity(value);
        }
        
        public bool HasItem => Item;

        [SerializeField] private GrantableItemDef _item;
        [SerializeField] private uint _quantity;

        public GrantableItemDef GetItem() => _item;
        
        public void SetItem(GrantableItemDef value) => _item = value;

        public ulong GetItemDefId() => Item.ItemDefId;

        public void SetItemDefId(ulong value) => Item.ItemDefId = value;

        public uint GetQuantity() => _quantity;

        public void SetQuantity(uint value) => _quantity = value;
    }

    [Serializable]
    [JsonConverter(typeof(BundleContentsConverter))]
    public class BundleContents : IList<BundleElement>
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

        #region Validation

        /// <summary>
        /// Adds a message for every entry Steam would reject in the contents of <paramref name="owner"/>.
        /// </summary>
        public static void CollectSchemaWarnings(ItemDef owner, BundleContents contents, List<string> warnings)
        {
            var amountName = owner is Bundle ? "quantity" : "weight";

            if (contents == null || contents.Count == 0)
            {
                warnings.Add($"{owner.Type} has no contents.");
                return;
            }

            for (var i = 0; i < contents.Count; i++)
            {
                var element = contents[i];

                if (element is not { HasItem: true })
                    warnings.Add($"Contents entry {i} has no item and is skipped.");
                else if (element.Item == owner)
                    warnings.Add($"Contents entry {i} references this {owner.Type} itself.");

                if (element != null && element.Quantity == 0)
                    warnings.Add($"Contents entry {i} has a {amountName} of 0.");
            }
        }

        #endregion
    }
}