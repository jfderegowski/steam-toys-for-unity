using Runtime;
using Steamworks;
using UnityEngine;

namespace SteamToys.Runtime
{
	[DisallowMultipleComponent]
	public class SteamManager : SingletonBehaviour<SteamManager>
	{
		public static event SteamAPIWarningMessageHook_t onWarningMessage;
		
		private static bool _everInitialized;

		public static bool SteamInitialized => Instance._steamInitialized;

		private bool _steamInitialized;
		
		private SteamAPIWarningMessageHook_t _steamAPIWarningMessageHook;
		
		private void Awake()
		{
			if (_everInitialized)
				throw new System.Exception("Tried to Initialize the SteamAPI twice in one session!");

			if (!Packsize.Test())
				Debug.LogError(
					"[Steamworks.NET] Packsize Test returned false, " +
					"the wrong version of Steamworks.NET is being run in this platform.",
					this);

			if (!DllCheck.Test())
				Debug.LogError(
					"[Steamworks.NET] DllCheck Test returned false, " +
					"One or more of the Steamworks binaries seems to be the wrong version.",
					this);

			try
			{
				// If Steam is not running or the game wasn't started through Steam, SteamAPI_RestartAppIfNecessary starts the
				// Steam client and also launches this game again if the User owns it. This can act as a rudimentary form of DRM.

				// Once you get a Steam AppID assigned by Valve, you need to replace AppId_t.Invalid with it and
				// remove steam_appid.txt from the game depot. eg: "(AppId_t)480" or "new AppId_t(480)".
				// See the Valve documentation for more information: https://partner.steamgames.com/doc/sdk/api#initialization_and_shutdown
				if (SteamAPI.RestartAppIfNecessary(AppId_t.Invalid))
				{
					Application.Quit();
					return;
				}
			}
			catch (System.DllNotFoundException e)
			{
				// We catch this exception here, as it will be the first occurrence of it.
				Debug.LogError(
					"[Steamworks.NET] Could not load [lib]steam_api.dll/so/dylib. " +
					"It's likely not in the correct location. Refer to the README for more details.\n" +
					e, this);

				Application.Quit();
				return;
			}

			// Initializes the Steamworks API.
			// If this returns false then this indicates one of the following conditions:
			// [*] The Steam client isn't running. A running Steam client is required to provide implementations of the various Steamworks interfaces.
			// [*] The Steam client couldn't determine the App ID of game. If you're running your application from the executable or debugger directly then you must have a [code-inline]steam_appid.txt[/code-inline] in your game directory next to the executable, with your app ID in it and nothing else. Steam will look for this file in the current working directory. If you are running your executable from a different directory you may need to relocate the [code-inline]steam_appid.txt[/code-inline] file.
			// [*] Your application is not running under the same OS user context as the Steam client, such as a different user or administration access level.
			// [*] Ensure that you own a license for the App ID on the currently active Steam account. Your game must show up in your Steam library.
			// [*] Your App ID is not completely set up, i.e. in Release State: Unavailable, or it's missing default packages.
			// Valve's documentation for this is located here:
			// https://partner.steamgames.com/doc/sdk/api#initialization_and_shutdown
			_steamInitialized = SteamAPI.Init();
			if (!_steamInitialized)
			{
				Debug.LogError(
					"[Steamworks.NET] SteamAPI_Init() failed. Refer to Valve's documentation or the comment above this line for more information.",
					this);

				return;
			}

			_everInitialized = true;
		}

		private void OnEnable()
		{
			if (!_steamInitialized)
				return;

			TrySetWarnigMessageHook();
		}

		private void OnDestroy()
		{
			if (!_steamInitialized) 
				return;

			SteamAPI.Shutdown();
		}

		private void Update()
		{
			if (!_steamInitialized)
				return;

			SteamAPI.RunCallbacks();
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStaticMembersSteamManager() => _everInitialized = false;

		/// <summary>
		/// Set up callback to receive warning messages from Steam.
		/// You must launch with "-debug_steamapi" in the launch args to receive warnings.
		/// </summary>
		private void TrySetWarnigMessageHook()
		{
			if (_steamAPIWarningMessageHook != null) return;
			
			_steamAPIWarningMessageHook = SteamAPIDebugTextHook;
			
			SteamClient.SetWarningMessageHook(_steamAPIWarningMessageHook);
		}

		[AOT.MonoPInvokeCallback(typeof(SteamAPIWarningMessageHook_t))]
		protected static void SteamAPIDebugTextHook(int nSeverity, System.Text.StringBuilder pchDebugText)
		{
			Debug.LogWarning(pchDebugText);
			
			onWarningMessage?.Invoke(nSeverity, pchDebugText);
		}
	}
}