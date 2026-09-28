using System;
using System.Collections.Generic;
using Runtime;
using SteamToys.Runtime.Core;
using SteamToys.Runtime.StatsSystem;
using Steamworks;
using UnityEngine;

namespace SteamToys.Runtime.AchievementsSystem
{
    /// <summary>
    /// The achievements of the game gathered in one asset, together with the achievement calls
    /// that belong to no single achievement.
    /// <para>
    /// The achievements it holds are sub-assets of it, created, compared with Steam and removed from
    /// its inspector, which "Window/Steam Toys/Steam Achievements DB" opens. An achievement can
    /// still be an asset of its own; the DB only knows the ones inside it.
    /// </para>
    /// <para>
    /// Achievements travel with the stats on the Steam side: <see cref="SteamStatsDB.StoreStats"/>
    /// stores both and <see cref="SteamStatsDB.ResetAllStats"/> can reset both. The Steamworks
    /// session itself belongs to <see cref="SteamManager"/>; achievements only check
    /// <see cref="SteamManager.SteamInitialized"/> and say so clearly when it is false.
    /// </para>
    /// <see href="https://partner.steamgames.com/doc/features/achievements"/>
    /// </summary>
    public class SteamAchievementsDB : SingletonObject<SteamAchievementsDB>
    {
        #region Events

        /// <summary>
        /// Raised when Steam stored an achievement or showed its progress, with the API Name, the
        /// current progress and the max progress. Both numbers are zero when the achievement was
        /// unlocked, including an unlock its progress stat triggered.
        /// </summary>
        public static event Action<string, uint, uint> OnAchievementStored;

        #endregion

        #region Properties

        /// <summary>
        /// True while <see cref="SteamManager.SteamInitialized"/>, which is what makes an achievement
        /// call safe: the raw <c>SteamUserStats</c> calls throw rather than fail when the API is down.
        /// </summary>
        public static bool Initialized => SteamManager.SteamInitialized;

        /// <summary>The achievements held by this asset, in the order they were added.</summary>
        public IReadOnlyList<SteamAchievement> Achievements => _achievements;

        #endregion

        #region Inspector Serialized Fields

        [SerializeField, Tooltip("The achievements held by this asset, all of them sub-assets of it. Managed from its inspector.")]
        private List<SteamAchievement> _achievements = new();

        #endregion

        #region Private Fields

        private static bool _warnedNotInitialized;

        // Held in a field on purpose: Steamworks.NET requires a live reference, or the garbage
        // collector takes the registration away.
        private static Callback<UserAchievementStored_t> _achievementStoredCallback;

        #endregion

        #region Achievements

        /// <summary>
        /// The achievement of this asset with the given API Name, or null when it holds none. A
        /// linear search, so keep the result rather than looking it up every frame.
        /// </summary>
        public SteamAchievement Get(string apiName)
        {
            foreach (var achievement in _achievements)
            {
                if (achievement && achievement.ApiName == apiName)
                    return achievement;
            }

            return null;
        }

        /// <summary>
        /// Finds the achievement of this asset with the given API Name, as long as it is a
        /// <typeparamref name="TAchievement"/>.
        /// </summary>
        public bool TryGet<TAchievement>(string apiName, out TAchievement achievement) where TAchievement : SteamAchievement
        {
            achievement = Get(apiName) as TAchievement;

            return achievement;
        }

        /// <summary>
        /// Reads the state of every achievement of this asset from Steam into its local cache, e.g.
        /// after <see cref="ClearAllAchievements"/>. Returns false when Steam is unreachable or
        /// refused any of the reads.
        /// </summary>
        public bool PullAllFromSteam()
        {
            if (!EnsureInitialized(this))
                return false;

            var pulled = true;

            foreach (var achievement in _achievements)
            {
                if (achievement)
                    pulled &= achievement.TryPullFromSteam();
            }

            return pulled;
        }

        #endregion

        #region Steam

        /// <summary>
        /// Locks every achievement Steam has for the game again and stores the change, leaving the
        /// stats alone. This wipes real progress, so it is meant for testing.
        /// <para>
        /// It goes through every achievement Steam has rather than the ones of the DB, so none is
        /// missed. Achievements that already read their state keep serving it from their cache; call
        /// <see cref="PullAllFromSteam"/> to pick the cleared state up.
        /// </para>
        /// </summary>
        public static bool ClearAllAchievements()
        {
            if (!EnsureInitialized(null))
                return false;

            var count = SteamUserStats.GetNumAchievements();
            var cleared = true;

            for (uint i = 0; i < count; i++)
                cleared &= SteamUserStats.ClearAchievement(SteamUserStats.GetAchievementName(i));

            if (!cleared)
                Debug.LogWarning("Steam turned down clearing some of the achievements.");

            return SteamStatsDB.StoreStats() && cleared;
        }

        /// <summary>
        /// True when achievement calls can be made. Warns only the first time it fails, because
        /// achievements may be checked as often as every frame and a warning per call would flood
        /// the console.
        /// </summary>
        internal static bool EnsureInitialized(UnityEngine.Object context)
        {
            if (Initialized)
                return true;

            if (_warnedNotInitialized)
                return false;

            _warnedNotInitialized = true;

            Debug.LogWarning("Steam achievements are not initialized because no Steam session is running. " +
                             "Add a SteamManager to the game, or outside play mode turn " +
                             "on \"Window/Steam Toys/Connect To Steam\"; if that is already done, " +
                             "the SteamToys error logged when the session failed to start says why. " +
                             "Unlocks are kept locally until then.", context);

            return false;
        }

        /// <summary>
        /// Prepares achievements for a new session. Called by <see cref="SteamSession"/> each time
        /// it starts, right after the stats, for the reasons <see cref="SteamStatsDB"/> gives for its
        /// own: the warning is re-armed and the callback hooked up again.
        /// </summary>
        internal static void OnSessionStarted()
        {
            _warnedNotInitialized = false;

            _achievementStoredCallback?.Dispose();
            _achievementStoredCallback = Callback<UserAchievementStored_t>.Create(OnUserAchievementStored);
        }

        // Entering play mode with domain reload disabled keeps this alive from the previous run,
        // and that run's warning should not silence the next one.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _warnedNotInitialized = false;
        }

        private static void OnUserAchievementStored(UserAchievementStored_t callback) =>
            OnAchievementStored?.Invoke(callback.m_rgchAchievementName, callback.m_nCurProgress, callback.m_nMaxProgress);

        #endregion
    }
}
