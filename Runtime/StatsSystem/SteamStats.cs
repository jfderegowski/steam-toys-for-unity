using System;
using SteamToys.Runtime.Core;
using Steamworks;
using UnityEngine;

namespace SteamToys.Runtime.StatsSystem
{
    /// <summary>
    /// The stat calls that belong to no single stat: committing the pending writes and resetting
    /// everything stored for the user.
    /// <para>
    /// The Steamworks session itself belongs to <see cref="SteamManager"/>. Stats only check
    /// <see cref="SteamManager.SteamInitialized"/> and say so clearly when it is false.
    /// </para>
    /// <see href="https://partner.steamgames.com/doc/features/achievements/stats_guide"/>
    /// </summary>
    public static class SteamStats
    {
        #region Events

        /// <summary>
        /// Raised when Steam delivered the stats of the user. Current Steamworks versions
        /// synchronise stats before the game process starts and no longer expose
        /// <c>RequestCurrentStats</c>, so this fires when Steam pushes an update of its own
        /// accord rather than in response to a request.
        /// </summary>
        public static event Action OnStatsReceived;

        /// <summary>
        /// Raised once Steam answered a <see cref="StoreStats"/> attempt, carrying whether it was
        /// accepted. Reaching the game requires the session to be pumped, which
        /// <see cref="SteamManager"/> does every frame and the editor driver does outside play mode.
        /// </summary>
        public static event Action<bool> OnStatsStored;

        #endregion

        #region Properties

        /// <summary>
        /// True while <see cref="SteamManager.SteamInitialized"/>, which is what makes a stat call
        /// safe: the raw <c>SteamUserStats</c> calls throw rather than fail when the API is down.
        /// </summary>
        public static bool Initialized => SteamManager.SteamInitialized;

        #endregion

        #region Private Fields

        private static bool _warnedNotInitialized;

        // Held in fields on purpose: Steamworks.NET requires a live reference, or the garbage
        // collector takes the registration away.
        private static Callback<UserStatsReceived_t> _statsReceivedCallback;
        private static Callback<UserStatsStored_t> _statsStoredCallback;

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
            if (!EnsureInitialized(null))
                return false;

            if (SteamUserStats.StoreStats())
                return true;

            Debug.LogWarning("Steam turned down the request to store the stats. This usually means the stats for the current user are not loaded yet, or the running app ID does not match the one the stats are configured for.");

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
            if (!EnsureInitialized(null))
                return false;

            if (SteamUserStats.ResetAllStats(achievementsToo))
                return true;

            Debug.LogWarning("Steam turned down the request to reset the stats.");

            return false;
        }

        /// <summary>
        /// True when stat calls can be made. Warns only the first time it fails, because stats are
        /// written as often as every frame and a warning per call would flood the console.
        /// </summary>
        internal static bool EnsureInitialized(UnityEngine.Object context)
        {
            if (Initialized)
                return true;

            if (_warnedNotInitialized)
                return false;

            _warnedNotInitialized = true;

            Debug.LogWarning("Steam stats are not initialized because no Steam session is running. " +
                             "Add a SteamManager to the game, or outside play mode turn " +
                             "on \"Window/Steam Toys/Connect To Steam\"; if that is already done, " +
                             "the SteamToys error logged when the session failed to start says why. " +
                             "Values are kept locally until then.", context);

            return false;
        }

        /// <summary>
        /// Hooks the stat callbacks up for a new session. Called by <see cref="SteamSession"/> each
        /// time it starts, because shutting a session down unregisters every callback on the
        /// Steamworks.NET side while the objects in these fields live on looking untouched.
        /// <para>
        /// The old objects are disposed rather than dropped: after a session that never shut down
        /// cleanly they may still be registered, and would otherwise go on firing alongside the
        /// new ones.
        /// </para>
        /// </summary>
        internal static void RegisterCallbacks()
        {
            _statsReceivedCallback?.Dispose();
            _statsStoredCallback?.Dispose();

            _statsReceivedCallback = Callback<UserStatsReceived_t>.Create(OnUserStatsReceived);
            _statsStoredCallback = Callback<UserStatsStored_t>.Create(OnUserStatsStored);
        }

        // Entering play mode with domain reload disabled keeps this alive from the previous run,
        // and that run's warning should not silence the next one.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _warnedNotInitialized = false;
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
    }
}
