using System;
using System.Globalization;
using System.Text;
using SteamToys.Runtime.StatsSystem;
using Steamworks;
using UnityEngine;

namespace SteamToys.Runtime.AchievementsSystem
{
    /// <summary>
    /// A single Steam achievement. One asset per achievement, either of its own or a sub-asset of
    /// <see cref="SteamAchievementsDB"/>: the asset mirrors one row of the Achievements page on the
    /// Steamworks partner site.
    /// <para>
    /// Unlocking is two steps on the Steam side: <c>SetAchievement</c> changes the state held by the
    /// Steam client, and only <c>StoreStats</c> sends it to the servers and shows the notification.
    /// <see cref="Unlock"/> does both unless told otherwise.
    /// </para>
    /// <para>
    /// An achievement with a progress stat also unlocks on its own once the stat reaches the
    /// progress max and the stats are stored, without the game calling <see cref="Unlock"/>. The
    /// asset picks that up from <see cref="SteamAchievementsDB.OnAchievementStored"/>.
    /// </para>
    /// <see href="https://partner.steamgames.com/doc/features/achievements"/>
    /// </summary>
    [CreateAssetMenu(fileName = "New Achievement", menuName = "SteamToys/Achievements/Achievement", order = 0)]
    public class SteamAchievement : ScriptableObject
    {
        #region Properties

        /// <summary>The API Name of the achievement, exactly as configured on the partner site.</summary>
        public string ApiName
        {
            get => GetApiName();
            set => SetApiName(value);
        }

        /// <summary>
        /// Whether the Community page hides the achievement until it is unlocked, matching Hidden on
        /// the partner site. A mirror only: Steam applies it itself.
        /// </summary>
        public bool Hidden
        {
            get => GetHidden();
            set => SetHidden(value);
        }

        /// <summary>
        /// The stat that fills the progress bar of the achievement and unlocks it on reaching
        /// <see cref="ProgressMax"/>, matching Progress Stat on the partner site. Null when the
        /// achievement has none.
        /// </summary>
        public SteamStat ProgressStat
        {
            get => GetProgressStat();
            set => SetProgressStat(value);
        }

        /// <summary>
        /// The stat value the progress bar starts at. A <c>double</c>, like the value of a stat read
        /// through <see cref="SteamStat.GetValueAsDouble"/>: Steam keeps the range in the type of the
        /// stat, and a <c>float</c> would round an int range above 16777216.
        /// </summary>
        public double ProgressMin
        {
            get => GetProgressMin();
            set => SetProgressMin(value);
        }

        /// <summary>The stat value the progress bar ends at, and the achievement unlocks at.</summary>
        public double ProgressMax
        {
            get => GetProgressMax();
            set => SetProgressMax(value);
        }

        /// <summary>True when the achievement has a progress stat.</summary>
        public bool HasProgress => GetProgressStat();

        /// <summary>
        /// Whether the user has the achievement. Reading pulls from Steam once and then serves the
        /// local cache, which every unlock and clear through this asset keeps up to date.
        /// </summary>
        public bool IsAchieved
        {
            get
            {
                if (!_synced)
                    TryPullFromSteam();

                return _achieved;
            }
        }

        /// <summary>
        /// When the achievement was unlocked, in local time. Null while it is locked, and also for
        /// achievements unlocked before Steam started tracking unlock times in December 2009.
        /// </summary>
        public DateTime? UnlockTime => IsAchieved ? _unlockTime : null;

        /// <summary>
        /// False while the cached state is still the locked state the achievement starts with, until
        /// the first read from Steam or the first unlock or clear.
        /// </summary>
        public bool IsSynced => _synced;

        /// <summary>
        /// How far the progress stat has come from <see cref="ProgressMin"/> to
        /// <see cref="ProgressMax"/>, from 0 to 1. Worked out from the stat, so it needs no Steam
        /// session beyond the one the stat itself reads through. Null without a progress stat or
        /// with a range that is empty.
        /// </summary>
        public float? Progress
        {
            get
            {
                var stat = GetProgressStat();
                var min = GetProgressMin();
                var max = GetProgressMax();

                if (!stat || max <= min)
                    return null;

                return Mathf.Clamp01((float)((stat.GetValueAsDouble() - min) / (max - min)));
            }
        }

        #endregion

        /// <summary>Raised after the cached state changed, with the new state.</summary>
        public event Action<bool> onAchievedChanged;

        #region Inspector Serialized Fields

        [Header("Steam Configuration")]
        [SerializeField, Tooltip("API Name of the achievement, copied verbatim from the Achievements page on the Steamworks partner site.")]
        private string _apiName;
        [SerializeField, Tooltip("Hides the achievement on the Community page until it is unlocked, matching Hidden on the partner site. A mirror only.")]
        private bool _hidden;

