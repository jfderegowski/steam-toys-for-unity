using System.Collections.Generic;
using System.Linq;
using System.Text;
using SteamToys.Runtime.InventorySystem.CodeGenerators;
using SteamToys.Runtime.InventorySystem.Converters;
using UnityEditor;

namespace SteamToys.Editor.InventorySystem.CodeGenerators
{
    /// <summary>
    /// The <see cref="TagCodeGenerator"/> inspector, whose Generate writes the enum, e.g.
    /// <c>public enum Rarity { Common = 0, Rare = 1 }</c>, with every value at its own number.
    /// </summary>
    [CustomEditor(typeof(TagCodeGenerator))]
    public class TagCodeGeneratorEditor : CodeGeneratorEditor
    {
        private new TagCodeGenerator Generator => (TagCodeGenerator)target;

        protected override string ClassNameExample => "Rarity";

        protected override void CollectProblems(List<string> problems) => CollectValueProblems(Generator, problems);

        protected override string BuildBody()
        {
            var body = new StringBuilder();

            body.Append($"/// <summary>Values of the Steam tag <c>{Generator.ClassName.ToSnakeCase()}</c>.</summary>\n");
            body.Append($"public enum {Generator.ClassName}\n{{\n");

            foreach (var value in Generator.Values.Where(value => value != null))
                body.Append($"    {value.Name} = {value.Number},\n");

            body.Append("}\n");

            return body.ToString();
        }

        /// <summary>
        /// Adds a message for everything in <paramref name="generator"/> that keeps it from generating, besides where
        /// it writes.
        /// </summary>
        internal static void CollectTagProblems(TagCodeGenerator generator, List<string> problems)
        {
            CollectNameProblems("Class Name", generator.ClassName, "Rarity", problems);
            CollectNamespaceProblems(generator.Namespace, problems);
            CollectValueProblems(generator, problems);
        }

        private static void CollectValueProblems(TagCodeGenerator generator, List<string> problems)
        {
            if (generator.Values.Count == 0)
            {
                problems.Add("The tag has no values.");
                return;
            }

            var names = new HashSet<string>();
            var steamNames = new Dictionary<string, string>();

            foreach (var value in generator.Values)
            {
                var count = problems.Count;

                CollectNameProblems("A value name", value?.Name, "Legendary", problems);

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
    }
}
