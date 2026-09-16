using System.Text;

namespace SteamToys.Editor.InventorySystem
{
    /// <summary>
    /// Turns raw JSON into UI Toolkit rich text so keys, values and punctuation can be told apart at a glance.
    /// </summary>
    public static class JsonSyntaxHighlighter
    {
        private static readonly string[] KEYWORDS = { "true", "false", "null" };

        #region Theme

        /// <summary>
        /// The colours (as RGB hex, without the leading '#') used for each kind of JSON token.
        /// </summary>
        public readonly struct Theme
        {
            public readonly string Key;
            public readonly string String;
            public readonly string Number;
            public readonly string Keyword;
            public readonly string Punctuation;

            public Theme(string key, string @string, string number, string keyword, string punctuation)
            {
                Key = key;
                String = @string;
                Number = number;
                Keyword = keyword;
                Punctuation = punctuation;
            }

            public static Theme Dark => new Theme("9CDCFE", "CE9178", "B5CEA8", "569CD6", "808080");

            public static Theme Light => new Theme("0451A5", "A31515", "098658", "0000FF", "6E6E6E");
        }

        #endregion

        #region Public API

        /// <summary>
        /// Wraps every token of <paramref name="json"/> in a rich text colour tag.
        /// Whitespace is passed through untouched so the original indentation survives.
        /// </summary>
        public static string Highlight(string json, Theme theme)
        {
            if (string.IsNullOrEmpty(json))
                return string.Empty;

            var builder = new StringBuilder(json.Length * 2);

            for (var i = 0; i < json.Length; i++)
            {
                var character = json[i];

                switch (character)
                {
                    case '"':
                        var stringEnd = FindStringEnd(json, i);
                        var literal = json.Substring(i, stringEnd - i + 1);

                        AppendColored(builder, literal, IsKey(json, stringEnd) ? theme.Key : theme.String);

                        i = stringEnd;
                        break;

                    case '{':
                    case '}':
                    case '[':
                    case ']':
                    case ',':
                    case ':':
                        AppendColored(builder, character.ToString(), theme.Punctuation);
                        break;

                    default:
                        if (IsNumberStart(character))
                        {
                            var numberEnd = FindNumberEnd(json, i);

                            AppendColored(builder, json.Substring(i, numberEnd - i + 1), theme.Number);

                            i = numberEnd;
                        }
                        else if (TryMatchKeyword(json, i, out var keyword))
                        {
                            AppendColored(builder, keyword, theme.Keyword);

                            i += keyword.Length - 1;
                        }
                        else
                        {
                            // Whitespace, line breaks and anything unexpected are kept verbatim.
                            builder.Append(character);
                        }

                        break;
                }
            }

            return builder.ToString();
        }

        #endregion

        #region Tokenizing

        /// <summary>
        /// Returns the index of the quote closing the string that starts at <paramref name="start"/>.
        /// </summary>
        private static int FindStringEnd(string json, int start)
        {
            for (var i = start + 1; i < json.Length; i++)
            {
                if (json[i] == '\\')
                {
                    // Skip the escaped character so an escaped quote does not end the string.
                    i++;
                    continue;
                }

                if (json[i] == '"')
                    return i;
            }

            return json.Length - 1;
        }

        /// <summary>
        /// A string is a property name when the next meaningful character after it is a colon.
        /// </summary>
        private static bool IsKey(string json, int stringEnd)
        {
            for (var i = stringEnd + 1; i < json.Length; i++)
            {
                if (char.IsWhiteSpace(json[i]))
                    continue;

                return json[i] == ':';
            }

            return false;
        }

        private static bool IsNumberStart(char character) => character == '-' || char.IsDigit(character);

        private static int FindNumberEnd(string json, int start)
        {
            var i = start + 1;

            while (i < json.Length)
            {
                var character = json[i];

                if (!char.IsDigit(character) && character != '.' && character != 'e' && character != 'E' &&
                    character != '+' && character != '-')
                    break;

                i++;
            }

            return i - 1;
        }

        private static bool TryMatchKeyword(string json, int index, out string keyword)
        {
            foreach (var candidate in KEYWORDS)
            {
                if (index + candidate.Length > json.Length)
                    continue;

                if (string.CompareOrdinal(json, index, candidate, 0, candidate.Length) != 0)
                    continue;

                keyword = candidate;
                return true;
            }

            keyword = null;
            return false;
        }

        #endregion

        #region Rich Text

        private static void AppendColored(StringBuilder builder, string text, string colorHex)
        {
            builder.Append("<color=#").Append(colorHex).Append('>');

            AppendLiteral(builder, text);

            builder.Append("</color>");
        }

        /// <summary>
        /// Appends <paramref name="text"/> so that a '&lt;' inside a JSON value is never mistaken for a rich text tag.
        /// </summary>
        private static void AppendLiteral(StringBuilder builder, string text)
        {
            if (text.IndexOf('<') < 0)
            {
                builder.Append(text);
                return;
            }

            builder.Append("<noparse>")
                .Append(text.Replace("</noparse>", string.Empty))
                .Append("</noparse>");
        }

        #endregion
    }
}
