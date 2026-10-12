using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using SteamToys.Runtime.AchievementsSystem;
using SteamToys.Runtime.InventorySystem.Converters;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// Abstract base class for a single promo rule.
    /// Derive from this class to create new rule types.
    /// <see href="https://partner.steamgames.com/doc/features/inventory/schema#PromoItems"/>
    /// </summary>
    [Serializable]
    public class PromoRule
    {
        /// <summary>
        /// Converts this rule to the Steam promo string fragment,
        /// e.g. <c>"manual"</c>, <c>"owns:480"</c>, <c>"ach:WIN_ONE_GAME"</c>, <c>"played:480/120"</c>.
        /// An empty string means the rule is incomplete and is left out.
        /// </summary>
        public virtual string ToSteamString() => string.Empty;
    }

    /// <summary>
    /// <c>manual</c> — item is granted manually via API call.
    /// </summary>
    [Serializable]
    public class ManualPromoRule : PromoRule
    {
        public override string ToSteamString() => "manual";
    }

    /// <summary>
    /// <c>owns:&lt;appid&gt;</c> — granted when the user owns the specified app.
    /// </summary>
    [Serializable]
    public class OwnsPromoRule : PromoRule
    {
        [SerializeField] private uint _appId;

        public uint AppId
        {
            get => _appId;
            set => _appId = value;
        }

        public override string ToSteamString() => $"owns:{_appId}";
    }

    /// <summary>
    /// <c>ach:&lt;achievement API name&gt;</c> — granted when the user unlocks an achievement of this app.
    /// </summary>
    [Serializable]
    public class AchievementPromoRule : PromoRule
    {
        [SerializeField] private SteamAchievement _achievement;

        public SteamAchievement Achievement
        {
            get => _achievement;
            set => _achievement = value;
        }

        public override string ToSteamString() =>
            _achievement == null || string.IsNullOrEmpty(_achievement.ApiName) ? string.Empty : $"ach:{_achievement.ApiName}";
    }

    /// <summary>
    /// <c>played:&lt;appid&gt;/&lt;minutes&gt;</c> — granted after the user has played an app for the given number of minutes.
    /// </summary>
    [Serializable]
    public class PlayedPromoRule : PromoRule
    {
        [SerializeField] private uint _appId;
        [SerializeField] private uint _minutes = 1;

        public uint AppId
        {
            get => _appId;
            set => _appId = value;
        }

        public uint Minutes
        {
            get => _minutes;
            set => _minutes = value;
        }

        public override string ToSteamString() => $"played:{_appId}/{_minutes}";
    }

    /// <summary>
    /// Represents a collection of promo rules for a Steam Inventory item.
    /// Uses <see cref="SerializeReference"/> so the Unity Inspector shows a type-picker
    /// when adding new elements.
    /// <para>
    /// Serializes to the Steam format: semicolon-delimited rules,
    /// e.g. <c>"manual;owns:480;ach:WIN_ONE_GAME;played:480/120"</c>.
    /// </para>
    /// <see href="https://partner.steamgames.com/doc/features/inventory/schema#PromoItems"/>
    /// </summary>
    [Serializable]
    [JsonConverter(typeof(PromoConverter))]
    public class Promo : IList<PromoRule>
    {
        [SerializeReference] private List<PromoRule> _rules = new();

        #region IList Implementation

        public IEnumerator<PromoRule> GetEnumerator() => _rules.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(PromoRule item) => _rules.Add(item);

        public void Clear() => _rules.Clear();

        public bool Contains(PromoRule item) => _rules.Contains(item);

        public void CopyTo(PromoRule[] array, int arrayIndex) => _rules.CopyTo(array, arrayIndex);

        public bool Remove(PromoRule item) => _rules.Remove(item);

        public int Count => _rules.Count;

        public bool IsReadOnly => false;

        public int IndexOf(PromoRule item) => _rules.IndexOf(item);

        public void Insert(int index, PromoRule item) => _rules.Insert(index, item);

        public void RemoveAt(int index) => _rules.RemoveAt(index);

        public PromoRule this[int index]
        {
            get => _rules[index];
            set => _rules[index] = value;
        }

        #endregion
    }
}