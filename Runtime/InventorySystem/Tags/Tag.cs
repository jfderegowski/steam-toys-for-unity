using System;
using SteamToys.Runtime.InventorySystem.Converters;

namespace SteamToys.Runtime.InventorySystem
{
    [Serializable]
    public abstract class Tag
    {
        public abstract string GetName();

        public abstract string GetValue();

        public abstract bool SetValue(string value);

        public abstract Type GetValueType();
    }

    [Serializable]
    public class Tag<TEnum> : Tag where TEnum : Enum
    {
        public TEnum Value;

        public sealed override Type GetValueType() => typeof(TEnum);

        public override string GetName() => typeof(TEnum).Name.ToSnakeCase();

        public override string GetValue() => Value.ToString().ToSnakeCase();

        public void SetValue(TEnum value) => Value = value;

        public override bool SetValue(string value)
        {
            if (!TryParse(value, out var parsed))
                return false;

            Value = parsed;
            return true;
        }

        private static bool TryParse(string value, out TEnum result)
        {
            result = default;

            if (string.IsNullOrEmpty(value))
                return false;

            var normalized = value.ToSnakeCase();

            foreach (var name in Enum.GetNames(typeof(TEnum)))
            {
                if (name.ToSnakeCase() != normalized)
                    continue;

                result = (TEnum)Enum.Parse(typeof(TEnum), name);
                return true;
            }

            return false;
        }
    }
}