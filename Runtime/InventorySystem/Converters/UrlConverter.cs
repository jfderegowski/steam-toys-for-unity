using System;
using Newtonsoft.Json;

namespace SteamToys.Runtime.InventorySystem.Converters
{
    /// <summary>
    /// Serializes <see cref="Url"/> as a plain JSON string value.
    /// </summary>
    public class UrlConverter : JsonConverter<Url>
    {
        public override void WriteJson(JsonWriter writer, Url value, JsonSerializer serializer)
        {
            if (ReferenceEquals(value, null) || !value.HasValue)
                writer.WriteNull();
            else
                writer.WriteValue(value.Value);
        }

        public override Url ReadJson(JsonReader reader, Type objectType,
            Url existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return new Url();

            var str = reader.Value as string ?? string.Empty;
            return new Url(str);
        }
    }
}


