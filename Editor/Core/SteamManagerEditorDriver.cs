using System;
using System.IO;
using System.Text;
using SteamToys.Editor.SteamSettings;
using SteamToys.Runtime;
using SteamToys.Runtime.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;

namespace SteamToys.Editor.Core
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

            // Reconnects after an editor start or a domain reload if the toggle was left on. A
            // static constructor runs while Unity is still loading the domain, which is no place to
            // be starting a native API, so this waits for the first editor tick.
            EditorApplication.delayCall += SteamSession.Sync;
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
            // Every phase, not a chosen one: Sync only acts on a difference, and ShouldRun already
            // tells which side of the transition the editor is on.
            SteamSession.Sync();
        }

        #region Connect To Steam

        [MenuItem(ConnectPath)]
        private static void ToggleConnect()
        {
            if (SteamSession.IsRunning)
            {
                Disconnect();

                return;
            }

            SteamSession.EditModeEnabled = true;

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

        /// <summary>
        /// Disconnecting restarts the editor, because Steam drops the in-game status only once the
        /// process that connected has exited.
        /// <para>
        /// The replacement editor must not be started by this process. Steam also tracks every
        /// process a tracked one starts, detached or not, so an editor relaunched from here would
        /// carry the status straight over. WMI starts it instead, from a host of its own.
        /// </para>
        /// </summary>
        private static void Disconnect()
        {
            if (!EditorUtility.DisplayDialog(
                    "Disconnect From Steam",
                    "Steam shows this editor as in-game until its process exits, so disconnecting restarts the " +
                    "editor.\n\nModified scenes are saved first, with the usual prompt.",
                    "Restart", "Cancel"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // Before anything is torn down, so that a failure leaves the session as it was.
            if (!ScheduleRelaunch())
                return;

            SteamSession.EditModeEnabled = false;

            SteamSession.Stop();

            EditorApplication.Exit(0);
        }

        /// <summary>
        /// Leaves behind a hidden PowerShell, outside this process tree, that waits for the editor to
        /// exit and then opens the project again. Returns false, with the reason logged, if it could
        /// not be started.
        /// </summary>
        private static bool ScheduleRelaunch()
        {
            var projectPath = Directory.GetParent(Application.dataPath).FullName;

            // Only the project path is carried over: the rest of the command line can hold one-off
            // launch state, such as the Hub's session tokens.
            var relaunch =
                $"Wait-Process -Id {System.Diagnostics.Process.GetCurrentProcess().Id} -ErrorAction SilentlyContinue\n" +
                $"Start-Process -FilePath {PowerShellString(EditorApplication.applicationPath)} " +
                $"-ArgumentList {PowerShellString($"-projectPath \"{projectPath}\"")} " +
                $"-WorkingDirectory {PowerShellString(projectPath)}";

            // Win32_Process.Create runs the command from WmiPrvSE, which is what keeps it out of
            // Steam's reach. ShowWindow 0 spares the console window it would otherwise flash.
            // An encoded command writes errors and progress to stderr as XML, so progress is
            // silenced and the error is written out as plain text for the log.
            var broker =
                "$ProgressPreference = 'SilentlyContinue'\n" +
                "try {\n" +
                "    $startup = New-CimInstance -ClassName Win32_ProcessStartup -ClientOnly -Property @{ ShowWindow = [uint16]0 }\n" +
                "    $result = Invoke-CimMethod -ErrorAction Stop -ClassName Win32_Process -MethodName Create -Arguments @{ " +
                $"CommandLine = {PowerShellString("powershell.exe " + PowerShellArguments(relaunch))}; " +
                "ProcessStartupInformation = $startup }\n" +
                "    exit $result.ReturnValue\n" +
                "} catch {\n" +
                "    [Console]::Error.WriteLine($_)\n" +
                "    exit 1\n" +
                "}";

            var startInfo = new System.Diagnostics.ProcessStartInfo("powershell.exe", PowerShellArguments(broker))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);

            if (!process.WaitForExit(30000))
            {
                process.Kill();

                Debug.LogError("[SteamToys] Scheduling the editor restart timed out, so the session stays up.");

                return false;
            }

            if (process.ExitCode != 0)
            {
                Debug.LogError(
                    $"[SteamToys] Could not schedule the editor restart (exit code {process.ExitCode}), " +
                    $"so the session stays up.\n{process.StandardError.ReadToEnd()}");

                return false;
            }

            return true;
        }

        // Encoded, so the script needs no quoting beyond its own string literals.
        private static string PowerShellArguments(string script) =>
            "-NoProfile -NonInteractive -EncodedCommand " +
            Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        private static string PowerShellString(string value) => $"'{value.Replace("'", "''")}'";

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

        #region Toolbar

        private const string ToolbarPath = "Steam Toys";

        /// <summary>
        /// Puts the "Window/Steam Toys" menu on the left of the main toolbar, as a dropdown holding
        /// the same entries. Where the toolbar shows it is a preference of its own from there on,
        /// so this only decides where it starts out.
        /// </summary>
        [MainToolbarElement(ToolbarPath, defaultDockPosition = MainToolbarDockPosition.Left, defaultDockIndex = 20)]
        private static MainToolbarElement CreateToolbarDropdown() =>
            new MainToolbarDropdown(new MainToolbarContent(ToolbarPath, "Window/Steam Toys"), ShowToolbarMenu);

        private static void ShowToolbarMenu(Rect rect)
        {
            var menu = new GenericMenu();

            // Each entry asks the menu's own validate function whether it may be used, so the
            // dropdown cannot end up offering something the menu refuses.
            AddToolbarItem(menu, ConnectPath, ToggleConnectValidate(), ToggleConnect);
            AddToolbarItem(menu, RunClientPath, ToggleClientValidate(), ToggleClient);
            AddToolbarItem(menu, SteamSettingsMenuItems.SettingsPath, true, SteamSettingsMenuItems.OpenSteamSettings);

            menu.DropDown(rect);
        }

        /// <summary>
        /// Adds one menu entry to the dropdown under the name it carries in the menu. The checkmark
        /// is read back from the menu rather than worked out again: the validate function has just
        /// run, which is what sets it.
        /// </summary>
        private static void AddToolbarItem(
            GenericMenu menu, string path, bool enabled, GenericMenu.MenuFunction action)
        {
            var label = new GUIContent(path.Substring(path.LastIndexOf('/') + 1));

            if (enabled)
                menu.AddItem(label, Menu.GetChecked(path), action);
            else
                menu.AddDisabledItem(label, Menu.GetChecked(path));
        }

        #endregion
    }
}
