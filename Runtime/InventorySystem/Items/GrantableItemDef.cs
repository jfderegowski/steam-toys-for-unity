using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.Converters;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// An ItemDef that ends up as items in a player's inventory: an item itself, or a bundle / generator that
    /// expands into items. Everything except a tag generator.
    /// </summary>
    public abstract class GrantableItemDef : ItemDef
    {
        #region Properties

        public string Description
        {
            get => GetDescription();
            set => SetDescription(value);
        }

        public bool Hidden
        {
            get => GetHidden();
            set => SetHidden(value);
        }

        /// <summary>
        /// Steam <c>tags</c> string, e.g. <c>"rarity:rare;quality:unique"</c>. Empty unless a generic subclass
        /// adds a <see cref="InventorySystem.Tags"/> type.
        /// </summary>
        public string Tags
        {
            get => GetTags();
            set => SetTags(value);
        }

        #endregion

        #region Inspector Serialized Fields

        [SerializeField, TextArea, Tooltip("Detailed description of the item.")]
        private string _description;

        [Header("Visibility & Restrictions")]
        [SerializeField, Tooltip("Hides the definition from clients and makes it unpurchasable.")]
        private bool _hidden;

        #endregion

        #region Getters and Setters

        public virtual string GetDescription() => _description;

        public virtual void SetDescription(string value) => _description = value;

        public virtual bool GetHidden() => _hidden;

        public virtual void SetHidden(bool value) => _hidden = value;

        public virtual string GetTags() => string.Empty;

        public virtual void SetTags(string value) { }

        #endregion

        #region Json

        public override void WriteJsonProperties(JsonWriter writer)
        {
            base.WriteJsonProperties(writer);

            writer.WriteIfNotEmpty("description", Description);
            writer.WriteIfNotEmpty("tags", Tags);
            writer.WriteBoolIfTrue("hidden", Hidden);
        }

        #endregion
    }
}
