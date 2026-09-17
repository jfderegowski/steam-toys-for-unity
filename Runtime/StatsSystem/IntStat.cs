using System;
using Steamworks;
using UnityEngine;

namespace SteamToys.Runtime.StatsSystem
{
    /// <summary>
    /// An integer stat, the <c>STAT_INT</c> kind on the partner site, e.g. "NumGames" or
    /// "NumWins". Resolves the <c>int</c> overloads of <c>GetStat</c> and <c>SetStat</c>.
    /// <see href="https://partner.steamgames.com/doc/features/achievements/stats_guide"/>
    /// </summary>
    [CreateAssetMenu(fileName = "New Int Stat", menuName = "SteamToys/Stats/Int Stat", order = 0)]
    public class IntStat : SteamStat<int>
    {
        public override SteamStatType StatType => SteamStatType.Int;

        /// <summary>
        /// Moves the value by <paramref name="amount"/>. Returns false when the mirrored
        /// constraints turned the change down or it did not reach Steam.
        /// </summary>
        public bool Add(int amount) =>
            TrySetValue((int)Math.Clamp((long)GetValue() + amount, int.MinValue, int.MaxValue));

        /// <summary>Moves the value up by one, the common case for a counter.</summary>
        public bool Increment() => Add(1);

        protected override bool TryGetFromSteam(out int value) => SteamUserStats.GetStat(ApiName, out value);

        protected override bool TryWriteToSteam() => SteamUserStats.SetStat(ApiName, RuntimeValue);

        protected override bool ValidateChange(int current, int desired, out int corrected)
        {
            corrected = desired;

            if (IncrementOnly && desired < current)
            {
                LogRejectedChange(current, desired, "the stat is increment only");

                return false;
            }

            if (MinValue.hasValue && corrected < MinValue.value)
                corrected = MinValue.value;

            if (MaxValue.hasValue && corrected > MaxValue.value)
                corrected = MaxValue.value;

            if (!MaxChange.hasValue || MaxChange.value <= 0)
                return true;

            // Widened to long so clamping a very large requested jump cannot overflow on the way.
            var change = (long)corrected - current;

            if (change > MaxChange.value)
                corrected = (int)Math.Min((long)current + MaxChange.value, int.MaxValue);
            else if (change < -MaxChange.value)
                corrected = (int)Math.Max((long)current - MaxChange.value, int.MinValue);

            return true;
        }

        [ContextMenu("Sync")]
        public void Sync()
        {
            TryPullFromSteam();
        }
    }
}
