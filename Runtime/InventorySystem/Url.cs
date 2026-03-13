using System;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.Converters;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    /// <summary>
    /// Represents a URL string with validation, implicit/explicit conversions,
    /// equality semantics and custom JSON serialization.
    /// Serializes to a plain JSON string (e.g. "https://example.com/icon.png").
    /// </summary>
    [Serializable]
    [JsonConverter(typeof(UrlConverter))]
    public sealed class Url : IEquatable<Url>, IEquatable<string>, IComparable<Url>, IComparable<string>
    {
        [SerializeField] private string _value;

        #region Constructors

        public Url()
        {
            _value = string.Empty;
        }

        public Url(string url)
        {
            _value = url ?? string.Empty;
        }

        #endregion

        #region Value Access

        /// <summary>The raw URL string.</summary>
        public string Value
        {
            get => _value;
            set => _value = value ?? string.Empty;
        }

        /// <summary>Returns true when the URL is not null or whitespace.</summary>
        public bool HasValue => !string.IsNullOrWhiteSpace(_value);

        #endregion

        #region Validation

        /// <summary>
        /// Returns true if the value is a well-formed absolute HTTP or HTTPS URI.
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(_value))
                return false;

            return Uri.TryCreate(_value, UriKind.Absolute, out var uri)
                   && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        #endregion

        #region Open in Browser

        /// <summary>
        /// Opens the URL in the default system browser.
        /// </summary>
        public void Open()
        {
            if (IsValid())
                Application.OpenURL(_value);
        }

        #endregion

        #region Implicit / Explicit Conversions

        public static implicit operator string(Url url)
        {
            return url?._value;
        }

        public static implicit operator Url(string value)
        {
            return new Url(value);
        }

        public static explicit operator Uri(Url url)
        {
            if (ReferenceEquals(url, null) || !url.IsValid())
                return null;

            return new Uri(url._value);
        }

        public static explicit operator Url(Uri uri)
        {
            return new Url(uri?.AbsoluteUri);
        }

        #endregion

        #region Equality

        public bool Equals(Url other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(_value, other._value, StringComparison.Ordinal);
        }

        public bool Equals(string other)
        {
            return string.Equals(_value, other, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            if (obj is Url url) return Equals(url);
            if (obj is string str) return Equals(str);
            return false;
        }

        public override int GetHashCode()
        {
            return _value != null ? _value.GetHashCode() : 0;
        }

        public static bool operator ==(Url left, Url right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (ReferenceEquals(left, null) || ReferenceEquals(right, null)) return false;
            return left.Equals(right);
        }

        public static bool operator !=(Url left, Url right)
        {
            return !(left == right);
        }

        public static bool operator ==(Url left, string right)
        {
            if (ReferenceEquals(left, null)) return right == null;
            return left.Equals(right);
        }

        public static bool operator !=(Url left, string right)
        {
            return !(left == right);
        }

        public static bool operator ==(string left, Url right)
        {
            return right == left;
        }

        public static bool operator !=(string left, Url right)
        {
            return !(right == left);
        }

        #endregion

        #region Comparison

        public int CompareTo(Url other)
        {
            if (ReferenceEquals(other, null)) return 1;
            return string.Compare(_value, other._value, StringComparison.Ordinal);
        }

        public int CompareTo(string other)
        {
            return string.Compare(_value, other, StringComparison.Ordinal);
        }

        #endregion

        #region Object Overrides

        public override string ToString()
        {
            return _value ?? string.Empty;
        }

        /// <summary>
        /// Returns a shortened, user-friendly representation of the URL.
        /// Shows the beginning and the end with "…" in the middle when the URL
        /// exceeds <paramref name="maxLength"/> characters.
        /// <para>Example: "https://store.steam…/icon_large.png"</para>
        /// </summary>
        /// <param name="maxLength">
        /// Maximum total length of the returned string (minimum 8).
        /// </param>
        public string ToShortString(int maxLength = 40)
        {
            const string ellipsis = "…";
            const int absoluteMin = 8;

            var text = _value ?? string.Empty;

            if (maxLength < absoluteMin)
                maxLength = absoluteMin;

            if (text.Length <= maxLength)
                return text;

            // Reserve 1 char for the ellipsis; split the rest 60/40 (head gets more)
            var available = maxLength - ellipsis.Length;
            var headLength = (available * 3 + 2) / 5;   // ~60 %
            var tailLength = available - headLength;      // ~40 %

            return string.Concat(text.Substring(0, headLength), ellipsis, text.Substring(text.Length - tailLength));
        }

        #endregion
    }
}



