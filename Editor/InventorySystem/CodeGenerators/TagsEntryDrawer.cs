using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.CodeGenerators
{
    /// <summary>
    /// Draws a <see cref="TagsEntry"/> as one row: the tag, picked by name from the tags of
    /// <see cref="SteamItemDefinitions"/> and kept by its id, and whether it is on the inventory item.
    /// </summary>
    [CustomPropertyDrawer(typeof(TagsEntry))]
    public class TagsEntryDrawer : PropertyDrawer
    {
        private const string NONE = "";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var tagId = property.FindPropertyRelative(nameof(TagsEntry.TagId));
            var onInventoryItem = property.FindPropertyRelative(nameof(TagsEntry.OnInventoryItem));

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };

            var ids = new List<string> { NONE };
            ids.AddRange(SteamItemDefinitions.instance.Tags.Where(tag => tag != null).Select(tag => tag.Id));

            // An id no tag has any more stays, shown as missing, so it is not lost without a word.
            if (!ids.Contains(tagId.stringValue))
                ids.Add(tagId.stringValue);

            var tag = new PopupField<string>(ids, tagId.stringValue, FormatTag, FormatTag)
            {
                tooltip = "The tag, picked by name and kept by the id of its definition.",
                style = { flexGrow = 1, flexBasis = 0, minWidth = 0 }
            };

            tag.RegisterValueChangedCallback(change =>
            {
                tagId.stringValue = change.newValue;
                tagId.serializedObject.ApplyModifiedProperties();
            });

            var toggle = new Toggle("On Inventory Item")
            {
                tooltip = onInventoryItem.tooltip,
                style = { marginLeft = 8 }
            };

            toggle.BindProperty(onInventoryItem);

            row.Add(tag);
            row.Add(toggle);

            return row;
        }

        private static string FormatTag(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "(none)";

            var tag = SteamItemDefinitions.instance.FindTag(id);

            return tag != null ? tag.DisplayName : "(missing tag)";
        }
    }
}
