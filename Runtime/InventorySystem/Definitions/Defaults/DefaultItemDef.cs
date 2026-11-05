using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>ItemDef of type <c>item</c> with tags of any category.</summary>
    [CreateAssetMenu(fileName = "New Item", menuName = "SteamToys/Inventory/Item/Default Item", order = -6)]
    public class DefaultItemDef : ItemDef<DefaultTags> { }
}
