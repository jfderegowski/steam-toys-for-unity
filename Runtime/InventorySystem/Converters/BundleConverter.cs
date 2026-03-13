using System;
using System.Text;
using Newtonsoft.Json;

namespace SteamToys.Runtime.InventorySystem.Converters
{
    /// <summary>
    /// Custom JSON converter for <see cref="Bundle"/> that serializes/deserializes
    /// to/from the Steam Inventory Service bundle format.
    /// <para>
    /// Steam expects a semicolon-delimited string of <c>itemdefidxquantity</c> pairs,
    /// e.g. <c>"100x1;101x5;102x3"</c>.
    /// </para>
    /// <see href="https://partner.steamgames.com/doc/features/inventory/schema#SpecifyBundles"/>
    /// </summary>
    public class BundleConverter : JsonConverter<Bundle>
    {
        private const char ENTRY_SEPARATOR = ';';

        public override void WriteJson(JsonWriter writer, Bundle value, JsonSerializer serializer)
        {
            if (value == null || value.Count == 0)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteValue(ToString(value));
        }

        public override Bundle ReadJson(JsonReader reader, Type objectType,
            Bundle existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return new Bundle();

            var str = reader.Value as string ?? string.Empty;
            return FromString(str);
        }

        /// <summary>
        /// Converts a <see cref="Bundle"/> to the Steam bundle string format.
        /// Example output: <c>"100x1;101x5;102x3"</c>
        /// </summary>
        public static string ToString(Bundle bundle)
        {
            if (bundle == null || bundle.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();

            for (int i = 0; i < bundle.Count; i++)
            {
                if (i > 0)
                    sb.Append(ENTRY_SEPARATOR);

                sb.Append(BundleElementConverter.ToString(bundle[i]));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Parses a Steam bundle string (e.g. <c>"100x1;101x5;102x3"</c>) into a <see cref="Bundle"/>.
        /// </summary>
        public static Bundle FromString(string value)
        {
            var bundle = new Bundle();

            if (string.IsNullOrEmpty(value))
                return bundle;

            var entries = value.Split(ENTRY_SEPARATOR);

            foreach (var entry in entries)
            {
                var element = BundleElementConverter.FromString(entry);

                if (element != null)
                    bundle.Add(element);
            }

            return bundle;
        }
    }
}





