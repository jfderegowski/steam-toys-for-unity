using System;
using System.Linq;
using Newtonsoft.Json;

namespace SteamToys.Runtime.InventorySystem.Converters.Price
{
    public class PromotionConverter : JsonConverter<Promotion>
    {
        private const string PRICE_SEPARATOR = ",";
        private const string DATE_SEPARATOR = "-";

        public override bool CanRead => false;

        public override void WriteJson(JsonWriter writer, Promotion value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteValue(ToString(value));
        }

        // ReadJson intentionally not implemented; this converter is write-only.
        public override Promotion ReadJson(JsonReader reader, Type objectType, Promotion existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotSupportedException("PromotionConverter is write-only. Read is not supported.");
        }

        /// <summary>
        /// Converts a <see cref="Promotion"/> to one sale of the Steam price format: its date range, end first, then
        /// its prices, e.g. <c>"20130609T080000Z-20130606T080000ZUSD50,EUR40"</c> for 6/6 until 6/9.
        /// Empty when it lacks a start, an end or a price, since Steam takes a sale only with all of them.
        /// <see href="https://partner.steamgames.com/doc/features/inventory/schema#SpecifyingPrices"/>
        /// </summary>
        public static string ToString(Promotion value)
        {
            if (value == null || value.StartTime is not { HasValue: true } || value.EndTime is not { HasValue: true })
                return string.Empty;

            var prices = string.Join(PRICE_SEPARATOR, (value.DiscountedPrices ?? Enumerable.Empty<PriceValue>())
                .Where(price => price != null)
                .Select(PriceValueConverter.ToString));

            if (prices.Length == 0)
                return string.Empty;

            return value.EndTime.ToSteamString() + DATE_SEPARATOR + value.StartTime.ToSteamString() + prices;
        }
    }
}
