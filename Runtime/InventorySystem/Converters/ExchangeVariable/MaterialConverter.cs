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
    /// Tag-based: <c>"tagname:tagvalue*quantity"</c>, e.g. <c>"class:common*1"</c>.
    /// </para>
    /// </summary>
    public class MaterialConverter : JsonConverter<Material>
    {
        private const string ITEM_QUANTITY_SEPARATOR = "x";
        private const string TAG_SEPARATOR = ":";
        private const string TAG_QUANTITY_SEPARATOR = "*";

        public override bool CanRead => false;

        public override void WriteJson(JsonWriter writer, Material value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteValue(ToString(value));
        }

        public override Material ReadJson(JsonReader reader, Type objectType, Material existingValue, bool hasExistingValue,
            JsonSerializer serializer) => null; // Deserialization is not supported for this converter.

        /// <summary>
        /// Converts a <see cref="Material"/> to one material of the Steam exchange format: an item when it has no tag
        /// name, e.g. <c>"100x1"</c>, a tag otherwise, e.g. <c>"class:common*1"</c>. Empty when it is null.
        /// </summary>
        /// <exception cref="InvalidOperationException">When it has no value or a quantity of zero.</exception>
        public static string ToString(Material value)
        {
            if (value == null)
                return string.Empty;

            var tagName = value.GetTagName();
            var tagValue = value.GetTagValue();
            var quantity = value.GetQuantity();

            if (quantity == 0)
                throw new InvalidOperationException($"Quantity must be greater than zero for {value.GetType().Name}.");

            return string.IsNullOrEmpty(tagName)
                ? tagValue + ITEM_QUANTITY_SEPARATOR + quantity
                : tagName + TAG_SEPARATOR + tagValue + TAG_QUANTITY_SEPARATOR + quantity;
        }
    }
}
