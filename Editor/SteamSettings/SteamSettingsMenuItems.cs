using UnityEditor;

namespace SteamToys.Editor.SteamSettings
{
    public static class SteamSettingsMenuItems
    {
        [MenuItem("Window/Steam Toys/Steam Settings")]
        public static void OpenSteamSettings()
        {
            EditorUtility.OpenPropertyEditor(Runtime.Core.SteamSettings.Instance);
        }
    }
}