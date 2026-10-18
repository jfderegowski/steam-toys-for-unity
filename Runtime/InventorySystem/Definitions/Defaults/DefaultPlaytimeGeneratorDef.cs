using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>ItemDef of type <c>playtimegenerator</c> with tags of any category.</summary>
    [CreateAssetMenu(fileName = "New Playtime Generator", menuName = "SteamToys/Inventory/Playtime Generator", order = 4)]
    public class DefaultPlaytimeGeneratorDef : PlaytimeGeneratorDef<DefaultTags> { }
}