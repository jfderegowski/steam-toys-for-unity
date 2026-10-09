using System;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem.ExchangeVariable
{
    [Serializable]
    public abstract class Material
    {
        public virtual string ToSteamString() => string.Empty;
    }
}