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
    
    /// <summary>Tags of the rarity and wear categories only.</summary>
    [Serializable]
    public class TestItemDefaultTags : DefaultTags
    {
        private static readonly HashSet<string> Categories = new() { GetCategory<Rarity>(), GetCategory<Wear>() };

        public override bool CanHold(string category) => Categories.Contains(category);
    }

    [CreateAssetMenu(fileName = "TestItem", menuName = "SteamToys/Inventory/Test/TestItem", order = 1)]
    public class TestItem : Item<TestItemDefaultTags>
    {
        
    }
}