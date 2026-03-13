using System;
using System.Globalization;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.Converters;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// Represents a UTC date/time value used by the Steam Inventory Service.
    /// <para>
    /// Steam expects timestamps in the format <c>"YYYYMMDDTHHMMSSz"</c>,
    /// e.g. <c>"20170801T120000Z"</c>.
    /// </para>
    /// <see href="https://partner.steamgames.com/doc/features/inventory/schema"/>
    /// </summary>
    [Serializable]
    [JsonConverter(typeof(SteamDateTimeConverter))]
    public sealed class SteamDateTime : IEquatable<SteamDateTime>, IComparable<SteamDateTime>
    {
        /// <summary>
        /// The Steam-specific date/time format string: <c>yyyyMMddTHHmmssZ</c>.
        /// </summary>
        public const string STEAM_FORMAT = "yyyyMMdd'T'HHmmss'Z'";

        [SerializeField] private long _ticks;
        [SerializeField] private bool _hasValue;

        #region Constructors

        public SteamDateTime()
        {
            _ticks = 0;
            _hasValue = false;
        }

        public SteamDateTime(DateTime utcDateTime)
        {
            _ticks = utcDateTime.ToUniversalTime().Ticks;
            _hasValue = true;
        }

        #endregion

        #region Value Access

        /// <summary>Returns true when a date/time has been assigned.</summary>
        public bool HasValue => _hasValue;

        /// <summary>The UTC <see cref="DateTime"/> value.</summary>
        public DateTime UtcDateTime
        {
            get => _hasValue ? new DateTime(_ticks, DateTimeKind.Utc) : DateTime.MinValue;
            set
            {
                _ticks = value.ToUniversalTime().Ticks;
                _hasValue = true;
            }
        }

        /// <summary>Clears the value so <see cref="HasValue"/> returns false.</summary>
        public void Clear()
        {
            _ticks = 0;
            _hasValue = false;
        }

        #endregion

        #region Steam Format Conversion

        /// <summary>
        /// Converts this instance to the Steam timestamp format: <c>"YYYYMMDDTHHMMSSz"</c>.
        /// Returns an empty string if <see cref="HasValue"/> is false.
        /// </summary>
        public string ToSteamString()
        {
            if (!_hasValue)
                return string.Empty;

            return UtcDateTime.ToString(STEAM_FORMAT, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses a Steam-format timestamp string (e.g. <c>"20170801T120000Z"</c>)
        /// into a <see cref="SteamDateTime"/>. Returns an empty instance if parsing fails.
        /// </summary>
        public static SteamDateTime FromSteamString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return new SteamDateTime();

            if (DateTime.TryParseExact(value.Trim(), STEAM_FORMAT,
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var dt))
            {
                return new SteamDateTime(dt);
            }

            return new SteamDateTime();
        }

        #endregion

        #region Equality & Comparison

        public bool Equals(SteamDateTime other)
        {
            if (other is null) return false;
            if (!_hasValue && !other._hasValue) return true;
            if (_hasValue != other._hasValue) return false;
            return _ticks == other._ticks;
        }

        public override bool Equals(object obj) => Equals(obj as SteamDateTime);

        public override int GetHashCode() => _hasValue ? _ticks.GetHashCode() : 0;

        public int CompareTo(SteamDateTime other)
        {
            if (other is null) return 1;
            if (!_hasValue && !other._hasValue) return 0;
            if (!_hasValue) return -1;
            if (!other._hasValue) return 1;
            return _ticks.CompareTo(other._ticks);
        }

        public static bool operator ==(SteamDateTime a, SteamDateTime b)
        {
            if (a is null) return b is null;
            return a.Equals(b);
        }

        public static bool operator !=(SteamDateTime a, SteamDateTime b) => !(a == b);

        #endregion

        public override string ToString() => _hasValue ? ToSteamString() : "(none)";
    }
}




