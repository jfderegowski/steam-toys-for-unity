using System;
using System.Collections.Generic;

namespace SteamToys.Runtime.InventorySystem.NewTagsLogic
{
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
    public abstract class Tag
    {
        public abstract string GetName();

        public abstract string GetValue();

        public virtual Type GetValueType() => typeof(string);
    }
    
    [Serializable]
    public class Tag<TEnum> : Tag where TEnum : Enum
    {
        public TEnum Value;

        public sealed override Type GetValueType() => typeof(TEnum);

        public override string GetName() => typeof(TEnum).Name;

        public override string GetValue() => Value.ToString();
    }

    [Serializable]
    public abstract class Tags
    {
        public abstract HashSet<Tag> GetTags();
        
        public TEnum GetValue<TEnum>() where TEnum : Enum
        {
            var tags = GetTags();
            
            foreach (var tag in tags)
            {
                if (tag.GetValueType() == typeof(TEnum))
                    return ((Tag<TEnum>)tag).Value;
            }

            return default;
        }
        
        public string GetValue(string tagName)
        {
            var tags = GetTags();
            
            foreach (var tag in tags)
            {
                if (tag.GetName() == tagName)
                    return tag.GetValue();
            }

            return null;
        }
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
            return new HashSet<Tag> { Rarity, Condition };
        }
    }
}