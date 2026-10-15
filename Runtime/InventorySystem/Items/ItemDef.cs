using System.Collections.Generic;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.Converters;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// A single Steam Inventory item definition. Each subclass is one ItemDef <c>type</c> and carries only
    /// the schema properties that type supports.
    /// <see href="https://partner.steamgames.com/doc/features/inventory/schema"/>
    /// </summary>
    [JsonConverter(typeof(ItemConverter))]
    public abstract class ItemDef : ScriptableObject
    {
        #region Properties

        public ulong ItemDefId
        {
            get => GetItemDefId();
            set => SetItemDefId(value);
        }

        /// <summary>
        /// The ItemDef <c>type</c>, fixed by the class of the asset.
        /// </summary>
        public ItemType Type => GetItemType();

        public string Name
        {
            get => GetName();
            set => SetName(value);
        }

        #endregion

        #region Inspector Serialized Fields

        [Header("Basic Item Data")]
        [SerializeField, Tooltip("Unique identifier for this item definition.")]
        private ulong _itemDefId;
        [SerializeField, Tooltip("Display name of the item.")]
        private string _name;

        #endregion

        #region Getters and Setters

        public virtual ulong GetItemDefId() => _itemDefId;

        public virtual void SetItemDefId(ulong value) => _itemDefId = value;

        public abstract ItemType GetItemType();

        public virtual string GetName() => _name;

        public virtual void SetName(string value) => _name = value;

        #endregion

        #region Validation

        /// <summary>
        /// Adds a message for every setting Steam would reject or silently ignore. Overrides call base first.
        /// </summary>
        public virtual void CollectSchemaWarnings(List<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(Name))
                warnings.Add("Name is empty: Steam rejects the definition, and an upload stops there.");
        }

        #endregion

        #region Json

        public virtual string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented);

        /// <summary>
        /// Writes this definition's schema properties into the already opened JSON object.
        /// Overrides call base first, so the shared properties come before the type specific ones.
        /// </summary>
        public virtual void WriteJsonProperties(JsonWriter writer)
        {
            writer.WritePropertyName("itemdefid");
            writer.WriteValue(ItemDefId);

            writer.WritePropertyName("type");
            writer.WriteValue(ItemTypeConverter.ToString(Type));

            writer.WriteIfNotEmpty("name", Name);
        }

        #endregion
    }
}
