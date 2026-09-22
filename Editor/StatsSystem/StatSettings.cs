using System;
using System.Collections.Generic;
using System.Globalization;
using SteamToys.Editor.Core;
using SteamToys.Runtime.StatsSystem;
using Steamworks;
using UnityEditor;
using UnityEngine.UIElements;

namespace SteamToys.Editor.StatsSystem
{
    /// <summary>The settings a stat mirrors from the partner site, in the order the page lists them.</summary>
    internal enum StatSetting
    {
        Type,
        DefaultValue,
        MinValue,
        MaxValue,
        MaxChange,
        IncrementOnly,
        WindowSize
    }

    /// <summary>
    /// The settings of one stat, read either from a stat asset or from the stat on Steam, so that
    /// both sides compare the same way. A null number is a setting left unset.
    /// </summary>
    internal readonly struct StatValues
    {
        public readonly SteamStatType Type;

        /// <summary>Int stats hold ints and the other two floats.</summary>
        public readonly bool IsFloat;

        public readonly double Default;
        public readonly double? Min;
        public readonly double? Max;
        public readonly double? MaxChange;
        public readonly bool IncrementOnly;

        /// <summary>Only an average rate stat has a window.</summary>
        public readonly bool HasWindowSize;

        public readonly double? WindowSize;

        public StatValues(
            SteamStatType type, bool isFloat, double defaultValue, double? min, double? max, double? maxChange,
            bool incrementOnly, bool hasWindowSize, double? windowSize)
        {
            Type = type;
            IsFloat = isFloat;
            Default = defaultValue;
            Min = min;
            Max = max;
            MaxChange = maxChange;
            IncrementOnly = incrementOnly;
            HasWindowSize = hasWindowSize;
            WindowSize = windowSize;
        }
    }

    /// <summary>
    /// One setting of a stat next to the same setting on Steam, both as text. A side that is not
    /// there at all, such as a stat Steam does not have, is null and counts as matching, since there
    /// is nothing to differ from.
    /// </summary>
    internal readonly struct SettingComparison
    {
        public readonly StatSetting Setting;
        public readonly string Asset;
        public readonly string Steam;
        public readonly bool Matches;

        public SettingComparison(StatSetting setting, string asset, string steam, bool matches)
        {
            Setting = setting;
            Asset = asset;
            Steam = steam;
            Matches = matches;
        }
    }

    /// <summary>
    /// The stat schema of the App ID in the Steam Settings, with a message to show next to it: where it
    /// came from when there is one, and why not when there is none.
    /// </summary>
    internal readonly struct SchemaLookup
    {
        public readonly AppId_t AppId;
        public readonly StatSchema Schema;
        public readonly HelpBoxMessageType MessageType;
        public readonly string Message;

        public SchemaLookup(AppId_t appId, StatSchema schema, HelpBoxMessageType messageType, string message)
        {
            AppId = appId;
            Schema = schema;
            MessageType = messageType;
            Message = message;
        }
    }

    /// <summary>
    /// Reads, compares and copies the settings a stat mirrors from the partner site. Shared by the stat
    /// inspector and the stats DB, so that both judge a stat the same way.
    /// <para>
    /// Assets are read through their serialized fields, which reads every value type the same way and
    /// gives writes undo.
    /// </para>
    /// </summary>
    internal static class StatSettings
    {
        // The serialized fields of SteamStat, SteamStat<TValue> and AvgRateStat.
        internal const string ApiNameField = "_apiName";
        private const string DefaultValueField = "_defaultValue";
        private const string MinValueField = "_minValue";
        private const string MaxValueField = "_maxValue";
        private const string MaxChangeField = "_maxChange";
        private const string IncrementOnlyField = "_incrementOnly";
        internal const string WindowSizeField = "_windowSize";

        public static string GetLabel(StatSetting setting) => ObjectNames.NicifyVariableName(setting.ToString());

