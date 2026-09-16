using Runtime;
using UnityEngine;

namespace SteamToys.Runtime
{
    /// <summary>
    /// Owns the Steam session. It is up while an enabled manager exists, and in the editor only
    /// while the "Window/Steam Toys/Connect To Steam" toggle is on as well.
    /// <para>
    /// The work itself lives in <see cref="SteamSession"/>; what this component contributes is a
    /// lifetime to hang it on, so that disabling or destroying the manager ends the session the
    /// same way in the editor, in play mode and in a build.
    /// </para>
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class SteamManager : SingletonBehaviour<SteamManager>
    {
        /// <summary>
        /// True while the Steamworks API is up.
        /// </summary>
        public static bool SteamInitialized => SteamSession.IsRunning;

        private void OnEnable()
        {
#if UNITY_EDITOR
            // [ExecuteAlways] also runs for the copies Unity keeps in prefab mode and in the
            // preview scenes behind asset thumbnails. Those are not the game, and a session they
            // claimed would end the moment the window closed.
            if (UnityEditor.SceneManagement.EditorSceneManager.IsPreviewSceneObject(this))
                return;
#endif

            SteamSession.Claim(this);
        }

        // OnDisable rather than OnDestroy, because it is the one hook that covers all four ways
        // the manager stops being in charge: destroyed, leaving play mode, a domain reload, and
        // the component simply being switched off. OnDestroy misses the domain reload, which would
        // leave the native API up with nothing holding it.
        private void OnDisable() => SteamSession.Release(this);

        private void Update()
        {
            // Edit mode is pumped by the editor driver instead: [ExecuteAlways] only gets an
            // Update when the editor repaints, so an idle editor would stop dispatching callbacks.
            if (!Application.isPlaying)
                return;

            SteamSession.Pump();
        }
    }
}
