using System.Collections.Generic;
using System.Linq;
using fefek5.Toys.Editor.Icons;
using fefek5.Toys.Editor.VisualElements;
using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    /// <summary>
    /// Drawer for <see cref="Tags"/> and its subclasses: a section under the label of the field holding the tags, with
    /// their fields one after another. Under it, an error for every string tag named like one of
    /// <see cref="Tags.GetTagNames"/>.
    /// </summary>
    [CustomPropertyDrawer(typeof(Tags), true)]
    public class TagsDrawer : PropertyDrawer
    {
        private const string ICON_NAME = "section-tags-icon";
        private const string STRING_TAGS_PROP_NAME = "_stringTags";
        private const string STRING_TAGS_LABEL = "Tags";
        private const string ENTRIES_PROP_NAME = "entries";
        private const string KEY_PROP_NAME = "key";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            EditorIconsDatabase.TryGetIcon<Texture2D>(ICON_NAME, out var icon);

            var stringTags = property.FindPropertyRelative(STRING_TAGS_PROP_NAME);
            var entries = stringTags.FindPropertyRelative(ENTRIES_PROP_NAME);
            var errors = new VisualElement();
            var section = new SectionElement(property, icon);

            // The section makes a field for each property; the string tags read as plain tags.
            var stringTagsField = section.Query<PropertyField>()
                .Where(field => field.bindingPath == stringTags.propertyPath)
                .First();

            if (stringTagsField != null)
                stringTagsField.label = STRING_TAGS_LABEL;

            RefreshErrors(errors, property, entries);
            errors.TrackPropertyValue(entries, _ => RefreshErrors(errors, property, entries));

            // Outside the section, so an error still shows while it is folded.
            var root = new VisualElement();

            root.Add(section);
            root.Add(errors);

            return root;
        }

        private static void RefreshErrors(VisualElement errors, SerializedProperty property, SerializedProperty entries)
        {
            errors.Clear();

            var tags = (Tags)property.boxedValue;

            foreach (var tagName in GetDuplicateTagNames(tags, entries))
                errors.Add(new HelpBox(
                    $"Cannot add the string tag '{tagName}': {tags.GetType().Name} already has a tag with this name.",
                    HelpBoxMessageType.Error));
        }

        private static IEnumerable<string> GetDuplicateTagNames(Tags tags, SerializedProperty entries)
        {
            var tagNames = tags.GetTagNames();

            return tagNames == null
                ? Enumerable.Empty<string>()
                : Enumerable.Range(0, entries.arraySize)
                    .Select(i => entries.GetArrayElementAtIndex(i).FindPropertyRelative(KEY_PROP_NAME).stringValue)
                    .Intersect(tagNames);
        }
    }
}
