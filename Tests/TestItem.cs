using System;
using System.Collections.Generic;
using SteamToys.Runtime.InventorySystem;
using SteamToys.Runtime.InventorySystem.NewTagsLogic;
using UnityEngine;
using Tags = SteamToys.Runtime.InventorySystem.NewTagsLogic.Tags;

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

        public Tag<Rarity> RarityTag = new Tag<Rarity>();
        public Tag<Quality> QualityTag = new Tag<Quality>();

        public override HashSet<Tag> GetTags() =>
            new HashSet<Tag> { RarityTag, QualityTag };
    }

    [CreateAssetMenu(fileName = "TestItem", menuName = "SteamToys/Inventory/TestItem", order = 1)]
    public class TestItem : Item<TestItemTags>
    {
        [Header("New Tags Testign")]
        [SerializeField] private ExampleTags _exampleTags;
    }
  
    public enum RarityType
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }
    
    public enum ConditionType
    {
        New,
        Used,
        Refurbished
    }
    
    [Serializable]
    public class ExampleTagRarity : Tag<RarityType> { }
    
    [Serializable]
    public class ExampleTagCondition : Tag<ConditionType> { }

    [Serializable]
    public class ExampleTags : Tags
    {
        public Tag<RarityType> Rarity;
        public Tag<ConditionType> Condition;
        public ExampleTagRarity ExampleRarity;
        public ExampleTagCondition ExampleCondition;
        
        public override HashSet<Tag> GetTags()
        {
            return new HashSet<Tag> { Rarity, Condition, ExampleRarity, ExampleCondition };
        }
    }
}