using System.Collections.Generic;
using System.Globalization;
using SteamToys.Runtime.InventorySystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem.PropertyDrawers
{
    /// <summary>
    /// Drawer for <see cref="PriceValue"/>: one <c>Price</c> row with the amount, the currency and what it is in
    /// dollars, e.g. <c>4.99 PLN ≈ 1.35 $</c>.
    /// </summary>
    [CustomPropertyDrawer(typeof(PriceValue))]
    public class PriceValueDrawer : PropertyDrawer
    {
        private const string LABEL = "Price";
        private const string AMOUNT_FIELD = "_amount";
        private const string CURRENCY_FIELD = "_currency";

        private static readonly Color _previewColor = new(0.6f, 0.6f, 0.6f, 1f);

        // Approximate dollars per unit, only to give a sense of the price; Steam converts nothing by these.
        private static readonly Dictionary<CurrencyType, double> UsdRates = new()
        {
            { CurrencyType.EUR, 1.16 }, { CurrencyType.PLN, 0.27 }, { CurrencyType.GBP, 1.33 },
            { CurrencyType.CHF, 1.25 }, { CurrencyType.NOK, 0.099 }, { CurrencyType.SEK, 0.106 },
            { CurrencyType.DKK, 0.155 }, { CurrencyType.CZK, 0.048 }, { CurrencyType.HUF, 0.003 },
            { CurrencyType.UAH, 0.024 }, { CurrencyType.KZT, 0.0019 }, { CurrencyType.CAD, 0.72 },
            { CurrencyType.AUD, 0.65 }, { CurrencyType.NZD, 0.57 }, { CurrencyType.JPY, 0.0066 },
            { CurrencyType.KRW, 0.0007 }, { CurrencyType.CNY, 0.14 }, { CurrencyType.INR, 0.0113 },
            { CurrencyType.SGD, 0.77 }, { CurrencyType.HKD, 0.1286 }, { CurrencyType.TWD, 0.032 },
            { CurrencyType.BRL, 0.185 }, { CurrencyType.SAR, 0.2667 }, { CurrencyType.AED, 0.2723 },
            { CurrencyType.ZAR, 0.057 }, { CurrencyType.MXN, 0.054 }, { CurrencyType.CLP, 0.00106 },
            { CurrencyType.PEN, 0.295 }, { CurrencyType.COP, 0.00026 }, { CurrencyType.IDR, 0.00006 },
            { CurrencyType.MYR, 0.24 }, { CurrencyType.PHP, 0.017 }, { CurrencyType.THB, 0.031 },
            { CurrencyType.VND, 0.000038 }, { CurrencyType.ILS, 0.3 }
        };

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            // The amount field carries the aligned Price label; the currency and the dollars follow its input.
            var root = new UnsignedIntegerField(LABEL)
            {
                bindingPath = property.FindPropertyRelative(AMOUNT_FIELD).propertyPath,
                tooltip = "Amount in the smallest unit of the currency, e.g. cents: 499 is 4.99."
            };

            root.AddToClassList(BaseField<uint>.alignedFieldUssClassName);

            var currency = new EnumField
            {
                bindingPath = property.FindPropertyRelative(CURRENCY_FIELD).propertyPath,
                style = { width = 100, marginLeft = 4 }
            };

            var dollars = new Label
            {
                style =
                {
                    marginLeft = 6,
                    minWidth = 120,
                    color = _previewColor,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };

            root.Add(currency);
            root.Add(dollars);

            Refresh(property, dollars);
            dollars.TrackPropertyValue(property, changed => Refresh(changed, dollars));

            return root;
        }

        private static void Refresh(SerializedProperty property, Label dollars)
        {
            if (property.boxedValue is not PriceValue price)
            {
                dollars.text = string.Empty;
                return;
            }

            var amount = price.GetFloatAmount();

            dollars.tooltip = $"Sent to Steam as {price.ToSteamString()}.";

            if (IsDollars(price.Currency))
            {
                dollars.text = $"{Format(amount)} $";
                return;
            }

            dollars.text = UsdRates.TryGetValue(price.Currency, out var rate)
                ? $"{Format(amount)} {price.Currency} ≈ {Format(amount * rate)} $"
                : $"{Format(amount)} {price.Currency}";
        }

        // VLV is Valve's price table in dollars; the regional USD currencies are dollars too.
        private static bool IsDollars(CurrencyType currency) =>
            currency is CurrencyType.USD or CurrencyType.VLV or CurrencyType.CIS_USD or CurrencyType.LATAM_USD or
                CurrencyType.MENA_USD;

        private static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
