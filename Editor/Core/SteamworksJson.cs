using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SteamToys.Editor.AchievementsSystem;
using SteamToys.Editor.StatsSystem;
using SteamToys.Runtime.AchievementsSystem;
using SteamToys.Runtime.StatsSystem;
using Steamworks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SteamToys.Editor.Core
{
    /// <summary>
    /// Stats and achievements as JSON for the Steam Toys web extension, which pastes them into the Stats
    /// and Achievements pages of the Steamworks partner site. Steam has no API that writes them, so the
    /// extension sends what those pages send when they are filled in by hand.
    /// <para>
    /// The JSON is <c>{ "format": "steam-toys/1", "kind": "stats" | "achievements", "appId", "items" }</c>,
    /// one kind at a time, since each page takes only its own. Items are matched to Steam by API Name.
    /// A setting left unset is null, and icons travel inside as base64.
    /// </para>
    /// </summary>
    internal static class SteamworksJson
    {
        internal const string Format = "steam-toys/1";

        // The partner site refuses larger icons: MAX_FILE_SIZE of its upload form.
        private const int MaxIconBytes = 3000000;

        // Steam takes icons from 64x64 and recommends 256x256.
        private const int MinIconSize = 64;

        #region Stats

        /// <summary>The JSON of <paramref name="stats"/> as a preview shows it, problems or not.</summary>
        public static string PreviewStats(IEnumerable<SteamStat> stats) =>
            Envelope("stats", BuildStats(Distinct(stats), new List<string>(), new List<string>()));

        /// <summary>
        /// The JSON of <paramref name="stats"/> for the Stats page, or null after showing why a stat cannot
        /// go to Steam as it is.
        /// </summary>
        public static string GetStatsJson(IEnumerable<SteamStat> stats)
        {
            var list = Distinct(stats);
            var errors = new List<string>();
            var warnings = new List<string>();

            return Validated("stats", "stat", "Stats", BuildStats(list, errors, warnings), errors, warnings, list.FirstOrDefault());
        }

        /// <summary>Copies the JSON of <paramref name="stats"/> to the clipboard; returns whether it did.</summary>
        public static bool CopyStats(IEnumerable<SteamStat> stats) => ToClipboard(GetStatsJson(stats));

        private static JArray BuildStats(List<SteamStat> stats, List<string> errors, List<string> warnings)
        {
            var items = new JArray();

            AddDuplicateErrors(stats.Select(stat => (stat.ApiName, stat.name)), "stat", errors);

            foreach (var stat in stats)
            {
                if (string.IsNullOrWhiteSpace(stat.ApiName))
                {
                    errors.Add($"Stat '{stat.name}' has no API Name.");

                    continue;
                }

                var values = StatSettings.Read(new SerializedObject(stat));

                errors.AddRange(StatSettings.FindProblems(values).Select(problem => $"Stat '{stat.ApiName}': {problem}"));

                // The page greys out increment only, max change and aggregated for an average rate.
                var isAvgRate = values.Type == SteamStatType.AvgRate;

                items.Add(new JObject
                {
                    ["apiName"] = stat.ApiName,
                    ["type"] = values.Type switch
                    {
                        SteamStatType.Int => "INT",
                        SteamStatType.Float => "FLOAT",
                        _ => "AVGRATE"
                    },
                    ["displayName"] = stat.DisplayName ?? string.Empty,
                    ["permission"] = stat.Permission.ToString(),
                    ["aggregated"] = !isAvgRate && stat.Aggregated,
                    ["incrementOnly"] = !isAvgRate && values.IncrementOnly,
                    ["default"] = Number(values.Default, values.IsFloat),
                    ["min"] = Number(values.Min, values.IsFloat),
                    ["max"] = Number(values.Max, values.IsFloat),
                    ["maxChange"] = isAvgRate ? JValue.CreateNull() : Number(values.MaxChange, values.IsFloat),
                    ["windowSize"] = values.HasWindowSize ? Number(values.WindowSize, true) : JValue.CreateNull()
                });
            }

            return items;
        }

        #endregion

        #region Achievements

        /// <summary>
        /// The JSON of <paramref name="achievements"/> as a preview shows it, problems or not. The icon
        /// files are left out, named by their size instead, since their base64 would fill the preview.
        /// </summary>
        public static string PreviewAchievements(IEnumerable<SteamAchievement> achievements) =>
            Envelope("achievements", BuildAchievements(Distinct(achievements), new List<string>(), new List<string>(), false));

        /// <summary>
        /// The JSON of <paramref name="achievements"/> for the Achievements page, icons included, or null
        /// after showing why an achievement cannot go to Steam as it is.
        /// </summary>
        public static string GetAchievementsJson(IEnumerable<SteamAchievement> achievements)
        {
            var list = Distinct(achievements);
            var errors = new List<string>();
            var warnings = new List<string>();

            return Validated("achievements", "achievement", "Achievements",
                BuildAchievements(list, errors, warnings, true), errors, warnings, list.FirstOrDefault());
        }

        /// <summary>Copies the JSON of <paramref name="achievements"/> to the clipboard; returns whether it did.</summary>
        public static bool CopyAchievements(IEnumerable<SteamAchievement> achievements) =>
            ToClipboard(GetAchievementsJson(achievements));

        private static JArray BuildAchievements(
            List<SteamAchievement> achievements, List<string> errors, List<string> warnings, bool iconData)
        {
            var items = new JArray();

            AddDuplicateErrors(achievements.Select(achievement => (achievement.ApiName, achievement.name)), "achievement", errors);

            foreach (var achievement in achievements)
            {
                if (string.IsNullOrWhiteSpace(achievement.ApiName))
                {
                    errors.Add($"Achievement '{achievement.name}' has no API Name.");

                    continue;
                }

                var label = $"Achievement '{achievement.ApiName}'";
                var values = AchievementSettings.Read(new SerializedObject(achievement));

                errors.AddRange(AchievementSettings.FindProblems(values).Select(problem => $"{label}: {problem}"));

                if (string.IsNullOrWhiteSpace(achievement.EnglishDisplayName))
                    warnings.Add($"{label} has no English Display Name.");

                var progressStat = achievement.ProgressStat;
                var isFloat = progressStat && progressStat.StatType != SteamStatType.Int;

                items.Add(new JObject
                {
                    ["apiName"] = achievement.ApiName,
                    ["displayName"] = achievement.EnglishDisplayName ?? string.Empty,
                    ["description"] = achievement.EnglishDescription ?? string.Empty,
                    ["permission"] = achievement.Permission.ToString(),
                    ["hidden"] = achievement.Hidden,
                    ["progress"] = progressStat
                        ? new JObject
                        {
                            ["stat"] = progressStat.ApiName,
                            ["min"] = Number(achievement.ProgressMin, isFloat),
                            ["max"] = Number(achievement.ProgressMax, isFloat)
                        }
                        : JValue.CreateNull(),
                    ["icon"] = Icon(achievement.Icon, $"{label}: Icon", errors, warnings, iconData),
                    ["lockedIcon"] = Icon(achievement.LockedIcon, $"{label}: Locked Icon", errors, warnings, iconData)
                });
            }

            return items;
        }

        /// <summary>
        /// The file behind <paramref name="sprite"/>, sent whole, since that is what the partner site takes.
        /// Null without a sprite, which the extension reads as "leave the icon on Steam as it is". Without
        /// <paramref name="data"/> the file is only described, for a preview.
        /// </summary>
        private static JToken Icon(Sprite sprite, string label, List<string> errors, List<string> warnings, bool data)
        {
            if (!sprite)
            {
                warnings.Add($"{label} is not set, so the one on Steam stays as it is.");

                return JValue.CreateNull();
            }

            var path = AssetDatabase.GetAssetPath(sprite);
            var extension = Path.GetExtension(path).ToLowerInvariant();

            if (extension is not (".png" or ".jpg" or ".jpeg"))
            {
                errors.Add($"{label} comes from {path}, and the partner site takes only PNG or JPG files.");

                return JValue.CreateNull();
            }

            var size = (int)new FileInfo(path).Length;

            if (size > MaxIconBytes)
                errors.Add($"{label} is {FormatSize(size)}, and the partner site takes at most {FormatSize(MaxIconBytes)}.");

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.GetSourceTextureWidthAndHeight(out var width, out var height);

                if (width != height)
                    warnings.Add($"{label} is {width}x{height}; Steam shows icons square.");
                else if (width < MinIconSize)
                    warnings.Add($"{label} is {width}x{height}; Steam takes {MinIconSize}x{MinIconSize} at the least and recommends 256x256.");
            }

            if (sprite.texture && sprite.rect.size != new Vector2(sprite.texture.width, sprite.texture.height))
                warnings.Add($"{label} is a part of {path}, and the whole file is what gets sent.");

            return new JObject
            {
                ["fileName"] = Path.GetFileName(path),
                ["data"] = data ? Convert.ToBase64String(File.ReadAllBytes(path)) : $"<{FormatSize(size)}, as base64 in the copy>"
            };
        }

        #endregion

        #region Shared

        private static List<T> Distinct<T>(IEnumerable<T> assets) where T : Object =>
            assets.Where(asset => asset).Distinct().ToList();

        private static void AddDuplicateErrors(IEnumerable<(string apiName, string assetName)> entries, string noun, List<string> errors)
        {
            foreach (var group in entries.Where(entry => !string.IsNullOrWhiteSpace(entry.apiName)).GroupBy(entry => entry.apiName))
            {
                if (group.Count() > 1)
                    errors.Add($"The {noun} API Name '{group.Key}' is used by {string.Join(", ", group.Select(entry => $"'{entry.assetName}'"))}.");
            }
        }

        private static AppId_t GetAppId()
        {
            var settings = Runtime.Core.SteamSettings.Instance;

            return settings ? settings.AppId : AppId_t.Invalid;
        }

        private static string Envelope(string kind, JArray items) =>
            new JObject
            {
                ["format"] = Format,
                ["kind"] = kind,
                ["appId"] = GetAppId().m_AppId,
                ["items"] = items
            }.ToString(Formatting.Indented);

        /// <summary>
        /// The JSON of <paramref name="items"/>, or null after a dialog listing <paramref name="errors"/>.
        /// Warnings go to the Console either way.
        /// </summary>
        private static string Validated(
            string kind, string noun, string page, JArray items, List<string> errors, List<string> warnings, Object context)
        {
            var appId = GetAppId();

            if (appId == AppId_t.Invalid)
                errors.Insert(0, "Choose the App ID in Window/Steam Toys/Steam Settings. The web extension checks it against the page it pastes into.");

            if (items.Count == 0 && errors.Count == 0)
                errors.Add($"There is no {noun} to copy.");

            foreach (var warning in warnings)
                Debug.LogWarning($"[SteamToys] {warning}", context);

            if (errors.Count > 0)
            {
                EditorUtility.DisplayDialog($"Copy {page} JSON", $"Nothing was copied.\n\n{string.Join("\n", errors)}", "OK");

                return null;
            }

            var json = Envelope(kind, items);

            Debug.Log($"[SteamToys] Copied {items.Count} {noun}{(items.Count == 1 ? string.Empty : "s")} of app {appId} " +
                      $"({FormatSize(json.Length)}). Paste them into the Steam Toys panel on the {page} page of Steamworks." +
                      (warnings.Count > 0 ? $" {warnings.Count} warning{(warnings.Count == 1 ? string.Empty : "s")} above." : string.Empty),
                context);

            return json;
        }

        private static bool ToClipboard(string json)
        {
            if (json == null)
                return false;

            EditorGUIUtility.systemCopyBuffer = json;

            return true;
        }

        /// <summary>
        /// A number in the type of its stat: an int stat holds whole numbers, and a float one is written
        /// at float precision, so 0.1 goes out as 0.1 rather than 0.100000001490116.
        /// </summary>
        private static JToken Number(double? value, bool isFloat) =>
            value is not { } number ? JValue.CreateNull()
            : isFloat ? new JValue((float)number)
            : new JValue((long)Math.Round(number));

        private static string FormatSize(int bytes) =>
            bytes >= 1000000 ? $"{(bytes / 1000000d).ToString("0.0", CultureInfo.InvariantCulture)} MB"
            : bytes >= 1000 ? $"{(bytes / 1000d).ToString("0", CultureInfo.InvariantCulture)} KB"
            : $"{bytes} B";

        #endregion
    }
}
