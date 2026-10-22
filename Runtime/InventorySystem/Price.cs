using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.Converters.Price;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    [JsonConverter(typeof(CurrencyTypeConverter))]
    public enum CurrencyType
    {
        /// <summary>
        /// Używa tabeli cenowej Valve (np. VLV100)
        /// </summary>
        VLV,

        // Główne waluty
        USD, // Dolar amerykański
        EUR, // Euro
        PLN, // Złoty polski
        GBP, // Funt brytyjski
    
        // Europa i kraje sąsiednie
        CHF, // Frank szwajcarski
        NOK, // Korona norweska
        SEK, // Korona szwedzka
        DKK, // Korona duńska
        CZK, // Korona czeska
        HUF, // Forint węgierski
        UAH, // Hrywna ukraińska
        KZT, // Tenge kazachskie
    
        // Ameryka i Azja
        CAD, // Dolar kanadyjski
        AUD, // Dolar australijski
        NZD, // Dolar nowozelandzki
        JPY, // Jen japoński (Waluta bez centów!)
        KRW, // Won południowokoreański (Waluta bez centów!)
        CNY, // Yuan chiński
        INR, // Rupia indyjska
        SGD, // Dolar singapurski
        HKD, // Dolar hongkoński
        TWD, // Dolar tajwański
    
        // Inne ważne rynki
        BRL, // Real brazylijski
        SAR, // Rial saudyjski
        AED, // Dirham ZEA
        ZAR, // Rand południowoafrykański
        MXN, // Peso meksykańskie
        CLP, // Peso chilijskie
        PEN, // Sol peruwiański
        COP, // Peso kolumbijskie
        IDR, // Rupia indonezyjska
        MYR, // Ringgit malezyjski
        PHP, // Peso filipińskie
        THB, // Baht tajski
        VND, // Dong wietnamski
        ILS, // Szekel izraelski
    
        // Specjalne regiony USD (zastąpiły waluty lokalne w 2023)
        CIS_USD,  // Wspólnota Niepodległych Państw - Dolar
        LATAM_USD, // Ameryka Łacińska - Dolar
        MENA_USD   // Bliski Wschód i Afryka Północna - Dolar
    }
    
    [Serializable]
    [JsonConverter(typeof(PriceValueConverter))]
    public class PriceValue
    {
        public CurrencyType Currency => GetCurrency();
        public uint Amount => GetAmount();
        
        [SerializeField] private CurrencyType _currency;
        [SerializeField] private uint _amount;
        
        public virtual CurrencyType GetCurrency()
        {
            return _currency;
        }
        
        public virtual uint GetAmount()
        {
            return _amount;
        }

        public float GetFloatAmount()
        {
            if (IsZeroDecimal(Currency))
                return Amount;

            return Amount / 100f;
        }

        /// <summary>
        /// About how many dollars this is, by <see cref="UsdRates"/>; null for a currency it has no rate for. A
        /// <c>VLV</c> amount is a tier of Valve's table: VLV100 is $0.99, VLV25 and VLV75 are $0.25 and $0.75.
        /// </summary>
        public double? GetUsdAmount()
        {
            if (Currency == CurrencyType.VLV)
                return Amount >= 50 && Amount % 50 == 0 ? Amount / 100.0 - 0.01 : Amount / 100.0;

            if (IsDollars(Currency))
                return GetFloatAmount();

            return UsdRates.TryGetValue(Currency, out var rate) ? GetFloatAmount() * rate : null;
        }

        /// <summary>
        /// The price as the inspector shows it: dollars as <c>4.99 $</c> (VLV too), any other currency as
        /// <c>14.99 PLN ≈ 4.05 $</c>, or <c>14.99 PLN</c> without a rate.
        /// </summary>
        public override string ToString()
        {
            var usd = GetUsdAmount();
            var dollars = usd.HasValue ? $"{usd.Value.ToString("0.00", CultureInfo.InvariantCulture)} $" : null;

            if (Currency == CurrencyType.VLV || IsDollars(Currency))
                return dollars;

            var amount = GetFloatAmount().ToString(IsZeroDecimal(Currency) ? "0" : "0.00", CultureInfo.InvariantCulture);

            return dollars == null ? $"{amount} {Currency}" : $"{amount} {Currency} ≈ {dollars}";
        }

        // The regional USD currencies are dollars too.
        private static bool IsDollars(CurrencyType currency) =>
            currency is CurrencyType.USD or CurrencyType.CIS_USD or CurrencyType.LATAM_USD or CurrencyType.MENA_USD;

        private static bool IsZeroDecimal(CurrencyType currency) =>
            currency is CurrencyType.JPY or CurrencyType.KRW or CurrencyType.VND;

        // Approximate dollars per unit, only to give a sense of a price; Steam converts nothing by these.
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
    }

    [Serializable]
    [JsonConverter(typeof(PromotionConverter))]
    public class Promotion
    {
        public SteamDateTime StartTime => GetStartTime();
        public SteamDateTime EndTime => GetEndTime();
        public List<PriceValue> DiscountedPrices => GetDiscountedPrices();

        [SerializeField] private SteamDateTime _startTime;
        [SerializeField] private SteamDateTime _endTime;
        [SerializeField] private List<PriceValue> _discountedPrices = new();

        public virtual SteamDateTime GetStartTime() => _startTime;

        public virtual SteamDateTime GetEndTime() => _endTime;

        public virtual List<PriceValue> GetDiscountedPrices() => _discountedPrices;

        /// <summary>When the sale runs and at what prices, e.g. <c>2026-11-01 08:00 → 2026-11-08 20:00: 2.99 $</c>.</summary>
        public override string ToString()
        {
            var prices = DiscountedPrices is { Count: > 0 }
                ? string.Join(", ", DiscountedPrices.Where(price => price != null))
                : "no prices";

            return $"{StartTime?.ToString() ?? "—"} → {EndTime?.ToString() ?? "—"}: {prices}";
        }
    }
    
    [Serializable]
    [JsonConverter(typeof(PriceConverter))]
    public class Price
    {
        public List<PriceValue> RegularPrices => GetRegularPrices();
        public List<Promotion> Promotions => GetPromotions();

        [SerializeField] private List<PriceValue> _regularPrices = new();
        [SerializeField] private List<Promotion> _promotions = new();

        public virtual List<PriceValue> GetRegularPrices() => _regularPrices;

        public virtual List<Promotion> GetPromotions() => _promotions;
    }
}