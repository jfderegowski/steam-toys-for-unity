using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    /// <summary>
    /// Draws any <see cref="PromoRule"/> managed reference.
    /// Shows a dropdown to pick / change the concrete type, then
    /// draws the child fields of the selected subclass below.
    /// All concrete (non-abstract) subclasses of <see cref="PromoRule"/>
    /// are discovered automatically via reflection.
    /// </summary>
    [CustomPropertyDrawer(typeof(PromoRule), true)]
    public class PromoRuleDrawer : PropertyDrawer
    {
        private static readonly List<(string Label, Type Type)> _ruleTypes = DiscoverRuleTypes();
        private static readonly List<string> _labels = _ruleTypes.Select(r => r.Label).ToList();

        /// <summary>
        /// Finds all concrete (non-abstract) subclasses of <see cref="PromoRule"/>
        /// across all loaded assemblies and builds a label/type list.
        /// The label is derived from the class name by stripping "PromoRule" / "Rule"
        /// suffix and inserting spaces before capital letters.
        /// </summary>
        private static List<(string Label, Type Type)> DiscoverRuleTypes()
        {
            var baseType = typeof(PromoRule);

            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
                })
                .Where(t => t != null
                             && t != baseType
                             && !t.IsAbstract
                             && baseType.IsAssignableFrom(t))
                .OrderBy(t => t.Name)
                .Select(t => (Label: NicifyTypeName(t), Type: t))
                .ToList();

            return types;
        }

        /// <summary>
        /// Converts a type name like "AchievementPromoRule" into "Achievement".
        /// Strips common suffixes and inserts spaces before uppercase letters.
        /// </summary>
        private static string NicifyTypeName(Type type)
        {
            var name = type.Name;

            // Strip common suffixes
            if (name.EndsWith("PromoRule"))
                name = name.Substring(0, name.Length - "PromoRule".Length);
            else if (name.EndsWith("Rule"))
                name = name.Substring(0, name.Length - "Rule".Length);

            // Insert spaces before uppercase letters (e.g. "OwnsApp" -> "Owns App")
            name = Regex.Replace(name, "(?<!^)([A-Z])", " $1");

            return string.IsNullOrWhiteSpace(name) ? type.Name : name;
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            root.style.marginBottom = 4;

            // --- Type dropdown ---
            var currentIndex = GetCurrentTypeIndex(property);

            var dropdown = new DropdownField("Type", _labels, currentIndex >= 0 ? currentIndex : 0);

            if (currentIndex < 0)
                dropdown.SetValueWithoutNotify("Select Type…");

            dropdown.RegisterValueChangedCallback(evt => {
                var selectedIndex = _labels.IndexOf(evt.newValue);
                if (selectedIndex < 0) return;

                var selectedType = _ruleTypes[selectedIndex].Type;

                // Don't recreate if already this type
                if (IsCurrentType(property, selectedType)) return;

                property.serializedObject.Update();
                property.managedReferenceValue = Activator.CreateInstance(selectedType);
                property.serializedObject.ApplyModifiedProperties();

                RebuildFields(root, property);
            });

            root.Add(dropdown);

            // --- Child fields ---
            BuildFields(root, property);

            return root;
        }

        private static void RebuildFields(VisualElement root, SerializedProperty property)
        {
            // Remove everything after the dropdown
            while (root.childCount > 1)
                root.RemoveAt(root.childCount - 1);

            BuildFields(root, property);

            // Re-bind so new PropertyFields pick up serialized data
            root.Bind(property.serializedObject);
        }

        private static void BuildFields(VisualElement root, SerializedProperty property)
        {
            if (string.IsNullOrEmpty(property.managedReferenceFullTypename))
                return;

            var iterator = property.Copy();
            var end = iterator.GetEndProperty();

            if (!iterator.NextVisible(true)) return;

            do
            {
                if (SerializedProperty.EqualContents(iterator, end)) break;

                var field = new PropertyField(iterator);
                field.BindProperty(iterator);
                root.Add(field);
            } while (iterator.NextVisible(false));
        }

        private static int GetCurrentTypeIndex(SerializedProperty property)
        {
            var fullTypeName = property.managedReferenceFullTypename;

            if (string.IsNullOrEmpty(fullTypeName))
                return -1;

            for (var i = 0; i < _ruleTypes.Count; i++)
            {
                if (fullTypeName.EndsWith(_ruleTypes[i].Type.FullName ?? _ruleTypes[i].Type.Name))
                    return i;
            }

            return -1;
        }

        private static bool IsCurrentType(SerializedProperty property, Type type)
        {
            var fullTypeName = property.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(fullTypeName))
                return false;

            return fullTypeName.EndsWith(type.FullName ?? type.Name);
        }
    }
}