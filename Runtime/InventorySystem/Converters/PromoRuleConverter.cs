using System;
using SteamToys.Runtime.AchievementsSystem;

namespace SteamToys.Runtime.InventorySystem.Converters
{
    /// <summary>
    /// Static helper that converts polymorphic <see cref="PromoRule"/> instances
    /// to/from the Steam promo string format.
    /// <para>
    /// Supported formats:
    /// <list type="bullet">
    ///   <item><c>manual</c></item>
    ///   <item><c>owns:&lt;appid&gt;</c></item>
    ///   <item><c>ach:&lt;achievement API name&gt;</c></item>
    ///   <item><c>played:&lt;appid&gt;/&lt;minutes&gt;</c>, minutes defaulting to 1</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class PromoRuleConverter
    {
        private const char SEPARATOR = ':';
        private const char MINUTES_SEPARATOR = '/';
        private const uint DEFAULT_MINUTES = 1;
        private const string MANUAL = "manual";
        private const string OWNS = "owns";
        private const string ACH = "ach";
        private const string PLAYED = "played";

        /// <summary>
        /// Converts a <see cref="PromoRule"/> to the Steam promo rule string format.
        /// Delegates to <see cref="PromoRule.ToSteamString"/>.
        /// </summary>
        public static string ToString(PromoRule rule)
        {
            if (rule == null)
                return string.Empty;

            return rule.ToSteamString();
        }

        /// <summary>
        /// Parses a single Steam promo rule string into the appropriate <see cref="PromoRule"/> subclass.
        /// Returns <c>null</c> if the string is invalid.
        /// </summary>
        public static PromoRule FromString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var trimmed = value.Trim();

            if (trimmed.Equals(MANUAL, StringComparison.OrdinalIgnoreCase))
                return new ManualPromoRule();

            var separatorIndex = trimmed.IndexOf(SEPARATOR);

            if (separatorIndex < 0)
                return null;

            var prefix = trimmed.Substring(0, separatorIndex).Trim().ToLowerInvariant();
            var argument = trimmed.Substring(separatorIndex + 1).Trim();

            switch (prefix)
            {
                case OWNS:
                {
                    if (!uint.TryParse(argument, out var appId))
                        return null;

                    return new OwnsPromoRule { AppId = appId };
                }

                case ACH:
                {
                    // The rule names the achievement only, so it resolves to the asset in the achievements DB.
                    var achievement = SteamAchievementsDB.Instance.Get(argument);

                    return achievement == null ? null : new AchievementPromoRule { Achievement = achievement };
                }

                case PLAYED:
                {
                    var parts = argument.Split(MINUTES_SEPARATOR);

                    if (!uint.TryParse(parts[0].Trim(), out var appId))
                        return null;

                    var minutes = DEFAULT_MINUTES;

                    if (parts.Length > 1 && !uint.TryParse(parts[1].Trim(), out minutes))
                        return null;

                    return new PlayedPromoRule
                    {
                        AppId = appId,
                        Minutes = minutes
                    };
                }

                default:
                    return null;
            }
        }
    }
}

