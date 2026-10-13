using System;
using System.Collections.Generic;
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
        #region Constants

        private const char TAG_SEPARATOR = ';';
        private const char CATEGORY_SEPARATOR = ':';

        #endregion

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
        /// Steam <c>tags</c> string, e.g. <c>"rarity:rare;quality:unique"</c>: the typed tags of a generic subclass,
        /// generated ones left out, followed by the <see cref="RawTags"/>.
        /// </summary>
        public string Tags
        {
            get => GetTags();
            set => SetTags(value);
        }

        /// <summary>
        /// Tags no typed tag of this class holds, as <c>category:value</c> pairs separated by <c>;</c>. They are
        /// exported as they are, so definitions made from Steam keep every tag without a class for it.
        /// </summary>
        public string RawTags
        {
            get => GetRawTags();
            set => SetRawTags(value);
        }

        #endregion

        #region Inspector Serialized Fields

        [SerializeField, TextArea, Tooltip("Detailed description of the item.")]
        private string _description;

        [SerializeField, Tooltip("Tags no typed tag of this class holds, as category:value pairs separated by ';'. " +
                                 "Exported as they are.")]
        private string _rawTags;

        [Header("Visibility & Restrictions")]
        [SerializeField, Tooltip("Hides the definition from clients and makes it unpurchasable.")]
        private bool _hidden;

        #endregion

        #region Getters and Setters

        public virtual string GetDescription() => _description;

        public virtual void SetDescription(string value) => _description = value;

        public virtual bool GetHidden() => _hidden;

        public virtual void SetHidden(bool value) => _hidden = value;

        public virtual string GetRawTags() => _rawTags ?? string.Empty;

        public virtual void SetRawTags(string value) => _rawTags = value;

        /// <summary>
        /// The tags of this definition as typed values, or null without a <see cref="InventorySystem.Tags"/> type.
        /// On an owned item they hold its own values, generated ones included.
        /// </summary>
        public virtual Tags GetTagsObject() => null;

        public virtual string GetTags()
        {
            var typed = GetTagsObject()?.GetDefinitionTagsString() ?? string.Empty;
            var raw = GetRawTags().Trim().Trim(TAG_SEPARATOR);

            if (typed.Length == 0)
                return raw;

            return raw.Length == 0 ? typed : typed + TAG_SEPARATOR + raw;
        }

        /// <summary>
        /// Takes a Steam <c>tags</c> string: each pair a typed tag holds goes to it, and the rest becomes the
        /// <see cref="RawTags"/>, replacing them.
        /// </summary>
        public virtual void SetTags(string value)
        {
            var typed = GetTagsObject();
            var raw = new List<string>();

            foreach (var pair in (value ?? string.Empty).Split(TAG_SEPARATOR, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = pair.Trim();
                var separator = trimmed.IndexOf(CATEGORY_SEPARATOR);

                // A value the enum cannot hold is kept raw too, so no tag is lost.
                var held = typed != null && separator > 0 &&
                           typed.SetValue(trimmed[..separator].Trim(), trimmed[(separator + 1)..].Trim());

                if (!held && trimmed.Length > 0)
                    raw.Add(trimmed);
            }

            SetRawTags(string.Join(TAG_SEPARATOR, raw));
        }

        #endregion

        #region Inventory

        /// <summary>
        /// Grants <paramref name="quantity"/> of this definition to the signed-in account through
        /// <see cref="Inventory.GenerateItems"/>. Works only for accounts of the Steamworks partner group and only once
        /// the definition is published on Steam, so it is for testing, never for the game itself.
        /// </summary>
        public bool GenerateItem(int quantity = 1)
        {
            if (quantity < 1)
            {
                Debug.LogWarning($"{name}: Generate Item needs a quantity of at least 1.");
                return false;
            }

            return Inventory.GenerateItems(new[] { ((ItemDef)this, (uint)quantity) }, succeeded =>
            {
                if (succeeded)
                    Debug.Log($"Generated {quantity} x {name} (itemdefid {ItemDefId}).");
                else
                    Debug.LogWarning($"Steam did not generate {name} (itemdefid {ItemDefId}).");
            });
        }

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
