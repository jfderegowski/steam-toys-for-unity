using System;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.ExchangeVariable;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem.Converters
{
    /// <summary>
    /// Custom JSON converter for <see cref="Item"/> that produces
    /// the exact format expected by the Steam Inventory Service.
    /// </summary>
    public class SteamInventoryItemConverter : JsonConverter<Item>
    {
        public override bool CanRead => false;

        public override void WriteJson(JsonWriter writer, Item value, JsonSerializer serializer)
        {
            writer.WriteStartObject();

            // itemdefid — always required
            writer.WritePropertyName("itemdefid");
            writer.WriteValue(value.ItemDefId);

            // type — enum as lowercase string
            writer.WritePropertyName("type");
            writer.WriteValue(ItemTypeConverter.ToString(value.Type));

            // name
            WriteIfNotEmpty(writer, "name", value.Name);

            // description
            WriteIfNotEmpty(writer, "description", value.Description);

            // name_color — written as hex without alpha, e.g. "FF0000"
            if (!IsDefaultColor(value.NameColor))
            {
                writer.WritePropertyName("name_color");
                writer.WriteValue(ColorToHex(value.NameColor));
            }

            // background_color
            if (!IsDefaultColor(value.BackgroundColor))
            {
                writer.WritePropertyName("background_color");
                writer.WriteValue(ColorToHex(value.BackgroundColor));
            }

            // icon_url
            WriteUrlIfNotEmpty(writer, "icon_url", value.IconUrl);

            // icon_url_large
            WriteUrlIfNotEmpty(writer, "icon_url_large", value.IconUrlLarge);

            // tags
            WriteIfNotEmpty(writer, "tags", value.Tags);

            // tradable
            writer.WritePropertyName("tradable");
            writer.WriteValue(value.Tradable);

            // marketable
            writer.WritePropertyName("marketable");
            writer.WriteValue(value.Marketable);

            // exchange
            WriteExchangeIfNotEmpty(writer, "exchange", value.Exchange);

            // bundle
            WriteBundleIfNotEmpty(writer, "bundle", value.Bundle);

            // price
            if (value.Price != null)
            {
                var priceStr = value.Price.ToSteamString();
                if (!string.IsNullOrEmpty(priceStr))
                {
                    writer.WritePropertyName("price");
                    writer.WriteValue(priceStr);
                }
            }

            // promo
            WritePromoIfNotEmpty(writer, "promo", value.Promo);

            // drop_start_time
            WriteSteamDateTimeIfHasValue(writer, "drop_start_time", value.DropStartTime);

            writer.WriteEndObject();
        }

        public override Item ReadJson(JsonReader reader, Type objectType,
            Item existingValue, bool hasExistingValue, JsonSerializer serializer) => null;

        #region Helper Methods

        private static void WriteIfNotEmpty(JsonWriter writer, string propertyName, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            writer.WritePropertyName(propertyName);
            writer.WriteValue(value);
        }

        private static void WriteUrlIfNotEmpty(JsonWriter writer, string propertyName, Url value)
        {
            if (ReferenceEquals(value, null) || !value.HasValue)
                return;

            writer.WritePropertyName(propertyName);
            writer.WriteValue(value.Value);
        }


        /// <summary>
        /// Converts Color32 to a 6-character hex string (RGB, no alpha), e.g. "FF0000".
        /// </summary>
        private static string ColorToHex(Color32 color)
        {
            return $"{color.r:X2}{color.g:X2}{color.b:X2}";
        }

        /// <summary>
        /// Parses a 6-character hex string (RGB) back to Color32 (alpha = 255).
        /// </summary>
        private static Color32 HexToColor(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length < 6)
                return new Color32(0, 0, 0, 255);

            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
            return new Color32(r, g, b, 255);
        }

        private static bool IsDefaultColor(Color32 color)
        {
            return color.r == 0 && color.g == 0 && color.b == 0 && color.a == 0;
        }

        private static void WriteBundleIfNotEmpty(JsonWriter writer, string propertyName, Bundle value)
        {
            if (value == null || value.Count == 0)
                return;

            writer.WritePropertyName(propertyName);
            writer.WriteValue(BundleConverter.ToString(value));
        }

        private static void WritePromoIfNotEmpty(JsonWriter writer, string propertyName, Promo value)
        {
            if (value == null || value.Count == 0)
                return;

            writer.WritePropertyName(propertyName);
            writer.WriteValue(PromoConverter.ToString(value));
        }

        private static void WriteExchangeIfNotEmpty(JsonWriter writer, string propertyName, Exchange value)
        {
            if (value == null || value.Count == 0)
                return;

            writer.WritePropertyName(propertyName);
            writer.WriteValue(value.ToSteamString());
        }

        private static void WriteSteamDateTimeIfHasValue(JsonWriter writer, string propertyName, SteamDateTime value)
        {
            if (value == null || !value.HasValue)
                return;

            writer.WritePropertyName(propertyName);
            writer.WriteValue(value.ToSteamString());
        }

        #endregion
    }
}
