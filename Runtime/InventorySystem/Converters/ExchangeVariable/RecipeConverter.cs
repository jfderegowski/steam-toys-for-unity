using System;
using System.Linq;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.ExchangeVariable;

namespace SteamToys.Runtime.InventorySystem.Converters.ExchangeVariable
{
    /// <summary>
    /// Custom JSON converter for <see cref="Recipe"/> that serializes/deserializes
    /// a single exchange recipe to/from the Steam format.
    /// <para>
    /// Materials within a recipe are separated by commas,
    /// e.g. <c>"100x1,101x5,class:common*3"</c>.
    /// </para>
    /// <see href="https://partner.steamgames.com/doc/features/inventory/schema#ExchangeFormat"/>
    /// </summary>
    public class RecipeConverter : JsonConverter<Recipe>
    {
        private const string MATERIAL_SEPARATOR = ",";

        public override bool CanRead => false;

        public override void WriteJson(JsonWriter writer, Recipe value, JsonSerializer serializer)
        {
            if (value == null || value.Count == 0)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteValue(ToString(value));
        }

        public override Recipe ReadJson(JsonReader reader, Type objectType, Recipe existingValue, bool hasExistingValue,
            JsonSerializer serializer) => null; // Deserialization is not supported for this converter.

        /// <summary>
        /// Converts a <see cref="Recipe"/> to one recipe of the Steam exchange format, its materials separated by
        /// commas. Empty when it has no materials.
        /// </summary>
        public static string ToString(Recipe value) =>
            value == null ? string.Empty : string.Join(MATERIAL_SEPARATOR, value.Select(material => material.ToSteamString()));
    }
}


