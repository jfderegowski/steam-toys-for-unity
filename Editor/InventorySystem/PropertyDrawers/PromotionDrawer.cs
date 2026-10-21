using System.Collections.Generic;
using System.Globalization;
using fefek5.Toys.Editor.VisualElements;
using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    /// <summary>
    /// Drawer for <see cref="Promotion"/>: a foldout of its fields whose header tells when it runs and at what prices,
    /// e.g. <c>2026-11-01 → 2026-11-08: 3.99 USD, 3.49 EUR</c>. In a list that is the whole header; as a field it
    /// follows the name of the field.
    /// </summary>
    [CustomPropertyDrawer(typeof(Promotion))]
    public class PromotionDrawer : PropertyDrawer
    {
        private const string START_TIME_FIELD = "_startTime";
        private const string END_TIME_FIELD = "_endTime";
        private const string DISCOUNTED_PRICES_FIELD = "_discountedPrices";
        private const string DATE_FORMAT = "yyyy-MM-dd HH:mm";
        private const string NO_DATE = "…";

        private static readonly Color _summaryColor = new(0.6f, 0.6f, 0.6f, 1f);

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var isListElement = property.propertyPath.EndsWith("]");

            var summary = new Label { style = { color = _summaryColor, marginLeft = 8 } };
            var foldout = isListElement ? new FoldoutElement(property) : new FoldoutElement(property, summary);

            if (!isListElement)
                foldout.text = preferredLabel;

            Refresh(property);
            foldout.TrackPropertyValue(property, Refresh);

            return foldout;

            void Refresh(SerializedProperty changed)
            {
                if (isListElement)
                    foldout.text = GetSummary(changed);
                else
                    summary.text = GetSummary(changed);
            }
        }

        private static string GetSummary(SerializedProperty property)
        {
            var start = FormatDate(property.FindPropertyRelative(START_TIME_FIELD));
            var end = FormatDate(property.FindPropertyRelative(END_TIME_FIELD));
            var prices = property.FindPropertyRelative(DISCOUNTED_PRICES_FIELD);
            var parts = new List<string>();

            for (var i = 0; i < prices.arraySize; i++)
            {
                if (prices.GetArrayElementAtIndex(i).boxedValue is PriceValue price)
                    parts.Add($"{price.GetFloatAmount().ToString("0.##", CultureInfo.InvariantCulture)} {price.Currency}");
            }

            var pricesText = parts.Count > 0 ? string.Join(", ", parts) : "no prices";

            return $"{start} → {end}: {pricesText}";
        }

        private static string FormatDate(SerializedProperty property) =>
            property.boxedValue is SteamDateTime { HasValue: true } dateTime
                ? dateTime.UtcDateTime.ToString(DATE_FORMAT, CultureInfo.InvariantCulture)
                : NO_DATE;
    }
}
