using System.Collections.Generic;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.Converters;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// ItemDef of type <c>generator</c>: grants one entry of its <see cref="Content"/>, picked by weight.
    /// Never sits in an inventory itself and should not be sold; sell a container <see cref="Item"/> and open it
    /// with this generator's exchange instead. Use <see cref="Generator{TTags}"/> to give the created items tags.
    /// </summary>
    [CreateAssetMenu(fileName = "New Generator", menuName = "SteamToys/Inventory/Generator", order = 3)]
    public class Generator : CraftableItemDef
    {
        #region Properties

        /// <summary>
        /// Definitions this generator can grant, with their relative odds.
        /// </summary>
        public BundleContent Content
        {
            get => GetContents();
            set => SetContents(value);
        }

        public List<TagGenerator> TagGenerators
        {
            get => GetTagGenerators();
            set => SetTagGenerators(value);
        }

        #endregion

        #region Inspector Serialized Fields

        [Header("Generator")]
        [SerializeField, Tooltip("Definitions this generator can grant, with their relative odds.")]
        private BundleContent _bundle = new();
        [SerializeField, Tooltip("Tag generators applied to every item this generator creates.")]
        private List<TagGenerator> _tagGenerators = new();

        #endregion

        #region Getters and Setters

        public override ItemType GetItemType() => ItemType.Generator;

        public virtual BundleContent GetContents() => _bundle;

        public virtual void SetContents(BundleContent value) => _bundle = value;

        public virtual List<TagGenerator> GetTagGenerators() => _tagGenerators;

        public virtual void SetTagGenerators(List<TagGenerator> value) => _tagGenerators = value;

        #endregion

        #region Validation

        public override void CollectSchemaWarnings(List<string> warnings)
        {
            base.CollectSchemaWarnings(warnings);

            BundleContent.CollectSchemaWarnings(this, Content, warnings);
        }

        #endregion

        #region Json

        public override void WriteJsonProperties(JsonWriter writer)
        {
            base.WriteJsonProperties(writer);

            writer.WriteBundleIfNotEmpty("bundle", Content);
            writer.WriteItemDefIdsIfNotEmpty("tag_generators", TagGenerators);
        }

        #endregion
    }

    public class Generator<TTags> : Generator where TTags : Tags, new()
    {
        [SerializeField, Tooltip("Tags copied to every item this generator creates.")]
        private TTags _tags;

        public TTags TagsData
        {
            get
            {
                EnsureTagsInitialized();

                return _tags;
            }
        }

        public override string GetTags()
        {
            EnsureTagsInitialized();

            return _tags.GetDefinitionTagsString();
        }

        public override void SetTags(string value)
        {
            EnsureTagsInitialized();

            _tags.SetTagsString(value);
        }

        private void EnsureTagsInitialized() => _tags ??= new TTags();
    }
}
