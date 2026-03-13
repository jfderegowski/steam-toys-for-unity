using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.Converters.ExchangeVariable;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem.ExchangeVariable
{
    [Serializable]
    [JsonConverter(typeof(ExchangeConverter))]
    public class Exchange : IList<Recipe> 
    {
        [SerializeField] private List<Recipe> _recipes = new();

        #region IList Implementation

        public IEnumerator<Recipe> GetEnumerator() => _recipes.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(Recipe item) => _recipes.Add(item);

        public void Clear() => _recipes.Clear();

        public bool Contains(Recipe item) => _recipes.Contains(item);

        public void CopyTo(Recipe[] array, int arrayIndex) => _recipes.CopyTo(array, arrayIndex);

        public bool Remove(Recipe item) => _recipes.Remove(item);

        public int Count => _recipes.Count;

        public bool IsReadOnly => false;

        public int IndexOf(Recipe item) => _recipes.IndexOf(item);

        public void Insert(int index, Recipe item) => _recipes.Insert(index, item);

        public void RemoveAt(int index) => _recipes.RemoveAt(index);

        public Recipe this[int index]
        {
            get => _recipes[index];
            set => _recipes[index] = value;
        }

        #endregion

        public string ToSteamString()
        {
            if (_recipes == null || _recipes.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();

            for (int i = 0; i < _recipes.Count; i++)
            {
                if (i > 0)
                    sb.Append(",");

                sb.Append(_recipes[i].ToSteamString());
            }

            return sb.ToString();
        }
    }
}
