using System;
using System.Globalization;
using Newtonsoft.Json;

namespace SteamToys.Runtime.InventorySystem.Converters.Price
{
    public class PriceValueConverter : JsonConverter<PriceValue>
    {
        public override bool CanRead => false;

        public override void WriteJson(JsonWriter writer, PriceValue value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteValue(ToString(value));
        }

        // ReadJson intentionally not implemented; this converter is write-only.

        public override PriceValue ReadJson(JsonReader reader, Type objectType, PriceValue existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotSupportedException("PriceValueConverter is write-only. Read is not supported.");
        }

        /// <summary>
        /// Converts a <see cref="PriceValue"/> to a Steam price token: the currency and the amount in its own units,
        /// e.g. <c>"USD499"</c>, <c>"JPY500"</c> or <c>"VLV100"</c>. Empty for null.
        /// </summary>
        public static string ToString(PriceValue value) =>
            value == null ? string.Empty : value.Currency + value.Amount.ToString(CultureInfo.InvariantCulture);
    }
}
