using System;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.ExchangeVariable;

namespace SteamToys.Runtime.InventorySystem.Converters.ExchangeVariable
{
    /// <summary>
    /// Custom JSON converter for <see cref="Material"/> that serializes/deserializes
    /// a single exchange material to/from the Steam format.
    /// <para>
    /// Item-based: <c>"itemdefidxquantity"</c>, e.g. <c>"100x1"</c>.
    /// Tag-based: <c>"tagname:tagvaluexquantity"</c>, e.g. <c>"class:commonx1"</c>.
    /// </para>
    /// </summary>
    public class MaterialConverter : JsonConverter<Material>
    {
        public override bool CanRead => false;

        public override void WriteJson(JsonWriter writer, Material value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteValue(value.ToSteamString());
        }

        public override Material ReadJson(JsonReader reader, Type objectType, Material existingValue, bool hasExistingValue,
            JsonSerializer serializer) => null; // Deserialization is not supported for this converter.
    }
}
