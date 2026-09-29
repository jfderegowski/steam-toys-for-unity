using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SteamToys.Editor.Core;
using SteamToys.Runtime.AchievementsSystem;
using SteamToys.Runtime.StatsSystem;
using UnityEditor;

namespace SteamToys.Editor.AchievementsSystem
{
    /// <summary>The settings an achievement mirrors from the partner site, in the order the page lists them.</summary>
    internal enum AchievementSetting
    {
        Hidden,
        ProgressStat,
        ProgressMin,
        ProgressMax,
        Icon,
        LockedIcon
    }

    /// <summary>
    /// The settings of one achievement, read either from an achievement asset or from the achievement
    /// on Steam, so that both sides compare the same way. Without a progress stat the progress range
    /// is null, since it means nothing then.
    /// </summary>
    internal readonly struct AchievementValues
    {
        public readonly bool Hidden;

        /// <summary>The API Name of the progress stat, or null when there is none.</summary>
        public readonly string ProgressStat;

        public readonly double? ProgressMin;
        public readonly double? ProgressMax;

        /// <summary>
        /// The file name Steam gave each icon, or null when there is none: on the asset side, when no
        /// icon has been downloaded.
        /// </summary>
        public readonly string Icon;

        public readonly string LockedIcon;

        public AchievementValues(
            bool hidden, string progressStat, double? progressMin, double? progressMax, string icon, string lockedIcon)
        {
            Hidden = hidden;
            ProgressStat = progressStat;
            ProgressMin = progressMin;
            ProgressMax = progressMax;
            Icon = icon;
            LockedIcon = lockedIcon;
        }
    }

    /// <summary>
    /// One setting of an achievement next to the same setting on Steam, both as text. A side that is
    /// not there at all is null and counts as matching, like a <c>SettingComparison</c> of a stat.
    /// </summary>
    internal readonly struct AchievementComparison
    {
        public readonly AchievementSetting Setting;
        public readonly string Asset;
        public readonly string Steam;
        public readonly bool Matches;

        public AchievementComparison(AchievementSetting setting, string asset, string steam, bool matches)
        {
            Setting = setting;
            Asset = asset;
            Steam = steam;
            Matches = matches;
        }
    }

    /// <summary>
    /// Reads, compares and copies the settings an achievement mirrors from the partner site. Shared by
    /// the achievement inspector and the achievements DB, so that both judge an achievement the same
    /// way. Assets are read through their serialized fields, which gives writes undo.
    /// </summary>
    internal static class AchievementSettings
    {
        internal const string EditOnSteamUrl = "https://partner.steamgames.com/apps/achievements/{0}";

        // The serialized fields of SteamAchievement.
        internal const string ApiNameField = "_apiName";
        private const string HiddenField = "_hidden";
        private const string ProgressStatField = "_progressStat";
        private const string ProgressMinField = "_progressMin";
        private const string ProgressMaxField = "_progressMax";
        internal const string IconField = "_icon";
        internal const string IconHashField = "_iconHash";
        internal const string LockedIconField = "_lockedIcon";
        internal const string LockedIconHashField = "_lockedIconHash";

        public static string GetLabel(AchievementSetting setting) => ObjectNames.NicifyVariableName(setting.ToString());

        public static AchievementValues Read(SerializedObject achievement)
        {
            var stat = achievement.FindProperty(ProgressStatField).objectReferenceValue as SteamStat;
            var hidden = achievement.FindProperty(HiddenField).boolValue;
            var icon = ReadIconHash(achievement, IconField, IconHashField);
            var lockedIcon = ReadIconHash(achievement, LockedIconField, LockedIconHashField);

            if (!stat)
                return new AchievementValues(hidden, null, null, null, icon, lockedIcon);

            return new AchievementValues(
                hidden,
                stat.ApiName ?? string.Empty,
                achievement.FindProperty(ProgressMinField).doubleValue,
                achievement.FindProperty(ProgressMaxField).doubleValue,
                icon,
                lockedIcon);
        }

        public static AchievementValues Read(AchievementDefinition steam) =>
            steam.ProgressStat == null
                ? new AchievementValues(steam.Hidden, null, null, null, NullIfEmpty(steam.Icon), NullIfEmpty(steam.IconGray))
                : new AchievementValues(steam.Hidden, steam.ProgressStat, steam.ProgressMin ?? 0, steam.ProgressMax ?? 0,
                    NullIfEmpty(steam.Icon), NullIfEmpty(steam.IconGray));

        /// <summary>
        /// The file name of the icon the asset downloaded, or null without an icon. An icon the asset
        /// holds that was not downloaded, e.g. one dragged in by hand, has no file name and so reads as
        /// differing from Steam.
        /// </summary>
        private static string ReadIconHash(SerializedObject achievement, string textureField, string hashField) =>
            achievement.FindProperty(textureField).objectReferenceValue
                ? NullIfEmpty(achievement.FindProperty(hashField).stringValue) ?? NotFromSteam
                : null;

        private const string NotFromSteam = "not from Steam";

        private static string NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;

