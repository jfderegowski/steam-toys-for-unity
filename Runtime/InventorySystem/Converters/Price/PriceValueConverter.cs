using System;
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

            // Use ToSteamString for serialization: e.g. "USD499" or "VLV100"
            writer.WriteValue(value.ToSteamString());
        }

        // ReadJson intentionally not implemented; this converter is write-only.

        public override PriceValue ReadJson(JsonReader reader, Type objectType, PriceValue existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotSupportedException("PriceValueConverter is write-only. Read is not supported.");
        }
    }
}
