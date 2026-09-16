#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_ANDROID || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
    #define DISABLESTEAMWORKS
#endif

using System;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace SteamToys.Runtime.StatsSystem
{
    /// <summary>
    /// The stat calls that belong to no single stat: committing the pending writes and resetting
    /// everything stored for the user.
    /// <para>
    /// The game owns the Steamworks lifecycle. This package never calls <c>SteamAPI.Init</c>,
    /// <c>SteamAPI.RunCallbacks</c> or <c>SteamAPI.Shutdown</c>; it only checks whether the API is
    /// up and says so clearly when it is not.
    /// </para>
    /// <see href="https://partner.steamgames.com/doc/features/achievements/stats_guide"/>
    /// </summary>
    public static class SteamStats
    {
        #region Events

#if DISABLESTEAMWORKS
        // Both events are raised from the Steamworks branch further down, so on a platform
        // without Steamworks they are declared but never invoked, which is exactly what CS0067
        // reports. The public surface stays the same on every platform on purpose.
        #pragma warning disable CS0067
#endif

        /// <summary>
        /// Raised when Steam delivered the stats of the user. Current Steamworks versions
        /// synchronise stats before the game process starts and no longer expose
        /// <c>RequestCurrentStats</c>, so this fires when Steam pushes an update of its own
        /// accord rather than in response to a request.
        /// </summary>
        public static event Action OnStatsReceived;

        /// <summary>
        /// Raised once Steam answered a <see cref="StoreStats"/> attempt, carrying whether it was
        /// accepted. Reaching the game requires the game to be pumping
        /// <c>SteamAPI.RunCallbacks</c>.
        /// </summary>
        public static event Action<bool> OnStatsStored;

#if DISABLESTEAMWORKS
        #pragma warning restore CS0067
#endif

        #endregion

        #region Properties

        /// <summary>
        /// True when the Steamworks API is initialized, which is what makes a stat call safe:
        /// the raw <c>SteamUserStats</c> calls throw rather than fail when the API is down.
        /// </summary>
        public static bool IsAvailable
        {
            get
            {
#if DISABLESTEAMWORKS
                return false;
#else
                return CallbackDispatcher.IsInitialized;
#endif
            }
        }

        #endregion

        #region Private Fields

        private static bool _warnedUnavailable;

#if !DISABLESTEAMWORKS
        // Held in fields on purpose: Steamworks.NET requires a live reference, or the garbage
        // collector takes the registration away.
        private static Callback<UserStatsReceived_t> _statsReceivedCallback;
        private static Callback<UserStatsStored_t> _statsStoredCallback;
#endif

        #endregion

        /// <summary>
        /// Sends every changed stat to Steam for permanent storage. Call it at a natural boundary,
        /// such as the end of a round, rather than after each individual write.
        /// <para>
        /// Steam reverts any value that breaks a constraint configured on the partner site, which
        /// is why each stat validates its changes before they get here. The outcome arrives in
        /// <see cref="OnStatsStored"/>.
        /// </para>
        /// </summary>
        public static bool StoreStats()
        {
            if (!EnsureAvailable(null))
                return false;

#if !DISABLESTEAMWORKS
            if (SteamUserStats.StoreStats())
                return true;

            Debug.LogWarning("Steam turned down the request to store the stats. This usually means the stats for the current user are not loaded yet, or the running app ID does not match the one the stats are configured for.");
#endif

            return false;
        }

        /// <summary>
        /// Resets every stat of the user to its default on the Steam servers, and optionally the
        /// achievements with them. This wipes real progress, so it is meant for testing.
        /// <para>
        /// Stats that already read their value keep serving it from their cache; call
        /// <see cref="SteamStat.TryPullFromSteam"/> on the ones you care about to pick the reset
        /// values up.
        /// </para>
        /// </summary>
        public static bool ResetAllStats(bool achievementsToo = false)
        {
            if (!EnsureAvailable(null))
                return false;

#if !DISABLESTEAMWORKS
            if (SteamUserStats.ResetAllStats(achievementsToo))
                return true;

            Debug.LogWarning("Steam turned down the request to reset the stats.");
#endif

            return false;
        }

        /// <summary>
        /// True when stat calls can be made, and the place where the callbacks get registered
        /// once the API is up. Warns only the first time it fails, because stats are written as
        /// often as every frame and a warning per call would flood the console.
        /// </summary>
        internal static bool EnsureAvailable(UnityEngine.Object context)
        {
            if (IsAvailable)
            {
#if !DISABLESTEAMWORKS
                EnsureCallbacks();
#endif

                return true;
            }

            if (_warnedUnavailable)
                return false;

            _warnedUnavailable = true;

#if DISABLESTEAMWORKS
            Debug.LogWarning("Steamworks is not available on this platform, so Steam stats keep their values locally only.", context);
#else
            Debug.LogWarning("Steam stats are unavailable because the Steamworks API is not initialized. The game is responsible for calling SteamAPI.Init, and SteamAPI.RunCallbacks every frame, before using stats. Values are kept locally until then.", context);
#endif

            return false;
        }

        // Steamworks.NET clears its own dispatcher when entering play mode with domain reload
        // disabled, so the stale callbacks and the warning flag from the previous session have to
        // go with it.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _warnedUnavailable = false;

#if !DISABLESTEAMWORKS
            _statsReceivedCallback = null;
            _statsStoredCallback = null;
#endif
        }

#if !DISABLESTEAMWORKS
        /// <summary>
        /// Registers the stat callbacks the first time they are needed. They cannot be created
        /// before the API is initialized, which is why this is lazy instead of running on load.
        /// </summary>
        private static void EnsureCallbacks()
        {
            if (_statsReceivedCallback != null)
                return;

            _statsReceivedCallback = Callback<UserStatsReceived_t>.Create(OnUserStatsReceived);
            _statsStoredCallback = Callback<UserStatsStored_t>.Create(OnUserStatsStored);
        }

        private static void OnUserStatsReceived(UserStatsReceived_t callback)
        {
            if (callback.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogWarning($"Steam failed to deliver the stats of the user: {callback.m_eResult}.");

                return;
            }

            OnStatsReceived?.Invoke();
        }

        private static void OnUserStatsStored(UserStatsStored_t callback)
        {
            var stored = callback.m_eResult == EResult.k_EResultOK;

            if (!stored)
            {
                Debug.LogWarning($"Steam failed to store the stats: {callback.m_eResult}. A value that breaks one of the constraints configured on the partner site is reverted to its previous value.");
            }

            OnStatsStored?.Invoke(stored);
        }
#endif
    }
}
