using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components.Common;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Components.Player;
using PokeSharp.Core.Logging;
using PokeSharp.Core.ScriptingApi;
using PokeSharp.Core.Systems;

namespace PokeSharp.Core.Scripting.Services;

/// <summary>
///     Player management service implementation.
/// </summary>
public class PlayerApiService(World world, ILogger<PlayerApiService> logger) : IPlayerApi
{
    private readonly ILogger<PlayerApiService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly World _world = world ?? throw new ArgumentNullException(nameof(world));
    private const string DefaultPlayerName = "PLAYER";

    /// <summary>
    ///     Gets the player's chosen name.
    /// </summary>
    /// <returns>Player name (e.g., "ASH", "RED"), or "PLAYER" if not found.</returns>
    public string GetPlayerName()
    {
        var playerEntity = GetPlayerEntity();
        if (playerEntity.HasValue)
        {
            if (_world.Has<Name>(playerEntity.Value))
            {
                ref var name = ref _world.Get<Name>(playerEntity.Value);
                return string.IsNullOrWhiteSpace(name.DisplayName)
                    ? DefaultPlayerName
                    : name.DisplayName;
            }

            _logger.LogEntityMissingComponent("Player", "Name", "retrieve player name");
        }
        else
        {
            _logger.LogEntityNotFound("Player", "retrieve player name");
        }

        return DefaultPlayerName;
    }

    /// <summary>
    ///     Gets the player's current money balance.
    /// </summary>
    /// <returns>Money in Pokédollars, or 0 if player not found.</returns>
    public int GetMoney()
    {
        var playerEntity = GetPlayerEntity();
        if (playerEntity.HasValue)
        {
            if (_world.Has<Wallet>(playerEntity.Value))
            {
                ref var wallet = ref _world.Get<Wallet>(playerEntity.Value);
                return wallet.Balance;
            }

            _logger.LogEntityMissingComponent("Player", "Wallet", "retrieve balance");
        }
        else
        {
            _logger.LogEntityNotFound("Player", "retrieve balance");
        }

        return 0;
    }

    public void GiveMoney(int amount)
    {
        if (amount < 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));

        var playerEntity = GetPlayerEntity();
        if (playerEntity.HasValue)
        {
            if (_world.Has<Wallet>(playerEntity.Value))
            {
                ref var wallet = ref _world.Get<Wallet>(playerEntity.Value);
                wallet.Balance += amount;
                _logger.LogInformation(
                    "Gave {Amount} money to player. New balance: {Balance}",
                    amount,
                    wallet.Balance
                );
            }
            else
            {
                _logger.LogEntityMissingComponent("Player", "Wallet", "receive funds");
            }
        }
        else
        {
            _logger.LogEntityNotFound("Player", "receive funds");
        }
    }

    public bool TakeMoney(int amount)
    {
        if (amount < 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));

        var playerEntity = GetPlayerEntity();
        if (playerEntity.HasValue)
        {
            if (_world.Has<Wallet>(playerEntity.Value))
            {
                ref var wallet = ref _world.Get<Wallet>(playerEntity.Value);
                if (wallet.Balance >= amount)
                {
                    wallet.Balance -= amount;
                    _logger.LogInformation(
                        "Took {Amount} money from player. New balance: {Balance}",
                        amount,
                        wallet.Balance
                    );
                    return true;
                }
            }
            else
            {
                _logger.LogEntityMissingComponent("Player", "Wallet", "deduct funds");
            }
        }
        else
        {
            _logger.LogEntityNotFound("Player", "deduct funds");
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
            _logger.LogEntityMissingComponent("Player", "GridMovement", "lock movement");
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

        _logger.LogEntityMissingComponent("Player", "GridMovement", "check movement lock");
        return false;
    }

    private Entity? GetPlayerEntity()
    {
        Entity? playerEntity = null;

        // Use centralized query for Player
        _world.Query(
            in Queries.Queries.Player,
            entity =>
            {
                playerEntity = entity;
            }
        );

        return playerEntity;
    }
}
