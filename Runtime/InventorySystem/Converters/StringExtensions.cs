using System;
using System.Collections.Generic;
using System.Text;

namespace SteamToys.Runtime.InventorySystem.Converters
{
    /// <summary>
    /// Extension methods for converting strings between common naming conventions:
    /// camelCase, PascalCase, snake_case, kebab-case, SCREAMING_SNAKE_CASE,
    /// SCREAMING-KEBAB-CASE, flatcase, UPPERFLATCASE, Train-Case,
    /// cobol-CASE (COBOL-CASE), dot.case, path/case.
    /// </summary>
    public static class StringExtensions
    {
        #region Core – Word Splitting

        /// <summary>
        /// Splits any string into its constituent words, regardless of the original
        /// naming convention. Handles camelCase boundaries, acronyms (e.g. "HTTP"),
        /// digit ↔ letter boundaries, and common separators (space, underscore,
        /// hyphen, dot, slash).
        /// </summary>
        private static List<string> SplitIntoWords(string input)
        {
            if (string.IsNullOrEmpty(input))
                return new List<string>(0);

            var words = new List<string>();
            var current = new StringBuilder();

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                // Treat common separators as word boundaries
                if (c == ' ' || c == '_' || c == '-' || c == '.' || c == '/')
                {
                    FlushWord(words, current);
                    continue;
                }

                // Drop any other non-alphanumeric character
                if (!char.IsLetterOrDigit(c))
                    continue;

                if (current.Length == 0)
                {
                    current.Append(c);
                    continue;
                }

                char prev = input[i - 1];

                // Digit ↔ letter boundary
                if (char.IsDigit(c) != char.IsDigit(prev) && char.IsLetterOrDigit(prev))
                {
                    FlushWord(words, current);
                    current.Append(c);
                    continue;
                }

                // Lowercase → Uppercase  (e.g. "camelCase" → "camel" | "Case")
                if (char.IsUpper(c) && char.IsLower(prev))
                {
                    FlushWord(words, current);
                    current.Append(c);
                    continue;
                }

                // Acronym boundary: Uppercase followed by Uppercase+Lowercase
                // e.g. "HTTPSConnection" → "HTTPS" | "Connection"
                if (char.IsUpper(c) && char.IsUpper(prev) && i + 1 < input.Length && char.IsLower(input[i + 1]))
                {
                    FlushWord(words, current);
                    current.Append(c);
                    continue;
                }

                current.Append(c);
            }

            FlushWord(words, current);
            return words;
        }

        private static void FlushWord(List<string> words, StringBuilder sb)
        {
            if (sb.Length > 0)
            {
                words.Add(sb.ToString());
                sb.Clear();
            }
        }

        #endregion

        #region Helpers

