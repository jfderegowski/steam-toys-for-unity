using System.Collections.Generic;
using System.IO;
using SteamToys.Runtime;
using SteamToys.Runtime.Core;
using Steamworks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SteamToys.Editor.PropertyDrawers
{
    [CustomPropertyDrawer(typeof(ProjectAppIdAttribute))]
    public class ProjectAppIdDrawer : PropertyDrawer
    {
        // Same location Steamworks.NET uses (project root).
        private static readonly string _steamAppIdPath = Path.Combine(Directory.GetCurrentDirectory(), "steam_appid.txt");

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            // Namespace SteamToys.Editor.SteamSettings shadows the class name here.
            var steamSettings = SteamToys.Runtime.SteamSettings.Instance;

            var appIdProperty = property.FindPropertyRelative("m_AppId");

            var root = new VisualElement();

            var appIdField = new PopupField<ProjectAppIdRef>(property.displayName) {
                formatSelectedValueCallback = FormatAppIdRef,
                formatListItemCallback = FormatAppIdRef
            };

            appIdField.AddToClassList("unity-base-field__aligned");

            var fileWarning = new HelpBox(string.Empty, HelpBoxMessageType.Warning);

            fileWarning.Add(new Button(() =>
            {
                File.WriteAllText(_steamAppIdPath, appIdProperty.uintValue.ToString());

                Refresh();
            }) { text = "Overwrite" });

            root.Add(appIdField);
            root.Add(fileWarning);

            Refresh();

            appIdField.RegisterValueChangedCallback(evt =>
            {
                appIdProperty.uintValue = evt.newValue.AppId.m_AppId;
                appIdProperty.serializedObject.ApplyModifiedProperties();

                File.WriteAllText(_steamAppIdPath, evt.newValue.AppId.ToString());

                Refresh();
            });

            appIdField.TrackPropertyValue(appIdProperty, _ => Refresh());
            appIdField.TrackSerializedObjectValue(new SerializedObject(steamSettings), _ => Refresh());

            return root;

            void Refresh()
            {
                var appId = appIdProperty.uintValue;
                var choices = new List<ProjectAppIdRef>(steamSettings.PosibleAppIds);
                var index = choices.FindIndex(appIdRef => appIdRef.AppId.m_AppId == appId);

                if (index < 0)
                {
                    choices.Insert(0, new ProjectAppIdRef { ProjectName = "Unknown", AppId = new AppId_t(appId) });
                    index = 0;
                }

                appIdField.choices = choices;
                appIdField.SetValueWithoutNotify(choices[index]);

                var fileAppId = File.Exists(_steamAppIdPath) ? File.ReadAllText(_steamAppIdPath).Trim() : null;

                fileWarning.text = fileAppId == null
                    ? $"steam_appid.txt not found in project root, expected {appId}."
                    : $"steam_appid.txt contains {fileAppId}, expected {appId}.";

                fileWarning.style.display = fileAppId == appId.ToString() ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        private static string FormatAppIdRef(ProjectAppIdRef appIdRef) => $"{appIdRef.AppId} ({appIdRef.ProjectName})";
    }
}
