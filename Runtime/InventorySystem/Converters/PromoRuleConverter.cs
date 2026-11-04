using System;
using System.Linq;
using Newtonsoft.Json;
using SteamToys.Runtime.AchievementsSystem;

namespace SteamToys.Runtime.InventorySystem.Converters
{
    /// <summary>
    /// Custom JSON converter for polymorphic <see cref="PromoRule"/> instances that serializes/deserializes
    /// to/from the Steam promo string format, by handing each rule to the converter of its type.
    /// <para>
    /// Steam supports these rules only:
    /// <list type="bullet">
    ///   <item><c>manual</c></item>
    ///   <item><c>owns:&lt;appid&gt;</c></item>
    ///   <item><c>ach:&lt;achievement API name&gt;</c></item>
    ///   <item><c>played:&lt;appid&gt;/&lt;minutes&gt;</c>, minutes defaulting to 1</item>
    /// </list>
    /// </para>
    /// <see href="https://partner.steamgames.com/doc/features/inventory/schema#PromoItems"/>
    /// </summary>
    public class PromoRuleConverter : JsonConverter<PromoRule>
    {
        private const char SEPARATOR = ':';

        private static readonly PromoRuleConverter[] RuleConverters =
        {
            new ManualPromoRuleConverter(),
            new OwnsPromoRuleConverter(),
            new AchievementPromoRuleConverter(),
            new PlayedPromoRuleConverter()
        };

        #region JSON

        public override void WriteJson(JsonWriter writer, PromoRule value, JsonSerializer serializer)
        {
            var rule = ToString(value);

            if (rule.Length == 0)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteValue(rule);
        }

        public override PromoRule ReadJson(JsonReader reader, Type objectType,
            PromoRule existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var rule = FromString(reader.Value as string);

            return rule != null && Converts(rule) ? rule : null;
        }

        #endregion

        #region Steam String

        /// <summary>
        /// Converts a <see cref="PromoRule"/> to the Steam promo rule string format.
        /// A rule that is incomplete, or whose type has no converter, gives an empty string.
        /// </summary>
        public static string ToString(PromoRule rule)
        {
            if (rule == null)
                return string.Empty;

            var converter = RuleConverters.FirstOrDefault(ruleConverter => ruleConverter.Converts(rule));

            return converter == null ? string.Empty : converter.Format(rule);
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
            var separatorIndex = trimmed.IndexOf(SEPARATOR);
            var ruleName = separatorIndex < 0 ? trimmed : trimmed.Substring(0, separatorIndex).Trim();
            var argument = separatorIndex < 0 ? string.Empty : trimmed.Substring(separatorIndex + 1).Trim();

            var converter = RuleConverters.FirstOrDefault(ruleConverter =>
                string.Equals(ruleConverter.RuleName, ruleName, StringComparison.OrdinalIgnoreCase));

            return converter?.Parse(argument);
        }

        #endregion

        #region Rule Converters

        /// <summary>The Steam name of the rule, e.g. <c>owns</c>. Null on this converter, which handles them all.</summary>
        protected virtual string RuleName => null;

        /// <summary>Whether this converter is the one for <paramref name="rule"/>.</summary>
        protected virtual bool Converts(PromoRule rule) => true;

        /// <summary>The Steam string of <paramref name="rule"/>: empty without a value, since the rule is incomplete.</summary>
        protected virtual string Format(PromoRule rule)
        {
            var value = rule.GetValue();

            return string.IsNullOrEmpty(value) ? string.Empty : RuleName + SEPARATOR + value;
        }

        /// <summary>The rule with the Steam argument <paramref name="argument"/>, or null if it is invalid.</summary>
        protected virtual PromoRule Parse(string argument) => null;

        #endregion
    }

    /// <summary>Converts <see cref="ManualPromoRule"/> to and from <c>manual</c>.</summary>
    public class ManualPromoRuleConverter : PromoRuleConverter
    {
        protected override string RuleName => "manual";

        protected override bool Converts(PromoRule rule) => rule is ManualPromoRule;

        // The only rule that is complete without a value.
        protected override string Format(PromoRule rule) => RuleName;

        protected override PromoRule Parse(string argument) =>
            argument.Length == 0 ? new ManualPromoRule() : null;
    }

    /// <summary>Converts <see cref="OwnsPromoRule"/> to and from <c>owns:&lt;appid&gt;</c>.</summary>
    public class OwnsPromoRuleConverter : PromoRuleConverter
    {
        protected override string RuleName => "owns";

        protected override bool Converts(PromoRule rule) => rule is OwnsPromoRule;

        protected override PromoRule Parse(string argument) =>
            uint.TryParse(argument, out var appId) ? new OwnsPromoRule { AppId = appId } : null;
    }

    /// <summary>Converts <see cref="AchievementPromoRule"/> to and from <c>ach:&lt;achievement API name&gt;</c>.</summary>
    public class AchievementPromoRuleConverter : PromoRuleConverter
    {
        protected override string RuleName => "ach";

        protected override bool Converts(PromoRule rule) => rule is AchievementPromoRule;

        protected override PromoRule Parse(string argument)
        {
            if (argument.Length == 0)
                return null;

            // The rule names the achievement only, so it resolves to the asset in the achievements DB.
            var achievement = SteamAchievementsDB.Instance.Get(argument);

            return achievement == null ? null : new AchievementPromoRule { Achievement = achievement };
        }
    }

    /// <summary>
    /// Converts <see cref="PlayedPromoRule"/> to and from <c>played:&lt;appid&gt;/&lt;minutes&gt;</c>, minutes
    /// defaulting to 1.
    /// </summary>
    public class PlayedPromoRuleConverter : PromoRuleConverter
    {
        public const char MINUTES_SEPARATOR = '/';

        private const uint DEFAULT_MINUTES = 1;

        protected override string RuleName => "played";

        protected override bool Converts(PromoRule rule) => rule is PlayedPromoRule;

        protected override PromoRule Parse(string argument)
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
    }
}
