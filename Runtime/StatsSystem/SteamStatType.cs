namespace SteamToys.Runtime.StatsSystem
{
    /// <summary>
    /// The stat kinds offered by the Stats Configuration page on the Steamworks partner site.
    /// <see href="https://partner.steamgames.com/doc/features/achievements/stats_guide"/>
    /// </summary>
    public enum SteamStatType
    {
        /// <summary>An integer counter, e.g. "NumGames" or "NumWins". Written with SetStat.</summary>
        Int,

        /// <summary>A decimal measurement, e.g. "FeetTraveled". Written with SetStat.</summary>
        Float,

        /// <summary>
        /// A rolling average maintained by Steam, e.g. "FeetTraveledPerHour".
        /// Fed with UpdateAvgRateStat rather than written directly.
        /// </summary>
        AvgRate
    }
}
