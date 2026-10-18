using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>ItemDef of type <c>item</c> with tags of any category.</summary>
    [CreateAssetMenu(fileName = "New Item", menuName = "SteamToys/Inventory/Item", order = 1)]
    public class DefaultItemDef : ItemDef<DefaultTags> { }
}
