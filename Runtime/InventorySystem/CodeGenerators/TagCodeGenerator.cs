using System;
using System.Collections.Generic;
using fefek5.Toys.Runtime.Attributes;
using fefek5.Toys.Runtime.Types;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem.CodeGenerators
{
    /// <summary>
    /// Defines one tag category, e.g. <c>rarity</c>, and its values, and generates the enum of it that
    /// <see cref="Tag{TEnum}"/> and <see cref="TagGenerator{TEnum}"/> are made with. The enum is named after
    /// <see cref="TagName"/>, and Steam gets its name and the names of its values in snake_case.
    /// <para>
    /// Unity saves an enum by its number, so every value keeps the number it was given when it was added, and a number
    /// is never given twice: renaming, reordering or removing values leaves what is saved in assets as it was.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "New Tag", menuName = "SteamToys/Inventory/Code Generators/Tag", order = 20)]
    public class TagCodeGenerator : ScriptableObject
    {
        #region Properties

        /// <summary>The name of the enum, e.g. <c>Rarity</c>; Steam gets it as <c>rarity</c>.</summary>
        public string TagName
        {
            get => _tagName;
            set => _tagName = value;
        }

        /// <summary>The namespace of the enum; empty for none.</summary>
        public string Namespace
        {
            get => _namespace;
            set => _namespace = value;
        }

        /// <summary>The folder the script is written to; empty for the folder of this asset.</summary>
        public string OutputFolder
        {
            get => _outputFolder;
            set => _outputFolder = value;
        }

        public IReadOnlyList<TagValue> Values => _values;

        #endregion

        #region Inspector Serialized Fields

        [SerializeField, Tooltip("Name of the enum, e.g. Rarity. Steam gets it in snake_case: rarity.")]
        private string _tagName;

        [SerializeField, Tooltip("Namespace of the enum; empty for none.")]
        private string _namespace;

        [SerializeField, FolderPath, Tooltip("Folder the script is written to; empty for the folder of this asset.")]
        private string _outputFolder;

        [SerializeField, Tooltip("Values of the tag. Each keeps its number for good, so assets keep what they saved.")]
        private List<TagValue> _values = new();

        [SerializeField, HideInInspector]
        private int _nextNumber;

        #endregion

        /// <summary>Adds a value with a number no value has had yet.</summary>
        public TagValue AddValue(string valueName)
        {
            var value = new TagValue { Name = valueName, Number = _nextNumber++ };
            _values.Add(value);

            return value;
        }

        public bool RemoveValue(TagValue value) => _values.Remove(value);

        // A value the inspector adds copies the last one, number included, so it gets a new number here.
        private void OnValidate()
        {
            var used = new HashSet<int>();

            foreach (var value in _values)
            {
                if (value == null)
                    continue;

                if (value.Number < 0 || !used.Add(value.Number))
                {
                    value.Number = _nextNumber;
                    used.Add(value.Number);
                }

                _nextNumber = Math.Max(_nextNumber, value.Number + 1);
            }
        }
    }

    [Serializable]
    public class TagValue
    {
        [Tooltip("Name of the enum value, e.g. Legendary. Steam gets it in snake_case: legendary.")]
        public string Name;

        [Tooltip("The number the enum value is saved as. Given when the value is added; changing it changes what " +
                 "assets that saved this value read.")]
        public int Number;
    }
}
