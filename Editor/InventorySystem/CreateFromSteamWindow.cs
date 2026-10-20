using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem
{
    /// <summary>
    /// The confirmation of Create From Steam in the items DB, with the class of each group of new definitions to
    /// choose: definitions of the same type and tag categories, which the same classes hold.
    /// </summary>
    internal sealed class CreateFromSteamWindow : EditorWindow
    {
        private const int MaxNamesInTooltip = 20;

        /// <summary>New definitions the same classes hold, and the class they are made with.</summary>
        internal sealed class Group
        {
            public string Label;
            public List<Type> Classes;
            public Type Choice;
            public readonly List<ItemDefsFromSteam.SteamDefinition> Definitions = new();
        }

        private string _message;
        private List<Group> _groups;
        private bool _confirmed;

        /// <summary>
        /// Asks with <paramref name="message"/> and waits for the answer. True to go on, with the choice of each of
        /// <paramref name="groups"/> set.
        /// </summary>
        internal static bool Show(string message, List<Group> groups)
        {
            var window = CreateInstance<CreateFromSteamWindow>();

            window.titleContent = new GUIContent("Create From Steam");
            window._message = message;
            window._groups = groups;
            window.minSize = new Vector2(620, 320);
            window.ShowModal();

            return window._confirmed;
        }

        internal static string FormatClass(Type type) =>
            ItemDefsFromSteam.IsPackageClass(type) ? $"{type.Name} (package)" : type.Name;

        private void CreateGUI()
        {
            var root = rootVisualElement;

            root.style.paddingLeft = root.style.paddingRight = root.style.paddingTop = root.style.paddingBottom = 8;

            root.Add(new HelpBox(_message, HelpBoxMessageType.Info));

            var list = new ScrollView { style = { flexGrow = 1, marginTop = 6 } };

            foreach (var group in _groups)
                list.Add(CreateRow(group));

            root.Add(list);

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd, marginTop = 6 } };

            buttons.Add(new Button(() =>
            {
                _confirmed = true;
                Close();
            }) { text = "Create And Update" });

            buttons.Add(new Button(Close) { text = "Cancel" });

            root.Add(buttons);
        }

        private static VisualElement CreateRow(Group group)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 } };
            var names = group.Definitions.Select(definition => $"{definition.ItemDefId} {definition.Name}").ToList();

            row.Add(new Label($"{group.Definitions.Count} × {group.Label}")
            {
                tooltip = string.Join("\n", names.Take(MaxNamesInTooltip)) +
                          (names.Count > MaxNamesInTooltip ? $"\n... and {names.Count - MaxNamesInTooltip} more" : string.Empty),
                style = { flexGrow = 1, flexShrink = 1, whiteSpace = WhiteSpace.Normal }
            });

            var field = new PopupField<Type>(group.Classes, group.Classes.IndexOf(group.Choice), FormatClass, FormatClass)
            {
                tooltip = group.Classes.Count > 1
                    ? "The class these definitions are made with."
                    : "The only class of their type with exactly their tags.",
                style = { width = 280, flexShrink = 0 }
            };

            field.RegisterValueChangedCallback(change => group.Choice = change.newValue);
            field.SetEnabled(group.Classes.Count > 1);

            row.Add(field);

            return row;
        }
    }
}
