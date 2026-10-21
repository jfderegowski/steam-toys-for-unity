using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SteamToys.Runtime.InventorySystem.Converters;
using UnityEditor;
using UnityEngine;

namespace SteamToys.Editor.InventorySystem.CodeGenerators
{
    /// <summary>
    /// One tag category, e.g. <c>rarity</c>, and its values. It generates the enum of it, e.g.
    /// <c>public enum Rarity { Common = 0, Rare = 1 }</c>, that the generated <c>Tags</c> classes and
    /// <c>TagGeneratorDef&lt;TEnum&gt;</c> are made with, and with <see cref="GenerateTagGeneratorDef"/> the tag
    /// generator class of it, e.g. <c>RarityTagGeneratorDef</c>. Steam gets the name of the enum and the names of its
    /// values in snake_case.
    /// <para>
    /// Unity saves an enum by its number, so every value keeps the number it was given when it was added, and a number
    /// is never given twice: renaming, reordering or removing values leaves what is saved in assets as it was.
    /// </para>
    /// </summary>
    [Serializable]
    public class TagCodeGenerator : CodeGenerator
    {
        #region Constants

        private const string ENUM_KEY = "Enum";
        private const string TAG_GENERATOR_DEF_KEY = "TagGeneratorDef";
        private const string TAG_GENERATOR_DEF_SUFFIX = "TagGeneratorDef";
        private const string TAG_GENERATOR_DEF_TYPE = "global::SteamToys.Runtime.InventorySystem.TagGeneratorDef";

        #endregion

        #region Properties

        public override string Kind => "Tag";

        public override string ClassNameExample => "Rarity";

        public IReadOnlyList<TagValue> Values => _values;

        /// <summary>Whether the tag generator class of the tag is generated too, e.g. <c>RarityTagGeneratorDef</c>.</summary>
        public bool GenerateTagGeneratorDef
        {
            get => _generateTagGeneratorDef;
            set => _generateTagGeneratorDef = value;
        }

        /// <summary>The enum from the global namespace, e.g. <c>global::Game.Rarity</c>.</summary>
        public string EnumName => CodeGeneration.GetGlobalName(this, ClassName);

        /// <summary>Whether the enum is generated as this is now, so it exists under <see cref="EnumName"/>.</summary>
        public bool IsEnumGenerated => CodeGeneration.IsGenerated(this, ENUM_KEY, ClassName);

        /// <summary>The name Steam gets, e.g. <c>rarity</c>.</summary>
        public string SteamName => ClassName.ToSnakeCase();

        #endregion

        #region Serialized Fields

        [SerializeField, Tooltip("Values of the tag. Each keeps its number for good, so assets keep what they saved.")]
        private List<TagValue> _values = new();

        [SerializeField, Tooltip("Generate the tag generator class of the tag too, e.g. RarityTagGeneratorDef.")]
        private bool _generateTagGeneratorDef = true;

        [SerializeField, HideInInspector]
        private int _nextNumber;

        #endregion

        #region Values

        /// <summary>Adds a value with a number no value has had yet.</summary>
        public TagValue AddValue(string valueName, int number = -1)
        {
            var value = new TagValue { Name = valueName, Number = number >= 0 ? number : _nextNumber };
            _values.Add(value);

            Validate();

            return value;
        }

        public bool RemoveValue(TagValue value) => _values.Remove(value);

        /// <summary>
        /// Gives a new number to a value whose number another value already has, e.g. one the list added as a copy of
        /// the last one, and keeps the next number past every number given.
        /// </summary>
        public override bool Validate()
        {
            var used = new HashSet<int>();
            var changed = false;

            foreach (var value in _values)
            {
                if (value == null)
                    continue;

                if (value.Number < 0 || !used.Add(value.Number))
                {
                    value.Number = _nextNumber;
                    used.Add(value.Number);
                    changed = true;
                }

                if (value.Number + 1 > _nextNumber)
                {
                    _nextNumber = value.Number + 1;
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>The number the next value gets, past every number given so far.</summary>
        public int NextNumber
        {
            get => _nextNumber;
            set => _nextNumber = Math.Max(_nextNumber, value);
        }

        #endregion

        #region Generation

        public override IEnumerable<ScriptSpec> GetScriptSpecs()
        {
            yield return new ScriptSpec(ENUM_KEY, ClassName, BuildEnum);

            if (GenerateTagGeneratorDef)
                yield return new ScriptSpec(TAG_GENERATOR_DEF_KEY, ClassName + TAG_GENERATOR_DEF_SUFFIX, BuildTagGeneratorDef);
        }

        private string BuildEnum()
        {
            var body = new StringBuilder();

            body.Append($"/// <summary>Values of the Steam tag <c>{SteamName}</c>.</summary>\n");
            body.Append($"public enum {ClassName}\n{{\n");

            foreach (var value in Values.Where(value => value != null))
                body.Append($"    {value.Name} = {value.Number},\n");

            body.Append("}\n");

            return body.ToString();
        }

        private string BuildTagGeneratorDef()
        {
            var className = ClassName + TAG_GENERATOR_DEF_SUFFIX;
            var label = $"{ObjectNames.NicifyVariableName(ClassName)} Tag Generator";

            return $"/// <summary>Tag generator of the Steam tag <c>{SteamName}</c>.</summary>\n" +
                   CodeGeneration.GetAssetMenuAttribute(this, label) +
                   $"public partial class {className} : {TAG_GENERATOR_DEF_TYPE}<{EnumName}> {{ }}\n";
        }

        public override void CollectProblems(List<string> problems)
        {
            if (Values.Count == 0)
            {
                problems.Add("The tag has no values.");
                return;
            }

            var names = new HashSet<string>();
            var steamNames = new Dictionary<string, string>();

            foreach (var value in Values)
            {
                var count = problems.Count;

                CodeGeneration.CollectNameProblems("A value name", value?.Name, "Legendary", problems);

                if (problems.Count > count)
                    continue;

                if (!names.Add(value!.Name))
                {
                    problems.Add($"Value {value.Name} is listed more than once.");
                    continue;
                }

                // Steam gets the snake_case name, so two names that read the same there are one value to it.
                var steamName = value.Name.ToSnakeCase();

                if (steamNames.TryGetValue(steamName, out var other))
                    problems.Add($"Values {other} and {value.Name} are both {steamName} on Steam.");
                else
                    steamNames.Add(steamName, value.Name);
            }
        }

        /// <summary>Whether the name, namespace and values are right, so the enum can be generated.</summary>
        public bool IsValid()
        {
            var problems = new List<string>();

            CodeGeneration.CollectNameProblems("Class Name", ClassName, ClassNameExample, problems);
            CodeGeneration.CollectNamespaceProblems(Namespace, problems);
            CollectProblems(problems);

            return problems.Count == 0;
        }

        #endregion
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
