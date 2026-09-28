using System.Collections.Generic;

namespace SteamToys.Editor.Core
{
    public static partial class SteamAppCache
    {
        /// <summary>
        /// Reads the achievements of one entry of the stat schema. The client keeps them in bits
        /// of an entry of their own, one section per achievement:
        /// <c>name</c>, <c>display</c> with <c>name</c>, <c>desc</c>, <c>hidden</c>, <c>icon</c>
        /// and <c>icon_gray</c>, and for an achievement with a progress stat <c>progress</c> with
        /// <c>min_val</c>, <c>max_val</c> and the stat in <c>value/operand1</c>.
        /// </summary>
        private static void ReadAchievements(Dictionary<string, object> bits, Dictionary<string, AchievementDefinition> achievements)
        {
            foreach (var bit in bits.Values)
            {
                if (bit is not Dictionary<string, object> achievement)
                    continue;

                var apiName = GetString(achievement, "name");

                if (string.IsNullOrEmpty(apiName))
                    continue;

                var display = achievement.TryGetValue("display", out var displaySection)
                    ? displaySection as Dictionary<string, object>
                    : null;

                string progressStat = null;
                double? progressMin = null, progressMax = null;

                if (achievement.TryGetValue("progress", out var progressSection) && progressSection is Dictionary<string, object> progress)
                {
                    if (progress.TryGetValue("value", out var valueSection) && valueSection is Dictionary<string, object> value)
                        progressStat = GetString(value, "operand1");

                    progressMin = GetNumber(progress, "min_val");
                    progressMax = GetNumber(progress, "max_val");
                }

                achievements[apiName] = new AchievementDefinition(
                    apiName,
                    display == null ? null : GetEnglish(display, "name"),
                    display == null ? null : GetEnglish(display, "desc"),
                    display != null && GetNumber(display, "hidden") is { } hidden && hidden != 0,
                    string.IsNullOrEmpty(progressStat) ? null : progressStat,
                    progressMin,
                    progressMax,
                    display == null ? null : GetString(display, "icon"),
                    display == null ? null : GetString(display, "icon_gray"));
            }
        }

        /// <summary>The English text of a localized entry, or the entry itself when it is plain text.</summary>
        private static string GetEnglish(Dictionary<string, object> section, string key) =>
            section.TryGetValue(key, out var value) && value is Dictionary<string, object> languages
                ? GetString(languages, "english")
                : GetString(section, key);
    }

    /// <summary>
    /// One achievement as configured on the partner site. Texts are the English ones; the game
    /// reads the language of the user through the Steam API instead.
    /// </summary>
    public readonly struct AchievementDefinition
    {
        public string ApiName { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public bool Hidden { get; }

        /// <summary>The API Name of the progress stat, or null for an achievement without one.</summary>
        public string ProgressStat { get; }

        public double? ProgressMin { get; }
        public double? ProgressMax { get; }

        /// <summary>The file name of the icon shown once achieved, e.g. <c>e9e4…c3.jpg</c>.</summary>
        public string Icon { get; }

        /// <summary>The file name of the icon shown while locked.</summary>
        public string IconGray { get; }

        internal AchievementDefinition(
            string apiName, string displayName, string description, bool hidden, string progressStat,
            double? progressMin, double? progressMax, string icon, string iconGray)
        {
            ApiName = apiName;
            DisplayName = displayName;
            Description = description;
            Hidden = hidden;
            ProgressStat = progressStat;
            ProgressMin = progressMin;
            ProgressMax = progressMax;
            Icon = icon;
            IconGray = iconGray;
        }
    }
}
