using Steamworks;
using UnityEngine;

namespace SteamToys.Runtime.StatsSystem
{
    /// <summary>
    /// A decimal stat, the <c>STAT_FLOAT</c> kind on the partner site, e.g. "FeetTraveled" or
    /// "MaxFeetTraveled". Resolves the <c>float</c> overloads of <c>GetStat</c> and
    /// <c>SetStat</c>.
    /// <see href="https://partner.steamgames.com/doc/features/achievements/stats_guide"/>
    /// </summary>
    [CreateAssetMenu(fileName = "New Float Stat", menuName = "SteamToys/Stats/Float Stat", order = 1)]
    public class FloatStat : SteamStat<float>
    {
        public override SteamStatType StatType => SteamStatType.Float;

        /// <summary>
        /// Moves the value by <paramref name="amount"/>. Returns false when the mirrored
        /// constraints turned the change down or it did not reach Steam.
        /// </summary>
        public bool Add(float amount) => TrySetValue(GetValue() + amount);

        /// <summary>
        /// Raises the value to <paramref name="value"/> only when it is higher than the current
        /// one, for the "max something" stats such as "MaxFeetTraveled".
        /// </summary>
        public bool RaiseTo(float value) => value > GetValue() && TrySetValue(value);

        protected override bool TryGetFromSteam(out float value) => SteamUserStats.GetStat(ApiName, out value);

        protected override bool TryWriteToSteam() => SteamUserStats.SetStat(ApiName, RuntimeValue);

        protected override bool ValidateChange(float current, float desired, out float corrected)
        {
            corrected = desired;

            // A non finite value would travel to the Steam servers and corrupt the stat there, so
            // it is stopped here rather than clamped into something arbitrary.
            if (float.IsNaN(desired) || float.IsInfinity(desired))
            {
                LogRejectedChange(current, desired, "the value is not a finite number");

                return false;
            }

            if (IncrementOnly && desired < current)
            {
                LogRejectedChange(current, desired, "the stat is increment only");

                return false;
            }

            if (MinValue.hasValue && corrected < MinValue.value)
                corrected = MinValue.value;

            if (MaxValue.hasValue && corrected > MaxValue.value)
                corrected = MaxValue.value;

            if (!MaxChange.hasValue || MaxChange.value <= 0f)
                return true;

            var change = corrected - current;

            if (change > MaxChange.value)
                corrected = current + MaxChange.value;
            else if (change < -MaxChange.value)
                corrected = current - MaxChange.value;

            return true;
        }
    }
}
