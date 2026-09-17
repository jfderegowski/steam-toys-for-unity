using UnityEditor;

namespace SteamToys.Editor.SteamSettings
{
    public static class SteamSettingsMenuItems
    {
        internal const string SettingsPath = "Window/Steam Toys/Steam Settings";

        [MenuItem(SettingsPath)]
        public static void OpenSteamSettings()
        {
            EditorUtility.OpenPropertyEditor(Runtime.Core.SteamSettings.Instance);
        }
    }
}