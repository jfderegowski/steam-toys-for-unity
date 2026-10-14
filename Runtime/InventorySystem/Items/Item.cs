using System.Collections.Generic;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.Converters;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// ItemDef of type <c>item</c>: the only type that actually sits in a player's inventory.
    /// Use <see cref="Item{TTags}"/> to give it tags. Each one a player owns is an <see cref="InventoryItem"/> of
    /// <see cref="Inventory"/>, which refers to this definition.
    /// </summary>
    [CreateAssetMenu(fileName = "New Item", menuName = "SteamToys/Inventory/Item", order = 1)]
    public class Item : StoreItemDef
    {
        #region Properties

        public bool Tradable
        {
            get => GetTradable();
            set => SetTradable(value);
        }

        public bool Marketable
        {
            get => GetMarketable();
            set => SetMarketable(value);
        }

        public bool GameOnly
        {
            get => GetGameOnly();
            set => SetGameOnly(value);
        }

        public bool AutoStack
        {
            get => GetAutoStack();
            set => SetAutoStack(value);
        }

        public uint PurchaseLimit
        {
            get => GetPurchaseLimit();
            set => SetPurchaseLimit(value);
        }

        /// <summary>
        /// Generator whose possible drops are listed in this item's description, for container items.
        /// </summary>
        public Generator ContainerContentsGenerator
        {
            get => GetContainerContentsGenerator();
            set => SetContainerContentsGenerator(value);
        }

        #endregion

        #region Inspector Serialized Fields

        [Header("Market & Trading")]
        [SerializeField, Tooltip("Whether this item can be traded between users.")]
        private bool _tradable;
        [SerializeField, Tooltip("Whether this item can be sold on the Steam Community Market.")]
        private bool _marketable;
        [SerializeField, Tooltip("Hides the item from the Steam Backpack, for items consumed right away.")]
        private bool _gameOnly;

        [Header("Stack Management")]
        [SerializeField, Tooltip("Whether grants are added to a single stack.")]
        private bool _autoStack;

        [Header("Purchase Limits")]
        [SerializeField, Tooltip("Blocks a purchase when the user already owns this many. 0 means no limit.")]
        private uint _purchaseLimit;

        [Header("Container")]
        [SerializeField, Tooltip("Generator whose possible drops are listed in this item's description.")]
        private Generator _containerContentsGenerator;

        #endregion

        #region Getters and Setters

        public override ItemType GetItemType() => ItemType.Item;

        public virtual bool GetTradable() => _tradable;

        public virtual void SetTradable(bool value) => _tradable = value;

        public virtual bool GetMarketable() => _marketable;

        public virtual void SetMarketable(bool value) => _marketable = value;

        public virtual bool GetGameOnly() => _gameOnly;

        public virtual void SetGameOnly(bool value) => _gameOnly = value;

        public virtual bool GetAutoStack() => _autoStack;

        public virtual void SetAutoStack(bool value) => _autoStack = value;

        public virtual uint GetPurchaseLimit() => _purchaseLimit;

        public virtual void SetPurchaseLimit(uint value) => _purchaseLimit = value;

        public virtual Generator GetContainerContentsGenerator() => _containerContentsGenerator;

        public virtual void SetContainerContentsGenerator(Generator value) => _containerContentsGenerator = value;

        #endregion

        #region Validation

        public override void CollectSchemaWarnings(List<string> warnings)
        {
            base.CollectSchemaWarnings(warnings);

            if (PurchaseLimit > 0 && (Tradable || Marketable))
                warnings.Add("Purchase Limit does not block repurchasing tradable or marketable items.");
        }

        #endregion

        #region Json

        public override void WriteJsonProperties(JsonWriter writer)
        {
            base.WriteJsonProperties(writer);

            writer.WriteBoolIfTrue("tradable", Tradable);
            writer.WriteBoolIfTrue("marketable", Marketable);
            writer.WriteBoolIfTrue("game_only", GameOnly);
            writer.WriteBoolIfTrue("auto_stack", AutoStack);
            writer.WriteUintIfNotZero("purchase_limit", PurchaseLimit);
            writer.WriteItemDefIdIfSet("container_contents_generator", ContainerContentsGenerator);
        }

        #endregion
    }

    public class Item<TTags> : Item where TTags : Tags, new()
    {
        [SerializeField] private TTags _tags = new();

        public TTags TagsData => _tags;

        public override Tags GetTagsObject() => TagsData;
    }
}
