using fefek5.Toys.Editor.Icons;
using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    [CustomPropertyDrawer(typeof(Url))]
    public class UrlDrawer : PropertyDrawer
    {
        // Approximate average character width relative to font size for the default Inspector font.
        private const float CHAR_WIDTH_RATIO = 0.55f;

        private const string OPEN_ICON = "open-icon";
        private const string COPY_ICON = "copy-icon";
        private const string PASTE_ICON = "paste-icon";

        private const float BUTTON_SIZE = 18;
        private const float ICON_SIZE = 12;
        private const float GAP = 2;

        private const long CLIPBOARD_CHECK_MS = 500;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var valueProp = property.FindPropertyRelative("_value");

            // Root
            var root = new VisualElement();

            // --- Row: TextField + buttons, under one label, so it ends where the fields around it end ---
            var inputs = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };

            var row = new RowField(preferredLabel, inputs);
            row.AddToClassList(BaseField<string>.alignedFieldUssClassName);

            var textField = new TextField {
                bindingPath = valueProp.propertyPath,
                style = {
                    flexGrow = 1,
                    flexShrink = 1
                }
            };

            SetMargins(textField, 0);

            var openBtn = CreateIconButton(OPEN_ICON, "Open in browser");
            var copyBtn = CreateIconButton(COPY_ICON, "Copy to clipboard");
            var pasteBtn = CreateIconButton(PASTE_ICON, "Paste from clipboard (an http:// or https:// URL)");

            inputs.Add(textField);
            inputs.Add(openBtn);
            inputs.Add(copyBtn);
            inputs.Add(pasteBtn);

            // --- Warning label ---
            var warningLabel = new Label("⚠ Invalid URL (expected http:// or https://)") {
                style = {
                    color = new Color(1f, 0.35f, 0.35f),
                    fontSize = 10,
                    marginLeft = EditorGUIUtility.labelWidth,
                    display = DisplayStyle.None
                }
            };

            root.Add(row);
            root.Add(warningLabel);

            // --- Shortened display state ---
            var isFocused = false;
            var url = new Url(valueProp.stringValue);

            textField.RegisterValueChangedCallback(evt =>
            {
                if (isFocused)
                    url.Value = evt.newValue;

                UpdateState(isFocused ? evt.newValue : url.Value);
            });

            // When the field gains focus – show the full URL for editing
            textField.RegisterCallback<FocusInEvent>(_ =>
            {
                isFocused = true;
                textField.SetValueWithoutNotify(url.Value);
            });

            // When the field loses focus – measure and show shortened URL if it doesn't fit
            textField.RegisterCallback<FocusOutEvent>(_ =>
            {
                isFocused = false;
                // Sync value back in case the user edited it
                url.Value = valueProp.stringValue;
                ApplyShortenedDisplay(textField, url);
                UpdateState(url.Value);
            });

            // Initial state after binding
            textField.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                textField.schedule.Execute(() =>
                {
                    url.Value = valueProp.stringValue;
                    ApplyShortenedDisplay(textField, url);
                    UpdateState(url.Value);
                });
            });

            // Re-evaluate on resize (Inspector width changes)
            textField.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (!isFocused)
                    ApplyShortenedDisplay(textField, url);
            });

            openBtn.clicked += () => Application.OpenURL(valueProp.stringValue);

            copyBtn.clicked += () => GUIUtility.systemCopyBuffer = valueProp.stringValue;

            // The clipboard changes outside the inspector, so the button checks it while it is shown.
            pasteBtn.schedule.Execute(() => pasteBtn.SetEnabled(IsValidUrl(GetClipboardText()))).Every(CLIPBOARD_CHECK_MS);

            pasteBtn.clicked += () =>
            {
                var pasted = GetClipboardText();
                valueProp.stringValue = pasted;
                valueProp.serializedObject.ApplyModifiedProperties();
                url.Value = pasted;

                if (isFocused)
                    textField.SetValueWithoutNotify(pasted);
                else
                    ApplyShortenedDisplay(textField, url);

                UpdateState(url.Value);
            };

            return root;

            // --- Validation & button logic ---
            void UpdateState(string rawUrl)
            {
                var hasValue = !string.IsNullOrWhiteSpace(rawUrl);
                var isValid = IsValidUrl(rawUrl);

                openBtn.SetEnabled(isValid);
                copyBtn.SetEnabled(hasValue);

                warningLabel.style.display = hasValue && !isValid
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

                // Red tint on invalid input
                textField.style.color = hasValue && !isValid
                    ? new StyleColor(new Color(1f, 0.45f, 0.45f))
                    : new StyleColor(StyleKeyword.Null);
            }
        }

        /// <summary>
        /// Measures how many characters fit inside the text input area and, if the full
        /// URL is too long, replaces the displayed value with a shortened version via
        /// <see cref="Url.ToShortString"/>.
        /// </summary>
        private static void ApplyShortenedDisplay(TextField textField, Url url)
        {
            var inputElement = textField.Q("unity-text-input");
            if (inputElement == null)
                return;

            var inputWidth = inputElement.resolvedStyle.width
                             - inputElement.resolvedStyle.paddingLeft
                             - inputElement.resolvedStyle.paddingRight;

            if (float.IsNaN(inputWidth) || inputWidth <= 0)
                return;

            var fontSize = inputElement.resolvedStyle.fontSize;
            if (fontSize <= 0)
                fontSize = 12f;

            var maxChars = Mathf.FloorToInt(inputWidth / (fontSize * CHAR_WIDTH_RATIO));

            if (maxChars < 8)
                maxChars = 8;

            var fullUrl = url.Value;
            if (string.IsNullOrEmpty(fullUrl))
            {
                textField.SetValueWithoutNotify(string.Empty);
                return;
            }

            var display = fullUrl.Length > maxChars
                ? url.ToShortString(maxChars)
                : fullUrl;

            textField.SetValueWithoutNotify(display);
        }

        private static Button CreateIconButton(string iconName, string tooltip)
        {
            EditorIconsDatabase.TryGetIcon<Texture2D>(iconName, out var icon);

            var button = new Button {
                tooltip = tooltip,
                style = {
                    width = BUTTON_SIZE,
                    height = BUTTON_SIZE,
                    flexShrink = 0,
                    paddingLeft = 0,
                    paddingRight = 0,
                    paddingTop = 0,
                    paddingBottom = 0,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center
                }
            };

            SetMargins(button, GAP);
            button.Add(new Image { image = icon, style = { width = ICON_SIZE, height = ICON_SIZE } });

            return button;
        }

        // Every element of the row but the first is GAP after the one before it, and none has other margins.
        private static void SetMargins(VisualElement element, float left)
        {
            element.style.marginLeft = left;
            element.style.marginRight = 0;
            element.style.marginTop = 0;
            element.style.marginBottom = 0;
        }

        // Trimmed, so a URL copied with the line break after it pastes as the URL alone.
        private static string GetClipboardText() => (GUIUtility.systemCopyBuffer ?? string.Empty).Trim();

        private static bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return System.Uri.TryCreate(url, System.UriKind.Absolute, out var uri)
                   && (uri.Scheme == System.Uri.UriSchemeHttp || uri.Scheme == System.Uri.UriSchemeHttps);
        }

        /// <summary>A label with any elements as its input, laid out as every other field of the inspector.</summary>
        private class RowField : BaseField<string>
        {
            public RowField(string label, VisualElement input) : base(label, input) { }
        }
    }
}

