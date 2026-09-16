using System;
using System.Globalization;
using System.Text;
using fefek5.Toys.Runtime;
using UnityEngine;

namespace SteamToys.Runtime.StatsSystem
{
    /// <summary>
    /// A single Steam user stat. One asset per stat: the asset mirrors one row of the
    /// Stats Configuration page on the Steamworks partner site.
    /// <para>
    /// This non generic base is the polymorphic handle, so a field, list or inspector can hold
    /// stats of mixed value types. The typed behaviour lives in <see cref="SteamStat{TValue}"/>
    /// and the Steam calls themselves in the concrete assets, because
    /// <c>SteamUserStats.GetStat</c> and <c>SetStat</c> are overloaded per value type and cannot
    /// be resolved without knowing it.
    /// </para>
    /// <see href="https://partner.steamgames.com/doc/features/achievements/stats_guide"/>
    /// </summary>
    public abstract class SteamStat : ScriptableObject
    {
        #region Properties

        /// <summary>The API Name of the stat, exactly as configured on the partner site.</summary>
        public string ApiName
        {
            get => GetApiName();
            set => SetApiName(value);
        }

        /// <summary>Which kind of stat this asset represents.</summary>
        public abstract SteamStatType StatType { get; }

        #endregion

        #region Inspector Serialized Fields

        [Header("Steam Configuration")]
        [SerializeField, Tooltip("API Name of the stat, copied verbatim from the Stats Configuration page on the Steamworks partner site.")]
        private string _apiName;

        #endregion

        #region Getters and Setters

        public virtual string GetApiName() => _apiName;

        public virtual void SetApiName(string value) => _apiName = value;

        #endregion

        #region Steam

        /// <summary>The type of the value held by this stat, e.g. <c>typeof(int)</c>.</summary>
        public abstract Type GetValueType();

        /// <summary>
        /// Reads the current value from Steam into the local cache.
        /// Returns false when Steam is unreachable or refused the read.
        /// </summary>
        public abstract bool TryPullFromSteam();

        /// <summary>
        /// Writes the pending state of this stat to Steam. The write only becomes permanent once
        /// <see cref="SteamStats.StoreStats"/> succeeds.
        /// </summary>
        public abstract bool TryPushToSteam();

        /// <summary>
        /// Puts the stat back to its configured default. The reset that clears the values held on
        /// the Steam servers is <see cref="SteamStats.ResetAllStats"/> instead.
        /// </summary>
        public abstract void ResetToDefault();

        /// <summary>
        /// The cached value rendered as text, for logging and editor display. Reading it never
        /// touches Steam.
        /// </summary>
        public abstract string GetValueString();

        #endregion

        /// <summary>
        /// True when this stat can talk to Steam. Logs why when it cannot, so a game that never
        /// called <c>SteamAPI.Init</c> gets one clear warning instead of an
        /// <see cref="InvalidOperationException"/> thrown from inside the Steamworks binding,
        /// which is what the raw <c>SteamUserStats</c> calls do when the API is down.
        /// </summary>
        protected bool CanReachSteam()
        {
            if (string.IsNullOrWhiteSpace(_apiName))
            {
                Debug.LogError($"Steam stat '{name}' has no API Name set, so it cannot be read or written.", this);

                return false;
            }

            return SteamStats.EnsureAvailable(this);
        }

        #region Object Overrides

        /// <summary>
        /// The stat described in one line for logs: asset name, API Name, kind and cached value.
        /// Never touches Steam.
        /// </summary>
        public override string ToString()
        {
            var apiName = string.IsNullOrWhiteSpace(_apiName) ? "(no API Name)" : _apiName;

            return $"{name} '{apiName}' [{StatType}] = {GetValueString()}";
        }

        #endregion

        #region Debug

        [ContextMenu("Debug Stat")]
        private void DebugStat() => Debug.Log(ToString(), this);

        #endregion
    }

