using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Components.Player;
using PokeSharp.Core.ScriptingApi;

namespace PokeSharp.Core.Scripting.Services;

/// <summary>
///     Player management service implementation.
/// </summary>
public class PlayerApiService(World world, ILogger<PlayerApiService> logger) : IPlayerApi
{
    private readonly ILogger<PlayerApiService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly World _world = world ?? throw new ArgumentNullException(nameof(world));

    /// <summary>
    ///     Gets the player's chosen name.
    /// </summary>
    /// <returns>Player name (e.g., "ASH", "RED"), or "PLAYER" if not found.</returns>
    public string GetPlayerName()
    {
        var playerEntity = GetPlayerEntity();
        if (playerEntity.HasValue && _world.Has<Player>(playerEntity.Value))
        {
            ref var player = ref _world.Get<Player>(playerEntity.Value);
            return string.IsNullOrWhiteSpace(player.PlayerName) ? "PLAYER" : player.PlayerName;
        }

        _logger.LogWarning("Player entity not found when getting player name");
        return "PLAYER";
    }

    /// <summary>
    ///     Gets the player's current money balance.
    /// </summary>
    /// <returns>Money in Pokédollars, or 0 if player not found.</returns>
    public int GetMoney()
    {
        var playerEntity = GetPlayerEntity();
        if (playerEntity.HasValue && _world.Has<Player>(playerEntity.Value))
        {
            ref var player = ref _world.Get<Player>(playerEntity.Value);
            return player.Money;
        }

        _logger.LogWarning("Player entity not found when getting money");
        return 0;
    }

    public void GiveMoney(int amount)
    {
        if (amount < 0) throw new ArgumentException("Amount must be positive", nameof(amount));

        var playerEntity = GetPlayerEntity();
        if (playerEntity.HasValue && _world.Has<Player>(playerEntity.Value))
        {
            ref var player = ref _world.Get<Player>(playerEntity.Value);
            player.Money += amount;
            _logger.LogInformation(
                "Gave {Amount} money to player. New balance: {Balance}",
                amount,
                player.Money
            );
        }
    }

    public bool TakeMoney(int amount)
    {
        if (amount < 0) throw new ArgumentException("Amount must be positive", nameof(amount));

        var playerEntity = GetPlayerEntity();
        if (playerEntity.HasValue && _world.Has<Player>(playerEntity.Value))
        {
            ref var player = ref _world.Get<Player>(playerEntity.Value);
            if (player.Money >= amount)
            {
                player.Money -= amount;
                _logger.LogInformation(
                    "Took {Amount} money from player. New balance: {Balance}",
                    amount,
                    player.Money
                );
                return true;
            }
        }

        return false;
    }

    public bool HasMoney(int amount)
    {
        return GetMoney() >= amount;
    }

    public Point GetPlayerPosition()
    {
        var playerEntity = GetPlayerEntity();
        if (playerEntity.HasValue && _world.Has<Position>(playerEntity.Value))
        {
            ref var position = ref _world.Get<Position>(playerEntity.Value);
            return new Point(position.X, position.Y);
        }

        return Point.Zero;
    }

    public Direction GetPlayerFacing()
    {
        var playerEntity = GetPlayerEntity();
        if (playerEntity.HasValue && _world.Has<GridMovement>(playerEntity.Value))
        {
            ref var movement = ref _world.Get<GridMovement>(playerEntity.Value);
            return movement.FacingDirection;
        }

        return Direction.None;
    }

    public void SetPlayerFacing(Direction direction)
    {
        var playerEntity = GetPlayerEntity();
        if (playerEntity.HasValue && _world.Has<GridMovement>(playerEntity.Value))
        {
            ref var movement = ref _world.Get<GridMovement>(playerEntity.Value);
            movement.FacingDirection = direction;
            _logger.LogDebug("Player facing set to: {Direction}", direction);
        }
    }

    /// <summary>
    ///     Locks or unlocks player movement (used during cutscenes, battles, and dialogue).
    /// </summary>
    /// <param name="locked">True to lock movement, false to unlock.</param>
    public void SetPlayerMovementLocked(bool locked)
    {
        var playerEntity = GetPlayerEntity();
        if (playerEntity.HasValue && _world.Has<GridMovement>(playerEntity.Value))
        {
            ref var movement = ref _world.Get<GridMovement>(playerEntity.Value);
            movement.MovementLocked = locked;
            _logger.LogInformation("Player movement {Status}", locked ? "locked" : "unlocked");
        }
        else
        {
            _logger.LogWarning("Cannot lock player movement: GridMovement component not found");
        }
    }

    /// <summary>
    ///     Checks if the player's movement is currently locked.
    /// </summary>
    /// <returns>True if player cannot move, false otherwise.</returns>
    public bool IsPlayerMovementLocked()
    {
        var playerEntity = GetPlayerEntity();
        if (playerEntity.HasValue && _world.Has<GridMovement>(playerEntity.Value))
        {
            ref var movement = ref _world.Get<GridMovement>(playerEntity.Value);
            return movement.MovementLocked;
        }

        _logger.LogWarning("Cannot check movement lock: GridMovement component not found");
        return false;
    }

    private Entity? GetPlayerEntity()
    {
        var query = new QueryDescription().WithAll<Player>();
        Entity? playerEntity = null;

        _world.Query(
            in query,
            entity => { playerEntity = entity; }
        );

        return playerEntity;
    }
}