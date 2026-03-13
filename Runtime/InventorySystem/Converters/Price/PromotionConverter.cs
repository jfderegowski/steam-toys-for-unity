using System;
using Newtonsoft.Json;

namespace SteamToys.Runtime.InventorySystem.Converters.Price
{
    public class PromotionConverter : JsonConverter<Promotion>
    {
        public override bool CanRead => false;

        public override void WriteJson(JsonWriter writer, Promotion value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            // Use Promotion.ToSteamString() representation
            writer.WriteValue(value.ToSteamString());
        }

        // ReadJson intentionally not implemented; this converter is write-only.
        public override Promotion ReadJson(JsonReader reader, Type objectType, Promotion existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotSupportedException("PromotionConverter is write-only. Read is not supported.");
        }
    }
}