        /// <summary>The asset a stat of <paramref name="type"/> takes, as the Create menu names it.</summary>
        public static string GetAssetName(SteamStatType type) => type switch
        {
            SteamStatType.Int => "Int Stat",
            SteamStatType.Float => "Float Stat",
            _ => "Avg Rate Stat"
        };

        /// <summary>The class that holds a stat of <paramref name="type"/>.</summary>
        public static Type GetStatClass(SteamStatType type) => type switch
        {
            SteamStatType.Int => typeof(IntStat),
            SteamStatType.Float => typeof(FloatStat),
            _ => typeof(AvgRateStat)
        };

        /// <summary>
        /// Looks up the stat schema of the App ID in the Steam Settings. <paramref name="subject"/> is
        /// what the messages say gets compared, e.g. "this stat".
        /// </summary>
        public static SchemaLookup FindSchema(string subject)
        {
            var settings = Runtime.Core.SteamSettings.Instance;
            var appId = settings ? settings.AppId : AppId_t.Invalid;

            if (appId == AppId_t.Invalid)
                return new SchemaLookup(appId, null, HelpBoxMessageType.Info,
                    $"Choose the App ID in Window/Steam Toys/Steam Settings to compare {subject} with Steam.");

            if (SteamAppCache.SteamPath == null)
                return new SchemaLookup(appId, null, HelpBoxMessageType.Warning,
                    $"Steam is not installed on this machine, so there is nothing to compare {subject} with.");

            if (!SteamAppCache.TryGetStatSchema(appId, out var schema))
                return new SchemaLookup(appId, null, HelpBoxMessageType.Info,
                    $"No stat schema of app {appId} could be read on this machine. The Steam client downloads it " +
                    "when the game connects to Steam, e.g. through Window/Steam Toys/Connect To Steam.");

            return new SchemaLookup(appId, schema, HelpBoxMessageType.Info,
                $"Schema version {schema.Version} of app {appId}, downloaded " +
                $"{schema.DownloadedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}.");
        }

        public static StatValues Read(SerializedObject stat)
        {
            var defaultValue = stat.FindProperty(DefaultValueField);
            var windowSize = stat.FindProperty(WindowSizeField);

            return new StatValues(
                ((SteamStat)stat.targetObject).StatType,
                defaultValue.propertyType == SerializedPropertyType.Float,
                ReadNumber(defaultValue),
                ReadOptional(stat.FindProperty(MinValueField)),
                ReadOptional(stat.FindProperty(MaxValueField)),
                ReadOptional(stat.FindProperty(MaxChangeField)),
                stat.FindProperty(IncrementOnlyField).boolValue,
                windowSize != null,
                windowSize?.floatValue);
        }

        public static StatValues Read(StatDefinition steam) =>
            new(steam.Type, steam.Type != SteamStatType.Int, steam.Default ?? 0, steam.Min, steam.Max, steam.MaxChange,
                steam.IncrementOnly, steam.Type == SteamStatType.AvgRate, steam.WindowSize);

        /// <summary>
        /// Every setting of the asset next to the one on Steam, in the order of the partner site page.
        /// Either side may be missing, but not both.
        /// <para>
        /// Numbers are compared as written, ticked or not: a ticked max change of 0 differs from none
        /// on Steam, even though the runtime treats both as no limit. Both sides are formatted the way
        /// the asset holds its numbers, at float precision for a float asset, so that e.g. 0.1 on Steam
        /// matches 0.1 in the asset.
        /// </para>
        /// </summary>
        public static List<SettingComparison> Compare(StatValues? asset, StatValues? steam)
        {
            var reference = asset ?? steam ?? throw new ArgumentException("There is neither an asset nor a stat on Steam to compare.");
            var isFloat = reference.IsFloat;

            var comparisons = new List<SettingComparison>
            {
                Text(StatSetting.Type, values => values.Type.ToString()),
                Number(StatSetting.DefaultValue, values => values.Default),
                Number(StatSetting.MinValue, values => values.Min),
                Number(StatSetting.MaxValue, values => values.Max),
                Number(StatSetting.MaxChange, values => values.MaxChange),
                Text(StatSetting.IncrementOnly, values => values.IncrementOnly.ToString())
            };

            // The asset decides when there is one: a window on Steam means nothing to a class without one.
            if (reference.HasWindowSize)
                comparisons.Add(Number(StatSetting.WindowSize, values => values.WindowSize));

            return comparisons;

            SettingComparison Text(StatSetting setting, Func<StatValues, string> read) =>
                new(setting,
                    asset is { } a ? read(a) : null,
                    steam is { } s ? read(s) : null,
                    asset is not { } x || steam is not { } y || read(x) == read(y));

            SettingComparison Number(StatSetting setting, Func<StatValues, double?> read) =>
                new(setting,
                    asset is { } a ? Format(read(a), isFloat) : null,
                    steam is { } s ? Format(read(s), isFloat) : null,
                    asset is not { } x || steam is not { } y || AreEqual(read(x), read(y), isFloat));
        }

