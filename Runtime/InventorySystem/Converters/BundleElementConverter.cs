using System;
using Newtonsoft.Json;

namespace SteamToys.Runtime.InventorySystem.Converters
{
    /// <summary>
    /// Custom JSON converter for <see cref="BundleElement"/> that serializes/deserializes
    /// a single bundle entry to/from the Steam format: <c>"itemdefidxquantity"</c>,
    /// e.g. <c>"100x1"</c>.
    /// </summary>
    public class BundleElementConverter : JsonConverter<BundleElement>
    {
        private const char QUANTITY_SEPARATOR = 'x';

        public override void WriteJson(JsonWriter writer, BundleElement value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteValue(ToString(value));
        }

        public override BundleElement ReadJson(JsonReader reader, Type objectType,
            BundleElement existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var str = reader.Value as string ?? string.Empty;
            return FromString(str);
        }

        /// <summary>
        /// Converts a <see cref="BundleElement"/> to the Steam element format.
        /// Example output: <c>"100x1"</c>
        /// </summary>
        public static string ToString(BundleElement element)
        {
            if (element == null)
                return string.Empty;

            return $"{element.ItemDefId}{QUANTITY_SEPARATOR}{element.Quantity}";
        }

        /// <summary>
        /// Parses a single Steam bundle entry (e.g. <c>"100x1"</c>) into a <see cref="BundleElement"/>.
        /// Returns <c>null</c> if the string is invalid.
        /// </summary>
        public static BundleElement FromString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var parts = value.Split(QUANTITY_SEPARATOR);

            if (parts.Length < 2)
                return null;

            if (!ulong.TryParse(parts[0].Trim(), out var itemDefId))
                return null;

            if (!uint.TryParse(parts[1].Trim(), out var quantity))
                return null;

            return new BundleElement
            {
                ItemDefId = itemDefId,
                Quantity = quantity
            };
        }
    }
}

