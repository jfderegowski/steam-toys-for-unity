using System;

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
    ///   <item><c>ach:&lt;appid&gt;:&lt;achievement_name&gt;</c></item>
    ///   <item><c>played:&lt;appid&gt;:&lt;minutes&gt;</c></item>
    /// </list>
    /// </para>
    /// </summary>
    public static class PromoRuleConverter
    {
        private const char SEPARATOR = ':';
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

            var parts = trimmed.Split(SEPARATOR);

            if (parts.Length < 2)
                return null;

            var prefix = parts[0].Trim().ToLowerInvariant();

            switch (prefix)
            {
                case OWNS:
                {
                    if (!uint.TryParse(parts[1].Trim(), out var appId))
                        return null;

                    return new OwnsPromoRule { AppId = appId };
                }

                case ACH:
                {
                    if (parts.Length < 3)
                        return null;

                    if (!uint.TryParse(parts[1].Trim(), out var appId))
                        return null;

                    return new AchievementPromoRule
                    {
                        AppId = appId,
                        AchievementName = parts[2].Trim()
                    };
                }

                case PLAYED:
                {
                    if (parts.Length < 3)
                        return null;

                    if (!uint.TryParse(parts[1].Trim(), out var appId))
                        return null;

                    if (!uint.TryParse(parts[2].Trim(), out var minutes))
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