    /// <summary>
    /// The behaviour shared by every stat regardless of value type: the cached value, the mirrored
    /// partner site constraints and the change notification. Concrete assets add the Steam calls
    /// for their own value type.
    /// </summary>
    public abstract class SteamStat<TValue> : SteamStat
        where TValue : struct, IComparable<TValue>, IFormattable
    {
        #region Properties

        /// <summary>
        /// The current value. Reading pulls from Steam once and then serves the local cache;
        /// assigning validates the change against the mirrored constraints and writes it through
        /// to Steam.
        /// </summary>
        public TValue Value
        {
            get => GetValue();
            set => SetValue(value);
        }

        /// <summary>The value the stat uses until Steam has been reached.</summary>
        public TValue DefaultValue
        {
            get => GetDefaultValue();
            set => SetDefaultValue(value);
        }

        public HasValue<TValue> MinValue
        {
            get => GetMinValue();
            set => SetMinValue(value);
        }

        public HasValue<TValue> MaxValue
        {
            get => GetMaxValue();
            set => SetMaxValue(value);
        }

        public HasValue<TValue> MaxChange
        {
            get => GetMaxChange();
            set => SetMaxChange(value);
        }

        public bool IncrementOnly
        {
            get => GetIncrementOnly();
            set => SetIncrementOnly(value);
        }

        #endregion

        /// <summary>
        /// Raised after the cached value changed, with the old and then the new value.
        /// </summary>
        public event Action<TValue, TValue> onValueChanged;

        #region Inspector Serialized Fields

        [Header("Value")]
        [SerializeField, Tooltip("Value used until Steam is reached, matching Default Value on the partner site.")]
        private TValue _defaultValue;

        [Header("Constraints (mirror of the partner site)")]
        [SerializeField, Tooltip("Lowest accepted value, matching Min Value on the partner site. Enforced locally only when set.")]
        private HasValue<TValue> _minValue;
        [SerializeField, Tooltip("Highest accepted value, matching Max Value on the partner site. Enforced locally only when set.")]
        private HasValue<TValue> _maxValue;
        [SerializeField, Tooltip("Largest accepted change in a single write, matching Max Change on the partner site. Enforced locally only when set.")]
        private HasValue<TValue> _maxChange;
        [SerializeField, Tooltip("Rejects any write that would lower the value, matching Increment Only on the partner site.")]
        private bool _incrementOnly;

        #endregion

        #region Runtime State

        // Runtime state, deliberately not serialized: a serialized field here would dirty the
        // asset while playing in the editor and bake the last played value into the build.
        [NonSerialized] private TValue _runtimeValue;
        [NonSerialized] private bool _synced;

        /// <summary>The cached value, read without triggering a pull from Steam.</summary>
        protected TValue RuntimeValue => _runtimeValue;

        #endregion

        protected virtual void OnEnable()
        {
            _runtimeValue = _defaultValue;
            _synced = false;
        }

        #region Getters and Setters

        public virtual TValue GetValue()
        {
            // The first read syncs with Steam; after that the cache is authoritative, because
            // every write goes through this class.
            if (!_synced)
                TryPullFromSteam();

            return _runtimeValue;
        }

        public virtual void SetValue(TValue value) => TrySetValue(value);

        /// <summary>
        /// Validates <paramref name="value"/> against the mirrored constraints, applies it locally
        /// and writes it to Steam. Returns false when the change was rejected outright or did not
        /// reach Steam. A rejected change leaves the value untouched, while an unreachable Steam
        /// still updates the local value so an offline session keeps counting.
        /// </summary>
        public virtual bool TrySetValue(TValue value)
        {
            var current = GetValue();

            if (!ValidateChange(current, value, out var corrected))
                return false;

            ApplyValue(corrected);

            return TryPushToSteam();
        }

        public virtual TValue GetDefaultValue() => _defaultValue;

        public virtual void SetDefaultValue(TValue value) => _defaultValue = value;

        public virtual HasValue<TValue> GetMinValue() => _minValue;

        public virtual void SetMinValue(HasValue<TValue> value) => _minValue = value;

        public virtual HasValue<TValue> GetMaxValue() => _maxValue;

        public virtual void SetMaxValue(HasValue<TValue> value) => _maxValue = value;

        public virtual HasValue<TValue> GetMaxChange() => _maxChange;

        public virtual void SetMaxChange(HasValue<TValue> value) => _maxChange = value;

        public virtual bool GetIncrementOnly() => _incrementOnly;

        public virtual void SetIncrementOnly(bool value) => _incrementOnly = value;

        #endregion

        #region Steam

        public sealed override Type GetValueType() => typeof(TValue);

        public override string GetValueString() => _runtimeValue.ToString(null, CultureInfo.InvariantCulture);

        public sealed override bool TryPullFromSteam()
        {
            if (!CanReachSteam())
                return false;

            if (!TryGetFromSteam(out var value))
            {
                Debug.LogWarning($"Steam refused to read stat '{ApiName}'. Check that this API Name exists on the partner site and that it is configured as {StatType}.", this);

                return false;
            }

            ApplyValue(value);

            return true;
        }

        public sealed override bool TryPushToSteam()
        {
            if (!CanReachSteam())
                return false;

            if (TryWriteToSteam())
                return true;

            Debug.LogWarning($"Steam refused to write stat '{ApiName}'. Check that this API Name exists on the partner site and that it is configured as {StatType}.", this);

            return false;
        }

        public override void ResetToDefault()
        {
            // Skips ValidateChange on purpose: an increment only stat would reject its own default.
            ApplyValue(_defaultValue);

            TryPushToSteam();
        }

        /// <summary>
        /// Reads the value from Steam with the <c>GetStat</c> overload that matches
        /// <typeparamref name="TValue"/>.
        /// </summary>
        protected abstract bool TryGetFromSteam(out TValue value);

        /// <summary>
        /// Writes the pending state of this stat to Steam. A counter pushes its value with
        /// <c>SetStat</c>, while an average rate stat feeds its session data to
        /// <c>UpdateAvgRateStat</c>, so each kind decides what a write means.
        /// </summary>
        protected abstract bool TryWriteToSteam();

        /// <summary>
        /// Checks a requested change against the mirrored partner site constraints. Returns false
        /// to reject it outright, or true with <paramref name="corrected"/> clamped to what the
        /// constraints allow.
        /// <para>
        /// Steam reverts values that break a constraint when the stats are stored, and does so
        /// silently, so catching it here keeps the legal part of the change instead of losing all
        /// of it.
        /// </para>
        /// </summary>
        protected abstract bool ValidateChange(TValue current, TValue desired, out TValue corrected);

        #endregion

        /// <summary>
        /// Applies a value to the cache and raises <see cref="onValueChanged"/> when it moved.
        /// Marks the stat as synced, so a later read cannot pull over a local write.
        /// </summary>
        protected void ApplyValue(TValue value)
        {
            var previous = _runtimeValue;

            _runtimeValue = value;
            _synced = true;

            if (previous.CompareTo(value) != 0)
                onValueChanged?.Invoke(previous, value);
        }

        /// <summary>Reports a change that the mirrored constraints turned down.</summary>
        protected void LogRejectedChange(TValue current, TValue desired, string reason) =>
            Debug.LogWarning($"Steam stat '{ApiName}' rejected the change " +
                             $"{current.ToString(null, CultureInfo.InvariantCulture)} -> " +
                             $"{desired.ToString(null, CultureInfo.InvariantCulture)}: {reason}.", this);

        #region Object Overrides

        /// <summary>
        /// Extends the base line with the default value and the mirrored constraints that are set,
        /// e.g. <c>Wins 'NumWins' [Int] = 12 (default 0, min 0, max 1000, increment only)</c>.
        /// </summary>
        public override string ToString()
        {
            var builder = new StringBuilder(base.ToString());

            builder.Append(" (default ").Append(Format(_defaultValue));

            if (_minValue.hasValue)
                builder.Append(", min ").Append(Format(_minValue.value));

            if (_maxValue.hasValue)
                builder.Append(", max ").Append(Format(_maxValue.value));

            if (_maxChange.hasValue)
                builder.Append(", max change ").Append(Format(_maxChange.value));

            if (_incrementOnly)
                builder.Append(", increment only");

            // The value shown is the local cache, which is only the default until the first read
            // or write.
            if (!_synced)
                builder.Append(", not synced yet");

            return builder.Append(')').ToString();

            static string Format(TValue value) => value.ToString(null, CultureInfo.InvariantCulture);
        }

        #endregion
    }
}
