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

        // Longer JSON, such as achievements carrying their icons as base64, is cut in the log.
        private const int MAX_LOGGED_LENGTH = 5000;

        private static readonly string[] MONOSPACE_FONT_NAMES = {
            "Consolas", "Menlo", "Monaco", "DejaVu Sans Mono", "Courier New"
        };

        #endregion

        #region Fields

        private static Font _monospaceFont;

        private readonly VisualElement _actions;
        private readonly TextElement _jsonText;
        private readonly ScrollView _scrollView;
        private readonly JsonSyntaxHighlighter.Theme _theme;

        private bool _wrapText;

        #endregion

        /// <param name="title">The header text.</param>
        /// <param name="maxHeight">
        /// The height past which the JSON scrolls vertically; 0 or less lets the preview grow with its text.
        /// </param>
        /// <param name="wrapText">
        /// Whether long lines wrap; without it they keep their length and the preview scrolls sideways when needed.
        /// </param>
        public JsonPreviewElement(string title, float maxHeight = 500, bool wrapText = true)
        {
            var proSkin = EditorGUIUtility.isProSkin;

            _theme = proSkin ? JsonSyntaxHighlighter.Theme.Dark : JsonSyntaxHighlighter.Theme.Light;

            StyleAsPanel(proSkin);

            _actions = CreateActionsContainer();

            Add(CreateHeader(title, proSkin, _actions));

            _jsonText = CreateJsonText();
            _scrollView = CreateBody(proSkin, _jsonText);

            Add(_scrollView);

            MaxHeight = maxHeight;
            WrapText = wrapText;
        }

        #region Public API

        /// <summary>
        /// The height past which the JSON scrolls vertically; 0 or less lets the preview grow with its text.
        /// Scrollbars show only while the text does not fit.
        /// </summary>
        public float MaxHeight
        {
            get => _scrollView.style.maxHeight.keyword == StyleKeyword.None ? 0 : _scrollView.style.maxHeight.value.value;
            set => _scrollView.style.maxHeight = value > 0 ? new StyleLength(value) : new StyleLength(StyleKeyword.None);
        }

        /// <summary>
        /// Whether long lines wrap. Without it they keep their length and the preview scrolls sideways when needed.
        /// </summary>
        public bool WrapText
        {
            get => _wrapText;
            set
            {
                _wrapText = value;

                _jsonText.style.whiteSpace = value ? WhiteSpace.PreWrap : WhiteSpace.Pre;

                // Wrapped text must take the width of the view, so only unwrapped text may scroll sideways.
                _scrollView.mode = value ? ScrollViewMode.Vertical : ScrollViewMode.VerticalAndHorizontal;
                _scrollView.horizontalScrollerVisibility = value ? ScrollerVisibility.Hidden : ScrollerVisibility.Auto;
            }
        }

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

            var logged = json.Length > MAX_LOGGED_LENGTH
                ? $"{json.Substring(0, MAX_LOGGED_LENGTH)}\n... ({json.Length - MAX_LOGGED_LENGTH} more characters on the clipboard)"
                : json;

            Debug.Log($"Copied to clipboard: (Select to show more) \n{logged}");

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

            // The advanced generator of Unity 6.3 throws in its native rich text parser when selectable rich text
            // is scrolled over; the standard one draws the same colors.
            jsonText.style.unityTextGenerator = TextGeneratorType.Standard;

            jsonText.style.fontSize = FONT_SIZE;

            var font = GetMonospaceFont();

            if (font != null)
                jsonText.style.unityFontDefinition = FontDefinition.FromFont(font);

            return jsonText;
        }

        private static ScrollView CreateBody(bool proSkin, VisualElement jsonText)
        {
            var body = new ScrollView {
                verticalScrollerVisibility = ScrollerVisibility.Auto
            };

            body.style.backgroundColor = proSkin ? new Color(0.13f, 0.13f, 0.13f) : new Color(0.94f, 0.94f, 0.94f);

            // Padding inside the scrolled content, so the scrollbars sit at the edges of the panel.
            body.contentContainer.style.paddingLeft = 8;
            body.contentContainer.style.paddingRight = 8;
            body.contentContainer.style.paddingTop = 6;
            body.contentContainer.style.paddingBottom = 6;

            body.Add(jsonText);

            return body;
        }

        /// <summary>
        /// The first monospaced OS font that is available, or null when none of them are installed.
        /// </summary>
        internal static Font GetMonospaceFont()
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
