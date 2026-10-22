using System;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.Converters.ExchangeVariable;

namespace SteamToys.Runtime.InventorySystem.ExchangeVariable
{
    /// <summary>
    /// One material of an exchange recipe, written by <see cref="MaterialConverter"/> from its tag name, tag value and
    /// quantity.
    /// </summary>
    [Serializable]
    [JsonConverter(typeof(MaterialConverter))]
    public abstract class Material
    {
        /// <summary>The Steam category of the tag it matches, e.g. <c>rarity</c>; empty when it is one item.</summary>
        public abstract string GetTagName();

        /// <summary>The Steam value of the tag it matches, e.g. <c>legendary</c>, or the ID of its item.</summary>
        /// <exception cref="InvalidOperationException">When it has no value to write, e.g. no item.</exception>
        public abstract string GetTagValue();

        public abstract uint GetQuantity();
    }
}
