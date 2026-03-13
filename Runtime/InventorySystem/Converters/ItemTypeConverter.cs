using System;
using Newtonsoft.Json;

namespace SteamToys.Runtime.InventorySystem.Converters
{
    /// <summary>
    /// Custom JSON converter for <see cref="ItemType"/> that serializes/deserializes
    /// enum values to/from lowercase Steam-expected strings.
    /// </summary>
    public class ItemTypeConverter : JsonConverter<ItemType>
    {
        public override void WriteJson(JsonWriter writer, ItemType value, JsonSerializer serializer)
        {
            writer.WriteValue(ToString(value));
        }

        public override ItemType ReadJson(JsonReader reader, Type objectType,
            ItemType existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var str = reader.Value as string;
            return FromString(str);
        }

        public static string ToString(ItemType type)
        {
            switch (type)
            {
                case ItemType.Item:              return "item";
                case ItemType.Bundle:            return "bundle";
                case ItemType.Generator:         return "generator";
                case ItemType.PlaytimeGenerator: return "playtimegenerator";
                case ItemType.TagGenerator:      return "tag_generator";
                default:                         return "item";
            }
        }

        public static ItemType FromString(string value)
        {
            switch (value?.ToLowerInvariant())
            {
                case "item":             return ItemType.Item;
                case "bundle":           return ItemType.Bundle;
                case "generator":        return ItemType.Generator;
                case "playtimegenerator": return ItemType.PlaytimeGenerator;
                case "tag_generator":    return ItemType.TagGenerator;
                default:                 return ItemType.Item;
            }
        }
    }
}

