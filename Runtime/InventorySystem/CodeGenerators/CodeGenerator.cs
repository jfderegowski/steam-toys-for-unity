using fefek5.Toys.Runtime.Attributes;
using UnityEngine;
using UnityEngine.Serialization;

namespace SteamToys.Runtime.InventorySystem.CodeGenerators
{
    /// <summary>
    /// Base of the assets that generate a script: the type it declares is <see cref="ClassName"/>, in
    /// <see cref="Namespace"/>, written to <c>OutputFolder/ClassName.cs</c>. It keeps the <see cref="Script"/> it
    /// wrote, so after a rename or a new folder the next generation moves that script instead of leaving it behind.
    /// </summary>
    public abstract class CodeGenerator : ScriptableObject
    {
        #region Properties

        /// <summary>The name of the type the script declares, and of the script.</summary>
        public string ClassName
        {
            get => _className;
            set => _className = value;
        }

        /// <summary>The namespace of the type; empty for none.</summary>
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

        /// <summary>The script this generated last, or null before the first generation.</summary>
        public TextAsset Script
        {
            get => _script;
            set => _script = value;
        }

        #endregion

        #region Inspector Serialized Fields

        [SerializeField, FormerlySerializedAs("_tagName")]
        [Tooltip("Name of the type the script declares, and of the script.")]
        private string _className;

        [SerializeField, Tooltip("Namespace of the type; empty for none.")]
        private string _namespace;

        [SerializeField, FolderPath, Tooltip("Folder the script is written to; empty for the folder of this asset.")]
        private string _outputFolder;

        [SerializeField, Tooltip("The script this generated last. Generating after a rename or a new folder moves it.")]
        private TextAsset _script;

        #endregion
    }
}
