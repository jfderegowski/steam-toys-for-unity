using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.CodeGenerators
{
    /// <summary>
    /// Edits <see cref="SteamItemDefinitions"/>: the tags and the kinds of items in a list on the left, the one picked
    /// on the right with what keeps it from generating, the scripts it writes, and Generate. Generate All writes the
    /// tags first and then the kinds of items, which use their enums.
    /// </summary>
    public class SteamItemDefinitionsWindow : EditorWindow
    {
        #region Constants

        internal const string TITLE = "Steam Item Definitions";
        internal const string MenuPath = "Window/Steam Toys/" + TITLE;
        private const string DEFAULT_OUTPUT_FOLDER = "Assets/Scripts";

        private const string TAGS_PROPERTY = "_tags";
        private const string ITEM_DEFS_PROPERTY = "_itemDefs";
        private const string SCRIPTS_PROPERTY = "_scripts";

        #endregion

        [SerializeField] private string _selectedId;

        private SerializedObject _serialized;
        private VisualElement _list;
        private VisualElement _detail;
        private VisualElement _problems;
        private VisualElement _unused;
        private Label _writes;
        private Button _generate;

        private static SteamItemDefinitions Definitions => SteamItemDefinitions.instance;

        [MenuItem(MenuPath)]
        public static void Open() => GetWindow<SteamItemDefinitionsWindow>(TITLE);

        #region Layout

        private void CreateGUI()
        {
            _serialized = new SerializedObject(Definitions);

            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(GenerateAll) { text = "Generate All", tooltip = "Generates the tags, then the kinds of items." });
            toolbar.Add(new ToolbarSpacer { flex = true });
            toolbar.Add(new ToolbarButton(AddTag) { text = "+ Tag" });
            toolbar.Add(new ToolbarButton(AddItemDefs) { text = "+ Item Defs" });
            rootVisualElement.Add(toolbar);

            var split = new TwoPaneSplitView(0, 220, TwoPaneSplitViewOrientation.Horizontal);
            _list = new ScrollView { style = { paddingTop = 4 } };
            _detail = new ScrollView { style = { paddingLeft = 8, paddingRight = 8, paddingTop = 6 } };
            split.Add(_list);
            split.Add(_detail);
            rootVisualElement.Add(split);

            rootVisualElement.TrackSerializedObjectValue(_serialized, _ => OnChanged());

            RebuildList();
            RebuildDetail();
        }

        private void RebuildList()
        {
            _list.Clear();

            AddSection("Tags", Definitions.Tags);
            AddSection("Item Defs", Definitions.ItemDefs);
        }

        private void AddSection<T>(string title, List<T> generators) where T : CodeGenerator
        {
            _list.Add(new Label(title) { style = { unityFontStyleAndWeight = FontStyle.Bold, marginLeft = 6, marginTop = 6 } });

            foreach (var generator in generators.Where(generator => generator != null))
            {
                var hasProblems = CodeGeneration.GetProblems(generator).Count > 0;
                var row = new Button(() => Select(generator.Id))
                {
                    text = (hasProblems ? "⚠ " : string.Empty) + generator.DisplayName,
                    tooltip = hasProblems ? "Has problems; pick it to see them." : string.Empty,
                    style = { unityTextAlign = TextAnchor.MiddleLeft }
                };

                if (generator.Id == _selectedId)
                    row.style.backgroundColor = new Color(0.24f, 0.37f, 0.59f);

                _list.Add(row);
            }
        }

        private void RebuildDetail()
        {
            _detail.Clear();
            _problems = _unused = null;

            var (generator, property) = FindSelected();

            if (generator == null)
            {
                _detail.Add(new HelpBox("Pick a tag or a kind of item on the left, or add one.", HelpBoxMessageType.Info));
                return;
            }

            _detail.Add(new Label($"{generator.Kind}: {generator.DisplayName}")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 14, marginBottom = 6 }
            });

            var fields = new VisualElement();
            var child = property.Copy();
            var end = property.GetEndProperty();

            for (var enter = true; child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end); enter = false)
            {
                var field = new PropertyField(child.Copy());

                // Generate keeps them.
                if (child.name == SCRIPTS_PROPERTY)
                    field.SetEnabled(false);

                fields.Add(field);
            }

            fields.Bind(_serialized);
            _detail.Add(fields);

            _problems = new VisualElement { style = { marginTop = 6 } };
            _unused = new VisualElement();
            _writes = new Label { style = { marginTop = 6, unityFontStyleAndWeight = FontStyle.Italic, whiteSpace = WhiteSpace.Normal } };

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            _generate = new Button(() => Generate(generator)) { text = "Generate", style = { flexGrow = 1, height = 24 } };
            buttons.Add(_generate);
            buttons.Add(new Button(() => Remove(generator)) { text = "Remove", style = { height = 24 } });

            _detail.Add(_problems);
            _detail.Add(_unused);
            _detail.Add(_writes);
            _detail.Add(buttons);

            RefreshState(generator);
        }

        // What can change with any edit, refreshed without building the fields again, which would lose the focus.
        private void RefreshState(CodeGenerator generator)
        {
            if (_problems == null)
                return;

            var problems = CodeGeneration.GetProblems(generator);

            _problems.Clear();

            foreach (var problem in problems)
                _problems.Add(new HelpBox(problem, HelpBoxMessageType.Error));

            if (generator is TagCodeGenerator tag)
            {
                var users = Definitions.ItemDefs.Where(itemDefs => itemDefs != null && itemDefs.Uses(tag)).ToList();

                if (users.Count > 0 && !tag.IsEnumGenerated)
                    _problems.Add(new HelpBox($"Generate {string.Join(", ", users.Select(itemDefs => itemDefs.DisplayName))} " +
                                              "again after this, since they use this tag.", HelpBoxMessageType.Info));
            }

            _unused.Clear();

            var unused = CodeGeneration.GetUnusedScripts(generator);

            if (unused.Count > 0)
            {
                _unused.Add(new Button(() => { CodeGeneration.DeleteScripts(generator, unused); Refresh(); }) { text = "Delete Unused Scripts" });
                _unused.Add(new Button(() => { CodeGeneration.KeepScripts(generator, unused); Refresh(); }) { text = "Keep As My Own Scripts" });
            }

            _writes.text = problems.Count > 0 ? string.Empty : string.Join("\n", CodeGeneration.DescribeWrites(generator));
            _generate.SetEnabled(problems.Count == 0);
        }

        #endregion

        #region Changes

        private void OnChanged()
        {
            var renumbered = false;

            foreach (var generator in Definitions.All.Where(generator => generator != null))
                renumbered |= generator.Validate();

            if (renumbered)
                _serialized.Update();

            Definitions.SaveDefinitions();
            Refresh();
        }

        private void Refresh()
        {
            _serialized.Update();
            RebuildList();
            RefreshState(FindSelected().generator);
        }

        private void Select(string id)
        {
            _selectedId = id;
            RebuildList();
            RebuildDetail();
        }

        private (CodeGenerator generator, SerializedProperty property) FindSelected()
        {
            var tagIndex = Definitions.Tags.FindIndex(tag => tag != null && tag.Id == _selectedId);

            if (tagIndex >= 0)
                return (Definitions.Tags[tagIndex], _serialized.FindProperty($"{TAGS_PROPERTY}.Array.data[{tagIndex}]"));

            var itemDefsIndex = Definitions.ItemDefs.FindIndex(itemDefs => itemDefs != null && itemDefs.Id == _selectedId);

            if (itemDefsIndex >= 0)
                return (Definitions.ItemDefs[itemDefsIndex], _serialized.FindProperty($"{ITEM_DEFS_PROPERTY}.Array.data[{itemDefsIndex}]"));

            return (null, null);
        }

        private void AddTag() => Add(Definitions.Tags, new TagCodeGenerator());

        private void AddItemDefs() => Add(Definitions.ItemDefs, new ItemDefsCodeGenerator());

        private void Add<T>(List<T> generators, T generator) where T : CodeGenerator
        {
            Undo.RecordObject(Definitions, $"Add {generator.Kind}");

            // Next to the definitions there are, as new ones usually go.
            generator.OutputFolder = Definitions.All.LastOrDefault(other => other != null && !string.IsNullOrWhiteSpace(other.OutputFolder))
                ?.OutputFolder ?? DEFAULT_OUTPUT_FOLDER;

            generators.Add(generator);
            Definitions.SaveDefinitions();

            _serialized.Update();
            Select(generator.Id);
        }

        private void Remove(CodeGenerator generator)
        {
            var scripts = generator.Scripts.Where(script => script.Script).ToList();
            var choice = scripts.Count == 0
                ? EditorUtility.DisplayDialog("Remove", $"Remove {generator.DisplayName}?", "Remove", "Cancel") ? 0 : 1
                : EditorUtility.DisplayDialogComplex("Remove",
                    $"Remove {generator.DisplayName}? Its scripts: {string.Join(", ", scripts.Select(script => AssetDatabase.GetAssetPath(script.Script)))}.",
                    "Remove, Keep Scripts", "Cancel", "Remove, Delete Scripts");

            if (choice == 1 || choice == 2 && !CodeGeneration.DeleteScripts(generator, scripts))
                return;

            Undo.RecordObject(Definitions, $"Remove {generator.Kind}");

            if (generator is TagCodeGenerator tag)
                Definitions.Tags.Remove(tag);
            else if (generator is ItemDefsCodeGenerator itemDefs)
                Definitions.ItemDefs.Remove(itemDefs);

            Definitions.SaveDefinitions();

            _serialized.Update();
            Select(null);
        }

        #endregion

        #region Generate

        private void Generate(CodeGenerator generator)
        {
            CodeGeneration.Generate(new[] { generator });
            Refresh();
        }

        // The tags first, so the kinds of items find their enums where the tags say they are.
        private void GenerateAll()
        {
            CodeGeneration.Generate(Definitions.Tags.Where(tag => tag != null));
            CodeGeneration.Generate(Definitions.ItemDefs.Where(itemDefs => itemDefs != null));
            Refresh();
        }

        #endregion
    }
}
