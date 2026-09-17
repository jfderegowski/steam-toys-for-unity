using Runtime;
using UnityEngine;

namespace SteamToys.Runtime.Core
{
    /// <summary>
    /// Owns the Steam session while the game runs, in play mode and in a build.
    /// <para>
    /// The work itself lives in <see cref="SteamSession"/>; what this component contributes is a
    /// lifetime to hang it on, and that lifetime is the singleton's: the session is claimed when
    /// the singleton initializes and released when it is disposed. Outside play mode the component
    /// does nothing, because there the editor driver runs the session on its own, following the
    /// "Window/Steam Toys/Connect To Steam" toggle.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class SteamManager : SingletonBehaviour<SteamManager>
    {
        /// <summary>
        /// True while the Steamworks API is up.
        /// </summary>
        public static bool SteamInitialized => SteamSession.IsRunning;

        protected override void OnInitialize()
        {
            base.OnInitialize();

            SteamSession.Claim(this);
        }

        protected override void OnDispose()
        {
            base.OnDispose();

            SteamSession.Release(this);
        }

        private void Update() => SteamSession.Pump();
    }
}
