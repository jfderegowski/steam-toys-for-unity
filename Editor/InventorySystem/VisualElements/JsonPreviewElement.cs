using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem
{
    /// <summary>
    /// A read-only, syntax highlighted JSON preview panel with copy buttons docked in its top-right corner.
    /// </summary>
    public class JsonPreviewElement : VisualElement
    {
        #region Constants

        private const int FONT_SIZE = 12;
        private const int BUTTON_HEIGHT = 18;
        private const long COPIED_FEEDBACK_MS = 1200;
        private const string COPIED_TEXT = "Copied!";

        private static readonly string[] MONOSPACE_FONT_NAMES = {
            "Consolas", "Menlo", "Monaco", "DejaVu Sans Mono", "Courier New"
        };

        #endregion

        #region Fields

        private static Font _monospaceFont;

        private readonly VisualElement _actions;
        private readonly TextElement _jsonText;
        private readonly JsonSyntaxHighlighter.Theme _theme;

        #endregion

        public JsonPreviewElement(string title)
        {
            var proSkin = EditorGUIUtility.isProSkin;

            _theme = proSkin ? JsonSyntaxHighlighter.Theme.Dark : JsonSyntaxHighlighter.Theme.Light;

            StyleAsPanel(proSkin);

            _actions = CreateActionsContainer();

            Add(CreateHeader(title, proSkin, _actions));

            _jsonText = CreateJsonText();

            Add(CreateBody(proSkin, _jsonText));
        }

        #region Public API

        /// <summary>
        /// Replaces the previewed JSON. The text is re-highlighted, the copy buttons keep working on live data.
        /// </summary>
        public void SetJson(string json) =>
            _jsonText.text = JsonSyntaxHighlighter.Highlight(json, _theme);

        /// <summary>
        /// Adds a copy button to the top-right corner. <paramref name="jsonProvider"/> is queried on every
        /// click, so the clipboard always receives the current state of the asset.
        /// </summary>
        public Button AddCopyAction(string text, string tooltip, Func<string> jsonProvider)
        {
            var button = new Button {
                text = text,
                tooltip = tooltip
            };

            button.style.height = BUTTON_HEIGHT;
            button.style.marginTop = 0;
            button.style.marginBottom = 0;
            button.style.marginRight = 0;
            button.style.marginLeft = 2;

            button.clicked += () => Copy(button, text, jsonProvider);

            _actions.Add(button);

            return button;
        }

        #endregion

        #region Copying

        private static void Copy(Button button, string label, Func<string> jsonProvider)
        {
            var json = jsonProvider?.Invoke();

            if (string.IsNullOrEmpty(json))
                return;

            EditorGUIUtility.systemCopyBuffer = json;

            Debug.Log($"Copied to clipboard: (Select to show more) \n{json}");

            // Pin the current width so the neighbouring buttons do not shift while the feedback shows.
            button.style.minWidth = button.resolvedStyle.width;
            button.text = COPIED_TEXT;

            button.schedule.Execute(() => {
                button.text = label;
                button.style.minWidth = StyleKeyword.Null;
            }).ExecuteLater(COPIED_FEEDBACK_MS);
        }

        #endregion

        #region Building

        private void StyleAsPanel(bool proSkin)
        {
            var border = proSkin ? new Color(0.14f, 0.14f, 0.14f) : new Color(0.62f, 0.62f, 0.62f);

            style.marginTop = 8;
            style.overflow = Overflow.Hidden;

            style.borderTopWidth = 1;
            style.borderRightWidth = 1;
            style.borderBottomWidth = 1;
            style.borderLeftWidth = 1;

            style.borderTopColor = border;
            style.borderRightColor = border;
            style.borderBottomColor = border;
            style.borderLeftColor = border;

            style.borderTopLeftRadius = 4;
            style.borderTopRightRadius = 4;
            style.borderBottomLeftRadius = 4;
            style.borderBottomRightRadius = 4;
        }

        private static VisualElement CreateActionsContainer()
        {
            var actions = new VisualElement();

            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexShrink = 0;

            return actions;
        }

        private static VisualElement CreateHeader(string title, bool proSkin, VisualElement actions)
        {
            var header = new VisualElement();

            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.backgroundColor = proSkin ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.80f, 0.80f, 0.80f);
            header.style.paddingLeft = 6;
            header.style.paddingRight = 4;
            header.style.paddingTop = 3;
            header.style.paddingBottom = 3;

            var label = new Label(title);

            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.flexShrink = 1;
            label.style.overflow = Overflow.Hidden;

            header.Add(label);
            header.Add(actions);

            return header;
        }

        private static TextElement CreateJsonText()
        {
            var jsonText = new TextElement {
                enableRichText = true,
                focusable = true
            };

            jsonText.selection.isSelectable = true;

            jsonText.style.whiteSpace = WhiteSpace.PreWrap;
            jsonText.style.fontSize = FONT_SIZE;

            var font = GetMonospaceFont();

            if (font != null)
                jsonText.style.unityFontDefinition = FontDefinition.FromFont(font);

            return jsonText;
        }

        private static VisualElement CreateBody(bool proSkin, VisualElement jsonText)
        {
            var body = new VisualElement();

            body.style.backgroundColor = proSkin ? new Color(0.13f, 0.13f, 0.13f) : new Color(0.94f, 0.94f, 0.94f);
            body.style.paddingLeft = 8;
            body.style.paddingRight = 8;
            body.style.paddingTop = 6;
            body.style.paddingBottom = 6;

            body.Add(jsonText);

            return body;
        }

        /// <summary>
        /// The first monospaced OS font that is available, or null when none of them are installed.
        /// </summary>
        private static Font GetMonospaceFont()
        {
            if (_monospaceFont != null)
                return _monospaceFont;

            _monospaceFont = Font.CreateDynamicFontFromOSFont(MONOSPACE_FONT_NAMES, FONT_SIZE);

            if (_monospaceFont != null)
                _monospaceFont.hideFlags = HideFlags.HideAndDontSave;

            return _monospaceFont;
        }

        #endregion
    }
}
