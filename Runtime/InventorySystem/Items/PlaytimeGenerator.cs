using System.Collections.Generic;
using fefek5.Toys.Runtime;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.Converters;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// ItemDef of type <c>playtimegenerator</c>: a generator granted only through
    /// <c>ISteamInventory::TriggerItemDrop</c>, paced by the drop settings. Any drop setting left unset falls back
    /// to the application level one. Use <see cref="PlaytimeGenerator{TTags}"/> to give the created items tags.
    /// </summary>
    [CreateAssetMenu(fileName = "New Playtime Generator", menuName = "SteamToys/Inventory/Playtime Generator", order = 4)]
    public class PlaytimeGenerator : GrantableItemDef
    {
        #region Constants

        private const uint MAX_DROPS_PER_WINDOW = 10;

        #endregion

        #region Properties

        /// <summary>
        /// Definitions this generator can drop, with their relative odds.
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

        public uint DropInterval
        {
            get => GetDropInterval();
            set => SetDropInterval(value);
        }

        public HasValue<uint> DropWindow
        {
            get => GetDropWindow();
            set => SetDropWindow(value);
        }

        public uint DropMaxPerWindow
        {
            get => GetDropMaxPerWindow();
            set => SetDropMaxPerWindow(value);
        }

        public HasValue<uint> DropLimit
        {
            get => GetDropLimit();
            set => SetDropLimit(value);
        }

        #endregion

        #region Inspector Serialized Fields

        [Header("Generator")]
        [SerializeField, Tooltip("Definitions this generator can drop, with their relative odds.")]
        private BundleContent _bundle = new();
        [SerializeField, Tooltip("Tag generators applied to every item this generator creates.")]
        private List<TagGenerator> _tagGenerators = new();

        [Header("Drop System")]
        [SerializeField, Tooltip("Playtime in minutes required before a drop. 0 uses the application setting.")]
        private uint _dropInterval;
        [SerializeField, Tooltip("Cool-down window in minutes. Drops are limited to the window only when set.")]
        private HasValue<uint> _dropWindow;
        [SerializeField, Tooltip("Drops allowed within the drop window (1-10). 0 uses the application setting.")]
        private uint _dropMaxPerWindow;
        [SerializeField, Tooltip("Maximum number of drops per user. Enforced only when set; 0 stops all future drops.")]
        private HasValue<uint> _dropLimit;

        #endregion

        #region Getters and Setters

        public override ItemType GetItemType() => ItemType.PlaytimeGenerator;

        public virtual BundleContent GetContents() => _bundle;

        public virtual void SetContents(BundleContent value) => _bundle = value;

        public virtual List<TagGenerator> GetTagGenerators() => _tagGenerators;

        public virtual void SetTagGenerators(List<TagGenerator> value) => _tagGenerators = value;

        public virtual uint GetDropInterval() => _dropInterval;

        public virtual void SetDropInterval(uint value) => _dropInterval = value;

        public virtual HasValue<uint> GetDropWindow() => _dropWindow;

        public virtual void SetDropWindow(HasValue<uint> value) => _dropWindow = value;

        public virtual uint GetDropMaxPerWindow() => _dropMaxPerWindow;

        public virtual void SetDropMaxPerWindow(uint value) => _dropMaxPerWindow = value;

        public virtual HasValue<uint> GetDropLimit() => _dropLimit;

        public virtual void SetDropLimit(HasValue<uint> value) => _dropLimit = value;

        #endregion

        #region Validation

        public override void CollectSchemaWarnings(List<string> warnings)
        {
            base.CollectSchemaWarnings(warnings);

            BundleContent.CollectSchemaWarnings(this, Content, warnings);

            if (DropMaxPerWindow > MAX_DROPS_PER_WINDOW)
                warnings.Add($"Drop Max Per Window cannot exceed {MAX_DROPS_PER_WINDOW}.");

            if (DropWindow.hasValue && DropWindow.value == 0)
                warnings.Add("Drop Window is enabled with 0 minutes, so it is not exported.");
        }

        #endregion

        #region Json

        public override void WriteJsonProperties(JsonWriter writer)
        {
            base.WriteJsonProperties(writer);

            writer.WriteBundleIfNotEmpty("bundle", Content);
            writer.WriteItemDefIdsIfNotEmpty("tag_generators", TagGenerators);

            writer.WriteUintIfNotZero("drop_interval", DropInterval);

            writer.WriteBoolIfTrue("use_drop_window", DropWindow.hasValue);
            if (DropWindow.hasValue)
                writer.WriteUintIfNotZero("drop_window", DropWindow.value);

            writer.WriteUintIfNotZero("drop_max_per_window", DropMaxPerWindow);

            // A drop limit of 0 is meaningful here: it stops all future drops.
            writer.WriteBoolIfTrue("use_drop_limit", DropLimit.hasValue);
            if (DropLimit.hasValue)
            {
                writer.WritePropertyName("drop_limit");
                writer.WriteValue(DropLimit.value);
            }
        }

        #endregion
    }

    public class PlaytimeGenerator<TTags> : PlaytimeGenerator where TTags : Tags, new()
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
