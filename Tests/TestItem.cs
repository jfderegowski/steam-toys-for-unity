using System;
using System.Collections.Generic;
using SteamToys.Runtime.InventorySystem;
using UnityEngine;

namespace SteamToys.Tests.Tests
{
    [Serializable]
    public class TestItemTags : Tags
    {
        public enum Rarity
        {
            Common,
            Uncommon,
            Rare,
            Mythical,
            Legendary,
            Ancient,
            Immortal,
            Arcana,
            Exalted,
            Vintage,
            Unusual
        }

        public enum Quality
        {
            Normal,
            Genuine,
            Vintage,
            Unusual,
            Unique,
            Community,
            Valve,
            SelfMade,
            Customized,
            Strangified,
            Completed
        }

        public Tag<Rarity> RarityTag = new();
        public Tag<Quality> QualityTag = new();

        public override HashSet<Tag> GetTags() => new() { RarityTag, QualityTag };
    }

    [CreateAssetMenu(fileName = "TestItem", menuName = "SteamToys/Inventory/TestItem", order = 1)]
    public class TestItem : Item<TestItemTags>
    {
        
    }
}