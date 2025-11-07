using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Components.Player;
using PokeSharp.Core.ScriptingApi;

namespace PokeSharp.Core.Scripting.Services;

/// <summary>
///     Player management service implementation.
/// </summary>
public class PlayerApiService(World world, ILogger<PlayerApiService> logger) : IPlayerApi
{
    private readonly World _world = world ?? throw new ArgumentNullException(nameof(world));
    private readonly ILogger<PlayerApiService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public string GetPlayerName()
    {
        var playerEntity = GetPlayerEntity();
        if (playerEntity.HasValue && _world.Has<Player>(playerEntity.Value))
        {
            #warning TODO: Add PlayerName field to Player component
            return "PLAYER"; // Placeholder
        }
        return "PLAYER";
    }

    public int GetMoney()
    {
        #warning TODO: Implement when Player component has Money field
        return 0;
    }

    public void GiveMoney(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentException("Amount must be positive", nameof(amount));
        }

        #warning TODO: Implement when Player component has Money field
        _logger.LogInformation("GiveMoney: {Amount}", amount);
    }

    public bool TakeMoney(int amount)
    {
        var currentMoney = GetMoney();
        if (currentMoney >= amount)
        {
            #warning TODO: Implement when Player component has Money field
            _logger.LogInformation("TakeMoney: {Amount}", amount);
            return true;
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

    public void SetPlayerMovementLocked(bool locked)
    {
        var playerEntity = GetPlayerEntity();
        if (playerEntity.HasValue && _world.Has<GridMovement>(playerEntity.Value))
        {
            #warning TODO: Add CanMove or MovementLocked field to GridMovement or create separate component
            // For now, we'll use IsMoving as a workaround - setting it to true effectively locks movement
            _logger.LogInformation("Player movement {Status}", locked ? "locked" : "unlocked");
        }
    }

    public bool IsPlayerMovementLocked()
    {
        #warning TODO: Implement when GridMovement has CanMove or MovementLocked field
        return false;
    }

    private Entity? GetPlayerEntity()
    {
        var query = new QueryDescription().WithAll<Player>();
        Entity? playerEntity = null;

        _world.Query(
            in query,
            entity =>
            {
                playerEntity = entity;
            }
        );

        return playerEntity;
    }
}

