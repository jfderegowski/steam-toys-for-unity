using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SteamToys.Runtime.StatsSystem;
using Steamworks;
using UnityEngine;

namespace SteamToys.Editor.Core
{
    public static partial class SteamAppCache
    {
        // Parsed schemas by app, with the write time of the file each was read from. A file that could
        // not be parsed is kept as a null schema, so it is reported once rather than on every call.
        private static readonly Dictionary<uint, (DateTime writeTime, StatSchema schema)> _statSchemas = new();

        /// <summary>
        /// The stats of <paramref name="appId"/> as configured and published on the partner site, in
        /// the copy the Steam client downloaded: <c>appcache/stats/UserGameStatsSchema_&lt;appid&gt;.bin</c>.
        /// <para>
        /// The client checks for a newer version when the app connects to Steam, so a change published
        /// on the partner site shows up here after the next connection, without restarting the client.
        /// Returns false when there is no file, which means the app never connected to Steam on this
        /// machine, or when it cannot be read.
        /// </para>
        /// <para>
        /// The file is parsed again only after the client rewrote it, so this is cheap to call on
        /// every redraw.
        /// </para>
        /// </summary>
        public static bool TryGetStatSchema(AppId_t appId, out StatSchema schema)
        {
            schema = null;

            var path = GetPath("stats", $"UserGameStatsSchema_{appId.m_AppId}.bin");

            if (path == null || !File.Exists(path))
                return false;

            var writeTime = File.GetLastWriteTimeUtc(path);

            if (_statSchemas.TryGetValue(appId.m_AppId, out var cached) && cached.writeTime == writeTime)
            {
                schema = cached.schema;

                return schema != null;
            }

            try
            {
                schema = ReadStatSchema(File.ReadAllBytes(path), appId, writeTime.ToLocalTime());
            }
            catch (IOException)
            {
                // The client may be writing the file at this very moment. Nothing is cached, so the
                // next call tries again.
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SteamToys] Could not read the stat schema Steam downloaded for app {appId}, at {path}: {exception.Message}");
            }

            _statSchemas[appId.m_AppId] = (writeTime, schema);

            return schema != null;
        }

        private static StatSchema ReadStatSchema(byte[] data, AppId_t appId, DateTime downloadedAt)
        {
            // The whole file is one section named after the app.
            if (!ReadKeyValues(data).TryGetValue(appId.m_AppId.ToString(CultureInfo.InvariantCulture), out var appSection) ||
                appSection is not Dictionary<string, object> app)
                throw new InvalidDataException($"The file holds no section for app {appId}.");

            var stats = new Dictionary<string, StatDefinition>(StringComparer.Ordinal);

            if (app.TryGetValue("stats", out var statsSection) && statsSection is Dictionary<string, object> entries)
            {
                foreach (var entry in entries.Values)
                {
                    // Achievements share the list, as entries of a type of their own.
                    if (entry is not Dictionary<string, object> stat || !TryGetStatType(stat, out var type))
                        continue;

                    var apiName = GetString(stat, "name");

                    if (string.IsNullOrEmpty(apiName))
                        continue;

                    stats[apiName] = new StatDefinition(
                        apiName,
                        type,
                        GetNumber(stat, "default"),
                        GetNumber(stat, "min"),
                        GetNumber(stat, "max"),
                        GetNumber(stat, "maxchange"),
                        GetNumber(stat, "incrementonly") is { } incrementOnly && incrementOnly != 0,
                        GetNumber(stat, "windowsize"),
                        GetDisplayName(stat));
                }
            }

            return new StatSchema((int)(GetNumber(app, "version") ?? 0), downloadedAt, stats);
        }

        /// <summary>
        /// The client writes the type either spelled out or as its number, and uses 4 and 5 for the
        /// two kinds of achievement entries, which are no stats.
        /// </summary>
        private static bool TryGetStatType(Dictionary<string, object> stat, out SteamStatType type)
        {
            switch (GetString(stat, "type")?.ToUpperInvariant())
            {
                case "INT":
                case "1":
                    type = SteamStatType.Int;
                    return true;
                case "FLOAT":
                case "2":
                    type = SteamStatType.Float;
                    return true;
                case "AVGRATE":
                case "3":
                    type = SteamStatType.AvgRate;
                    return true;
                default:
                    type = default;
                    return false;
            }
        }

        private static string GetDisplayName(Dictionary<string, object> stat)
        {
            if (!stat.TryGetValue("display", out var display) || display is not Dictionary<string, object> section)
                return null;

            // A plain string on stats; achievements keep a section of languages instead.
            return section.TryGetValue("name", out var name) && name is Dictionary<string, object> languages
                ? GetString(languages, "english")
                : GetString(section, "name");
        }

        private static string GetString(Dictionary<string, object> section, string key) =>
            section.TryGetValue(key, out var value) && value is not Dictionary<string, object>
                ? Convert.ToString(value, CultureInfo.InvariantCulture)
                : null;

        /// <summary>
        /// A number however the client stored it: as text, e.g. <c>"0.1"</c>, or as an integer or
        /// float entry. Null when the key is absent, which means the setting is left unset on Steam.
        /// </summary>
        private static double? GetNumber(Dictionary<string, object> section, string key)
        {
            if (!section.TryGetValue(key, out var value))
                return null;

            switch (value)
            {
                case string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number):
                    return number;
                case int number:
                    return number;
                case float number:
                    return number;
                case long number:
                    return number;
                case ulong number:
                    return number;
                default:
                    return null;
            }
        }
    }

    /// <summary>The stats of one app, as the Steam client downloaded them.</summary>
    public sealed class StatSchema
    {
        /// <summary>The version Steam gave the schema; publishing changed stats or achievements raises it.</summary>
        public int Version { get; }

        /// <summary>When the client wrote the file, which it does only when it downloads a newer version.</summary>
        public DateTime DownloadedAt { get; }

        /// <summary>The stats by API Name, compared exactly. Achievements are not included.</summary>
        public IReadOnlyDictionary<string, StatDefinition> Stats { get; }

        internal StatSchema(int version, DateTime downloadedAt, IReadOnlyDictionary<string, StatDefinition> stats)
        {
            Version = version;
            DownloadedAt = downloadedAt;
            Stats = stats;
        }
    }

    /// <summary>
    /// One stat as configured on the partner site. A null number is a setting left unset there, which
    /// the client leaves out of the file.
    /// </summary>
    public readonly struct StatDefinition
    {
        public string ApiName { get; }
        public SteamStatType Type { get; }
        public double? Default { get; }
        public double? Min { get; }
        public double? Max { get; }
        public double? MaxChange { get; }
        public bool IncrementOnly { get; }

        /// <summary>The window the rolling average of an <see cref="SteamStatType.AvgRate"/> stat covers.</summary>
        public double? WindowSize { get; }

        public string DisplayName { get; }

        internal StatDefinition(
            string apiName, SteamStatType type, double? defaultValue, double? min, double? max, double? maxChange,
            bool incrementOnly, double? windowSize, string displayName)
        {
            ApiName = apiName;
            Type = type;
            Default = defaultValue;
            Min = min;
            Max = max;
            MaxChange = maxChange;
            IncrementOnly = incrementOnly;
            WindowSize = windowSize;
            DisplayName = displayName;
        }
    }
}
