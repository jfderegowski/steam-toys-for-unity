using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.Converters;
using SteamToys.Runtime.InventorySystem.ExchangeVariable;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// An ItemDef that can be the result of an exchange: an item, a bundle or a generator. A container is a
    /// generator whose exchange consumes the container item (and, say, a key).
    /// </summary>
    public abstract class CraftableItemDef : GrantableItemDef
    {
        #region Properties

        public Exchange Exchange
        {
            get => GetExchange();
            set => SetExchange(value);
        }

        #endregion

        #region Inspector Serialized Fields

        [Header("Exchange System")]
        [SerializeField, Tooltip("Recipes that consume materials to create this item.")]
        private Exchange _exchange;

        #endregion

        #region Getters and Setters

        public virtual Exchange GetExchange() => _exchange;

        public virtual void SetExchange(Exchange value) => _exchange = value;

        #endregion

        #region Json

        public override void WriteJsonProperties(JsonWriter writer)
        {
            base.WriteJsonProperties(writer);

            writer.WriteExchangeIfNotEmpty("exchange", Exchange);
        }

        #endregion
    }
}