        /// <summary>
        /// Copies the settings Steam has into the asset, with undo. The type is left alone: it is the
        /// class of the asset, which no value copied into it can change.
        /// </summary>
        public static void CopyFrom(SerializedObject stat, StatDefinition steam)
        {
            stat.Update();

            WriteNumber(stat.FindProperty(DefaultValueField), steam.Default ?? 0);
            WriteOptional(stat.FindProperty(MinValueField), steam.Min);
            WriteOptional(stat.FindProperty(MaxValueField), steam.Max);
            WriteOptional(stat.FindProperty(MaxChangeField), steam.MaxChange);
            stat.FindProperty(IncrementOnlyField).boolValue = steam.IncrementOnly;

            if (stat.FindProperty(WindowSizeField) is { } windowSize && steam.WindowSize is { } window)
                windowSize.floatValue = (float)window;

            stat.ApplyModifiedProperties();
        }

        /// <summary>
        /// What is wrong with the settings of a stat on their own, found without asking Steam.
        /// </summary>
        public static IEnumerable<string> FindProblems(StatValues values)
        {
            if (values.Min is { } min && values.Max is { } max && min > max)
                yield return "Min Value is above Max Value.";

            if ((values.Min is { } lowest && values.Default < lowest) || (values.Max is { } highest && values.Default > highest))
                yield return "Default Value lies outside Min Value and Max Value.";

            if (values.MaxChange is { } maxChange && maxChange <= 0)
                yield return "Max Change is ticked but not above 0, so it limits nothing.";

            if (values.HasWindowSize && !(values.WindowSize > 0))
                yield return "Window Size is not above 0.";
        }

        public static string Format(double? value, bool isFloat) =>
            value is not { } number ? "not set"
            : isFloat ? ((float)number).ToString(CultureInfo.InvariantCulture)
            : number.ToString(CultureInfo.InvariantCulture);

        private static bool AreEqual(double? a, double? b, bool isFloat) =>
            a.HasValue == b.HasValue &&
            (!a.HasValue || (isFloat ? (float)a.Value == (float)b.Value : a.Value == b.Value));

        private static double ReadNumber(SerializedProperty property) =>
            property.propertyType == SerializedPropertyType.Float ? (double)property.floatValue : property.intValue;

        // Reads a HasValue<T>, whose value only counts while hasValue is ticked.
        private static double? ReadOptional(SerializedProperty property) =>
            property.FindPropertyRelative("hasValue").boolValue
                ? ReadNumber(property.FindPropertyRelative("value"))
                : null;

        private static void WriteNumber(SerializedProperty property, double value)
        {
            if (property.propertyType == SerializedPropertyType.Float)
                property.floatValue = (float)value;
            else
                property.intValue = (int)Math.Clamp(Math.Round(value), int.MinValue, int.MaxValue);
        }

        // Unticks hasValue for a setting Steam leaves unset, and keeps the value in the asset as it was.
        private static void WriteOptional(SerializedProperty property, double? value)
        {
            property.FindPropertyRelative("hasValue").boolValue = value.HasValue;

            if (value.HasValue)
                WriteNumber(property.FindPropertyRelative("value"), value.Value);
        }
    }
}
