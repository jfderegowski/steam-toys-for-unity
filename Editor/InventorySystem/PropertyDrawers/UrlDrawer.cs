using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    [CustomPropertyDrawer(typeof(Url))]
    public class UrlDrawer : PropertyDrawer
    {
        private static readonly Color _iconTint = new(0.77f, 0.77f, 0.77f, 1f);
        
        // Approximate average character width relative to font size for the default Inspector font.
        private const float CHAR_WIDTH_RATIO = 0.55f;

        private const string ICONS_PATH = "Packages/com.fefek5.steam-toys/Editor/InventorySystem/Icons";
        private readonly Texture _openIcon = EditorGUIUtility.IconContent("d_BuildSettings.Web.Small").image;
        private readonly Texture _copyIcon = EditorGUIUtility.IconContent("Clipboard").image;
        private readonly Texture _pasteIcon = EditorGUIUtility.IconContent("d_Import").image;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var valueProp = property.FindPropertyRelative("_value");

            // Root
            var root = new VisualElement();

            // --- Row: TextField + buttons ---
            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };

            var textField = new TextField(preferredLabel) {
                bindingPath = valueProp.propertyPath,
                style = {
                    flexGrow = 1,
                    flexShrink = 1
                }
            };

            textField.AddToClassList(BaseField<string>.alignedFieldUssClassName);
        
            var openBtn = CreateIconButton(_openIcon, "Open in browser");
            var copyBtn = CreateIconButton(_copyIcon, "Copy to clipboard");
            var pasteBtn = CreateIconButton(_pasteIcon, "Paste from clipboard");

            row.Add(textField);
            row.Add(openBtn);
            row.Add(copyBtn);
            row.Add(pasteBtn);

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

            pasteBtn.clicked += () =>
            {
                var pasted = GUIUtility.systemCopyBuffer ?? string.Empty;
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

        private static Button CreateIconButton(Texture icon, string tooltip)
        {

            var image = new Image {
                image = icon,
                style = {
                    width = 12,
                    height = 12,
                    unityBackgroundImageTintColor = _iconTint
                },
                tintColor = _iconTint
            };

            var btn = new Button();
            btn.Add(image);
            btn.tooltip = tooltip;
            btn.style.width = 20;
            btn.style.height = 20;
            btn.style.marginLeft = 2;
            btn.style.paddingLeft = 0;
            btn.style.paddingRight = 0;
            btn.style.paddingTop = 0;
            btn.style.paddingBottom = 0;
            btn.style.alignItems = Align.Center;
            btn.style.justifyContent = Justify.Center;

            return btn;
        }

        private static bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return System.Uri.TryCreate(url, System.UriKind.Absolute, out var uri)
                   && (uri.Scheme == System.Uri.UriSchemeHttp || uri.Scheme == System.Uri.UriSchemeHttps);
        }
    }
}

