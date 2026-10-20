using System.Collections.Generic;
using fefek5.Toys.Runtime;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// ItemDef of type <c>tag_generator</c> of any category: its name and values are typed as Steam has them, e.g.
    /// <c>rarity</c> with <c>legendary: 1</c> and <c>common: 9</c>, where a <see cref="TagGeneratorDef{TEnum}"/> takes
    /// them from an enum.
    /// </summary>
    [CreateAssetMenu(fileName = "New Tag Generator", menuName = "SteamToys/Inventory/Tag Generator", order = 5)]
    public class DefaultTagGeneratorDef : TagGeneratorDef
    {
        #region Constants

        private const char VALUE_SEPARATOR = ';';
        private const char WEIGHT_SEPARATOR = ':';

        #endregion

        #region Inspector Serialized Fields

        [SerializeField] private string _tagName;

        [SerializeField] private SerializableDictionary<string, uint> _tagValuesAndChances = new();

        #endregion

        #region Getters and Setters

        public override string TagName => _tagName;

        public override Dictionary<string, uint> TagValuesAndChances => new(_tagValuesAndChances);

        public void SetTagName(string tagName) => _tagName = tagName;

        public override List<string> SetTagValuesAndChances(IReadOnlyDictionary<string, uint> tagValuesAndChances)
        {
            var notHeld = new List<string>();

            _tagValuesAndChances.Clear();

            foreach (var (tagValue, tagChance) in tagValuesAndChances)
            {
                // The separators of the Steam format cannot be part of a value.
                var valid = !string.IsNullOrWhiteSpace(tagValue) && tagValue.IndexOf(VALUE_SEPARATOR) < 0 &&
                            tagValue.IndexOf(WEIGHT_SEPARATOR) < 0;

                if (!valid || !_tagValuesAndChances.TryAdd(tagValue.Trim(), tagChance))
                    notHeld.Add(tagValue);
            }

            return notHeld;
        }

        #endregion
    }
}
