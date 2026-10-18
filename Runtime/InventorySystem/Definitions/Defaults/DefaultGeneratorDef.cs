using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>ItemDef of type <c>generator</c> with tags of any category.</summary>
    [CreateAssetMenu(fileName = "New Generator", menuName = "SteamToys/Inventory/Generator", order = 3)]
    public class DefaultGeneratorDef : GeneratorDef<DefaultTags> { }
}
