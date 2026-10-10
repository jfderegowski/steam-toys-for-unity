using System;
using Newtonsoft.Json;

namespace SteamToys.Runtime.InventorySystem.Converters
{
    /// <summary>
    /// Custom JSON converter for <see cref="ItemDef"/> that produces
    /// the exact format expected by the Steam Inventory Service.
    /// Each ItemDef type writes its own properties through <see cref="ItemDef.WriteJsonProperties"/>.
    /// </summary>
    public class ItemConverter : JsonConverter<ItemDef>
    {
        public override bool CanRead => false;

        public override void WriteJson(JsonWriter writer, ItemDef value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartObject();

            value.WriteJsonProperties(writer);

            writer.WriteEndObject();
        }

        public override ItemDef ReadJson(JsonReader reader, Type objectType,
            ItemDef existingValue, bool hasExistingValue, JsonSerializer serializer) => null;
    }
}
