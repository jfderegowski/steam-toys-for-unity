using System;
using System.Linq;
using Newtonsoft.Json;

namespace SteamToys.Runtime.InventorySystem.Converters.Price
{
    public class PriceConverter : JsonConverter<InventorySystem.Price>
    {
        private const string VERSION_PREFIX = "1;";
        private const string PRICE_SEPARATOR = ",";
        private const string SALE_SEPARATOR = ";";

        public override bool CanRead => false;

        public override void WriteJson(JsonWriter writer, InventorySystem.Price value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteValue(ToString(value));
        }

        // ReadJson intentionally not implemented; this converter is write-only.

        public override InventorySystem.Price ReadJson(JsonReader reader, Type objectType, InventorySystem.Price existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotSupportedException("PriceConverter is write-only. Read is not supported.");
        }

        /// <summary>
        /// Converts a <see cref="InventorySystem.Price"/> to the Steam format <c>"1;&lt;original price&gt;(;&lt;sale&gt;)*"</c>,
        /// e.g. <c>"1;USD100,EUR080;20130609T080000Z-20130606T080000ZUSD50,EUR40"</c>. The sales go latest first, as
        /// Steam requires, and the incomplete ones are left out. Empty without a regular price, which Steam needs first.
        /// <see href="https://partner.steamgames.com/doc/features/inventory/schema#SpecifyingPrices"/>
        /// </summary>
        public static string ToString(InventorySystem.Price value)
        {
            if (value?.RegularPrices == null)
                return string.Empty;

            var regular = string.Join(PRICE_SEPARATOR, value.RegularPrices
                .Where(price => price != null)
                .Select(PriceValueConverter.ToString));

            if (regular.Length == 0)
                return string.Empty;

            var sales = (value.Promotions ?? Enumerable.Empty<Promotion>())
                .Where(promotion => promotion != null)
                .OrderByDescending(promotion => promotion.EndTime)
                .ThenByDescending(promotion => promotion.StartTime)
                .Select(PromotionConverter.ToString)
                .Where(sale => sale.Length > 0);

            return string.Join(SALE_SEPARATOR, sales.Prepend(VERSION_PREFIX + regular));
        }
    }
}
