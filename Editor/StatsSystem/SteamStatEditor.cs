using SteamToys.Runtime.StatsSystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SteamToys.Editor.StatsSystem
{
    [CustomEditor(typeof(SteamStat))]
    public class SteamStatEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var defInspector = new VisualElement();
            
            InspectorElement.FillDefaultInspector(defInspector, serializedObject, this);
            
            return defInspector;
        }
    }
}