        /// <summary>
        /// Every setting of the asset next to the one on Steam, in the order of the partner site page.
        /// Either side may be missing, but not both. The progress range is compared as
        /// <see cref="Format"/> writes it.
        /// </summary>
        public static List<AchievementComparison> Compare(AchievementValues? asset, AchievementValues? steam)
        {
            if (asset == null && steam == null)
                throw new ArgumentException("There is neither an asset nor an achievement on Steam to compare.");

            return new List<AchievementComparison>
            {
                Text(AchievementSetting.Hidden, values => values.Hidden.ToString()),
                Text(AchievementSetting.ProgressStat, values => values.ProgressStat ?? "none"),
                Number(AchievementSetting.ProgressMin, values => values.ProgressMin),
                Number(AchievementSetting.ProgressMax, values => values.ProgressMax),
                Text(AchievementSetting.Icon, values => FormatIcon(values.Icon)),
                Text(AchievementSetting.LockedIcon, values => FormatIcon(values.LockedIcon))
            };

            AchievementComparison Text(AchievementSetting setting, Func<AchievementValues, string> read) =>
                new(setting,
                    asset is { } a ? read(a) : null,
                    steam is { } s ? read(s) : null,
                    asset is not { } x || steam is not { } y || read(x) == read(y));

            AchievementComparison Number(AchievementSetting setting, Func<AchievementValues, double?> read) =>
                new(setting,
                    asset is { } a ? Format(read(a)) : null,
                    steam is { } s ? Format(read(s)) : null,
                    asset is not { } x || steam is not { } y || Format(read(x)) == Format(read(y)));
        }

        /// <summary>
        /// Copies the settings Steam has into the asset, with undo. The progress stat is looked up by
        /// its API Name among the stat assets of the project; returns false when Steam has a progress
        /// stat no asset stands for, which leaves the field empty.
        /// </summary>
        public static bool CopyFrom(SerializedObject achievement, AchievementDefinition steam)
        {
            achievement.Update();

            achievement.FindProperty(HiddenField).boolValue = steam.Hidden;

            var stat = steam.ProgressStat == null ? null : FindStat(steam.ProgressStat);

            achievement.FindProperty(ProgressStatField).objectReferenceValue = stat;

            // Kept as they were when there is no progress: they mean nothing without a stat.
            if (steam.ProgressStat != null)
            {
                achievement.FindProperty(ProgressMinField).doubleValue = steam.ProgressMin ?? 0;
                achievement.FindProperty(ProgressMaxField).doubleValue = steam.ProgressMax ?? 0;
            }

            achievement.ApplyModifiedProperties();

            return steam.ProgressStat == null || stat;
        }

        /// <summary>
        /// The stat asset with the given API Name, preferring one of the stats DB over a stat asset of
        /// its own. Null when the project has none.
        /// </summary>
        public static SteamStat FindStat(string apiName)
        {
            SteamStat found = null;

            foreach (var type in TypeCache.GetTypesDerivedFrom<SteamStat>())
            {
                if (type.IsAbstract || type.IsGenericTypeDefinition)
                    continue;

                foreach (var path in AssetDatabase.FindAssets($"t:{type.Name}").Select(AssetDatabase.GUIDToAssetPath).Distinct())
                {
                    foreach (var stat in AssetDatabase.LoadAllAssetsAtPath(path).OfType<SteamStat>())
                    {
                        if (stat.ApiName != apiName)
                            continue;

                        if (AssetDatabase.LoadMainAssetAtPath(path) is SteamStatsDB)
                            return stat;

                        found ??= stat;
                    }
                }
            }

            return found;
        }

        /// <summary>What is wrong with the settings of an achievement on their own, found without asking Steam.</summary>
        public static IEnumerable<string> FindProblems(AchievementValues values)
        {
            if (values.ProgressStat == null)
                yield break;

            if (string.IsNullOrWhiteSpace(values.ProgressStat))
                yield return "The progress stat has no API Name, so Steam cannot tie it to the achievement.";

            if (!(values.ProgressMax > values.ProgressMin))
                yield return "Progress Max is not above Progress Min, so the progress bar has no range.";
        }

        /// <summary>
        /// A progress bound as text, which is also what the two sides are compared by. A whole number is
        /// written out in full, since an int stat keeps its range exactly. A fraction can only come from
        /// a float stat, which Steam keeps at float precision, so it is written at that precision: 0.1
        /// typed into the asset then matches the 0.1 Steam read back as 0.100000001.
        /// </summary>
        /// <summary>
        /// An icon as its file name without the extension, cut to a length that tells icons apart at a
        /// glance and still fits a column: a 40 character SHA-1 would not.
        /// </summary>
        private static string FormatIcon(string fileName)
        {
            if (fileName is null or NotFromSteam)
                return fileName ?? "none";

            var hash = System.IO.Path.GetFileNameWithoutExtension(fileName);

            return hash.Length > 10 ? hash.Substring(0, 10) : hash;
        }

        public static string Format(double? value) =>
            value is not { } number ? "none"
            : number == Math.Floor(number) ? number.ToString("0", CultureInfo.InvariantCulture)
            : ((float)number).ToString(CultureInfo.InvariantCulture);
    }
}
