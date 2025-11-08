using Microsoft.Xna.Framework;
using PokeSharp.Core.Components.Movement;

namespace PokeSharp.Core.ScriptingApi;

/// <summary>
///     Player management API for scripts.
///     Provides access to player state, inventory, and control.
/// </summary>
public interface IPlayerApi
{
    /// <summary>
    ///     Gets the player's chosen name (set at game start).
    /// </summary>
    /// <returns>Player name (e.g., "ASH", "RED").</returns>
    string GetPlayerName();

    /// <summary>
    ///     Gets the player's current money balance.
    /// </summary>
    /// <returns>Money in Pokédollars (¥).</returns>
    int GetMoney();

    /// <summary>
    ///     Gives money to the player (e.g., battle prize, quest reward).
    /// </summary>
    /// <param name="amount">Amount in Pokédollars (must be positive).</param>
    /// <exception cref="ArgumentException">If amount is negative.</exception>
    void GiveMoney(int amount);

    /// <summary>
    ///     Takes money from the player (e.g., shop purchase).
    /// </summary>
    /// <param name="amount">Amount in Pokédollars.</param>
    /// <returns>True if player had enough money, false otherwise.</returns>
    bool TakeMoney(int amount);

    /// <summary>
    ///     Checks if the player has at least the specified amount of money.
    /// </summary>
    /// <param name="amount">Amount to check.</param>
    /// <returns>True if player has enough money.</returns>
    bool HasMoney(int amount);

    /// <summary>
    ///     Gets the player's current grid position on the active map.
    /// </summary>
    /// <returns>Tile coordinates (X, Y).</returns>
    Point GetPlayerPosition();

    /// <summary>
    ///     Gets the direction the player is currently facing.
    /// </summary>
    /// <returns>Direction enum (North, South, East, West).</returns>
    Direction GetPlayerFacing();

    /// <summary>
    ///     Sets the player's facing direction without moving.
    /// </summary>
    /// <param name="direction">New facing direction.</param>
    void SetPlayerFacing(Direction direction);

    /// <summary>
    ///     Locks or unlocks player movement.
    ///     Used during cutscenes, trainer battles, and dialogue.
    /// </summary>
    /// <param name="locked">True to lock movement, false to unlock.</param>
    void SetPlayerMovementLocked(bool locked);

    /// <summary>
    ///     Checks if the player's movement is currently locked.
    /// </summary>
    /// <returns>True if player cannot move.</returns>
    bool IsPlayerMovementLocked();
}
