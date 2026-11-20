using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SteamToys.Runtime.InventorySystem;
using UnityEditor;

namespace SteamToys.Editor.InventorySystem
{
    /// <summary>
    /// The definitions the Steam client reported, kept for every inspector after the Steam session ends, and the
    /// comparison of a definition of the project with the one Steam has under its itemdefid. The inspector of a
    /// definition frames the fields that differ; the Steam Items DB tints them in its table.
    /// <para>
    /// The definitions are read through the Steam client, so they need a Steam session. The client never lists
    /// bundles, tag generators and playtime generators, so those of the DB are looked up by itemdefid.
    /// </para>
    /// </summary>
    internal static class SteamComparison
    {
        // How long to wait before asking a Steam client that had no definitions yet again.
        private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

        /// <summary>The properties compared with Steam: the ones Create From Steam takes over.</summary>
        internal static readonly string[] ComparedProperties =
        {
            "type", "name", "description", "tags", "exchange", "tag_generators", "tag_generator_name",
            "tag_generator_values", "display_type", "icon_url", "icon_url_large", "name_color", "background_color",
            "store_hidden", "tradable", "marketable", "game_only", "auto_stack"
        };

        /// <summary>
        /// The serialized field of <see cref="ItemDef"/> each compared property is written from, which the inspector
        /// frames with the difference. The type has none: it comes from the class.
        /// </summary>
        private static readonly Dictionary<string, string> Fields = new()
        {
            { "name", ItemDefElement.NAME },
            { "description", ItemDefElement.DESCRIPTION },
            { "tags", ItemDefElement.TAGS },
            { "exchange", ItemDefElement.EXCHANGE },
            { "tag_generators", ItemDefElement.TAG_GENERATORS },
            { "tag_generator_name", ItemDefElement.TAG_NAME },
            { "tag_generator_values", ItemDefElement.TAG_VALUES_AND_CHANCES },
            { "display_type", ItemDefElement.DISPLAY_TYPE },
            { "icon_url", ItemDefElement.ICON_URL },
            { "icon_url_large", ItemDefElement.ICON_URL_LARGE },
            { "name_color", ItemDefElement.NAME_COLOR },
            { "background_color", ItemDefElement.BACKGROUND_COLOR },
            { "store_hidden", ItemDefElement.STORE_HIDDEN },
            { "tradable", ItemDefElement.TRADABLE },
            { "marketable", ItemDefElement.MARKETABLE },
            { "game_only", ItemDefElement.GAME_ONLY },
            { "auto_stack", ItemDefElement.AUTO_STACK }
        };

        private static readonly HashSet<string> BoolProperties = new() { "store_hidden", "tradable", "marketable", "game_only", "auto_stack" };
        private static readonly HashSet<string> ColorProperties = new() { "name_color", "background_color" };
        private static readonly HashSet<string> ListProperties = new() { "tags", "exchange", "tag_generators", "tag_generator_values" };

        private static List<ItemDefsFromSteam.SteamDefinition> _definitions;
        private static Dictionary<ulong, ItemDefsFromSteam.SteamDefinition> _byId = new();
        private static bool _stale = true;

        /// <summary>
        /// What the Steam client reported. Null until it was read; empty while the client has not loaded the
        /// definitions yet.
        /// </summary>
        internal static IReadOnlyList<ItemDefsFromSteam.SteamDefinition> Definitions => _definitions;

        /// <summary>When <see cref="Definitions"/> were read.</summary>
        internal static DateTime ReadAt { get; private set; }

        /// <summary>Whether there are definitions of Steam to compare with.</summary>
        internal static bool HasDefinitions => _definitions is { Count: > 0 };

        /// <summary>Raised when <see cref="Definitions"/> were read again.</summary>
        internal static event Action Changed;

        [InitializeOnLoadMethod]
        private static void WatchSteamDefinitions() => Inventory.OnDefinitionsChanged += MarkStale;

        /// <summary>Reads the definitions again on the next <see cref="Update"/>, for a change the client did not report.</summary>
        internal static void MarkStale() => _stale = true;

        /// <summary>
        /// Reads the definitions when they are stale, or the client had none a while ago. Cheap otherwise, so it can
        /// be polled. True when they were read.
        /// </summary>
        internal static bool Update()
        {
            if (!Inventory.Initialized ||
                !_stale && !(_definitions is { Count: 0 } && DateTime.Now - ReadAt > RetryInterval))
                return false;

            var definitions = ItemDefsFromSteam.ReadAll();

            if (definitions == null)
                return false;

            _stale = false;
            ReadAt = DateTime.Now;

            // Empty means the client has not loaded them yet, and was asked to.
            if (definitions.Count > 0)
            {
                var listed = new HashSet<ulong>(definitions.Select(definition => definition.ItemDefId));

                foreach (var item in SteamItemsDB.Instance.Items)
                {
                    if (item && listed.Add(item.ItemDefId) && ItemDefsFromSteam.Read(item.ItemDefId) is { } unlisted)
                        definitions.Add(unlisted);
                }
            }

            _definitions = definitions;
            _byId = new Dictionary<ulong, ItemDefsFromSteam.SteamDefinition>();

            foreach (var definition in definitions)
                _byId.TryAdd(definition.ItemDefId, definition);

            Changed?.Invoke();

            return true;
        }

