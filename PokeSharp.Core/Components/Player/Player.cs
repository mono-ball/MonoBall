namespace PokeSharp.Core.Components.Player;

/// <summary>
///     Tag component identifying an entity as the player.
///     Used for entity queries to find the player entity.
/// </summary>
public struct Player
{
    /// <summary>
    ///     The player's chosen name (e.g., "ASH", "RED").
    /// </summary>
    public string PlayerName;

    /// <summary>
    ///     The player's current money/currency in Pokédollars.
    /// </summary>
    public int Money;

    /// <summary>
    ///     Initializes a new instance of the Player struct with default values.
    /// </summary>
    /// <param name="playerName">The player's name (defaults to "PLAYER").</param>
    /// <param name="money">Initial money amount (defaults to 0).</param>
    public Player(string playerName = "PLAYER", int money = 0)
    {
        PlayerName = playerName ?? "PLAYER";
        Money = money;
    }
}
