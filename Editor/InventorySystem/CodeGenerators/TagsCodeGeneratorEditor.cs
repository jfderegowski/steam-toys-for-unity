using System.Collections.Generic;
using System.Linq;
using System.Text;
using SteamToys.Runtime.InventorySystem.CodeGenerators;
using SteamToys.Runtime.InventorySystem.Converters;
using UnityEditor;

namespace SteamToys.Editor.InventorySystem.CodeGenerators
{
    /// <summary>
    /// The <see cref="TagsCodeGenerator"/> inspector, whose Generate writes <c>OutputFolder/ClassName.cs</c>: a class
    /// holding the two <c>Tags</c> classes, <c>ClassNameItemDef</c> with the tags not marked generated and
    /// <c>ClassNameInventoryItem</c> with the ones marked generated. Each tag is a property of its enum that is null
    /// while the tag is not set.
    /// </summary>
    [CustomEditor(typeof(TagsCodeGenerator))]
    public class TagsCodeGeneratorEditor : CodeGeneratorEditor
    {
        #region Constants

        private const string TAGS_TYPE = "global::SteamToys.Runtime.InventorySystem.Tags";
        private const string TAG_TYPE = "global::SteamToys.Runtime.InventorySystem.Tag";
        private const string HAS_VALUE_TYPE = "global::fefek5.Toys.Runtime.HasValue";
        private const string TAG_NAMES_FIELD = "TagNames";
        private const string ITEM_DEF_SUFFIX = "ItemDef";
        private const string INVENTORY_ITEM_SUFFIX = "InventoryItem";

        // A property of one of these names would hide or clash with a member every Tags class has.
        private static readonly HashSet<string> ReservedNames = new()
        {
            "Keys", "Values", "Count", "Item", "TryGetValue", "TrySetValue", "ContainsKey", "GetEnumerator", "Equals",
            "GetHashCode", "GetType", "ToString", "MemberwiseClone", "Finalize", TAG_NAMES_FIELD
        };

        #endregion

        private new TagsCodeGenerator Generator => (TagsCodeGenerator)target;

        private string ItemDefClassName => Generator.ClassName + ITEM_DEF_SUFFIX;

        private string InventoryItemClassName => Generator.ClassName + INVENTORY_ITEM_SUFFIX;

        protected override string ClassNameExample => "EventTags";

        #region Code

        protected override string BuildBody()
        {
            var itemDef = BuildTagsClass(ItemDefClassName, false,
                $"Tags the definitions set. The tags tag generators set on an owned item are in {InventoryItemClassName}.");
            var inventoryItem = BuildTagsClass(InventoryItemClassName, true,
                $"Tags Steam reports on an owned item, set by tag generators when it is made. The tags of its definition are in {ItemDefClassName}.");

            return $"/// <summary>The tags of {Generator.ClassName}: {ItemDefClassName} and {InventoryItemClassName}.</summary>\n" +
                   $"public class {Generator.ClassName}\n{{\n{Indent(itemDef)}\n{Indent(inventoryItem)}}}\n";
        }

