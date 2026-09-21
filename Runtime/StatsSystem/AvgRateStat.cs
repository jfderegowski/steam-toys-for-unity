using System;
using Steamworks;
using UnityEngine;

namespace SteamToys.Runtime.StatsSystem
{
    /// <summary>
    /// An average rate stat, the <c>STAT_AVGRATE</c> kind on the partner site, e.g.
    /// "FeetTraveledPerHour".
    /// <para>
    /// Its value is never written directly. The game reports slices of play with
    /// <see cref="AddSession"/>, Steam keeps the rolling average, and
    /// <see cref="SteamStat{TValue}.Value"/> reads that average back.
    /// </para>
    /// <see href="https://partner.steamgames.com/doc/features/achievements/stats_guide"/>
    /// </summary>
    [CreateAssetMenu(fileName = "New Avg Rate Stat", menuName = "SteamToys/Stats/Avg Rate Stat", order = 2)]
    public class AvgRateStat : SteamStat<float>
    {
        public override SteamStatType StatType => SteamStatType.AvgRate;

        #region Properties

        /// <summary>Session count that Steam has not accepted yet.</summary>
        public float PendingCount => _pendingCount;

        /// <summary>Session length, in seconds, that Steam has not accepted yet.</summary>
        public double PendingSessionLength => _pendingSessionLength;

        /// <summary>
        /// How much play the average covers, matching Window on the partner site. A mirror only: Steam
        /// applies the window itself and the game never reads it.
        /// <para>
        /// It is in seconds, because <see cref="AddSession"/> reports session lengths in seconds and
        /// Valve's examples keep the window in the unit of the session length. Averaging over the last
        /// 20 hours of play takes 72000, while 20 would cover the last 20 seconds.
        /// </para>
        /// </summary>
        public float WindowSize
        {
            get => GetWindowSize();
            set => SetWindowSize(value);
        }

        #endregion

        #region Inspector Serialized Fields

        [Header("Average (mirror of the partner site)")]
        [SerializeField, Tooltip("How much play the average covers, matching Window on the partner site. In seconds, the unit AddSession reports session lengths in: 3600 averages over the last hour of play.")]
        private float _windowSize;

        #endregion

        #region Runtime State

        // Not serialized, like the cached value in the base: this is in flight session data, not
        // configuration of the asset.
        [NonSerialized] private float _pendingCount;
        [NonSerialized] private double _pendingSessionLength;

        #endregion

        protected override void OnEnable()
        {
            base.OnEnable();

            _pendingCount = 0f;
            _pendingSessionLength = 0d;
        }

        #region Getters and Setters

        public virtual float GetWindowSize() => _windowSize;

        public virtual void SetWindowSize(float value) => _windowSize = value;

        #endregion

        /// <summary>
        /// Reports a slice of play: how much of the counted thing happened
        /// (<paramref name="count"/>) over how many seconds
        /// (<paramref name="sessionLengthSeconds"/>), and feeds it to the rolling average Steam
        /// maintains. Anything Steam has not accepted yet stays pending and goes out with the
        /// next report.
        /// </summary>
        public bool AddSession(float count, double sessionLengthSeconds)
        {
            if (float.IsNaN(count) || float.IsInfinity(count))
            {
                Debug.LogError($"Steam stat '{ApiName}' was given a session count that is not a finite number.", this);

                return false;
            }

            if (sessionLengthSeconds <= 0d || double.IsNaN(sessionLengthSeconds) || double.IsInfinity(sessionLengthSeconds))
            {
                Debug.LogError($"Steam stat '{ApiName}' needs a session length greater than zero seconds, got {sessionLengthSeconds}.", this);

                return false;
            }

            _pendingCount += count;
            _pendingSessionLength += sessionLengthSeconds;

            return TryPushToSteam();
        }

        /// <summary>
        /// Not supported: Steam computes the value of an average rate stat. Report session data
        /// with <see cref="AddSession"/> instead.
        /// </summary>
        public override bool TrySetValue(float value)
        {
            Debug.LogError($"Steam stat '{ApiName}' is an average rate stat, so Steam computes its value. Report session data with AddSession(count, sessionLength) instead of assigning Value.", this);

            return false;
        }

        /// <summary>
        /// Drops the session data that has not been reported and puts the local cache back to the
        /// default. The average held by Steam can only be cleared with
        /// <see cref="SteamStats.ResetAllStats"/>.
        /// </summary>
        public override void ResetToDefault()
        {
            _pendingCount = 0f;
            _pendingSessionLength = 0d;

            ApplyValue(DefaultValue);
        }

        protected override bool TryGetFromSteam(out float value) => SteamUserStats.GetStat(ApiName, out value);

        protected override bool TryWriteToSteam()
        {
            if (_pendingSessionLength <= 0d)
                return true;

            if (!SteamUserStats.UpdateAvgRateStat(ApiName, _pendingCount, _pendingSessionLength))
                return false;

            _pendingCount = 0f;
            _pendingSessionLength = 0d;

            // Steam has just recomputed the average, so the cache is stale by definition.
            if (TryGetFromSteam(out var average))
                ApplyValue(average);

            return true;
        }

        // Unreachable through the public API, since TrySetValue turns direct writes down and
        // ResetToDefault bypasses validation. Implemented so the contract of the base class holds
        // for any future caller.
        protected override bool ValidateChange(float current, float desired, out float corrected)
        {
            corrected = current;

            return false;
        }
    }
}
