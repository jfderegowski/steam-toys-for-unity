using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SteamToys.Editor.InventorySystem.CodeGenerators
{
    /// <summary>
    /// A kind of item, e.g. <c>Event</c>, defined by its tags from <see cref="TagCodeGenerator"/>s. It generates
    /// <c>EventTags</c>, holding <c>EventTagsItemDef</c> with the tags its definitions set and
    /// <c>EventTagsInventoryItem</c> with the ones Steam reports on an owned item, each tag a property of its enum that
    /// is null while it is not set; and one script for each class chosen: <c>EventItemDef</c> and
    /// <c>EventInventoryItem</c>, <c>EventGeneratorDef</c> and <c>EventPlaytimeGeneratorDef</c>, all partial, so the
    /// game can add to them.
    /// <para>
    /// A generator copies its own tags to the item it makes, where Steam reports them on the owned item, so the
    /// generator classes take <c>EventTagsInventoryItem</c>, and a tag they set is marked
    /// <see cref="TagsEntry.OnInventoryItem"/>.
    /// </para>
    /// </summary>
    [Serializable]
    public class ItemDefsCodeGenerator : CodeGenerator
    {
        #region Constants

        private const string RUNTIME = "global::SteamToys.Runtime.InventorySystem";
        private const string TAGS_TYPE = RUNTIME + ".Tags";
        private const string HAS_VALUE_TYPE = "global::fefek5.Toys.Runtime.HasValue";
        private const string TAG_NAMES_FIELD = "TagNames";

        private const string TAGS_KEY = "Tags";
        private const string ITEM_DEF_KEY = "ItemDef";
        private const string INVENTORY_ITEM_KEY = "InventoryItem";
        private const string GENERATOR_DEF_KEY = "GeneratorDef";
        private const string PLAYTIME_GENERATOR_DEF_KEY = "PlaytimeGeneratorDef";

        // A property of one of these names would hide or clash with a member every Tags class has.
        private static readonly HashSet<string> ReservedNames = new()
        {
            "Keys", "Values", "Count", "Item", "TryGetValue", "TrySetValue", "ContainsKey", "Contains", "GetTagName",
            "GetTagValue", "TryParseTagValue", "GetEnumerator", "Equals", "GetHashCode", "GetType", "ToString",
            "MemberwiseClone", "Finalize", TAG_NAMES_FIELD
        };

        #endregion

        #region Properties

        public override string Kind => "Item Defs";

        public override string ClassNameExample => "Event";

        public List<TagsEntry> Tags => _tags;

        /// <summary>Whether <c>ClassNameItemDef</c> and its <c>ClassNameInventoryItem</c> are generated, each in its own script.</summary>
        public bool GenerateItemDef
        {
            get => _generateItemDef;
            set => _generateItemDef = value;
        }

        /// <summary>Whether <c>ClassNameGeneratorDef</c> is generated.</summary>
        public bool GenerateGeneratorDef
        {
            get => _generateGeneratorDef;
            set => _generateGeneratorDef = value;
        }

        /// <summary>Whether <c>ClassNamePlaytimeGeneratorDef</c> is generated.</summary>
        public bool GeneratePlaytimeGeneratorDef
        {
            get => _generatePlaytimeGeneratorDef;
            set => _generatePlaytimeGeneratorDef = value;
        }

        #endregion

        #region Serialized Fields

        [SerializeField, Tooltip("Tags of this kind of item.")]
        private List<TagsEntry> _tags = new();

        [SerializeField, Tooltip("Generate the item definition and the owned item class of it.")]
        private bool _generateItemDef = true;

        [SerializeField, Tooltip("Generate a generator definition, which copies its tags to the item it makes.")]
        private bool _generateGeneratorDef;

        [SerializeField, Tooltip("Generate a playtime generator definition, which copies its tags to the item it makes.")]
        private bool _generatePlaytimeGeneratorDef;

        #endregion

        #region Names

        private string TagsClassName => ClassName + "Tags";

        private string ItemDefTagsClassName => TagsClassName + "ItemDef";

        private string InventoryItemTagsClassName => TagsClassName + "InventoryItem";

        private string ItemDefClassName => ClassName + "ItemDef";

        private string InventoryItemClassName => ClassName + "InventoryItem";

        private string ItemDefTags => $"{CodeGeneration.GetGlobalName(this, TagsClassName)}.{ItemDefTagsClassName}";

        private string InventoryItemTags => $"{CodeGeneration.GetGlobalName(this, TagsClassName)}.{InventoryItemTagsClassName}";

        /// <summary>The tags of the entries marked <paramref name="onInventoryItem"/>, or of the others.</summary>
        private List<TagCodeGenerator> GetTags(bool onInventoryItem) =>
            Tags.Where(entry => entry != null && entry.OnInventoryItem == onInventoryItem)
                .Select(entry => SteamItemDefinitions.instance.FindTag(entry.TagId))
                .Where(tag => tag != null)
                .ToList();

        /// <summary>Whether <paramref name="tag"/> is one of the tags of this kind of item.</summary>
        public bool Uses(TagCodeGenerator tag) => tag != null && Tags.Any(entry => entry?.TagId == tag.Id);

        #endregion

        #region Generation

        public override IEnumerable<ScriptSpec> GetScriptSpecs()
        {
            yield return new ScriptSpec(TAGS_KEY, TagsClassName, BuildTags);

            if (GenerateItemDef)
            {
                yield return new ScriptSpec(ITEM_DEF_KEY, ItemDefClassName, BuildItemDef);
                yield return new ScriptSpec(INVENTORY_ITEM_KEY, InventoryItemClassName, BuildInventoryItem);
            }

            if (GenerateGeneratorDef)
                yield return new ScriptSpec(GENERATOR_DEF_KEY, ClassName + "GeneratorDef",
                    () => BuildGeneratorDef(ClassName + "GeneratorDef", "GeneratorDef", "Generator"));

            if (GeneratePlaytimeGeneratorDef)
                yield return new ScriptSpec(PLAYTIME_GENERATOR_DEF_KEY, ClassName + "PlaytimeGeneratorDef",
                    () => BuildGeneratorDef(ClassName + "PlaytimeGeneratorDef", "PlaytimeGeneratorDef", "Playtime Generator"));
        }

        private string BuildItemDef()
        {
            var label = $"{ObjectNames.NicifyVariableName(ClassName)} Item";

            return $"/// <summary>Item definition of {ClassName}, with the tags of {TagsClassName}.{ItemDefTagsClassName}.</summary>\n" +
                   CodeGeneration.GetAssetMenuAttribute(this, label) +
                   $"public partial class {ItemDefClassName} : {RUNTIME}.ItemDef<{ItemDefTags}>\n{{\n" +
                   $"    public override {RUNTIME}.InventoryItem CreateInventoryItem() => new {InventoryItemClassName}();\n" +
                   "}\n";
        }

        private string BuildInventoryItem() =>
            $"/// <summary>An owned {ClassName} item, its tags typed as {TagsClassName}.{InventoryItemTagsClassName}.</summary>\n" +
            $"public partial class {InventoryItemClassName} : {RUNTIME}.InventoryItem<{ItemDefClassName}>\n{{\n" +
            $"    public new {InventoryItemTags} Tags => ({InventoryItemTags})base.Tags;\n\n" +
            $"    protected override {TAGS_TYPE} CreateTags() => new {InventoryItemTags}();\n" +
            "}\n";

        // A generator copies its tags to the item it makes, where Steam reports them on the owned item.
        private string BuildGeneratorDef(string className, string baseClass, string kind)
        {
            var label = $"{ObjectNames.NicifyVariableName(ClassName)} {kind}";

            return $"/// <summary>{kind} definition of {ClassName}. Its tags go to the item it makes, so they are {TagsClassName}.{InventoryItemTagsClassName}.</summary>\n" +
                   CodeGeneration.GetAssetMenuAttribute(this, label) +
                   $"public partial class {className} : {RUNTIME}.{baseClass}<{InventoryItemTags}> {{ }}\n";
        }

        private string BuildTags()
        {
            var itemDef = BuildTagsClass(ItemDefTagsClassName, GetTags(false),
                $"Tags the definitions set. The tags Steam reports on an owned item are in {InventoryItemTagsClassName}.");
            var inventoryItem = BuildTagsClass(InventoryItemTagsClassName, GetTags(true),
                $"Tags Steam reports on an owned item: set by tag generators, or copied from the generator that made it. The tags of its definition are in {ItemDefTagsClassName}.");

            return $"/// <summary>The tags of {ClassName}: {ItemDefTagsClassName} and {InventoryItemTagsClassName}.</summary>\n" +
                   $"public class {TagsClassName}\n{{\n{CodeGeneration.Indent(itemDef)}\n{CodeGeneration.Indent(inventoryItem)}}}\n";
        }

        private static string BuildTagsClass(string className, List<TagCodeGenerator> tags, string summary)
        {
            var body = new StringBuilder();

            body.Append($"/// <summary>{summary}</summary>\n");
            body.Append("[global::System.Serializable]\n");
            body.Append($"public class {className} : {TAGS_TYPE}\n{{\n");
            body.Append($"    private static readonly string[] {TAG_NAMES_FIELD} = {{ {string.Join(", ", tags.Select(tag => $"\"{tag.SteamName}\""))} }};\n");

            foreach (var tag in tags)
            {
                var field = GetFieldName(tag);

                body.Append("\n");
                body.Append($"    [global::UnityEngine.SerializeField, global::UnityEngine.Tooltip(\"Steam tag {tag.SteamName}.\")]\n");
                body.Append($"    private {HAS_VALUE_TYPE}<{tag.EnumName}> {field};\n\n");
                body.Append($"    /// <summary>The Steam tag <c>{tag.SteamName}</c>; null when it is not set.</summary>\n");
                body.Append($"    public {tag.EnumName}? {tag.ClassName}\n    {{\n");
                body.Append($"        get => {field}.hasValue ? {field}.value : null;\n");
                body.Append($"        set => {field} = new {HAS_VALUE_TYPE}<{tag.EnumName}>(value.GetValueOrDefault(), value.HasValue);\n");
                body.Append("    }\n");
            }

            body.Append("\n");
            body.Append($"    public override global::System.Collections.Generic.IEnumerable<string> Keys => {TAG_NAMES_FIELD};\n\n");

            body.Append("    public override bool TryGetValue(string tagName, out string tagValue)\n    {\n");
            body.Append("        tagValue = null;\n\n");
            body.Append("        switch (tagName)\n        {\n");

            foreach (var tag in tags)
            {
                var field = GetFieldName(tag);

                body.Append($"            case \"{tag.SteamName}\":\n");
                body.Append($"                if (!{field}.hasValue)\n                    return false;\n\n");
                body.Append($"                tagValue = {TAGS_TYPE}.GetTagValue({field}.value);\n");
                body.Append("                return true;\n");
            }

            body.Append("            default:\n                return false;\n");
            body.Append("        }\n    }\n\n");

            body.Append("    public override bool TrySetValue(string tagName, string tagValue)\n    {\n");
            body.Append("        switch (tagName)\n        {\n");

            foreach (var tag in tags)
            {
                body.Append($"            case \"{tag.SteamName}\":\n            {{\n");
                body.Append($"                if (!{TAGS_TYPE}.TryParseTagValue(tagValue, out {tag.EnumName} value))\n                    return false;\n\n");
                body.Append($"                {GetFieldName(tag)} = value;\n");
                body.Append("                return true;\n            }\n");
            }

            body.Append("            default:\n                return false;\n");
            body.Append("        }\n    }\n");
            body.Append("}\n");

            return body.ToString();
        }

        private static string GetFieldName(TagCodeGenerator tag) =>
            "_" + char.ToLowerInvariant(tag.ClassName[0]) + tag.ClassName[1..];

        public override void CollectProblems(List<string> problems)
        {
            var used = new HashSet<string>();
            var tagNames = new HashSet<string>();
            var classNames = new[] { ItemDefTagsClassName, InventoryItemTagsClassName };

            for (var i = 0; i < Tags.Count; i++)
            {
                var tag = SteamItemDefinitions.instance.FindTag(Tags[i]?.TagId);

                if (tag == null)
                {
                    problems.Add($"Tag {i} is empty or no longer defined.");
                    continue;
                }

                if (!used.Add(tag.Id))
                {
                    problems.Add($"{tag.DisplayName} is listed more than once.");
                    continue;
                }

                if (!tag.IsValid())
                {
                    problems.Add($"{tag.DisplayName} has problems of its own; fix them and generate it first.");
                    continue;
                }

                if (!tag.IsEnumGenerated)
                    problems.Add($"{tag.DisplayName} is not generated as it is now, so its enum may not exist; generate it first.");

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

    [Serializable]
    public class TagsEntry
    {
        [Tooltip("The tag, by the id of its definition.")]
        public string TagId;

        [Tooltip("Steam reports it on the owned item and not on its definition: a tag generator sets it, or the " +
                 "generator that made the item copies it. It goes to the InventoryItem tags, which the generator " +
                 "classes take, instead of the ItemDef tags.")]
        public bool OnInventoryItem;
    }
}
