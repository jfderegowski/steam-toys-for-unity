using System;
using System.Collections.Generic;
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
    
    [Serializable]
    public class TestItemTags : Tags
    {
        public Tag<Rarity> Rarity = new();
        public Tag<Wear> Quality = new();

        public override HashSet<Tag> GetTags() => new() { Rarity, Quality };
    }

    [CreateAssetMenu(fileName = "TestItem", menuName = "SteamToys/Inventory/Test/TestItem", order = 1)]
    public class TestItem : Item<TestItemTags>
    {
        
    }
}