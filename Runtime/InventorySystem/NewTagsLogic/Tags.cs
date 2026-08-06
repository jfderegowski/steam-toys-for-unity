using System;
using System.Collections.Generic;
using SteamToys.Runtime.InventorySystem.Converters;

namespace SteamToys.Runtime.InventorySystem.NewTagsLogic
{
    [Serializable]
    public abstract class Tag
    {
        public abstract string GetName();

        public abstract string GetValue();

        public abstract Type GetValueType();
    }
    
    [Serializable]
    public class Tag<TEnum> : Tag where TEnum : Enum
    {
        public TEnum Value;

        public sealed override Type GetValueType() => typeof(TEnum);

        public override string GetName() => typeof(TEnum).Name.ToSnakeCase();

        public override string GetValue() => Value.ToString().ToSnakeCase();
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
}