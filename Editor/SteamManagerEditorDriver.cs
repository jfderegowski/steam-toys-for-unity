using SteamToys.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SteamToys.Editor
{
    /// <summary>
    /// Runs the Steam session while the editor is not playing: the two menu toggles, the tick that
    /// dispatches callbacks, and the teardown at the points where the managed side goes away but
    /// the native one would stay up.
    /// <para>
    /// Both toggles turn off the same way, for the same reason. Steam registers the editor process
    /// as a running game on the first connection and only lets go of it when that process exits,
    /// so disconnecting means restarting the editor, and closing the client means closing the
    /// editor with it.
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    internal static class SteamManagerEditorDriver
    {
        private const string ConnectPath = "Window/Steam Toys/Connect To Steam";
        private const string RunClientPath = "Window/Steam Toys/Run Steam";

        static SteamManagerEditorDriver()
        {
            EditorApplication.update += Pump;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            // Neither the managed session nor the objects holding it survive these, so hand the
            // native API back rather than leaving it running with nothing attached to it.
            AssemblyReloadEvents.beforeAssemblyReload += SteamSession.Stop;
            EditorApplication.quitting += SteamSession.Stop;

            // A static constructor runs while Unity is still loading, which is too early to be
            // creating objects, so pick the session back up on the first editor tick instead.
            EditorApplication.delayCall += Restore;
        }

        /// <summary>
        /// Reconnects after an editor start or a domain reload, if the toggle was left on.
        /// </summary>
        private static void Restore()
        {
            if (SteamSession.EditModeEnabled)
                EnsureOwner();

            SteamSession.Sync();
        }

        private static void Pump()
        {
            // In play mode the manager pumps from Update, and doing it twice a frame would only
            // dispatch the same queue out of turn.
            if (Application.isPlaying)
                return;

            SteamSession.Pump();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            // Leaving play mode takes the owner with it, so edit mode needs one of its own back.
            if (change == PlayModeStateChange.EnteredEditMode && SteamSession.EditModeEnabled)
                EnsureOwner();

            // Every phase, not a chosen one: Sync only acts on a difference, and which phase the
            // owner actually changes in depends on the project's Enter Play Mode settings.
            SteamSession.Sync();
        }

        /// <summary>
        /// Makes sure something owns the session in edit mode. A manager already present in an open
        /// scene is used as it is; otherwise the driver adds one of its own, kept out of the scene
        /// file so that connecting to Steam never edits whatever happens to be open.
        /// </summary>
        private static void EnsureOwner()
        {
            if (SteamSession.HasOwner)
                return;

            // A manager that exists but has not claimed yet still counts, or reconnecting early in
            // a domain reload would leave a second one behind.
            if (Object.FindFirstObjectByType<SteamManager>())
                return;

            var gameObject = new GameObject(nameof(SteamManager)) { hideFlags = HideFlags.DontSave };

            gameObject.AddComponent<SteamManager>();
        }

        #region Connect To Steam

        [MenuItem(ConnectPath)]
        private static void ToggleConnect()
        {
            if (SteamSession.IsRunning)
            {
                // Drops the session and nothing else - closing the editor is left to you. Whether
                // Steam clears the in-game status appears to depend on how the process ends, and
                // calling SteamAPI_Shutdown beforehand may be exactly what costs Steam the signal
                // it watches for, so the two are separated here to tell them apart.
                SteamSession.EditModeEnabled = false;

                SteamSession.Sync();

                return;
            }

            SteamSession.EditModeEnabled = true;

            EnsureOwner();

            SteamSession.Sync();

            // Steam turns the request down when the client is not running or the account has no
            // license for the App ID, and the stored setting should not go on claiming otherwise.
            SteamSession.EditModeEnabled = SteamSession.IsRunning;
        }

        [MenuItem(ConnectPath, isValidateFunction: true)]
        private static bool ToggleConnectValidate()
        {
            // The check follows the API itself rather than the stored setting, so that a failed
            // connection reads as off instead of quietly lying.
            Menu.SetChecked(ConnectPath, SteamSession.IsRunning);

            // In play mode the game owns the session; shutting it down from here would break it.
            return !Application.isPlaying;
        }

        #endregion

        #region Run Steam

        /// <summary>
        /// True while the Steam client itself is up, which is what the session needs before it can
        /// connect to anything. Read from the process list rather than SteamAPI.IsSteamRunning,
        /// which sits among the private steamclient wrappers and answers only once the native
        /// library is loaded.
        /// </summary>
        private static bool IsClientRunning => System.Diagnostics.Process.GetProcessesByName("steam").Length > 0;

        [MenuItem(RunClientPath)]
        private static void ToggleClient()
        {
            if (IsClientRunning)
            {
                ShutdownClient();

                return;
            }

            Application.OpenURL("steam://open/main");
        }

        [MenuItem(RunClientPath, isValidateFunction: true)]
        private static bool ToggleClientValidate()
        {
            Menu.SetChecked(RunClientPath, IsClientRunning);

            return !Application.isPlaying;
        }

        /// <summary>
        /// Closing the client closes the editor with it once this process has connected: Steam
        /// shuts down the games it is running, and the editor is one of them as far as it can tell.
        /// That is deliberate on Steam's part, since a game cannot work without the client, so the
        /// editor is quit on its own terms rather than left to be killed.
        /// </summary>
        private static void ShutdownClient()
        {
            // An editor that never connected is not one of Steam's running games, so closing the
            // client is none of its business and needs no warning.
            if (!SteamSession.ConnectedThisEditorSession)
            {
                Application.OpenURL("steam://exit");

                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Shut Down Steam",
                    "This editor is registered with Steam as a running game, so closing the client closes the " +
                    "editor too.\n\nUnity quits first and Steam follows. Modified scenes are saved first, with " +
                    "the usual prompt.",
                    "Quit Both", "Cancel"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            SteamSession.EditModeEnabled = false;

            SteamSession.Stop();

            // The request goes out before the editor quits, because nothing of ours runs after
            // Exit. The order still comes out the natural way round: Steam takes seconds over its
            // own shutdown, while the editor closes immediately and on its own terms.
            Application.OpenURL("steam://exit");

            EditorApplication.Exit(0);
        }

        #endregion
    }
}
