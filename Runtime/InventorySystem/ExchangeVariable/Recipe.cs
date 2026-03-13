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
    [JsonConverter(typeof(RecipeConverter))]
    public class Recipe : IList<Material> 
    {
        [SerializeReference] private List<Material> _materials = new();

        #region IList Implementation

        public IEnumerator<Material> GetEnumerator() => _materials.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(Material item) => _materials.Add(item);

        public void Clear() => _materials.Clear();

        public bool Contains(Material item) => _materials.Contains(item);

        public void CopyTo(Material[] array, int arrayIndex) => _materials.CopyTo(array, arrayIndex);

        public bool Remove(Material item) => _materials.Remove(item);

        public int Count => _materials.Count;

        public bool IsReadOnly => false;

        public int IndexOf(Material item) => _materials.IndexOf(item);

        public void Insert(int index, Material item) => _materials.Insert(index, item);

        public void RemoveAt(int index) => _materials.RemoveAt(index);

        public Material this[int index]
        {
            get => _materials[index];
            set => _materials[index] = value;
        }

        #endregion

        public string ToSteamString()
        {
            if (_materials == null || _materials.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();

            for (int i = 0; i < _materials.Count; i++)
            {
                if (i > 0)
                    sb.Append(";");

                var material = _materials[i];
                
                sb.Append(material.ToSteamString());
            }

            return sb.ToString();
        }
    }
}