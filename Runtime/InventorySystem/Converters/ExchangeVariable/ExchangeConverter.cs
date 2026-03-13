using System;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.ExchangeVariable;

namespace SteamToys.Runtime.InventorySystem.Converters.ExchangeVariable
{
    /// <summary>
    /// Custom JSON converter for <see cref="Exchange"/> that serializes/deserializes
    /// to/from the Steam Inventory Service exchange format.
    /// <para>
    /// Recipes are separated by commas, materials within a recipe by semicolons.
    /// e.g. <c>"100x1;101x1,100x1;102x3"</c> defines two alternative recipes.
    /// </para>
    /// <see href="https://partner.steamgames.com/doc/features/inventory/schema#ExchangeFormat"/>
    /// </summary>
    public class ExchangeConverter : JsonConverter<Exchange>
    {
        public override bool CanRead => false;
        
        public override void WriteJson(JsonWriter writer, Exchange value, JsonSerializer serializer)
        {
            if (value == null || value.Count == 0)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteValue(value.ToSteamString());
        }

        public override Exchange ReadJson(JsonReader reader, Type objectType, Exchange existingValue,
            bool hasExistingValue, JsonSerializer serializer) => null; // Deserialization is not supported for this converter.
    }
}

