using System;
using System.Collections.Generic;
using System.Linq;
using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem
{
    [CustomEditor(typeof(ItemDef), true), CanEditMultipleObjects]
    public class ItemDefEditor : UnityEditor.Editor
    {
        #region Constants

        private const string SCRIPT_PROPERTY = "m_Script";
        private const char SUBMENU_SEPARATOR = '/';

        private const string TYPE_TOOLTIP =
            "The Steam type and class of the definition. Choosing another class changes it; the values both classes have are kept.";

        #endregion

        #region Fields

        private static readonly Dictionary<Type, MonoScript> Scripts = new();

        #endregion

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var typeField = CreateTypeField();

            if (typeField != null)
                root.Add(typeField);

            root.Add(new ItemDefElement(serializedObject));

            return root;
        }

        #region Type

        /// <summary>
        /// Every class a definition can be made with, under its Steam type, with the class of the selected definitions
        /// chosen. Null when they are of different classes.
        /// </summary>
        private DropdownField CreateTypeField()
        {
            var current = target.GetType();

            if (targets.Any(item => item.GetType() != current))
                return null;

            var classes = GetClasses();
            var labels = classes.Select(GetLabel).ToList();
            var currentIndex = classes.IndexOf(current);

            // The chosen one shows its type alone, without the class of its submenu.
            var field = new DropdownField(labels, currentIndex) {
                tooltip = TYPE_TOOLTIP,
                formatSelectedValueCallback = label => label.Split(SUBMENU_SEPARATOR)[0]
            };

            field.RegisterValueChangedCallback(change => {
                var index = labels.IndexOf(change.newValue);

                if (index < 0 || index == currentIndex)
                    return;

                if (ConfirmChange(current, classes[index]))
                    ChangeClass(classes[index]);
                else
                    field.SetValueWithoutNotify(change.previousValue);
            });

            return field;
        }

        /// <summary>
        /// The classes inheriting <see cref="ItemDef"/> that Unity can make a ScriptableObject of, in the order of the
        /// Steam types, then by name. The same list for every definition.
        /// </summary>
        private static List<Type> GetClasses() =>
            Enum.GetValues(typeof(ItemType)).Cast<ItemType>()
                .SelectMany(type => ItemDefsFromSteam.GetConcreteTypes(type)
                    .Where(candidate => GetScript(candidate))
                    .OrderBy(candidate => candidate.Name))
                .ToList();

        /// <summary>The Steam type with the class in its submenu, e.g. <c>Generator/Default Generator Def</c>.</summary>
        private static string GetLabel(Type type) =>
            GetTypeName(type) + SUBMENU_SEPARATOR + ObjectNames.NicifyVariableName(type.Name);

        private static string GetTypeName(Type type) =>
            ObjectNames.NicifyVariableName(ItemDefsFromSteam.GetItemType(type).ToString());

        private bool ConfirmChange(Type from, Type to)
        {
            var subject = targets.Length == 1 ? target.name : $"the {targets.Length} selected definitions";
            var toType = GetTypeName(to);
            var fromTags = ItemDefsFromSteam.GetTagsType(from);
            var toTags = ItemDefsFromSteam.GetTagsType(to);

            var message = $"Change {subject} from {ObjectNames.NicifyVariableName(from.Name)} to " +
                          $"{ObjectNames.NicifyVariableName(to.Name)}, a {toType}?\n\n" +
                          "The values both classes have are kept, and only those the new type uses are shown and exported.";

            // Tags are a field of ItemDef<TTags>, so only the tags of the same TTags surely carry over.
            if (fromTags != null && toTags == null)
                message += $" The tags are lost: a {toType} has none.";
            else if (fromTags != null && toTags != fromTags)
                message += $" The tags may not carry over: {ObjectNames.NicifyVariableName(to.Name)} keeps tags of " +
                           $"{ObjectNames.NicifyVariableName(toTags.Name)}, not {ObjectNames.NicifyVariableName(fromTags.Name)}.";

            return EditorUtility.DisplayDialog("Change Item Type", message, "Change", "Cancel");
        }

        /// <summary>Swaps the script of the selected definitions; Unity rebuilds the inspector for the new class.</summary>
        private void ChangeClass(Type type)
        {
            serializedObject.FindProperty(SCRIPT_PROPERTY).objectReferenceValue = GetScript(type);
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>The script of <paramref name="type"/>, or null when its file is named otherwise and Unity cannot make assets of it.</summary>
        private static MonoScript GetScript(Type type)
        {
            if (Scripts.TryGetValue(type, out var script))
                return script;

            var instance = CreateInstance(type);

            try
            {
                script = MonoScript.FromScriptableObject(instance);

                return Scripts[type] = script && script.GetClass() == type ? script : null;
            }
            finally
            {
                DestroyImmediate(instance);
            }
        }

        #endregion
    }
}
