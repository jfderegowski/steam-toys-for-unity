using System;
using System.Text;
using Newtonsoft.Json;

namespace SteamToys.Runtime.InventorySystem.Converters
{
    /// <summary>
    /// Custom JSON converter for <see cref="Promo"/> that serializes/deserializes
    /// to/from the Steam Inventory Service promo format.
    /// <para>
    /// Steam expects a semicolon-delimited string of promo rules,
    /// e.g. <c>"manual;owns:480;ach:480:MyAchievement;played:480:120"</c>.
    /// </para>
    /// <see href="https://partner.steamgames.com/doc/features/inventory/schema#PromoItems"/>
    /// </summary>
    public class PromoConverter : JsonConverter<Promo>
    {
        private const char ENTRY_SEPARATOR = ';';

        public override void WriteJson(JsonWriter writer, Promo value, JsonSerializer serializer)
        {
            if (value == null || value.Count == 0)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteValue(ToString(value));
        }

        public override Promo ReadJson(JsonReader reader, Type objectType,
            Promo existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return new Promo();

            var str = reader.Value as string ?? string.Empty;
            return FromString(str);
        }

        /// <summary>
        /// Converts a <see cref="Promo"/> to the Steam promo string format.
        /// Example output: <c>"manual;owns:480;ach:480:MyAchievement"</c>
        /// </summary>
        public static string ToString(Promo promo)
        {
            if (promo == null || promo.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();

            for (int i = 0; i < promo.Count; i++)
            {
                if (i > 0)
                    sb.Append(ENTRY_SEPARATOR);

                sb.Append(PromoRuleConverter.ToString(promo[i]));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Parses a Steam promo string (e.g. <c>"manual;owns:480"</c>) into a <see cref="Promo"/>.
        /// </summary>
        public static Promo FromString(string value)
        {
            var promo = new Promo();

            if (string.IsNullOrEmpty(value))
                return promo;

            var entries = value.Split(ENTRY_SEPARATOR);

            foreach (var entry in entries)
            {
                var rule = PromoRuleConverter.FromString(entry);

                if (rule != null)
                    promo.Add(rule);
            }

            return promo;
        }
    }
}