        [Header("Progress (mirror of the partner site)")]
        [SerializeField, Tooltip("The stat that fills the progress bar and unlocks the achievement on reaching Progress Max, matching Progress Stat on the partner site. Leave empty for an achievement without one.")]
        private SteamStat _progressStat;
        [SerializeField, Tooltip("The stat value the progress bar starts at, matching the min value of the progress stat on the partner site.")]
        private double _progressMin;
        [SerializeField, Tooltip("The stat value the progress bar ends at and the achievement unlocks at, matching the max value of the progress stat on the partner site.")]
        private double _progressMax;

        #endregion

        #region Runtime State

        // Runtime state, deliberately not serialized, for the same reason as the value of a stat:
        // it would dirty the asset while playing and bake the last played state into the build.
        [NonSerialized] private bool _achieved;
        [NonSerialized] private DateTime? _unlockTime;
        [NonSerialized] private bool _synced;

        #endregion

        protected virtual void OnEnable()
        {
            _achieved = false;
            _unlockTime = null;
            _synced = false;

            SteamAchievementsDB.OnAchievementStored += HandleAchievementStored;
        }

        protected virtual void OnDisable()
        {
            SteamAchievementsDB.OnAchievementStored -= HandleAchievementStored;
        }

        #region Getters and Setters

        public virtual string GetApiName() => _apiName;

        public virtual void SetApiName(string value) => _apiName = value;

        public virtual bool GetHidden() => _hidden;

        public virtual void SetHidden(bool value) => _hidden = value;

        public virtual SteamStat GetProgressStat() => _progressStat;

        public virtual void SetProgressStat(SteamStat value) => _progressStat = value;

        public virtual double GetProgressMin() => _progressMin;

        public virtual void SetProgressMin(double value) => _progressMin = value;

        public virtual double GetProgressMax() => _progressMax;

        public virtual void SetProgressMax(double value) => _progressMax = value;

        #endregion

        #region Steam

        /// <summary>
        /// Unlocks the achievement for the user and, when <paramref name="store"/> is true, stores it
        /// on the Steam servers, which is also what shows the notification. Pass false to store it
        /// later with <see cref="SteamStatsDB.StoreStats"/> together with other changes.
        /// <para>
        /// Unlocking an achievement the user already has changes nothing. Without Steam the local
        /// state still turns unlocked and false is returned; call it again once Steam is reachable.
        /// </para>
        /// </summary>
        public virtual bool Unlock(bool store = true)
        {
            if (!IsAchieved)
                ApplyState(true, DateTime.Now);

            if (!CanReachSteam())
                return false;

            if (!SteamUserStats.SetAchievement(GetApiName()))
            {
                LogRefused("unlock");

                return false;
            }

            return !store || SteamStatsDB.StoreStats();
        }

        /// <summary>
        /// Locks the achievement again for the user, meant for testing. Stores the change like
        /// <see cref="Unlock"/> does unless <paramref name="store"/> is false.
        /// </summary>
        public virtual bool Clear(bool store = true)
        {
            ApplyState(false, null);

            if (!CanReachSteam())
                return false;

            if (!SteamUserStats.ClearAchievement(GetApiName()))
            {
                LogRefused("clear");

                return false;
            }

            return !store || SteamStatsDB.StoreStats();
        }

        /// <summary>
        /// Shows the user a notification with the progress of the achievement, read from its
        /// progress stat. Only a notification: the progress itself is the stat, and the achievement
        /// unlocks once the stat reaches <see cref="ProgressMax"/> and the stats are stored.
        /// </summary>
        public virtual bool IndicateProgress()
        {
            var stat = GetProgressStat();

            if (!stat)
            {
                Debug.LogError($"Steam achievement '{GetApiName()}' has no progress stat, so it has no progress to show. Use IndicateProgressOf(current, max) instead.", this);

                return false;
            }

            return IndicateProgressOf(ToProgress(stat.GetValueAsDouble()), ToProgress(GetProgressMax()));

            static uint ToProgress(double value) => (uint)Math.Clamp(Math.Round(value), 0d, uint.MaxValue);
        }

        /// <summary>
        /// Shows the user a notification that <paramref name="current"/> of
        /// <paramref name="max"/> is done. Steam shows nothing, and this returns false, for an
        /// achievement already unlocked or a <paramref name="current"/> that is not below
        /// <paramref name="max"/>, so those are skipped without a warning.
        /// </summary>
        public virtual bool IndicateProgressOf(uint current, uint max)
        {
            if (current >= max || IsAchieved)
                return false;

            if (!CanReachSteam())
                return false;

            if (SteamUserStats.IndicateAchievementProgress(GetApiName(), current, max))
                return true;

            LogRefused("show the progress of");

            return false;
        }

