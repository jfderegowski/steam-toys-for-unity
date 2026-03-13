using System;
using Newtonsoft.Json;

namespace SteamToys.Runtime.InventorySystem.Converters.Price
{
    public class PriceConverter : JsonConverter<InventorySystem.Price>
    {
        public override bool CanRead => false;

        public override void WriteJson(JsonWriter writer, InventorySystem.Price value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            // Use Price.ToSteamString() representation
            writer.WriteValue(value.ToSteamString());
        }

        // ReadJson intentionally not implemented; this converter is write-only.

        public override InventorySystem.Price ReadJson(JsonReader reader, Type objectType, InventorySystem.Price existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotSupportedException("PriceConverter is write-only. Read is not supported.");
        }
    }
}
