using System;
using System.Collections.Generic;
using Steamworks;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// One item the player owns, as <see cref="Inventory"/> read it from Steam: a stack of its <see cref="Definition"/>
    /// with the state of that one item. Items of one itemdefid differ only in that state (quantity, tags, when and how
    /// they were acquired); the name, icons and colors belong to the definition.
    /// <para>
    /// Kept across updates by its <see cref="InstanceId"/>, so a reference to it stays valid while the player owns it.
    /// </para>
    /// </summary>
    public class InventoryItem
    {
        #region Constants

        private const char TAG_SEPARATOR = ';';
        private const string TAGS_PROPERTY = "tags";
        private const string ACQUIRED_PROPERTY = "acquired";
        private const string STATE_CHANGED_PROPERTY = "state_changed_timestamp";
        private const string ORIGIN_PROPERTY = "Origin";
        private const string STATE_PROPERTY = "state";
        private const string ORIGINAL_ITEM_ID_PROPERTY = "originalitemid";

        private static readonly IReadOnlyDictionary<string, string> NoProperties = new Dictionary<string, string>();

        #endregion

        #region Properties

        /// <summary>
        /// The definition of this item, of type <c>item</c>: the asset of <see cref="SteamItemsDB"/>, or, for an
        /// itemdefid the DB lacks, one made at runtime from the definition published on Steam
        /// (<see cref="IsDefinitionInDB"/> tells them apart).
        /// </summary>
        public ItemDef Definition { get; private set; }

        /// <summary>Whether <see cref="Definition"/> is an asset of <see cref="SteamItemsDB"/>.</summary>
        public bool IsDefinitionInDB { get; private set; }

        /// <summary>The Steam ID of this one owned item.</summary>
        public SteamItemInstanceID_t InstanceId { get; }

        public ulong ItemDefId => Definition ? Definition.ItemDefId : 0;

        /// <summary>The display name of the definition.</summary>
        public string Name => Definition ? Definition.Name : string.Empty;

        /// <summary>How many this stack holds.</summary>
        public uint Quantity { get; private set; }

        /// <summary>The Steam flags of this item, e.g. whether it cannot be traded.</summary>
        public ESteamItemFlags Flags { get; private set; }

        /// <summary>Whether this item is account-locked and cannot be traded or given away.</summary>
        public bool IsNoTrade => (Flags & ESteamItemFlags.k_ESteamItemNoTrade) != 0;

        /// <summary>
        /// The Steam <c>tags</c> of this item, generated ones included, e.g. <c>"rarity:legendary;wear:worn"</c>.
        /// </summary>
        public string Tags => GetInstanceProperty(TAGS_PROPERTY);

        /// <summary>
        /// The tags of this item, generated ones included: the ones of its definition overridden by the ones Steam
        /// reported for it, in the <see cref="InventorySystem.Tags"/> type of the definition. Null when the definition has no tags.
        /// Its own object, so changing it leaves the definition alone.
        /// </summary>
        public Tags TagsObject { get; private set; }

        /// <summary>
        /// Every property Steam reports for this item, by name, e.g. <c>itemid</c>, <c>acquired</c>, <c>Origin</c>,
        /// <c>tags</c> or dynamic properties the game set.
        /// </summary>
        public IReadOnlyDictionary<string, string> Properties { get; private set; } = NoProperties;

        /// <summary>When the player got this item.</summary>
        public SteamDateTime Acquired => SteamDateTime.FromSteamString(GetInstanceProperty(ACQUIRED_PROPERTY));

        /// <summary>When the state of this item last changed, e.g. its quantity.</summary>
        public SteamDateTime StateChanged => SteamDateTime.FromSteamString(GetInstanceProperty(STATE_CHANGED_PROPERTY));

        /// <summary>How the player got this item, as Steam names it, e.g. <c>"exchange"</c> or <c>"external"</c>.</summary>
        public string Origin => GetInstanceProperty(ORIGIN_PROPERTY);

        /// <summary>The state Steam keeps for this item, usually empty.</summary>
        public string State => GetInstanceProperty(STATE_PROPERTY);

        /// <summary>
        /// The instance ID this item had when it was created, which stays the same when a stack is split or moved.
        /// </summary>
        public SteamItemInstanceID_t OriginalInstanceId =>
            ulong.TryParse(GetInstanceProperty(ORIGINAL_ITEM_ID_PROPERTY), out var id) ? new SteamItemInstanceID_t(id) : SteamItemInstanceID_t.Invalid;

        #endregion

        internal InventoryItem(SteamItemInstanceID_t instanceId) => InstanceId = instanceId;

        #region Tags

        /// <summary>The tags of this item, as long as they are a <typeparamref name="TTags"/>.</summary>
        public TTags GetTags<TTags>() where TTags : Tags => TagsObject as TTags;

        public bool TryGetTags<TTags>(out TTags tags) where TTags : Tags => (tags = GetTags<TTags>()) != null;

        /// <summary>
        /// Whether this item carries the tag value <paramref name="value"/>: one of the tags Steam reported for it, or
        /// a tag its definition sets.
        /// </summary>
        public bool HasTag<TEnum>(TEnum value) where TEnum : Enum
        {
            var pair = new Tag<TEnum> { Value = value }.ToString();

            foreach (var tag in Tags.Split(TAG_SEPARATOR))
            {
                if (tag.Trim() == pair)
                    return true;
            }

            return TagsObject != null && TagsObject.Has(value);
        }

        #endregion

        #region Steam Properties

        /// <summary>
        /// The property Steam reported for this item under <paramref name="name"/>, ignoring case, or an empty string
        /// when it has none.
        /// </summary>
        public string GetInstanceProperty(string name)
        {
            if (Properties.TryGetValue(name, out var value))
                return value ?? string.Empty;

            foreach (var (key, propertyValue) in Properties)
            {
                if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                    return propertyValue ?? string.Empty;
            }

            return string.Empty;
        }

        #endregion

        #region Inventory

        /// <summary>Points this item at its definition, and builds its typed tags from it.</summary>
        internal void SetDefinition(ItemDef definition, bool isInDB)
        {
            Definition = definition;
            IsDefinitionInDB = isInDB;

            UpdateTagsObject();
        }

        /// <summary>Takes the state Steam reported for this item.</summary>
        internal void Update(uint quantity, ESteamItemFlags flags, IReadOnlyDictionary<string, string> properties)
        {
            Quantity = quantity;
            Flags = flags;
            Properties = properties ?? NoProperties;

            UpdateTagsObject();
        }

        /// <summary>
        /// A fresh tags object of the definition's type, holding the definition's values overridden by this item's own.
        /// </summary>
        private void UpdateTagsObject()
        {
            var definitionTags = Definition && Definition.SchemaSupportsTags() ? Definition.GetTags() : null;

            if (definitionTags == null)
            {
                TagsObject = null;
                return;
            }

            var tags = (Tags)Activator.CreateInstance(definitionTags.GetType());

            tags.SetSteamString(definitionTags.ToSteamString());
            tags.MergeSteamString(Tags);

            TagsObject = tags;
        }

        #endregion

        public override string ToString() => $"{Name} x{Quantity} ({InstanceId.m_SteamItemInstanceID})";
    }
}
