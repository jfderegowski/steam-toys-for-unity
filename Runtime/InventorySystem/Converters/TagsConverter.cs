using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace SteamToys.Runtime.InventorySystem.Converters
{
    /// <summary>
    /// Custom JSON converter for <see cref="Tags"/> that serializes/deserializes to/from the Steam Inventory Service
    /// tags format.
    /// <para>
    /// Steam expects a semicolon-delimited string of <c>category:value</c> pairs,
    /// e.g. <c>"rarity:legendary;wear:field_tested"</c>.
    /// </para>
    /// <see href="https://partner.steamgames.com/doc/features/inventory/itemtags"/>
    /// </summary>
    public class TagsConverter : JsonConverter<Tags>
    {
        private const char PAIR_SEPARATOR = ';';
        private const char CATEGORY_SEPARATOR = ':';

        public override void WriteJson(JsonWriter writer, Tags value, JsonSerializer serializer)
        {
            var tags = ToString(value);

            if (tags.Length == 0)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteValue(tags);
        }

        public override Tags ReadJson(JsonReader reader, Type objectType,
            Tags existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var tags = hasExistingValue && existingValue != null ? existingValue : Create(objectType);

            if (reader.TokenType != JsonToken.Null)
                FromString(reader.Value as string, tags);

            return tags;
        }

        /// <summary>
        /// Converts <see cref="Tags"/> to the Steam tags string format, e.g. <c>"rarity:legendary;wear:worn"</c>.
        /// Pairs with an empty category or value are left out.
        /// </summary>
        public static string ToString(Tags tags)
        {
            if (tags == null)
                return string.Empty;

            return string.Join(PAIR_SEPARATOR, tags
                .Where(tag => !string.IsNullOrEmpty(tag.Key) && !string.IsNullOrEmpty(tag.Value))
                .Select(tag => tag.Key + CATEGORY_SEPARATOR + tag.Value));
        }

        /// <summary>
        /// Sets the pairs of a Steam tags string (e.g. <c>"rarity:legendary;wear:worn"</c>) on <paramref name="tags"/>
        /// through <see cref="Tags.TrySetValue"/>.
        /// </summary>
        /// <returns>The pairs left out: malformed, or turned down by <see cref="Tags.TrySetValue"/>.</returns>
        public static List<string> FromString(string value, Tags tags)
        {
            var notHeld = new List<string>();

            if (string.IsNullOrEmpty(value))
                return notHeld;

            foreach (var pair in value.Split(PAIR_SEPARATOR, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = pair.Trim();
                var separator = trimmed.IndexOf(CATEGORY_SEPARATOR);

                var held = separator > 0 && separator < trimmed.Length - 1 &&
                           tags.TrySetValue(trimmed[..separator].Trim(), trimmed[(separator + 1)..].Trim());

                if (!held && trimmed.Length > 0)
                    notHeld.Add(trimmed);
            }

            return notHeld;
        }

        // The declared type may be the abstract Tags itself; DefaultTags takes any category then.
        private static Tags Create(Type objectType) =>
            objectType == null || objectType.IsAbstract
                ? new DefaultTags()
                : (Tags)Activator.CreateInstance(objectType);
    }
}
