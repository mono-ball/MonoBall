using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.ScriptingApi;

namespace PokeSharp.Core.Scripting.Services;

/// <summary>
///     NPC management service implementation.
/// </summary>
public class NpcApiService(World world, ILogger<NpcApiService> logger) : INPCApi
{
    private readonly World _world = world ?? throw new ArgumentNullException(nameof(world));
    private readonly ILogger<NpcApiService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public void MoveNPC(Entity npc, Direction direction)
    {
        if (!_world.IsAlive(npc))
        {
            _logger.LogWarning("Attempted to move dead/invalid NPC entity");
            return;
        }

        if (_world.Has<MovementRequest>(npc))
        {
            ref var movement = ref _world.Get<MovementRequest>(npc);
            movement.Direction = direction;
            movement.Processed = false;
        }
        else
        {
            // Add MovementRequest component if it doesn't exist
            _world.Add(npc, new MovementRequest(direction));
        }
    }

    public void FaceDirection(Entity npc, Direction direction)
    {
        if (!_world.IsAlive(npc))
        {
            _logger.LogWarning("Attempted to face dead/invalid NPC entity");
            return;
        }

        if (_world.Has<GridMovement>(npc))
        {
            ref var movement = ref _world.Get<GridMovement>(npc);
            movement.FacingDirection = direction;
        }
    }

    public void FaceEntity(Entity npc, Entity target)
    {
        if (!_world.IsAlive(npc) || !_world.IsAlive(target))
        {
            _logger.LogWarning("Attempted to face with dead/invalid entities");
            return;
        }

        if (!_world.Has<Position>(npc) || !_world.Has<Position>(target))
        {
            _logger.LogWarning("Entities missing Position component for facing");
            return;
        }

        ref var npcPos = ref _world.Get<Position>(npc);
        ref var targetPos = ref _world.Get<Position>(target);

        var dx = targetPos.X - npcPos.X;
        var dy = targetPos.Y - npcPos.Y;

        Direction direction;
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            direction = dx > 0 ? Direction.Right : Direction.Left;
        }
        else if (Math.Abs(dy) > 0)
        {
            direction = dy > 0 ? Direction.Down : Direction.Up;
        }
        else
        {
            direction = Direction.Down; // Default if at same position
        }

        FaceDirection(npc, direction);
    }

    public Point GetNPCPosition(Entity npc)
    {
        if (!_world.IsAlive(npc))
        {
            return Point.Zero;
        }

        if (_world.Has<Position>(npc))
        {
            ref var position = ref _world.Get<Position>(npc);
            return new Point(position.X, position.Y);
        }

        return Point.Zero;
    }

    public void SetNPCPath(Entity npc, Point[] waypoints, bool loop)
    {
        if (!_world.IsAlive(npc))
        {
            _logger.LogWarning("Attempted to set path on dead/invalid NPC entity");
            return;
        }

        #warning TODO: Implement when PathComponent is created
        _logger.LogInformation(
            "SetNPCPath called for entity {Entity} with {Count} waypoints",
            npc.Id,
            waypoints.Length
        );
    }

    public bool IsNPCMoving(Entity npc)
    {
        if (!_world.IsAlive(npc))
        {
            return false;
        }

        if (_world.Has<GridMovement>(npc))
        {
            ref var movement = ref _world.Get<GridMovement>(npc);
            return movement.IsMoving;
        }

        return false;
    }

    public void StopNPC(Entity npc)
    {
        if (!_world.IsAlive(npc))
        {
            return;
        }

        if (_world.Has<GridMovement>(npc))
        {
            ref var movement = ref _world.Get<GridMovement>(npc);
            movement.CompleteMovement();
        }

        // Clear any pending movement requests
        if (_world.Has<MovementRequest>(npc))
        {
            _world.Remove<MovementRequest>(npc);
        }
    }
}