        /// <summary>The definition Steam has under <paramref name="itemDefId"/>, or null.</summary>
        internal static ItemDefsFromSteam.SteamDefinition Find(ulong itemDefId) =>
            _byId.TryGetValue(itemDefId, out var definition) ? definition : null;

        /// <summary>A definition of the project next to the one Steam has under its itemdefid.</summary>
        internal sealed class Result
        {
            /// <summary>The JSON of the definition; null when it cannot be written.</summary>
            public JObject Json;

            /// <summary>Why the JSON cannot be written; null when it can.</summary>
            public string JsonError;

            /// <summary>The compared properties that differ, with the value Steam has.</summary>
            public readonly Dictionary<string, string> Differences = new();
        }

        /// <summary>Compares <paramref name="item"/> with <paramref name="steam"/>, property by property.</summary>
        internal static Result Compare(ItemDef item, ItemDefsFromSteam.SteamDefinition steam)
        {
            var result = new Result();

            try
            {
                result.Json = JObject.FromObject(item);
            }
            catch (Exception exception) when (exception is InvalidOperationException or NullReferenceException)
            {
                // An exchange entry that is not filled in, which the export stops on too.
                result.JsonError = exception.Message;

                return result;
            }

            foreach (var property in ComparedProperties)
            {
                var here = Normalize(property, result.Json.TryGetValue(property, out var token) ? token.ToString() : null);
                var there = Normalize(property, steam.Get(property));

                if (here != there)
                    result.Differences.Add(property, there);
            }

            return result;
        }

        /// <summary>
        /// The warnings of <paramref name="item"/> against the definition Steam has under its itemdefid, each on the
        /// field it is written from. None until Steam was read.
        /// </summary>
        internal static IEnumerable<ItemDefWarnign> GetWarnings(ItemDef item)
        {
            if (!HasDefinitions)
                yield break;

            var steam = Find(item.ItemDefId);

            if (steam == null)
            {
                // The client does not hand out the other types, so their absence says nothing.
                if (item.Type is ItemType.Item or ItemType.Generator)
                    yield return new ItemDefWarnign(item, ItemDefElement.ITEM_DEF_ID,
                        $"Steam has no definition with itemdefid {item.ItemDefId}. Save the JSON and upload it on the " +
                        "Inventory Service page of Steamworks.");

                yield break;
            }

            var result = Compare(item, steam);

            if (result.JsonError != null)
            {
                yield return new ItemDefWarnign(item, null, $"Cannot be written as JSON, so it is not compared with Steam: {result.JsonError}");
                yield break;
            }

            foreach (var (property, there) in result.Differences)
            {
                if (property == "type")
                {
                    yield return new ItemDefWarnign(item, null,
                        $"Steam has this definition as a {steam.Type}, and the type comes from the class of the asset, " +
                        "so Create From Steam leaves it alone.");
                    continue;
                }

                var here = Normalize(property, result.Json.TryGetValue(property, out var token) ? token.ToString() : null);

                yield return new ItemDefWarnign(item, Fields.TryGetValue(property, out var field) ? field : null,
                    $"{property} differs from Steam: \"{here}\" here, \"{there}\" on Steam.");
            }
        }

        /// <summary>
        /// A property as the schema means it, so the JSON of the DB and what the client reports compare equal: the
        /// client writes flags as 1 and 0 and leaves out false ones, a color may have a leading #, and the order of
        /// list entries means nothing.
        /// </summary>
        internal static string Normalize(string property, string value)
        {
            value = value?.Trim() ?? string.Empty;

            if (BoolProperties.Contains(property))
                return value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ? "true" : "false";

            if (ColorProperties.Contains(property))
                return value.TrimStart('#').ToUpperInvariant();

            if (ListProperties.Contains(property))
                return string.Join(";", value
                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(entry => string.Join(",", entry
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(part => part.Trim())
                        .OrderBy(part => part, StringComparer.Ordinal)))
                    .Where(entry => entry.Length > 0)
                    .OrderBy(entry => entry, StringComparer.Ordinal));

            return value;
        }
    }
}
