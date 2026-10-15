using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.Converters;
using SteamToys.Runtime.InventorySystem.ExchangeVariable;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// An ItemDef that ends up as items in a player's inventory: an item itself, or a bundle / generator that
    /// expands into items. Everything except a tag generator. Each of them can be the target of an exchange recipe
    /// and granted by a promo.
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
        /// Steam <c>tags</c> string, e.g. <c>"rarity:rare;quality:unique"</c>, of the typed tags of a generic subclass,
        /// generated ones left out. Empty for a class without a <see cref="InventorySystem.Tags"/> type.
        /// </summary>
        public string Tags
        {
            get => GetTags();
            set => SetTags(value);
        }

        public Exchange Exchange
        {
            get => GetExchange();
            set => SetExchange(value);
        }

        public Promo Promo
        {
            get => GetPromo();
            set => SetPromo(value);
        }

        public SteamDateTime DropStartTime
        {
            get => GetDropStartTime();
            set => SetDropStartTime(value);
        }

        public bool GrantedManually
        {
            get => GetGrantedManually();
            set => SetGrantedManually(value);
        }

        /// <summary>
        /// With a manual promo, the minutes before it grants this again, e.g. 10080 for once a week; 0 grants it only
        /// once. On a <see cref="PlaytimeGenerator"/> also the minutes of playtime before a drop.
        /// </summary>
        public uint DropInterval
        {
            get => GetDropInterval();
            set => SetDropInterval(value);
        }

        #endregion

        #region Inspector Serialized Fields

        [SerializeField, TextArea, Tooltip("Detailed description of the item.")]
        private string _description;

        [Header("Visibility & Restrictions")]
        [SerializeField, Tooltip("Hides the definition from clients and makes it unpurchasable. It can still be granted.")]
        private bool _hidden;

        [Header("Exchange System")]
        [SerializeField, Tooltip("Recipes that consume materials to create this item.")]
        private Exchange _exchange;

        [Header("Promo")]
        [SerializeField, Tooltip("Rules that grant this item automatically.")]
        private Promo _promo;
        [SerializeField, Tooltip("Timestamp before which the manual promo does not grant this item.")]
        private SteamDateTime _dropStartTime;
        [SerializeField, Tooltip("Granted only by AddPromoItem(s) with its explicit ID. Without it GrantPromoItems " +
                                 "grants a manual promo too.")]
        private bool _grantedManually;
        [SerializeField, Tooltip("With a manual promo: minutes before it grants this again, e.g. 10080 for once a week; " +
                                 "0 grants it only once. On a playtime generator: minutes of playtime before a drop, " +
                                 "0 uses the application setting.")]
        private uint _dropInterval;

        #endregion

        #region Getters and Setters

        public virtual string GetDescription() => _description;

        public virtual void SetDescription(string value) => _description = value;

        public virtual bool GetHidden() => _hidden;

        public virtual void SetHidden(bool value) => _hidden = value;

        public virtual Exchange GetExchange() => _exchange;

        public virtual void SetExchange(Exchange value) => _exchange = value;

        public virtual Promo GetPromo() => _promo;

        public virtual void SetPromo(Promo value) => _promo = value;

        public virtual SteamDateTime GetDropStartTime() => _dropStartTime;

        public virtual void SetDropStartTime(SteamDateTime value) => _dropStartTime = value;

        public virtual bool GetGrantedManually() => _grantedManually;

        public virtual void SetGrantedManually(bool value) => _grantedManually = value;

        public virtual uint GetDropInterval() => _dropInterval;

        public virtual void SetDropInterval(uint value) => _dropInterval = value;

        /// <summary>
        /// The tags of this definition as typed values, or null without a <see cref="InventorySystem.Tags"/> type.
        /// On an owned item they hold its own values, generated ones included.
        /// </summary>
        public virtual Tags GetTagsObject() => null;

        public virtual string GetTags() => GetTagsObject()?.GetDefinitionTagsString() ?? string.Empty;

        /// <summary>
        /// Takes a Steam <c>tags</c> string into the typed tags of this definition.
        /// </summary>
        /// <returns>
        /// The <c>category:value</c> pairs no typed tag of this class holds, or whose value its enum lacks. They are
        /// left out.
        /// </returns>
        public virtual List<string> SetTags(string value)
        {
            var typed = GetTagsObject();
            var notHeld = new List<string>();

            foreach (var pair in (value ?? string.Empty).Split(TAG_SEPARATOR, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = pair.Trim();
                var separator = trimmed.IndexOf(CATEGORY_SEPARATOR);

                var held = typed != null && separator > 0 &&
                           typed.SetValue(trimmed[..separator].Trim(), trimmed[(separator + 1)..].Trim());

                if (!held && trimmed.Length > 0)
                    notHeld.Add(trimmed);
            }

            return notHeld;
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

        #region Validation

        /// <summary>Whether <see cref="DropInterval"/> means something without a manual promo.</summary>
        protected virtual bool DropIntervalPacesDrops => false;

        public override void CollectSchemaWarnings(List<string> warnings)
        {
            base.CollectSchemaWarnings(warnings);

            if (DropStartTime is { HasValue: true } && !HasManualPromo())
                warnings.Add("Drop Start Time only applies to a manual promo; add a Manual promo rule or clear it.");

            if (DropInterval > 0 && !HasManualPromo() && !DropIntervalPacesDrops)
                warnings.Add("Drop Interval only applies to a manual promo; add a Manual promo rule or clear it.");

            if (Promo != null)
            {
                for (var i = 0; i < Promo.Count; i++)
                {
                    if (string.IsNullOrEmpty(Promo[i]?.ToSteamString()))
                        warnings.Add($"Promo rule {i} is incomplete and is left out.");
                }
            }
        }

        private bool HasManualPromo()
        {
            if (Promo == null)
                return false;

            foreach (var rule in Promo)
            {
                if (rule is ManualPromoRule)
                    return true;
            }

            return false;
        }

        #endregion

        #region Json

        public override void WriteJsonProperties(JsonWriter writer)
        {
            base.WriteJsonProperties(writer);

            writer.WriteIfNotEmpty("description", Description);
            writer.WriteIfNotEmpty("tags", Tags);
            writer.WriteBoolIfTrue("hidden", Hidden);
            writer.WriteExchangeIfNotEmpty("exchange", Exchange);
            writer.WritePromoIfNotEmpty("promo", Promo);
            writer.WriteSteamDateTimeIfHasValue("drop_start_time", DropStartTime);
            writer.WriteBoolIfTrue("granted_manually", GrantedManually);
            writer.WriteUintIfNotZero("drop_interval", DropInterval);
        }

        #endregion
    }
}
