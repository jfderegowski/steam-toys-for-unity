using fefek5.Toys.Runtime;
using SteamToys.Runtime.InventorySystem;
using UnityEngine;

namespace SteamToys.Tests.Tests
{
    public enum Rarity
    {
        Common,
        Uncommon,
        Rare,
        Legendary,
        Immortal,
    }

    public enum Wear
    {
        FactoryNew,
        MinimalWear,
        FieldTested,
        WellWorn,
        BattleScarred,
    }

    [CreateAssetMenu(fileName = "TestItem", menuName = "SteamToys/Inventory/Test/TestItem", order = 1)]
    public class TestItem : Item<TestItemTags>
    {
        
    }
}