namespace SteamToys.Runtime.Core
{
    /// <summary>
    /// Who may write a stat or unlock an achievement, matching Set By on the partner site. Steam
    /// enforces it: a client write to a game server stat is refused.
    /// </summary>
    public enum SteamPermission
    {
        /// <summary>The game itself, through <c>SteamUserStats</c>. What almost every game uses.</summary>
        Client = 0,

        /// <summary>A game server, through <c>SteamGameServerStats</c>.</summary>
        GameServer = 1,

        /// <summary>Only a game server Valve has marked as official for the app.</summary>
        OfficialGameServer = 2
    }
}
