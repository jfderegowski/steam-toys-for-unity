using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SteamToys.Editor.InventorySystem.CodeGenerators
{
    /// <summary>
    /// The tags and kinds of items the scripts of the game are generated from, edited in
    /// <see cref="SteamItemDefinitionsWindow"/>. Kept in one file in ProjectSettings, not in Resources: only the editor
    /// needs it, so it stays out of builds.
    /// </summary>
    [FilePath("ProjectSettings/SteamItemDefinitions.asset", FilePathAttribute.Location.ProjectFolder)]
    public class SteamItemDefinitions : ScriptableSingleton<SteamItemDefinitions>
    {
        #region Serialized Fields

        [SerializeField] private List<TagCodeGenerator> _tags = new();

        [SerializeField] private List<ItemDefsCodeGenerator> _itemDefs = new();

        #endregion

        #region Properties

        public List<TagCodeGenerator> Tags => _tags;

        public List<ItemDefsCodeGenerator> ItemDefs => _itemDefs;

        /// <summary>Every definition, the tags first, which the kinds of items are generated from.</summary>
        public IEnumerable<CodeGenerator> All => _tags.Cast<CodeGenerator>().Concat(_itemDefs);

        #endregion

        // A ScriptableSingleton is not editable by default, and the window edits it.
        private void OnEnable() => hideFlags &= ~HideFlags.NotEditable;

        /// <summary>The tag with the id <paramref name="id"/>, or null.</summary>
        public TagCodeGenerator FindTag(string id) =>
            string.IsNullOrEmpty(id) ? null : _tags.FirstOrDefault(tag => tag != null && tag.Id == id);

        /// <summary>Writes the definitions to their file.</summary>
        public void SaveDefinitions() => Save(true);
    }
}
