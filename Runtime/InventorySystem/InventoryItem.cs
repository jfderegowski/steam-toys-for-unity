using System;
using System.Collections.Generic;
using SteamToys.Runtime.InventorySystem.Converters;
using Steamworks;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// One item the player owns, as <see cref="Inventory"/> read it from Steam: a stack of its <see cref="Definition"/>
    /// with the state of that one item. Items of one itemdefid differ only in that state (quantity, tags, when and how
    /// they were acquired); the name, icons and colors belong to the definition.
    /// <para>
    /// Its <see cref="Tags"/> are the ones Steam reports on the item, which tag generators and the generator that made
    /// it set; the tags of its definition stay on <see cref="Definition"/>. <see cref="ItemDef.CreateInventoryItem"/>
    /// makes it, so a class of the game can have its own, e.g. <see cref="InventoryItem{TItemDef}"/> with typed tags.
    /// </para>
    /// <para>
    /// Kept across updates by its <see cref="InstanceId"/>, so a reference to it stays valid while the player owns it.
    /// </para>
    /// </summary>
    public class InventoryItem
    {
        #region Constants

        private const char TAG_SEPARATOR = ';';
        private const char CATEGORY_SEPARATOR = ':';
        private const string TAGS_PROPERTY = "tags";
        private const string ACQUIRED_PROPERTY = "acquired";
        private const string STATE_CHANGED_PROPERTY = "state_changed_timestamp";
        private const string ORIGIN_PROPERTY = "Origin";
        private const string STATE_PROPERTY = "state";
        private const string ORIGINAL_ITEM_ID_PROPERTY = "originalitemid";

        private static readonly IReadOnlyDictionary<string, string> NoProperties = new Dictionary<string, string>();

        #endregion

        #region Fields

        private Tags _tags;

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
        public SteamItemInstanceID_t InstanceId { get; private set; }

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
        /// The <c>tags</c> Steam reports on this item as it sends them, e.g. <c>"rarity:legendary;wear:worn"</c>: the
        /// ones tag generators and the generator that made it set, not the ones of its definition.
        /// </summary>
        public string SteamTags => GetInstanceProperty(TAGS_PROPERTY);

        /// <summary>
        /// <see cref="SteamTags"/> in the tags object of <see cref="CreateTags"/>. The tags of the definition are on
        /// <see cref="Definition"/>; <see cref="TryGetTag"/> looks in both.
        /// </summary>
        public Tags Tags => _tags;

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

        #region Tags

        /// <summary>
        /// The tags object <see cref="SteamTags"/> are read into: <see cref="DefaultTags"/>, which takes any category,
        /// unless a subclass takes a class of the game.
        /// </summary>
        protected virtual Tags CreateTags() => new DefaultTags();

        /// <summary>
        /// A new, empty tags object of the kind this item reads its tags into, e.g. to tell which categories it holds.
        /// </summary>
        public Tags CreateEmptyTags() => CreateTags();

        /// <summary>
        /// The value of the tag <paramref name="tagName"/> of this item: the one Steam reports on it, or else the one
        /// its definition sets.
        /// </summary>
        public bool TryGetTag(string tagName, out string tagValue)
        {
            foreach (var pair in SteamTags.Split(TAG_SEPARATOR, StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = pair.IndexOf(CATEGORY_SEPARATOR);

                if (separator <= 0 || pair[..separator].Trim() != tagName)
                    continue;

                tagValue = pair[(separator + 1)..].Trim();
                return true;
            }

            tagValue = null;

            return Definition && Definition.SchemaSupportsTags() && Definition.Tags != null &&
                   Definition.Tags.TryGetValue(tagName, out tagValue);
        }

        /// <summary>
        /// Whether this item carries the tag value <paramref name="value"/>: one Steam reports on it, or one its
        /// definition sets.
        /// </summary>
        public bool HasTag<TEnum>(TEnum value) where TEnum : Enum =>
            TryGetTag(Tags.GetTagName<TEnum>(), out var held) && held == Tags.GetTagValue(value);

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

        /// <summary>Gives this item, just made by its definition, the Steam ID it has.</summary>
        internal void SetInstanceId(SteamItemInstanceID_t instanceId) => InstanceId = instanceId;

        /// <summary>Points this item at its definition.</summary>
        internal void SetDefinition(ItemDef definition, bool isInDB)
        {
            Definition = definition;
            IsDefinitionInDB = isInDB;
        }

        /// <summary>Takes the state Steam reported for this item, and reads its tags anew.</summary>
        internal void Update(uint quantity, ESteamItemFlags flags, IReadOnlyDictionary<string, string> properties)
        {
            Quantity = quantity;
            Flags = flags;
            Properties = properties ?? NoProperties;

            _tags = CreateTags();

            if (_tags != null)
                TagsConverter.FromString(SteamTags, _tags);
        }

        #endregion

        public override string ToString() => $"{Name} x{Quantity} ({InstanceId.m_SteamItemInstanceID})";
    }

    /// <summary>
    /// An owned item whose definition is a <typeparamref name="TItemDef"/>, so <see cref="Definition"/> is typed. A
    /// class of the game subclasses it to type <see cref="InventoryItem.Tags"/> as well, and its definition makes it
    /// in <see cref="ItemDef.CreateInventoryItem"/>.
    /// </summary>
    public class InventoryItem<TItemDef> : InventoryItem where TItemDef : ItemDef
    {
        public new TItemDef Definition => (TItemDef)base.Definition;
    }
}
