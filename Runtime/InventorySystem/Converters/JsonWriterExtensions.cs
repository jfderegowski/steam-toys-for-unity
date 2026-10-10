using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.ExchangeVariable;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem.Converters
{
    /// <summary>
    /// Writes single ItemDef properties in the format the Steam Inventory Service expects.
    /// Every method skips the property when it holds its Steam default, so the JSON stays minimal.
    /// </summary>
    public static class JsonWriterExtensions
    {
        private const char LIST_SEPARATOR = ';';

        public static void WriteIfNotEmpty(this JsonWriter writer, string propertyName, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            writer.WritePropertyName(propertyName);
            writer.WriteValue(value);
        }

        public static void WriteBoolIfTrue(this JsonWriter writer, string propertyName, bool value)
        {
            if (!value)
                return;

            writer.WritePropertyName(propertyName);
            writer.WriteValue(true);
        }

        public static void WriteUintIfNotZero(this JsonWriter writer, string propertyName, uint value)
        {
            if (value == 0)
                return;

            writer.WritePropertyName(propertyName);
            writer.WriteValue(value);
        }

        public static void WriteUrlIfNotEmpty(this JsonWriter writer, string propertyName, Url value)
        {
            if (ReferenceEquals(value, null) || !value.HasValue)
                return;

            writer.WritePropertyName(propertyName);
            writer.WriteValue(value.Value);
        }

        /// <summary>
        /// Writes the color as a 6-character hex string (RGB, no alpha), e.g. "FF0000".
        /// A fully transparent black counts as unset.
        /// </summary>
        public static void WriteColorIfSet(this JsonWriter writer, string propertyName, Color32 value)
        {
            if (value.r == 0 && value.g == 0 && value.b == 0 && value.a == 0)
                return;

            writer.WritePropertyName(propertyName);
            writer.WriteValue($"{value.r:X2}{value.g:X2}{value.b:X2}");
        }

        public static void WriteSteamDateTimeIfHasValue(this JsonWriter writer, string propertyName, SteamDateTime value)
        {
            if (value == null || !value.HasValue)
                return;

            writer.WritePropertyName(propertyName);
            writer.WriteValue(value.ToSteamString());
        }

        public static void WriteBundleIfNotEmpty(this JsonWriter writer, string propertyName, BundleContent value)
        {
            if (value == null || value.Count == 0)
                return;

            writer.WriteIfNotEmpty(propertyName, BundleContentConverter.ToString(value));
        }

        public static void WritePromoIfNotEmpty(this JsonWriter writer, string propertyName, Promo value)
        {
            if (value == null || value.Count == 0)
                return;

            writer.WriteIfNotEmpty(propertyName, PromoConverter.ToString(value));
        }

        public static void WriteExchangeIfNotEmpty(this JsonWriter writer, string propertyName, Exchange value)
        {
            if (value == null || value.Count == 0)
                return;

            writer.WriteIfNotEmpty(propertyName, value.ToSteamString());
        }

        public static void WritePriceIfNotEmpty(this JsonWriter writer, string propertyName, InventorySystem.Price value)
        {
            if (value == null)
                return;

            writer.WriteIfNotEmpty(propertyName, value.ToSteamString());
        }

        /// <summary>
        /// Writes the itemdefid of the referenced definition, skipping a missing reference.
        /// </summary>
        public static void WriteItemDefIdIfSet(this JsonWriter writer, string propertyName, ItemDef value)
        {
            if (value == null)
                return;

            writer.WritePropertyName(propertyName);
            writer.WriteValue(value.ItemDefId);
        }

        /// <summary>
        /// Writes the itemdefids of the referenced definitions as a semicolon-delimited list, e.g. "1000;1001".
        /// </summary>
        public static void WriteItemDefIdsIfNotEmpty<T>(this JsonWriter writer, string propertyName, IEnumerable<T> values)
            where T : ItemDef
        {
            if (values == null)
                return;

            var stringBuilder = new StringBuilder();

            foreach (var value in values)
            {
                if (value == null)
                    continue;

                if (stringBuilder.Length > 0)
                    stringBuilder.Append(LIST_SEPARATOR);

                stringBuilder.Append(value.ItemDefId);
            }

            writer.WriteIfNotEmpty(propertyName, stringBuilder.ToString());
        }

        /// <summary>
        /// Writes the values as a semicolon-delimited list, skipping blank entries.
        /// </summary>
        public static void WriteListIfNotEmpty<T>(this JsonWriter writer, string propertyName, IEnumerable<T> values)
        {
            if (values == null)
                return;

            var stringBuilder = new StringBuilder();

            foreach (var value in values)
            {
                var text = value?.ToString();

                if (string.IsNullOrWhiteSpace(text))
                    continue;

                if (stringBuilder.Length > 0)
                    stringBuilder.Append(LIST_SEPARATOR);

                stringBuilder.Append(text.Trim());
            }

            writer.WriteIfNotEmpty(propertyName, stringBuilder.ToString());
        }
    }
}
