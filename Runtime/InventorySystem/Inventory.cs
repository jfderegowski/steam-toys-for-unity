using System;
using System.Collections.Generic;
using System.Linq;
using SteamToys.Runtime.Core;
using SteamToys.Runtime.InventorySystem.Converters;
using Steamworks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// The Steam inventory of the signed-in player: the items they own, and the calls that change it.
    /// <para>
    /// Every owned item is an <see cref="InventoryItem"/>, made by <see cref="ItemDef.CreateInventoryItem"/> of its
    /// definition from <see cref="SteamItemsDB"/>: the state of that one item (instance ID, quantity, its own tags and
    /// every other property Steam reports for it) and a reference to the definition. An itemdefid the DB lacks gets a definition made at runtime from the one published
    /// on Steam, shared by every item of it, so its name, icons and colors still show. Items are kept across updates
    /// by their instance ID, so a reference to one stays valid while the player owns it. Only items sit in an
    /// inventory: bundles and generators turn into items when granted.
    /// </para>
    /// <para>
    /// Every call is asynchronous: Steam answers once the session is pumped, which <see cref="SteamManager"/> does
    /// every frame. <see cref="OnChanged"/> is raised whenever an answer changed the items, and each call takes an
    /// optional callback with whether it succeeded.
    /// </para>
    /// <see href="https://partner.steamgames.com/doc/features/inventory"/>
    /// </summary>
    public static class Inventory
    {
        #region Events

        /// <summary>Raised when Steam reported a change of the owned items.</summary>
        public static event Action OnChanged;

        #endregion

        #region Properties

        /// <summary>
        /// True while <see cref="SteamManager.SteamInitialized"/>, which is what makes an inventory call safe:
        /// the raw <c>SteamInventory</c> calls throw rather than fail when the API is down.
        /// </summary>
        public static bool Initialized => SteamManager.SteamInitialized;

        /// <summary>True once the whole inventory was read from Steam since the session started.</summary>
        public static bool IsReady { get; private set; }

        /// <summary>Every item the player owns, as last reported by Steam.</summary>
        public static IReadOnlyList<InventoryItem> Items => _items;

        #endregion

        #region Constants

        private const char PROPERTY_NAME_SEPARATOR = ',';

        // Steam fills at most this much of a property per call; a longer value is asked for again in full.
        private const uint PROPERTY_BUFFER_SIZE = 1024;

        #endregion

        #region Private Fields

        private static readonly List<InventoryItem> _items = new();

        // Definitions made from the ones published on Steam, for itemdefids the DB lacks; one per itemdefid.
        private static readonly Dictionary<ulong, DefaultItemDef> _publishedDefinitions = new();

        // What each pending result handle is for, and who waits for it.
        private static readonly Dictionary<SteamInventoryResult_t, PendingResult> _pending = new();

        private static bool _warnedNotInitialized;

        // Held in a field on purpose: Steamworks.NET requires a live reference, or the garbage
        // collector takes the registration away.
        private static Callback<SteamInventoryResultReady_t> _resultReadyCallback;
        private static Callback<SteamInventoryDefinitionUpdate_t> _definitionUpdateCallback;

        private readonly struct PendingResult
        {
            public readonly bool IsFullInventory;
            public readonly Action<bool> OnComplete;

            public PendingResult(bool isFullInventory, Action<bool> onComplete)
            {
                IsFullInventory = isFullInventory;
                OnComplete = onComplete;
            }
        }

        #endregion

        #region Items

        /// <summary>
        /// Every owned item that is a <typeparamref name="TItem"/>, the class its definition makes, e.g. one of the
        /// game with typed tags.
        /// </summary>
        public static IEnumerable<TItem> GetItems<TItem>() where TItem : InventoryItem => _items.OfType<TItem>();

        /// <summary>Every owned item whose definition is a <typeparamref name="TItem"/>, e.g. a class of the game.</summary>
        public static IEnumerable<InventoryItem> GetItemsOfType<TItem>() where TItem : ItemDef =>
            _items.Where(item => item.Definition is TItem);

        /// <summary>Every owned item matching <paramref name="predicate"/>.</summary>
        public static IEnumerable<InventoryItem> GetAll(Func<InventoryItem, bool> predicate) => _items.Where(predicate);

        /// <summary>Every owned item of the definition <paramref name="definition"/>.</summary>
        public static IEnumerable<InventoryItem> GetAll(ItemDef definition) =>
            definition ? GetAll(definition.ItemDefId) : Enumerable.Empty<InventoryItem>();

        /// <summary>Every owned item with the itemdefid <paramref name="itemDefId"/>.</summary>
        public static IEnumerable<InventoryItem> GetAll(ulong itemDefId) => _items.Where(item => item.ItemDefId == itemDefId);

        /// <summary>Every owned item with the display name <paramref name="name"/>.</summary>
        public static IEnumerable<InventoryItem> GetAllByName(string name) =>
            _items.Where(item => string.Equals(item.Name, name, StringComparison.Ordinal));

        /// <summary>Every owned item carrying the tag value <paramref name="value"/>, generated or not.</summary>
        public static IEnumerable<InventoryItem> GetAllWithTag<TEnum>(TEnum value) where TEnum : Enum =>
            _items.Where(item => item.HasTag(value));

        /// <summary>The first owned item matching <paramref name="predicate"/>, or null.</summary>
        public static InventoryItem Get(Func<InventoryItem, bool> predicate) => _items.FirstOrDefault(predicate);

        /// <summary>The first owned item of the definition <paramref name="definition"/>, or null.</summary>
        public static InventoryItem Get(ItemDef definition) => GetAll(definition).FirstOrDefault();

        /// <summary>The first owned item with the itemdefid <paramref name="itemDefId"/>, or null.</summary>
        public static InventoryItem Get(ulong itemDefId) => GetAll(itemDefId).FirstOrDefault();

        /// <summary>The first owned item with the display name <paramref name="name"/>, or null.</summary>
        public static InventoryItem GetByName(string name) => GetAllByName(name).FirstOrDefault();

        /// <summary>The first owned item carrying the tag value <paramref name="value"/>, or null.</summary>
        public static InventoryItem GetWithTag<TEnum>(TEnum value) where TEnum : Enum => GetAllWithTag(value).FirstOrDefault();

        /// <summary>The owned item with the Steam instance ID <paramref name="instanceId"/>, or null.</summary>
        public static InventoryItem GetByInstanceId(SteamItemInstanceID_t instanceId) =>
            _items.FirstOrDefault(item => item.InstanceId == instanceId);

        /// <summary>The first owned item whose definition is a <typeparamref name="TItem"/>, or null.</summary>
        public static InventoryItem GetOfType<TItem>() where TItem : ItemDef => GetItemsOfType<TItem>().FirstOrDefault();

        public static bool TryGet(Func<InventoryItem, bool> predicate, out InventoryItem item) => (item = Get(predicate)) != null;

        public static bool TryGet(ItemDef definition, out InventoryItem item) => (item = Get(definition)) != null;

        public static bool TryGet(ulong itemDefId, out InventoryItem item) => (item = Get(itemDefId)) != null;

        /// <summary>The first owned item of the definition that is a <typeparamref name="TItem"/>.</summary>
        public static bool TryGet<TItem>(ItemDef definition, out TItem item) where TItem : InventoryItem =>
            (item = GetAll(definition).OfType<TItem>().FirstOrDefault()) != null;

        /// <summary>The first owned item with the itemdefid that is a <typeparamref name="TItem"/>.</summary>
        public static bool TryGet<TItem>(ulong itemDefId, out TItem item) where TItem : InventoryItem =>
            (item = GetAll(itemDefId).OfType<TItem>().FirstOrDefault()) != null;

        public static bool TryGetByName(string name, out InventoryItem item) => (item = GetByName(name)) != null;

        public static bool TryGetWithTag<TEnum>(TEnum value, out InventoryItem item) where TEnum : Enum =>
            (item = GetWithTag(value)) != null;

        public static bool TryGetByInstanceId(SteamItemInstanceID_t instanceId, out InventoryItem item) =>
            (item = GetByInstanceId(instanceId)) != null;

        public static bool TryGetOfType<TItem>(out InventoryItem item) where TItem : ItemDef => (item = GetOfType<TItem>()) != null;

        public static bool Contains(Func<InventoryItem, bool> predicate) => _items.Any(predicate);

        /// <summary>Whether the player owns at least <paramref name="quantity"/> of the definition, across stacks.</summary>
        public static bool Contains(ItemDef definition, uint quantity = 1) => Count(definition) >= quantity;

        /// <summary>Whether the player owns at least <paramref name="quantity"/> of the itemdefid, across stacks.</summary>
        public static bool Contains(ulong itemDefId, uint quantity = 1) => Count(itemDefId) >= quantity;

        public static bool ContainsName(string name) => GetByName(name) != null;

        /// <summary>Whether the player owns an item carrying the tag value <paramref name="value"/>.</summary>
        public static bool ContainsTag<TEnum>(TEnum value) where TEnum : Enum => GetWithTag(value) != null;

        public static bool ContainsInstance(SteamItemInstanceID_t instanceId) => GetByInstanceId(instanceId) != null;

        public static bool ContainsOfType<TItem>() where TItem : ItemDef => GetOfType<TItem>() != null;

        /// <summary>How many of the definition the player owns, summed over every stack.</summary>
        public static uint Count(ItemDef definition) => Sum(GetAll(definition));

        /// <summary>How many of the itemdefid the player owns, summed over every stack.</summary>
        public static uint Count(ulong itemDefId) => Sum(GetAll(itemDefId));

        /// <summary>How many items carrying the tag value the player owns, summed over every stack.</summary>
        public static uint CountWithTag<TEnum>(TEnum value) where TEnum : Enum => Sum(GetAllWithTag(value));

        public static uint Count(Func<InventoryItem, bool> predicate) => Sum(GetAll(predicate));

        private static uint Sum(IEnumerable<InventoryItem> items)
        {
            uint total = 0;

            foreach (var item in items)
                total += item.Quantity;

            return total;
        }

        #endregion

        #region Steam

        /// <summary>
        /// Reads every owned item from Steam. Steam may answer from a cache when asked too often, so call it when
        /// the inventory is about to be shown or is expected to have changed.
        /// </summary>
        public static bool Refresh(Action<bool> onComplete = null)
        {
            if (!EnsureInitialized())
                return Fail(onComplete);

            return Track(SteamInventory.GetAllItems(out var handle), handle, true, onComplete);
        }

        /// <summary>
        /// Turns the playtime credit of the player into a drop of <paramref name="generator"/>, if they have earned
        /// one. Call it at a natural pause, such as between rounds; an empty answer means no drop is due.
        /// </summary>
        public static bool TriggerItemDrop(ItemDef generator, Action<bool> onComplete = null)
        {
            if (!generator || !EnsureInitialized())
                return Fail(onComplete);

            if (generator.Type != ItemType.PlaytimeGenerator)
            {
                Debug.LogWarning($"{generator.name} is a {generator.Type}; only a playtime generator drops.");
                return Fail(onComplete);
            }

            return Track(SteamInventory.TriggerItemDrop(out var handle, ToSteam(generator)), handle, false, onComplete);
        }

        /// <summary>Grants the promo definition <paramref name="definition"/>, if the player meets its promo rules.</summary>
        public static bool AddPromoItem(ItemDef definition, Action<bool> onComplete = null)
        {
            if (!definition || !EnsureInitialized())
                return Fail(onComplete);

            return Track(SteamInventory.AddPromoItem(out var handle, ToSteam(definition)), handle, false, onComplete);
        }

        /// <summary>Grants each promo definition of <paramref name="definitions"/> the player meets the promo rules of.</summary>
        public static bool AddPromoItems(IEnumerable<ItemDef> definitions, Action<bool> onComplete = null)
        {
            var ids = definitions?.Where(definition => definition).Select(ToSteam).ToArray();

            if (ids == null || ids.Length == 0 || !EnsureInitialized())
                return Fail(onComplete);

            return Track(SteamInventory.AddPromoItems(out var handle, ids, (uint)ids.Length), handle, false, onComplete);
        }

        /// <summary>
        /// Grants every promo item the player meets the promo rules of, manual promos included, except the ones set to be
        /// granted only manually.
        /// </summary>
        public static bool GrantPromoItems(Action<bool> onComplete = null)
        {
            if (!EnsureInitialized())
                return Fail(onComplete);

            return Track(SteamInventory.GrantPromoItems(out var handle), handle, false, onComplete);
        }

        /// <summary>
        /// Removes <paramref name="quantity"/> of the owned <paramref name="item"/> for good, e.g. a used consumable.
        /// </summary>
        public static bool ConsumeItem(InventoryItem item, uint quantity = 1, Action<bool> onComplete = null)
        {
            if (item == null || quantity == 0 || !EnsureInitialized())
                return Fail(onComplete);

            return Track(SteamInventory.ConsumeItem(out var handle, item.InstanceId, quantity), handle, false, onComplete);
        }

        /// <summary>
        /// Creates <paramref name="target"/> from the owned <paramref name="materials"/>, which Steam checks against the
        /// target's exchange recipes and destroys on success, e.g. opening a container with a key.
        /// </summary>
        public static bool ExchangeItems(ItemDef target, IEnumerable<(InventoryItem item, uint quantity)> materials,
            Action<bool> onComplete = null)
        {
            var destroy = materials?.Where(material => material.item != null && material.quantity > 0).ToArray();

            if (!target || destroy == null || destroy.Length == 0 || !EnsureInitialized())
                return Fail(onComplete);

            // Steam generates exactly one item per exchange.
            var generated = SteamInventory.ExchangeItems(out var handle,
                new[] { ToSteam(target) }, new uint[] { 1 }, 1,
                destroy.Select(material => material.item.InstanceId).ToArray(),
                destroy.Select(material => material.quantity).ToArray(), (uint)destroy.Length);

            return Track(generated, handle, false, onComplete);
        }

        /// <inheritdoc cref="ExchangeItems(ItemDef, IEnumerable{ValueTuple{InventoryItem, uint}}, Action{bool})"/>
        public static bool ExchangeItems(ItemDef target, params (InventoryItem item, uint quantity)[] materials) =>
            ExchangeItems(target, materials, null);

        /// <summary>
        /// Grants <paramref name="items"/> outright. Works only for accounts of the Steamworks partner group, so it is
        /// for testing, never for the game itself.
        /// </summary>
        public static bool GenerateItems(IEnumerable<(ItemDef definition, uint quantity)> items, Action<bool> onComplete = null)
        {
            var generate = items?.Where(item => item.definition && item.quantity > 0).ToArray();

            if (generate == null || generate.Length == 0 || !EnsureInitialized())
                return Fail(onComplete);

            var generated = SteamInventory.GenerateItems(out var handle,
                generate.Select(item => ToSteam(item.definition)).ToArray(),
                generate.Select(item => item.quantity).ToArray(), (uint)generate.Length);

            return Track(generated, handle, false, onComplete);
        }

        /// <summary>
        /// True when inventory calls can be made. Warns only the first time it fails, so a game that asks every frame
        /// does not flood the console.
        /// </summary>
        internal static bool EnsureInitialized()
        {
            if (Initialized)
                return true;

            if (_warnedNotInitialized)
                return false;

            _warnedNotInitialized = true;

            Debug.LogWarning("The Steam inventory is not initialized because no Steam session is running. " +
                             "Add a SteamManager to the game, or outside play mode turn " +
                             "on \"Window/Steam Toys/Connect To Steam\"; if that is already done, " +
                             "the SteamToys error logged when the session failed to start says why.");

            return false;
        }

        /// <summary>
        /// Prepares the inventory for a new session. Called by <see cref="SteamSession"/> each time it starts: the
        /// items of the previous session may belong to another account, and its callback was unregistered.
        /// </summary>
        internal static void OnSessionStarted()
        {
            _warnedNotInitialized = false;
            _pending.Clear();
            IsReady = false;

            ClearItems();

            _resultReadyCallback?.Dispose();
            _resultReadyCallback = Callback<SteamInventoryResultReady_t>.Create(OnResultReady);

            _definitionUpdateCallback?.Dispose();
            _definitionUpdateCallback = Callback<SteamInventoryDefinitionUpdate_t>.Create(OnDefinitionUpdate);

            // The published definitions fill in items the DB lacks; Steam answers with a definition update.
            SteamInventory.LoadItemDefinitions();
        }

        // Entering play mode with domain reload disabled keeps this alive from the previous run.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _warnedNotInitialized = false;
            _pending.Clear();
            IsReady = false;

            ClearItems();
        }

        private static bool Track(bool started, SteamInventoryResult_t handle, bool isFullInventory, Action<bool> onComplete)
        {
            if (!started)
                return Fail(onComplete);

            _pending[handle] = new PendingResult(isFullInventory, onComplete);

            return true;
        }

        private static bool Fail(Action<bool> onComplete)
        {
            onComplete?.Invoke(false);

            return false;
        }

        private static void OnResultReady(SteamInventoryResultReady_t callback)
        {
            // Results of calls made elsewhere belong to their caller, who destroys them.
            if (!_pending.Remove(callback.m_handle, out var pending))
                return;

            var succeeded = callback.m_result == EResult.k_EResultOK;

            try
            {
                if (succeeded)
                    succeeded = Apply(callback.m_handle, pending.IsFullInventory);
                else
                    Debug.LogWarning($"[SteamToys] A Steam inventory call failed: {callback.m_result}.");
            }
            finally
            {
                SteamInventory.DestroyResult(callback.m_handle);
            }

            pending.OnComplete?.Invoke(succeeded);
        }

        /// <summary>
        /// Takes the items of a result: the whole inventory replaces the list, any other result only changes the
        /// items it lists, removing the ones Steam marks as removed.
        /// </summary>
        private static bool Apply(SteamInventoryResult_t handle, bool isFullInventory)
        {
            uint count = 0;

            if (!SteamInventory.GetResultItems(handle, null, ref count))
                return false;

            var details = new SteamItemDetails_t[count];

            if (count > 0 && !SteamInventory.GetResultItems(handle, details, ref count))
                return false;

            var reported = new HashSet<SteamItemInstanceID_t>();

            for (uint i = 0; i < count; i++)
            {
                var detail = details[i];
                var flags = (ESteamItemFlags)detail.m_unFlags;

                reported.Add(detail.m_itemId);

                if ((flags & ESteamItemFlags.k_ESteamItemRemoved) != 0)
                {
                    RemoveItem(detail.m_itemId);
                    continue;
                }

                var item = GetByInstanceId(detail.m_itemId) ?? AddItem(detail);

                item.Update(detail.m_unQuantity, flags, GetProperties(handle, i));
            }

            if (isFullInventory)
            {
                for (var i = _items.Count - 1; i >= 0; i--)
                {
                    if (!reported.Contains(_items[i].InstanceId))
                        RemoveItemAt(i);
                }

                IsReady = true;
            }

            OnChanged?.Invoke();

            return true;
        }

        /// <summary>Adds the reported item, pointed at its definition.</summary>
        private static InventoryItem AddItem(SteamItemDetails_t detail)
        {
            var itemDefId = (ulong)(int)detail.m_iDefinition;
            var definition = SteamItemsDB.Instance ? SteamItemsDB.Instance.Get(itemDefId) : null;
            var isInDB = definition && definition.Type == ItemType.Item;

            if (!isInDB)
                definition = GetPublishedDefinition(itemDefId);

            // The definition makes the item, so a class of the game gets its own kind of item.
            var item = definition.CreateInventoryItem();

            item.SetInstanceId(detail.m_itemId);
            item.SetDefinition(definition, isInDB);

            _items.Add(item);

            return item;
        }

        /// <summary>
        /// The definition made at runtime for an itemdefid <see cref="SteamItemsDB"/> lacks, so nothing the player owns
        /// goes missing. Made once and shared by every item of the itemdefid.
        /// </summary>
        private static DefaultItemDef GetPublishedDefinition(ulong itemDefId)
        {
            if (_publishedDefinitions.TryGetValue(itemDefId, out var definition) && definition)
                return definition;

            definition = ScriptableObject.CreateInstance<DefaultItemDef>();
            definition.name = $"Item {itemDefId}";
            definition.hideFlags = HideFlags.DontSave;
            definition.ItemDefId = itemDefId;

            ApplyPublishedDefinition(definition);

            _publishedDefinitions[itemDefId] = definition;

            return definition;
        }

        /// <summary>Every property Steam reports for the item at <paramref name="index"/> of the result, by name.</summary>
        private static Dictionary<string, string> GetProperties(SteamInventoryResult_t handle, uint index)
        {
            var properties = new Dictionary<string, string>();

            // Asking without a name lists the names the item has.
            var names = ReadProperty((string name, ref uint size, out string value) =>
                SteamInventory.GetResultItemProperty(handle, index, name, out value, ref size), null);

            foreach (var name in names.Split(PROPERTY_NAME_SEPARATOR))
            {
                var trimmed = name.Trim();

                if (trimmed.Length > 0)
                    properties[trimmed] = ReadProperty((string propertyName, ref uint size, out string value) =>
                        SteamInventory.GetResultItemProperty(handle, index, propertyName, out value, ref size), trimmed);
            }

            return properties;
        }

        /// <summary>
        /// Fills a definition made at runtime from the one published on Steam. Steam may not have loaded its
        /// definitions yet; <see cref="OnDefinitionUpdate"/> fills it in once it has.
        /// </summary>
        private static bool ApplyPublishedDefinition(DefaultItemDef defaultItemDef)
        {
            var id = ToSteam(defaultItemDef);

            string Read(string name) => ReadProperty((string propertyName, ref uint size, out string value) =>
                SteamInventory.GetItemDefinitionProperty(id, propertyName, out value, ref size), name);

            var displayName = Read("name");

            if (string.IsNullOrEmpty(displayName))
                return false;

            defaultItemDef.Name = displayName;
            var tags = new DefaultTags();
            TagsConverter.FromString(Read("tags"), tags);
            // item.Tags = tags;
            defaultItemDef.Description = Read("description");
            defaultItemDef.DisplayType = Read("display_type");
            defaultItemDef.IconUrl = new Url(Read("icon_url"));
            defaultItemDef.IconUrlLarge = new Url(Read("icon_url_large"));
            defaultItemDef.NameColor = ParseColor(Read("name_color"));
            defaultItemDef.BackgroundColor = ParseColor(Read("background_color"));
            defaultItemDef.Tradable = ParseBool(Read("tradable"));
            defaultItemDef.Marketable = ParseBool(Read("marketable"));
            defaultItemDef.GameOnly = ParseBool(Read("game_only"));

            return true;
        }

        private static void OnDefinitionUpdate(SteamInventoryDefinitionUpdate_t callback)
        {
            var changed = false;

            foreach (var definition in _publishedDefinitions.Values)
            {
                if (definition && string.IsNullOrEmpty(definition.Name))
                    changed |= ApplyPublishedDefinition(definition);
            }

            if (changed)
                OnChanged?.Invoke();
        }

        private delegate bool PropertyReader(string name, ref uint size, out string value);

        private static string ReadProperty(PropertyReader reader, string name)
        {
            var size = PROPERTY_BUFFER_SIZE;

            if (!reader(name, ref size, out var value))
                return string.Empty;

            // A full buffer may mean the value was cut short, and the size Steam reports then is what it needs.
            if (size >= PROPERTY_BUFFER_SIZE)
            {
                size = Math.Max(size + 1, PROPERTY_BUFFER_SIZE * 8);

                if (!reader(name, ref size, out value))
                    return string.Empty;
            }

            return value ?? string.Empty;
        }

        // Six hex digits, as in the schema; anything else stays unset.
        private static Color32 ParseColor(string hex) =>
            hex is { Length: 6 } && uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var rgb)
                ? new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 0xFF)
                : default;

        // The client reports flags as "1" / "0"; the schema writes them as "true" / "false".
        private static bool ParseBool(string value) =>
            value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

        private static void RemoveItem(SteamItemInstanceID_t instanceId)
        {
            var index = _items.FindIndex(item => item.InstanceId == instanceId);

            if (index >= 0)
                RemoveItemAt(index);
        }

        private static void RemoveItemAt(int index) => _items.RemoveAt(index);

        private static void ClearItems()
        {
            _items.Clear();

            foreach (var definition in _publishedDefinitions.Values)
                Destroy(definition);

            _publishedDefinitions.Clear();
        }

        private static void Destroy(Object definition)
        {
            if (!definition)
                return;

            if (Application.isPlaying)
                Object.Destroy(definition);
            else
                Object.DestroyImmediate(definition);
        }

        private static SteamItemDef_t ToSteam(ItemDef definition) => new((int)definition.ItemDefId);

        #endregion
    }
}
