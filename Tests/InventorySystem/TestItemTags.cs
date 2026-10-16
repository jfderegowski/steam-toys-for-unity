using System;
using System.Collections.Generic;
using SteamToys.Runtime.InventorySystem;
using SteamToys.Runtime.InventorySystem.Converters;

namespace SteamToys.Tests.Tests
{
    /// <summary>Tags of the rarity and wear categories only.</summary>
    [Serializable]
    public class TestItemTags : Tags
    {
        public Rarity Rarity;
        
        public Wear Wear;

        public override IEnumerable<string> Keys { get; } = new[] { "rarity", "wear" };

        public override bool TryGetValue(string tagName, out string tagValue)
        {
            switch (tagName)
            {
                case "rarity":
                    tagValue = Rarity.ToString().ToKebabCase();
                    return true;
                case "wear":
                    tagValue = Wear.ToString().ToKebabCase();
                    return true;
                default:
                    tagValue = null;
                    return false;
            }
        }

        public override bool TrySetValue(string tagName, string tagValue)
        {
            switch (tagName)
            {
                case "rarity":
                    Rarity = (Rarity)Enum.Parse(typeof(Rarity), tagValue);
                    return true;
                case "wear":
                    Wear = (Wear)Enum.Parse(typeof(Wear), tagValue);
                    return true;
                default:
                    return false;
            }
        }
    }
}