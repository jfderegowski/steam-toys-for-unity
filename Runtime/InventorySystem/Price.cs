using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using SteamToys.Runtime.InventorySystem.Converters.Price;
using UnityEngine;

namespace SteamToys.Runtime.InventorySystem
{
    [JsonConverter(typeof(CurrencyTypeConverter))]
    public enum CurrencyType
    {
        /// <summary>Używa tabeli cenowej Valve (np. VLV100)</summary>
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
            if (Currency is CurrencyType.JPY or CurrencyType.KRW or CurrencyType.VND)
                return Amount;

            return Amount / 100f;
        }

        public string ToSteamString()
        {
            // Steam expects price tokens like "USD499" or "JPY500" or "VLV100".
            // The repo stores amounts as integer minor units for most currencies (e.g. cents),
            // and whole units for zero-decimal currencies (JPY/KRW/VND). We therefore
            // concatenate the currency token (enum name) with the raw Amount value.
            var token = Currency.ToString();
            return token + Amount.ToString(CultureInfo.InvariantCulture);
        }
    }

    [Serializable]
    [JsonConverter(typeof(PromotionConverter))]
    public class Promotion
    {
        [SerializeField] private SteamDateTime _startTime;
        [SerializeField] private SteamDateTime _endTime;
        [SerializeField] private List<PriceValue> _discountedPrices = new();

        public string ToSteamString()
        {
            // Build discounted prices part (e.g. "USD399,EUR349")
            var pricesPart = _discountedPrices is { Count: > 0 }
                ? string.Join(",", _discountedPrices.ConvertAll(p => p.ToSteamString()))
                : string.Empty;

            // Build time range part using SteamDateTime.ToSteamString() which returns empty when not set
            var start = _startTime.ToSteamString();
            var end = _endTime.ToSteamString();
            var timePart = string.Empty;

            if (!string.IsNullOrEmpty(start) || !string.IsNullOrEmpty(end))
                timePart = $"{start}-{end}";

            if (!string.IsNullOrEmpty(timePart) && !string.IsNullOrEmpty(pricesPart))
                return $"{timePart}{pricesPart}";

            if (!string.IsNullOrEmpty(pricesPart))
                return pricesPart;

            if (!string.IsNullOrEmpty(timePart))
                return timePart;

            return string.Empty;
        }
    }
    
    [Serializable]
    [JsonConverter(typeof(PriceConverter))]
    public class Price
    {
        public List<PriceValue> RegularPrices => GetRegularPrices();
        public List<Promotion> Promotions => GetPromotions();
        
        private const string VERSION_PREFIX = "1;";

        [SerializeField] private List<PriceValue> _regularPrices = new();
        [SerializeField] private List<Promotion> _promotions = new();
        
        public virtual List<PriceValue> GetRegularPrices() => _regularPrices;
        
        public virtual List<Promotion> GetPromotions() => _promotions;

        /// <summary>
        /// Converts the price to the Steam format <c>"1;&lt;original price&gt;(;&lt;sale&gt;)*"</c>,
        /// e.g. <c>"1;USD100,EUR080;20130607T080000Z-20130606T080000ZUSD50,EUR40"</c>.
        /// Returns an empty string when no price is set.
        /// </summary>
        public string ToSteamString()
        {
            // Regular prices part (e.g. "USD499,EUR499")
            var regularPart = _regularPrices is { Count: > 0 }
                ? string.Join(",", _regularPrices.ConvertAll(p => p.ToSteamString()))
                : string.Empty;

            // Promotions part: one sale per entry, joined with ';' (e.g. "start-endUSD399;start2-end2USD349")
            var promotionsPart = _promotions is { Count: > 0 }
                ? string.Join(";", _promotions.ConvertAll(pr => pr.ToSteamString()))
                : string.Empty;

            if (string.IsNullOrEmpty(regularPart) && string.IsNullOrEmpty(promotionsPart))
                return string.Empty;

            // Steam requires the version prefix on every price; the sales follow the original price.
            var result = VERSION_PREFIX + regularPart;

            if (!string.IsNullOrEmpty(promotionsPart))
                result += ";" + promotionsPart;

            return result;
        }
    }
}