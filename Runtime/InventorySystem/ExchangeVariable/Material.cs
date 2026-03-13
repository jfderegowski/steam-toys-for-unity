using System;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem.ExchangeVariable
{
    [Serializable]
    public class Material
    {
        public virtual string ToSteamString()
        {
            Debug.Log($"aaa");
            
            throw new NotImplementedException("ToSteamString must be implemented in derived classes.");
        }
    }
}