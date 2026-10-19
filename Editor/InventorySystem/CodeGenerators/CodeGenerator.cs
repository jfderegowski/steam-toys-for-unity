using System;
using System.Collections.Generic;
using fefek5.Toys.Runtime.Attributes;
using UnityEngine;

namespace SteamToys.Editor.InventorySystem.CodeGenerators
{
    /// <summary>
    /// Base of the definitions <see cref="SteamItemDefinitions"/> keeps, each generating scripts: the types they
    /// declare are named from <see cref="ClassName"/>, in <see cref="Namespace"/>, and written to
    /// <see cref="OutputFolder"/>, one script each. It keeps the <see cref="Scripts"/> it wrote by a key, so after a
    /// rename or a new folder the next generation moves each one instead of leaving it behind.
    /// </summary>
    [Serializable]
    public abstract class CodeGenerator
    {
        #region Properties

        /// <summary>Stays the same for good, so others can refer to this across renames.</summary>
        public string Id => _id;

        /// <summary>The name the types the scripts declare are named from.</summary>
        public string ClassName
        {
            get => _className;
            set => _className = value;
        }

        /// <summary>The namespace of the types; empty for none.</summary>
        public string Namespace
        {
            get => _namespace;
            set => _namespace = value;
        }

        /// <summary>The folder the scripts are written to, e.g. <c>Assets/Scripts/Items</c>.</summary>
        public string OutputFolder
        {
            get => _outputFolder;
            set => _outputFolder = value;
        }

        /// <summary>
        /// The folder of the Create menu the generated asset classes go in, e.g. <c>Inventory</c>; empty for the top.
        /// </summary>
        public string AssetMenu
        {
            get => _assetMenu;
            set => _assetMenu = value;
        }

        /// <summary>The scripts this generated last, by their key.</summary>
        public IReadOnlyList<GeneratedScript> Scripts => _scripts;

        /// <summary>The name shown for this, e.g. in the list of the window.</summary>
        public string DisplayName => string.IsNullOrWhiteSpace(ClassName) ? $"New {Kind}" : ClassName;

        /// <summary>What kind of definition this is, e.g. <c>Tag</c>.</summary>
        public abstract string Kind { get; }

        /// <summary>An example of <see cref="ClassName"/>, for the message when it is not valid.</summary>
        public abstract string ClassNameExample { get; }

        #endregion

        #region Serialized Fields

        [SerializeField, HideInInspector]
        private string _id = Guid.NewGuid().ToString("N");

        [SerializeField, Tooltip("Name the types the scripts declare are named from.")]
        private string _className;

        [SerializeField, Tooltip("Namespace of the types; empty for none.")]
        private string _namespace;

        [SerializeField, FolderPath, Tooltip("Folder the scripts are written to, e.g. Assets/Scripts/Items.")]
        private string _outputFolder;

        [SerializeField, Tooltip("Folder of the Create menu the generated asset classes go in, e.g. Inventory; empty " +
                                 "for the top.")]
        private string _assetMenu;

        [SerializeField, Tooltip("The scripts this generated last. Generating after a rename or a new folder moves them.")]
        private List<GeneratedScript> _scripts = new();

        #endregion

        #region Generation

        /// <summary>
        /// Adds a message for everything of this definition's own that keeps it from generating; the name, the
        /// namespace, the folder and the paths are checked by <see cref="CodeGeneration"/>.
        /// </summary>
        public abstract void CollectProblems(List<string> problems);

        /// <summary>The scripts this writes as it is now.</summary>
        public abstract IEnumerable<ScriptSpec> GetScriptSpecs();

        /// <summary>Puts right what an edit may have left wrong, e.g. numbers given twice. True when it changed.</summary>
        public virtual bool Validate() => false;

        #endregion

        #region Scripts

        /// <summary>The script this generated last under <paramref name="key"/>, or null.</summary>
        public TextAsset GetScript(string key)
        {
            foreach (var script in _scripts)
            {
                if (script.Key == key)
                    return script.Script;
            }

            return null;
        }

        /// <summary>Keeps <paramref name="script"/> as the one this generated under <paramref name="key"/>.</summary>
        public void SetScript(string key, TextAsset script)
        {
            foreach (var generated in _scripts)
            {
                if (generated.Key != key)
                    continue;

                generated.Script = script;
                return;
            }

            _scripts.Add(new GeneratedScript { Key = key, Script = script });
        }

        /// <summary>Forgets the script this generated under <paramref name="key"/>.</summary>
        public bool RemoveScript(string key) => _scripts.RemoveAll(script => script.Key == key) > 0;

        #endregion
    }

    [Serializable]
    public class GeneratedScript
    {
        [Tooltip("What the script is to its definition, e.g. ItemDef.")]
        public string Key;

        [Tooltip("The script.")]
        public TextAsset Script;
    }

    /// <summary>One script a definition writes: <c>OutputFolder/FileName.cs</c>, kept under its key.</summary>
    public readonly struct ScriptSpec
    {
        public readonly string Key;
        public readonly string FileName;
        public readonly Func<string> BuildBody;

        /// <param name="key">What the script is to the definition, the same across renames, e.g. ItemDef.</param>
        /// <param name="fileName">The name of the script, which is the name of the type it declares.</param>
        /// <param name="buildBody">The code of the script inside its namespace, written for the top level.</param>
        public ScriptSpec(string key, string fileName, Func<string> buildBody)
        {
            Key = key;
            FileName = fileName;
            BuildBody = buildBody;
        }
    }
}
