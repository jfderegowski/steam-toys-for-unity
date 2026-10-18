using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace SteamToys.Runtime.InventorySystem.Converters
{
    /// <summary>
    /// Custom JSON converter for <see cref="TagGeneratorDef"/>: the properties every ItemDef writes, then
    /// <c>tag_generator_name</c> and <c>tag_generator_values</c> in the format of the Steam Inventory Service.
    /// <see href="https://partner.steamgames.com/doc/features/inventory/itemtags"/>
    /// </summary>
    public class TagGeneratorConverter : JsonConverter<TagGeneratorDef>
    {
        private const char VALUE_SEPARATOR = ';';
        private const char WEIGHT_SEPARATOR = ':';

        public override bool CanRead => false;

        public override void WriteJson(JsonWriter writer, TagGeneratorDef value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartObject();

            value.WriteJsonProperties(writer);

            writer.WriteIfNotEmpty("tag_generator_name", value.TagName);
            writer.WriteIfNotEmpty("tag_generator_values", ToString(value.TagValuesAndChances));

            writer.WriteEndObject();
        }

        public override TagGeneratorDef ReadJson(JsonReader reader, Type objectType, TagGeneratorDef existingValue,
            bool hasExistingValue, JsonSerializer serializer) => null;

        /// <summary>
        /// Converts tag values and their weights to the Steam format, e.g. <c>"legendary:1;common:9"</c>. Values with a
        /// weight of 0 are left out.
        /// </summary>
        public static string ToString(IReadOnlyDictionary<string, uint> valuesAndChances)
        {
            if (valuesAndChances == null)
                return string.Empty;

            var stringBuilder = new StringBuilder();

            foreach (var (tagValue, chance) in valuesAndChances)
            {
                if (string.IsNullOrEmpty(tagValue) || chance == 0)
                    continue;

                if (stringBuilder.Length > 0)
                    stringBuilder.Append(VALUE_SEPARATOR);

                stringBuilder.Append(tagValue.ToSnakeCase())
                    .Append(WEIGHT_SEPARATOR)
                    .Append(chance);
            }

            return stringBuilder.ToString();
        }
    }
}
