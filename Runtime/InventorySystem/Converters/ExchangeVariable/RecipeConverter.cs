using System;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.ExchangeVariable;

namespace SteamToys.Runtime.InventorySystem.Converters.ExchangeVariable
{
    /// <summary>
    /// Custom JSON converter for <see cref="Recipe"/> that serializes/deserializes
    /// a single exchange recipe to/from the Steam format.
    /// <para>
    /// Materials within a recipe are separated by semicolons,
    /// e.g. <c>"100x1;101x5;class:commonx3"</c>.
    /// </para>
    /// <see href="https://partner.steamgames.com/doc/features/inventory/schema#ExchangeFormat"/>
    /// </summary>
    public class RecipeConverter : JsonConverter<Recipe>
    {
        public override bool CanRead => false;

        public override void WriteJson(JsonWriter writer, Recipe value, JsonSerializer serializer)
        {
            if (value == null || value.Count == 0)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteValue(value.ToSteamString());
        }

        public override Recipe ReadJson(JsonReader reader, Type objectType, Recipe existingValue, bool hasExistingValue,
            JsonSerializer serializer) => null; // Deserialization is not supported for this converter.
    }
}


