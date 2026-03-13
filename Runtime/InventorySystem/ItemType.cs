using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.Converters;

namespace SteamToys.Runtime.InventorySystem
{
    [JsonConverter(typeof(ItemTypeConverter))]
    public enum ItemType
    {
        Item,
        Bundle,
        Generator,
        PlaytimeGenerator,
        TagGenerator,
    }
}