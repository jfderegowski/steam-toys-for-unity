using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>ItemDef of type <c>playtimegenerator</c> with tags of any category.</summary>
    [CreateAssetMenu(fileName = "New Playtime Generator", menuName = "SteamToys/Inventory/Playtime Generator/Default Playtime Generator", order = -3)]
    public class DefaultPlaytimeGeneratorDef : PlaytimeGeneratorDef<DefaultTags> { }
}