        /// <summary>
        /// Reads the state from Steam into the local cache. Returns false when Steam is unreachable
        /// or refused the read.
        /// </summary>
        public virtual bool TryPullFromSteam()
        {
            if (!CanReachSteam())
                return false;

            if (!SteamUserStats.GetAchievementAndUnlockTime(GetApiName(), out var achieved, out var unlockTime))
            {
                LogRefused("read");

                return false;
            }

            // Zero on an unlocked achievement means it predates unlock times.
            ApplyState(achieved, achieved && unlockTime != 0
                ? DateTimeOffset.FromUnixTimeSeconds(unlockTime).LocalDateTime
                : null);

            return true;
        }

        /// <summary>
        /// The name of the achievement in the language of the user, as the notification shows it.
        /// Null when Steam is unreachable or does not know the achievement.
        /// </summary>
        public virtual string GetDisplayName() => GetDisplayAttribute("name");

        /// <summary>The description of the achievement in the language of the user, or null.</summary>
        public virtual string GetDescription() => GetDisplayAttribute("desc");

        private string GetDisplayAttribute(string key)
        {
            if (!CanReachSteam())
                return null;

            var value = SteamUserStats.GetAchievementDisplayAttribute(GetApiName(), key);

            return string.IsNullOrEmpty(value) ? null : value;
        }

        /// <summary>
        /// True when this achievement can talk to Steam, logging why when it cannot, like
        /// <see cref="SteamStat"/> does.
        /// </summary>
        protected bool CanReachSteam()
        {
            if (string.IsNullOrWhiteSpace(GetApiName()))
            {
                Debug.LogError($"Steam achievement '{name}' has no API Name set, so it cannot be read or written.", this);

                return false;
            }

            return SteamAchievementsDB.EnsureInitialized(this);
        }

        #endregion

        /// <summary>
        /// Applies a state to the cache and raises <see cref="onAchievedChanged"/> when it moved.
        /// Marks the achievement as synced, so a later read cannot pull over a local change.
        /// </summary>
        protected void ApplyState(bool achieved, DateTime? unlockTime)
        {
            var previous = _achieved;

            _achieved = achieved;
            _unlockTime = achieved ? unlockTime : null;
            _synced = true;

            if (previous != achieved)
                onAchievedChanged?.Invoke(achieved);
        }

        // Steam reports an unlock with both progress numbers at zero. That covers the unlocks the
        // game did not ask for, the ones a progress stat triggers, which is why this listens at all.
        private void HandleAchievementStored(string apiName, uint current, uint max)
        {
            if (current == 0 && max == 0 && apiName == GetApiName())
                TryPullFromSteam();
        }

        private void LogRefused(string action) =>
            Debug.LogWarning($"Steam refused to {action} achievement '{GetApiName()}'. Check that this API Name exists on the partner site and that the change is published.", this);

        #region Object Overrides

        /// <summary>
        /// The achievement described in one line for logs, e.g.
        /// <c>Win 'ACH_WIN_ONE_GAME' = unlocked 2026-09-28 14:02 (progress NumWins 0..1)</c>.
        /// Never touches Steam.
        /// </summary>
        public override string ToString()
        {
            var apiName = string.IsNullOrWhiteSpace(_apiName) ? "(no API Name)" : _apiName;
            var builder = new StringBuilder($"{name} '{apiName}' = ");

            builder.Append(_achieved ? "unlocked" : "locked");

            if (_achieved && _unlockTime is { } time)
                builder.Append(' ').Append(time.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));

            var details = new StringBuilder();

            if (_progressStat)
                details.Append("progress ").Append(_progressStat.ApiName).Append(' ')
                    .Append(_progressMin.ToString(CultureInfo.InvariantCulture)).Append("..")
                    .Append(_progressMax.ToString(CultureInfo.InvariantCulture));

            if (_hidden)
                details.Append(details.Length > 0 ? ", " : string.Empty).Append("hidden");

            if (!_synced)
                details.Append(details.Length > 0 ? ", " : string.Empty).Append("not synced yet");

            if (details.Length > 0)
                builder.Append(" (").Append(details).Append(')');

            return builder.ToString();
        }

        #endregion

        #region Debug

        [ContextMenu("Debug Achievement")]
        private void DebugAchievement() => Debug.Log(ToString(), this);

        #endregion
    }
}
