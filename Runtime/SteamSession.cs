using System;
using Steamworks;
using UnityEngine;

namespace SteamToys.Runtime
{
    /// <summary>
    /// The one place that talks to the Steamworks API. Every call here is idempotent, so a caller
    /// never has to know what happened before it.
    /// <para>
    /// The session belongs to a <see cref="SteamManager"/>: it runs while an enabled manager owns
    /// it, and in the editor only while the menu toggle is on as well. Each lifecycle event calls
    /// <see cref="Sync"/> and nothing else, which is why none of them need to agree on the order
    /// Unity happens to run them in.
    /// </para>
    /// </summary>
    public static class SteamSession
    {
        #region Events

        /// <summary>
        /// Raised for every warning Steam sends.
        /// You must launch with "-debug_steamapi" in the launch args to receive warnings.
        /// </summary>
        public static event SteamAPIWarningMessageHook_t onWarningMessage;

        #endregion

        #region Properties

        /// <summary>
        /// True while the Steamworks API is up. This reads the state of the Steamworks dispatcher
        /// rather than a flag of our own, because that is what every Steam call is measured
        /// against: the raw calls throw when it is down, and it resets itself on entering play
        /// mode.
        /// </summary>
        public static bool IsRunning => CallbackDispatcher.IsInitialized;

        /// <summary>
        /// True while a <see cref="SteamManager"/> holds the session.
        /// </summary>
        public static bool HasOwner => _owner;

