using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Runtime;
using SteamToys.Runtime.Core;
using SteamToys.Runtime.InventorySystem.ExchangeVariable.Materials;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// The item definitions of the game: every <see cref="ItemDef"/> asset in the Resources folder
    /// <see cref="ResourcesPath"/>, gathered for lookups by itemdefid and for the JSON file the Steam Inventory
    /// Service imports.
    /// <para>
    /// The definitions are assets of their own rather than sub-assets, so the DB keeps no list: it loads the folder
    /// the first time it is asked. Its inspector, which "Window/Steam Toys/Steam Items DB" opens, adds and removes
    /// definitions there. A definition elsewhere still works on its own; the DB only knows the ones in the folder.
    /// </para>
    /// <see href="https://partner.steamgames.com/doc/features/inventory/schema"/>
    /// </summary>
    public class SteamItemsDB : SingletonObject<SteamItemsDB>
    {
        #region Constants

        /// <summary>The folder, relative to a Resources folder, the definitions are loaded from.</summary>
        public const string ResourcesPath = "Steam/Items";

        /// <summary>The folder new definitions are created in.</summary>
        public const string AssetsFolder = "Assets/Resources/" + ResourcesPath;

        /// <summary>Steam reserves itemdefids from here up for Workshop items.</summary>
        public const ulong FirstWorkshopItemDefId = 1_000_000;

        /// <summary>The highest itemdefid Steam accepts.</summary>
        public const ulong MaxItemDefId = int.MaxValue;

        #endregion

        #region Properties

        /// <summary>
        /// The app the definitions belong to, from the Steam Settings: the <c>appid</c> of the exported JSON.
        /// </summary>
        public static uint AppId => SteamSettings.Instance ? SteamSettings.Instance.AppId.m_AppId : 0;

        /// <summary>Every definition in <see cref="ResourcesPath"/>, ordered by itemdefid.</summary>
        public IReadOnlyList<ItemDef> Items
        {
            get
            {
                EnsureLoaded();

                return _items;
            }
        }

        #endregion

        #region Private Fields

        private List<ItemDef> _items;
        private Dictionary<ulong, ItemDef> _byId;

        #endregion

        #region Items

        /// <summary>
        /// Loads the folder again, e.g. after definitions were added, removed or given another itemdefid.
        /// </summary>
        public void Reload()
        {
            _items = Resources.LoadAll<ItemDef>(ResourcesPath).OrderBy(item => item.ItemDefId).ToList();
            _byId = new Dictionary<ulong, ItemDef>();

            // The first definition with an itemdefid wins; the duplicates are reported by CollectSchemaWarnings.
            foreach (var item in _items)
                _byId.TryAdd(item.ItemDefId, item);
        }

        /// <summary>The definition with the given itemdefid, or null when the DB has none.</summary>
        public ItemDef Get(ulong itemDefId)
        {
            EnsureLoaded();

            if (_byId.TryGetValue(itemDefId, out var item) && item && item.ItemDefId == itemDefId)
                return item;

#if UNITY_EDITOR
            // Definitions are edited while the DB is loaded, so in the editor a miss may only mean the cache is stale.
            Reload();

            return _byId.TryGetValue(itemDefId, out item) ? item : null;
#else
            return null;
#endif
        }

        /// <summary>
        /// Finds the definition with the given itemdefid, as long as it is a <typeparamref name="TItemDef"/>.
        /// </summary>
        public bool TryGet<TItemDef>(ulong itemDefId, out TItemDef item) where TItemDef : ItemDef
        {
            item = Get(itemDefId) as TItemDef;

            return item;
        }

        /// <summary>Every definition of the DB that is a <typeparamref name="TItemDef"/>.</summary>
        public IEnumerable<TItemDef> GetAll<TItemDef>() where TItemDef : ItemDef => Items.OfType<TItemDef>();

        /// <summary>The lowest itemdefid above every definition of the DB, for a new definition.</summary>
        public ulong GetNextItemDefId() => Items.Count == 0 ? 1 : Items.Max(item => item.ItemDefId) + 1;

        private void EnsureLoaded()
        {
            if (_items == null)
                Reload();
        }

        #endregion

        #region Validation

        /// <summary>
        /// Adds a message for every problem of the definitions as a set, which no single definition can see:
        /// itemdefids Steam rejects or that repeat, and references to definitions the exported file would not have.
        /// </summary>
        public void CollectSchemaWarnings(List<string> warnings)
        {
            var ids = new HashSet<ulong>();
            var known = new HashSet<ItemDef>(Items);

            foreach (var item in Items)
            {
                if (!item)
                    continue;

                var id = item.ItemDefId;

                if (id == 0)
                    warnings.Add($"{item.name} has no itemdefid.");
                else if (id > MaxItemDefId)
                    warnings.Add($"{item.name} has itemdefid {id}, above the highest Steam accepts ({MaxItemDefId}).");
                else if (id >= FirstWorkshopItemDefId)
                    warnings.Add($"{item.name} has itemdefid {id}; Steam reserves {FirstWorkshopItemDefId} and up for Workshop items.");

                if (!ids.Add(id))
                    warnings.Add($"Itemdefid {id} is used by more than one definition.");

                foreach (var reference in GetReferences(item))
                {
                    if (reference && !known.Contains(reference))
                        warnings.Add($"{item.name} references {reference.name}, which is not in {AssetsFolder}, so Steam would not know it.");
                }
            }
        }

        /// <summary>Every definition <paramref name="item"/> names in its JSON.</summary>
        private static IEnumerable<ItemDef> GetReferences(ItemDef item)
        {
            var contents = item switch
            {
                Bundle bundle => bundle.Content,
                Generator generator => generator.Content,
                PlaytimeGenerator generator => generator.Content,
                _ => null
            };

            if (contents != null)
            {
                foreach (var element in contents)
                {
                    if (element != null)
                        yield return element.Item;
                }
            }

            var tagGenerators = item switch
            {
                Bundle bundle => bundle.TagGenerators,
                Generator generator => generator.TagGenerators,
                PlaytimeGenerator generator => generator.TagGenerators,
                _ => null
            };

            if (tagGenerators != null)
            {
                foreach (var tagGenerator in tagGenerators)
                    yield return tagGenerator;
            }

            if (item is Item { ContainerContentsGenerator: { } containerContentsGenerator })
                yield return containerContentsGenerator;

            if (item is CraftableItemDef { Exchange: { } exchange })
            {
                foreach (var recipe in exchange)
                {
                    if (recipe == null)
                        continue;

                    foreach (var material in recipe)
                    {
                        if (material is ItemMaterial itemMaterial)
                            yield return itemMaterial.Item;
                    }
                }
            }
        }

        #endregion

        #region Json

        /// <summary>
        /// Every definition of the DB in the file the Steam Inventory Service imports:
        /// an <c>appid</c> plus an <c>items</c> array.
        /// </summary>
        public string ToJson() => ToImportJson(Items);

        /// <summary>
        /// Wraps <paramref name="items"/> in the file the Steam Inventory Service imports, with the
        /// <see cref="AppId"/> of the Steam Settings.
        /// </summary>
        public static string ToImportJson(IEnumerable<ItemDef> items)
        {
            using var stringWriter = new StringWriter();
            using var writer = new JsonTextWriter(stringWriter) { Formatting = Formatting.Indented };

            var serializer = JsonSerializer.CreateDefault();

            writer.WriteStartObject();

            writer.WritePropertyName("appid");
            writer.WriteValue(AppId);

            writer.WritePropertyName("items");
            writer.WriteStartArray();

            foreach (var item in items)
            {
                if (item)
                    serializer.Serialize(writer, item);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();

            return stringWriter.ToString();
        }

        #endregion
    }
}