        private static string JoinWith(List<string> words, char separator, Func<string, int, string> transform)
        {
            if (words.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            for (int i = 0; i < words.Count; i++)
            {
                if (i > 0) sb.Append(separator);
                sb.Append(transform(words[i], i));
            }
            return sb.ToString();
        }

        private static string ToLowerWord(string word) => word.ToLowerInvariant();
        private static string ToUpperWord(string word) => word.ToUpperInvariant();

        private static string Capitalize(string word)
        {
            if (word.Length == 0) return word;
            if (word.Length == 1) return word.ToUpperInvariant();
            return char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant();
        }

        #endregion

        #region Public API

        // ────────────────────────────────────────────────────────────────
        //  camelCase
        // ────────────────────────────────────────────────────────────────
        /// <summary>camelCase – first word lowercase, subsequent words capitalised.</summary>
        /// <example>"some_variable_name" → "someVariableName"</example>
        public static string ToCamelCase(this string str)
        {
            var words = SplitIntoWords(str);
            if (words.Count == 0) return str ?? string.Empty;

            var sb = new StringBuilder();
            for (int i = 0; i < words.Count; i++)
                sb.Append(i == 0 ? ToLowerWord(words[i]) : Capitalize(words[i]));
            return sb.ToString();
        }

        // ────────────────────────────────────────────────────────────────
        //  PascalCase
        // ────────────────────────────────────────────────────────────────
        /// <summary>PascalCase – every word capitalised, no separator.</summary>
        /// <example>"some_variable_name" → "SomeVariableName"</example>
        public static string ToPascalCase(this string str)
        {
            var words = SplitIntoWords(str);
            if (words.Count == 0) return str ?? string.Empty;

            var sb = new StringBuilder();
            foreach (var w in words)
                sb.Append(Capitalize(w));
            return sb.ToString();
        }

        // ────────────────────────────────────────────────────────────────
        //  snake_case
        // ────────────────────────────────────────────────────────────────
        /// <summary>snake_case – all lowercase, words joined with underscores.</summary>
        /// <example>"SomeVariableName" → "some_variable_name"</example>
        public static string ToSnakeCase(this string str)
        {
            var words = SplitIntoWords(str);
            if (words.Count == 0) return str ?? string.Empty;
            return JoinWith(words, '_', (w, _) => ToLowerWord(w));
        }

        // ────────────────────────────────────────────────────────────────
        //  SCREAMING_SNAKE_CASE
        // ────────────────────────────────────────────────────────────────
        /// <summary>SCREAMING_SNAKE_CASE – all uppercase, words joined with underscores.</summary>
        /// <example>"someVariableName" → "SOME_VARIABLE_NAME"</example>
        public static string ToScreamingSnakeCase(this string str)
        {
            var words = SplitIntoWords(str);
            if (words.Count == 0) return str ?? string.Empty;
            return JoinWith(words, '_', (w, _) => ToUpperWord(w));
        }

        // ────────────────────────────────────────────────────────────────
        //  kebab-case
        // ────────────────────────────────────────────────────────────────
        /// <summary>kebab-case – all lowercase, words joined with hyphens.</summary>
        /// <example>"SomeVariableName" → "some-variable-name"</example>
        public static string ToKebabCase(this string str)
        {
            var words = SplitIntoWords(str);
            if (words.Count == 0) return str ?? string.Empty;
            return JoinWith(words, '-', (w, _) => ToLowerWord(w));
        }

        // ────────────────────────────────────────────────────────────────
        //  SCREAMING-KEBAB-CASE  (a.k.a. COBOL-CASE)
        // ────────────────────────────────────────────────────────────────
        /// <summary>SCREAMING-KEBAB-CASE – all uppercase, words joined with hyphens.</summary>
        /// <example>"someVariableName" → "SOME-VARIABLE-NAME"</example>
        public static string ToScreamingKebabCase(this string str)
        {
            var words = SplitIntoWords(str);
            if (words.Count == 0) return str ?? string.Empty;
            return JoinWith(words, '-', (w, _) => ToUpperWord(w));
        }

        // ────────────────────────────────────────────────────────────────
        //  Train-Case
        // ────────────────────────────────────────────────────────────────
        /// <summary>Train-Case – each word capitalised, joined with hyphens.</summary>
        /// <example>"some_variable_name" → "Some-Variable-Name"</example>
        public static string ToTrainCase(this string str)
        {
            var words = SplitIntoWords(str);
            if (words.Count == 0) return str ?? string.Empty;
            return JoinWith(words, '-', (w, _) => Capitalize(w));
        }

        // ────────────────────────────────────────────────────────────────
        //  flatcase
        // ────────────────────────────────────────────────────────────────
        /// <summary>flatcase – all lowercase, no separator.</summary>
        /// <example>"SomeVariableName" → "somevariablename"</example>
        public static string ToFlatCase(this string str)
        {
            var words = SplitIntoWords(str);
            if (words.Count == 0) return str ?? string.Empty;

            var sb = new StringBuilder();
            foreach (var w in words)
                sb.Append(ToLowerWord(w));
            return sb.ToString();
        }

        // ────────────────────────────────────────────────────────────────
        //  UPPERFLATCASE
        // ────────────────────────────────────────────────────────────────
        /// <summary>UPPERFLATCASE – all uppercase, no separator.</summary>
        /// <example>"someVariableName" → "SOMEVARIABLENAME"</example>
        public static string ToUpperFlatCase(this string str)
        {
            var words = SplitIntoWords(str);
            if (words.Count == 0) return str ?? string.Empty;

            var sb = new StringBuilder();
            foreach (var w in words)
                sb.Append(ToUpperWord(w));
            return sb.ToString();
        }

        // ────────────────────────────────────────────────────────────────
        //  dot.case
        // ────────────────────────────────────────────────────────────────
        /// <summary>dot.case – all lowercase, words joined with dots.</summary>
        /// <example>"SomeVariableName" → "some.variable.name"</example>
        public static string ToDotCase(this string str)
        {
            var words = SplitIntoWords(str);
            if (words.Count == 0) return str ?? string.Empty;
            return JoinWith(words, '.', (w, _) => ToLowerWord(w));
        }

        // ────────────────────────────────────────────────────────────────
        //  path/case
        // ────────────────────────────────────────────────────────────────
        /// <summary>path/case – all lowercase, words joined with forward slashes.</summary>
        /// <example>"SomeVariableName" → "some/variable/name"</example>
        public static string ToPathCase(this string str)
        {
            var words = SplitIntoWords(str);
            if (words.Count == 0) return str ?? string.Empty;
            return JoinWith(words, '/', (w, _) => ToLowerWord(w));
        }

        // ────────────────────────────────────────────────────────────────
        //  Title Case
        // ────────────────────────────────────────────────────────────────
        /// <summary>Title Case – each word capitalised, separated by spaces.</summary>
        /// <example>"some_variable_name" → "Some Variable Name"</example>
        public static string ToTitleCase(this string str)
        {
            var words = SplitIntoWords(str);
            if (words.Count == 0) return str ?? string.Empty;
            return JoinWith(words, ' ', (w, _) => Capitalize(w));
        }

        // ────────────────────────────────────────────────────────────────
        //  Sentence case
        // ────────────────────────────────────────────────────────────────
        /// <summary>Sentence case – first word capitalised, rest lowercase, separated by spaces.</summary>
        /// <example>"some_variable_name" → "Some variable name"</example>
        public static string ToSentenceCase(this string str)
        {
            var words = SplitIntoWords(str);
            if (words.Count == 0) return str ?? string.Empty;
            return JoinWith(words, ' ', (w, i) => i == 0 ? Capitalize(w) : ToLowerWord(w));
        }

        // ────────────────────────────────────────────────────────────────
        //  Pascal_Snake_Case
        // ────────────────────────────────────────────────────────────────
        /// <summary>Pascal_Snake_Case – each word capitalised, joined with underscores.</summary>
        /// <example>"some variable name" → "Some_Variable_Name"</example>
        public static string ToPascalSnakeCase(this string str)
        {
            var words = SplitIntoWords(str);
            if (words.Count == 0) return str ?? string.Empty;
            return JoinWith(words, '_', (w, _) => Capitalize(w));
        }

        // ────────────────────────────────────────────────────────────────
        //  camel_Snake_Case
        // ────────────────────────────────────────────────────────────────
        /// <summary>camel_Snake_Case – first word lowercase, subsequent capitalised, joined with underscores.</summary>
        /// <example>"some variable name" → "some_Variable_Name"</example>
        public static string ToCamelSnakeCase(this string str)
        {
            var words = SplitIntoWords(str);
            if (words.Count == 0) return str ?? string.Empty;
            return JoinWith(words, '_', (w, i) => i == 0 ? ToLowerWord(w) : Capitalize(w));
        }

        #endregion
    }
}