        /// <summary>
        /// Whether the session should be up at this moment: an enabled manager has to own it, and
        /// outside play mode the editor toggle has to be on as well.
        /// </summary>
        public static bool ShouldRun
        {
            get
            {
                if (!_owner)
                    return false;

#if UNITY_EDITOR
                if (!Application.isPlaying)
                    return EditModeEnabled;
#endif

                return true;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Whether Steam may be connected while the editor is not playing, as set by the
        /// "Window/Steam Toys/Connect To Steam" toggle. Scoped to this project on purpose: editor
        /// preferences are shared by every project opened with the same Unity install.
        /// </summary>
        public static bool EditModeEnabled
        {
            get => UnityEditor.EditorPrefs.GetBool(_editModeKey, false);
            set => UnityEditor.EditorPrefs.SetBool(_editModeKey, value);
        }

        private static readonly string _editModeKey = $"SteamToys.ConnectToSteam.{Application.dataPath}";

        /// <summary>
        /// True once this editor process has connected to Steam at least once. The client goes on
        /// showing the editor as in-game from that first connection until the process exits, so
        /// this outlives any individual session, and it is kept in SessionState precisely because
        /// that lifetime is the editor process itself.
        /// </summary>
        public static bool ConnectedThisEditorSession
        {
            get => UnityEditor.SessionState.GetBool(ConnectedKey, false);
            private set => UnityEditor.SessionState.SetBool(ConnectedKey, value);
        }

        private const string ConnectedKey = "SteamToys.ConnectedThisEditorSession";
#endif

        #endregion

        #region Private Fields

        private static SteamManager _owner;

        // Held in a field on purpose: Steamworks.NET requires a live reference, or the garbage
        // collector takes the registration away.
        private static SteamAPIWarningMessageHook_t _warningMessageHook;

        #endregion

        #region Ownership

        /// <summary>
        /// Hands the session to a manager. The last one to be enabled wins, matching how the
        /// singleton itself settles duplicates.
        /// </summary>
        internal static void Claim(SteamManager owner)
        {
            _owner = owner;

            Sync();
        }

        /// <summary>
        /// Takes the session away from a manager that is being disabled or destroyed. Ignores
        /// managers that never held it, so a stray copy cannot shut down a live session.
        /// </summary>
        internal static void Release(SteamManager owner)
        {
            if (_owner != owner)
                return;

            _owner = null;

            Sync();
        }

        /// <summary>
        /// Brings the session in line with <see cref="ShouldRun"/>. This is the single entry point
        /// every lifecycle event goes through, so the order Unity raises them in does not matter.
        /// </summary>
        public static void Sync()
        {
            if (ShouldRun == IsRunning)
                return;

            if (ShouldRun)
                Start();
            else
                Stop();
        }

        #endregion

        #region Steam API

        /// <summary>
        /// Initializes the Steamworks API. Failure is reported and returned rather than thrown,
        /// because the usual cause is simply that the Steam client is not running.
        /// <para>
        /// Other reasons Valve lists: the client could not work out the App ID, which needs
        /// steam_appid.txt in the working directory; the app runs under a different OS user than
        /// the client; the active Steam account does not own a license for the App ID; or the App
        /// ID is not fully set up on the partner site.
        /// </para>
        /// <see href="https://partner.steamgames.com/doc/sdk/api#initialization_and_shutdown"/>
        /// </summary>
        private static bool Start()
        {
            if (IsRunning)
                return true;

            if (!Packsize.Test())
            {
                Debug.LogError(
                    "[Steamworks.NET] Packsize Test returned false, " +
                    "the wrong version of Steamworks.NET is being run in this platform.");

                return false;
            }

            if (!DllCheck.Test())
            {
                Debug.LogError(
                    "[Steamworks.NET] DllCheck Test returned false, " +
                    "One or more of the Steamworks binaries seems to be the wrong version.");

                return false;
            }

            try
            {
#if !UNITY_EDITOR
                // If Steam is not running or the game wasn't started through Steam, this starts the
                // Steam client and launches the game again if the user owns it, which doubles as a
                // rudimentary form of DRM. Never in the editor: there it would tell Steam to launch
                // the built game instead of the one being edited.
                if (SteamAPI.RestartAppIfNecessary(SteamSettings.Instance.AppId))
                {
                    Application.Quit();

                    return false;
                }
#endif

                var result = SteamAPI.InitEx(out var error);

                if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                {
                    Debug.LogError($"[SteamToys] SteamAPI.InitEx() returned {result}: {error}");

                    return false;
                }
            }
            catch (DllNotFoundException e)
            {
                // A missing binary surfaces on the first native call, and in the editor that is
                // InitEx rather than RestartAppIfNecessary.
                Debug.LogError(
                    "[Steamworks.NET] Could not load [lib]steam_api.dll/so/dylib. " +
                    "It's likely not in the correct location. Refer to the README for more details.\n" +
                    e);

                return false;
            }

            SteamClient.SetWarningMessageHook(_warningMessageHook ??= SteamAPIDebugTextHook);

            // Worth naming the app: the ID comes from steam_appid.txt in the working directory,
            // which is easy to have pointing somewhere other than the project's own setting.
            Debug.Log($"[SteamToys] Steam session started for app {SteamUtils.GetAppID()}.");

#if UNITY_EDITOR
            // Registering the process with Steam is what cannot be taken back, so the mark is set
            // by the connection itself rather than by whichever menu entry asked for it.
            ConnectedThisEditorSession = true;
#endif

            return true;
        }

        /// <summary>
        /// Shuts the Steamworks API down. Safe to call at any point, including from the editor
        /// hooks that fire when the managed side is about to go away on its own.
        /// </summary>
        public static void Stop()
        {
            if (!IsRunning)
            {
                Debug.Log("[SteamToys] Stop() reached a session that was already down.");

                return;
            }

            SteamAPI.Shutdown();

            // Reading the state back is the whole point of logging here: it separates "our side
            // refused to let go" from "our side let go and the Steam client is showing its own".
            if (IsRunning)
            {
                Debug.LogWarning(
                    "[SteamToys] SteamAPI.Shutdown() ran, but the dispatcher still reports the session as up. " +
                    "That happens when the API was initialized more times than it was shut down.");

                return;
            }

            Debug.Log(
                "[SteamToys] Steam session stopped. The Steam client goes on showing the editor as in-game " +
                "until the editor process itself exits.");
        }

        /// <summary>
        /// Dispatches the callbacks Steam has queued up. Has to run every frame the session is up,
        /// or nothing Steam answers ever reaches the game.
        /// </summary>
        public static void Pump()
        {
            if (!IsRunning)
                return;

            SteamAPI.RunCallbacks();
        }

        #endregion

        // Entering play mode with domain reload disabled keeps these alive while Steamworks.NET
        // resets its own dispatcher underneath them, so the play mode session has to start from
        // nothing and claim an owner of its own.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticMembersSteamSession()
        {
            _owner = null;
            _warningMessageHook = null;
        }

        [AOT.MonoPInvokeCallback(typeof(SteamAPIWarningMessageHook_t))]
        private static void SteamAPIDebugTextHook(int nSeverity, System.Text.StringBuilder pchDebugText)
        {
            Debug.LogWarning(pchDebugText);

            onWarningMessage?.Invoke(nSeverity, pchDebugText);
        }
    }
}
