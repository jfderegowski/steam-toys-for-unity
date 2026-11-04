using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    [JsonConverter(typeof(PromoRuleConverter))]
    public abstract class PromoRule
    {
        /// <summary>
        /// The Steam argument of this rule, e.g. <c>480</c> for <c>owns:480</c>. Empty for a rule that takes none.
        /// The Steam name of the rule comes from its converter.
        /// </summary>
        public abstract string GetValue();
    }

    /// <summary>
    /// <c>manual</c> — item is granted manually via API call.
    /// </summary>
    [Serializable]
    [JsonConverter(typeof(ManualPromoRuleConverter))]
    public class ManualPromoRule : PromoRule
    {
        public override string GetValue() => string.Empty;

        public override string ToString() => "Manual";
    }

    /// <summary>
    /// <c>owns:&lt;appid&gt;</c> — granted when the user owns the specified app.
    /// </summary>
    [Serializable]
    [JsonConverter(typeof(OwnsPromoRuleConverter))]
    public class OwnsPromoRule : PromoRule
    {
        [SerializeField] private uint _appId;

        public uint AppId
        {
            get => _appId;
            set => _appId = value;
        }

        public override string GetValue() => _appId.ToString();

        /// <summary>The app it needs, e.g. <c>App 480</c>.</summary>
        public override string ToString() => $"App {_appId}";
    }

    /// <summary>
    /// <c>ach:&lt;achievement API name&gt;</c> — granted when the user unlocks an achievement of this app.
    /// </summary>
    [Serializable]
    [JsonConverter(typeof(AchievementPromoRuleConverter))]
    public class AchievementPromoRule : PromoRule
    {
        [SerializeField] private SteamAchievement _achievement;

        public SteamAchievement Achievement
        {
            get => _achievement;
            set => _achievement = value;
        }

        public override string GetValue() => _achievement == null ? string.Empty : _achievement.ApiName;

        /// <summary>The achievement it needs, e.g. <c>Win</c>.</summary>
        public override string ToString() => _achievement ? _achievement.name : "—";
    }

    /// <summary>
    /// <c>played:&lt;appid&gt;/&lt;minutes&gt;</c> — granted after the user has played an app for the given number of minutes.
    /// </summary>
    [Serializable]
    [JsonConverter(typeof(PlayedPromoRuleConverter))]
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

        public override string GetValue() => $"{_appId}{PlayedPromoRuleConverter.MINUTES_SEPARATOR}{_minutes}";

        /// <summary>The app and the playtime it needs, e.g. <c>App 480, 120 min</c>.</summary>
        public override string ToString() => $"App {_appId}, {_minutes} min";
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

        /// <summary>Its rules, e.g. <c>Manual, App 480</c>.</summary>
        public override string ToString() =>
            _rules is { Count: > 0 }
                ? string.Join(", ", _rules.Select(rule => rule?.ToString() ?? "-"))
                : "no rules";
    }
}