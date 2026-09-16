using Steamworks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SteamToys.Editor.PropertyDrawers
{
    [CustomPropertyDrawer(typeof(AppId_t))]
    public class AppId_tDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var appIdProperty = property.FindPropertyRelative("m_AppId");

            var appIdField = new UnsignedIntegerField(appIdProperty.displayName);
            
            appIdField.AddToClassList("unity-base-field__aligned");

            appIdField.BindProperty(appIdProperty);
            
            return appIdField;
        }
    }
}