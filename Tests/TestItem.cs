using SteamToys.Runtime.InventorySystem;
using UnityEngine;

namespace SteamToys.Tests.Tests
{
    [System.Serializable]
    public class TestItemTags : Tags
    {
        public enum RarityType
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

        public enum QualityType
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

        public RarityType Rarity;
        public QualityType Quality;

        public override string[] GetTagNames() =>
            new[] { "rarity", "quality" };

        public override string GetTag(string tagName) =>
            tagName switch {
                "rarity" => Rarity.ToString(),
                "quality" => Quality.ToString(),
                _ => throw new System.ArgumentException($"Tag '{tagName}' not found.")
            };

        public override string SetTag(string tagName, string tagValue)
        {
            switch (tagName)
            {
                case "rarity":
                    if (!System.Enum.TryParse(tagValue, out RarityType rarity))
                        throw new System.ArgumentException($"Invalid value '{tagValue}' for tag 'rarity'.");
                    Rarity = rarity;
                    return tagValue;
                case "quality":
                    if (!System.Enum.TryParse(tagValue, out QualityType quality))
                        throw new System.ArgumentException($"Invalid value '{tagValue}' for tag 'quality'.");
                    Quality = quality;
                    return tagValue;
                default:
                    throw new System.ArgumentException($"Tag '{tagName}' not found.");
            }
        }
    }

    [CreateAssetMenu(fileName = "TestItem", menuName = "SteamToys/Inventory/TestItem", order = 1)]
    public class TestItem : Item<TestItemTags> { }
}