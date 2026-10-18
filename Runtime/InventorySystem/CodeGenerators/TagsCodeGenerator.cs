using System;
using System.Collections.Generic;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem.CodeGenerators
{
    /// <summary>
    /// Defines the tags of a kind of item from <see cref="TagCodeGenerator"/>s and generates a class of
    /// <see cref="CodeGenerator.ClassName"/> holding two <see cref="InventorySystem.Tags"/> classes of them: <c>ItemDef</c>, the tags
    /// its definitions set, and <c>InventoryItem</c>, the tags Steam reports on an owned item, which are the ones a tag
    /// generator sets when the item is made. A tag marked <see cref="TagsEntry.Generated"/> goes to the second.
    /// For <c>EventTags</c> they are <c>EventTags.EventTagsItemDef</c> and <c>EventTags.EventTagsInventoryItem</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "New Tags", menuName = "SteamToys/Inventory/Code Generators/Tags", order = 21)]
    public class TagsCodeGenerator : CodeGenerator
    {
        #region Properties

        public List<TagsEntry> Tags => _tags;

        #endregion

        #region Inspector Serialized Fields

        [SerializeField, Tooltip("Tags of this kind of item.")]
        private List<TagsEntry> _tags = new();

        #endregion
    }

    [Serializable]
    public class TagsEntry
    {
        [Tooltip("The tag, defined by its Tag code generator.")]
        public TagCodeGenerator Tag;

        [Tooltip("Set by a tag generator when the item is made, so Steam reports it on the owned item and not on the " +
                 "definition: it goes to the InventoryItem tags instead of the ItemDef tags.")]
        public bool Generated;
    }
}
