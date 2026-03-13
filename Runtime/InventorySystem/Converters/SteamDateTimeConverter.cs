using System;
using Newtonsoft.Json;

namespace SteamToys.Runtime.InventorySystem.Converters
{
    /// <summary>
    /// Custom JSON converter for <see cref="SteamDateTime"/> that serializes/deserializes
    /// to/from the Steam Inventory Service timestamp format (<c>"YYYYMMDDTHHMMSSz"</c>).
    /// </summary>
    public class SteamDateTimeConverter : JsonConverter<SteamDateTime>
    {
        public override void WriteJson(JsonWriter writer, SteamDateTime value, JsonSerializer serializer)
        {
            if (value == null || !value.HasValue)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteValue(value.ToSteamString());
        }

        public override SteamDateTime ReadJson(JsonReader reader, Type objectType,
            SteamDateTime existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return new SteamDateTime();

            var str = reader.Value as string ?? string.Empty;
            return SteamDateTime.FromSteamString(str);
        }
    }
}