        /// <summary>A <c>Tags</c> class of the tags marked <paramref name="generated"/> or of the others.</summary>
        private string BuildTagsClass(string className, bool generated, string summary)
        {
            var tags = Generator.Tags.Where(entry => entry.Generated == generated).Select(entry => entry.Tag).ToList();
            var body = new StringBuilder();

            body.Append($"/// <summary>{summary}</summary>\n");
            body.Append("[global::System.Serializable]\n");
            body.Append($"public class {className} : {TAGS_TYPE}\n{{\n");
            body.Append($"    private static readonly string[] {TAG_NAMES_FIELD} = {{ {string.Join(", ", tags.Select(tag => $"\"{GetSteamName(tag)}\""))} }};\n");

            foreach (var tag in tags)
                AppendTag(body, tag);

            body.Append("\n");
            body.Append($"    public override global::System.Collections.Generic.IEnumerable<string> Keys => {TAG_NAMES_FIELD};\n\n");

            AppendTryGetValue(body, tags);
            body.Append("\n");
            AppendTrySetValue(body, tags);

            body.Append("}\n");

            return body.ToString();
        }

        private static void AppendTag(StringBuilder body, TagCodeGenerator tag)
        {
            var enumName = GetGlobalName(tag);
            var field = GetFieldName(tag);
            var steamName = GetSteamName(tag);

            body.Append("\n");
            body.Append($"    [global::UnityEngine.SerializeField, global::UnityEngine.Tooltip(\"Steam tag {steamName}.\")]\n");
            body.Append($"    private {HAS_VALUE_TYPE}<{enumName}> {field};\n\n");
            body.Append($"    /// <summary>The Steam tag <c>{steamName}</c>; null when it is not set.</summary>\n");
            body.Append($"    public {enumName}? {tag.ClassName}\n    {{\n");
            body.Append($"        get => {field}.hasValue ? {field}.value : null;\n");
            body.Append($"        set => {field} = new {HAS_VALUE_TYPE}<{enumName}>(value.GetValueOrDefault(), value.HasValue);\n");
            body.Append("    }\n");
        }

        private static void AppendTryGetValue(StringBuilder body, List<TagCodeGenerator> tags)
        {
            body.Append("    public override bool TryGetValue(string tagName, out string tagValue)\n    {\n");
            body.Append("        tagValue = null;\n\n");
            body.Append("        switch (tagName)\n        {\n");

            foreach (var tag in tags)
            {
                var field = GetFieldName(tag);

                body.Append($"            case \"{GetSteamName(tag)}\":\n");
                body.Append($"                if (!{field}.hasValue)\n                    return false;\n\n");
                body.Append($"                tagValue = new {TAG_TYPE}<{GetGlobalName(tag)}> {{ Value = {field}.value }}.GetValue();\n");
                body.Append("                return true;\n");
            }

            body.Append("            default:\n                return false;\n");
            body.Append("        }\n    }\n");
        }

        private static void AppendTrySetValue(StringBuilder body, List<TagCodeGenerator> tags)
        {
            body.Append("    public override bool TrySetValue(string tagName, string tagValue)\n    {\n");
            body.Append("        switch (tagName)\n        {\n");

            foreach (var tag in tags)
            {
                var enumName = GetGlobalName(tag);

                body.Append($"            case \"{GetSteamName(tag)}\":\n            {{\n");
                body.Append($"                var tag = new {TAG_TYPE}<{enumName}>();\n\n");
                body.Append("                if (!tag.SetValue(tagValue))\n                    return false;\n\n");
                body.Append($"                {GetFieldName(tag)} = tag.Value;\n");
                body.Append("                return true;\n            }\n");
            }

            body.Append("            default:\n                return false;\n");
            body.Append("        }\n    }\n");
        }

        private static string GetSteamName(TagCodeGenerator tag) => tag.ClassName.ToSnakeCase();

        private static string GetFieldName(TagCodeGenerator tag) => "_" + char.ToLowerInvariant(tag.ClassName[0]) + tag.ClassName[1..];

        #endregion

        #region Problems

        protected override void CollectProblems(List<string> problems)
        {
            var tags = new HashSet<TagCodeGenerator>();
            var tagNames = new HashSet<string>();
            var classNames = new[] { ItemDefClassName, InventoryItemClassName };

            for (var i = 0; i < Generator.Tags.Count; i++)
            {
                var tag = Generator.Tags[i]?.Tag;

                if (!tag)
                {
                    problems.Add($"Tag {i} is empty.");
                    continue;
                }

                if (!tags.Add(tag))
                {
                    problems.Add($"{tag.name} is listed more than once.");
                    continue;
                }

                var tagProblems = new List<string>();
                TagCodeGeneratorEditor.CollectTagProblems(tag, tagProblems);

                if (tagProblems.Count > 0)
                {
                    problems.Add($"{tag.name} has problems of its own; fix them and generate it first.");
                    continue;
                }

                if (!IsGenerated(tag))
                    problems.Add($"{tag.name} is not generated as it is now, so its enum {tag.ClassName} may not exist; " +
                                 "generate it first.");

                if (!tagNames.Add(tag.ClassName))
                    problems.Add($"Two tags are named {tag.ClassName}, so they are one tag on Steam.");
                else if (ReservedNames.Contains(tag.ClassName))
                    problems.Add($"{tag.ClassName} is the name of a member every Tags class has, so a tag cannot have it.");
                else if (classNames.Contains(tag.ClassName))
                    problems.Add($"{tag.ClassName} is also the name of a class this generates, so a tag cannot have it.");
            }
        }

        #endregion
    }
}
