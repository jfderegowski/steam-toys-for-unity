using System;
using Newtonsoft.Json;

namespace SteamToys.Runtime.InventorySystem.Converters.Price
{
    public class CurrencyTypeConverter : JsonConverter<CurrencyType>
    {
        public override bool CanRead => false;

        public override void WriteJson(JsonWriter writer, CurrencyType value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }

        public override CurrencyType ReadJson(JsonReader reader, Type objectType, CurrencyType existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotSupportedException("CurrencyTypeConverter is write-only. Read is not supported.");
        }
    }
}
