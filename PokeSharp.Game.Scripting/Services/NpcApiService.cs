using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.NPCs;
using PokeSharp.Game.Scripting.Api;

namespace PokeSharp.Game.Scripting.Services;

/// <summary>
///     NPC management service implementation.
/// </summary>
public class NpcApiService(World world, ILogger<NpcApiService> logger) : INPCApi
{
    private readonly ILogger<NpcApiService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly World _world = world ?? throw new ArgumentNullException(nameof(world));

    public void MoveNPC(Entity npc, Direction direction)
    {
        if (!_world.IsAlive(npc))
        {
            _logger.LogEntityOperationInvalid("NPC", "move", "entity is dead or invalid");
            return;
        }

        if (_world.Has<MovementRequest>(npc))
        {
            // Reuse existing component (component pooling)
            ref MovementRequest movement = ref _world.Get<MovementRequest>(npc);
            movement.Direction = direction;
            movement.Active = true;
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
            _logger.LogEntityOperationInvalid("NPC", "face direction", "entity is dead or invalid");
            return;
        }

        if (_world.Has<GridMovement>(npc))
        {
            ref GridMovement movement = ref _world.Get<GridMovement>(npc);
            movement.FacingDirection = direction;
        }
    }

    public void FaceEntity(Entity npc, Entity target)
    {
        if (!_world.IsAlive(npc) || !_world.IsAlive(target))
        {
            _logger.LogEntityOperationInvalid(
                "NPC targeting",
                "face entity",
                "source or target invalid"
            );
            return;
        }

        if (!_world.Has<Position>(npc) || !_world.Has<Position>(target))
        {
            _logger.LogEntityMissingComponent(
                "NPC facing pair",
                "Position",
                "align facing direction"
            );
            return;
        }

        ref Position npcPos = ref _world.Get<Position>(npc);
        ref Position targetPos = ref _world.Get<Position>(target);

        int dx = targetPos.X - npcPos.X;
        int dy = targetPos.Y - npcPos.Y;

        Direction direction;
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            direction = dx > 0 ? Direction.East : Direction.West;
        }
        else if (Math.Abs(dy) > 0)
        {
            direction = dy > 0 ? Direction.South : Direction.North;
        }
        else
        {
            direction = Direction.South; // Default if at same position
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
            ref Position position = ref _world.Get<Position>(npc);
            return new Point(position.X, position.Y);
        }

        return Point.Zero;
    }

    public void SetNPCPath(Entity npc, Point[] waypoints, bool loop)
    {
        if (!_world.IsAlive(npc))
        {
            _logger.LogEntityOperationInvalid("NPC", "set path", "entity is dead or invalid");
            return;
        }

        if (waypoints == null || waypoints.Length == 0)
        {
            _logger.LogOperationSkipped("NPC.SetPath", "waypoint list is empty");
            return;
        }

        var pathComponent = new MovementRoute
        {
            Waypoints = waypoints,
            CurrentWaypointIndex = 0,
            Loop = loop,
            WaypointWaitTime = 0f,
            CurrentWaitTime = 0f,
        };

        if (_world.Has<MovementRoute>(npc))
        {
            _world.Set(npc, pathComponent);
        }
        else
        {
            _world.Add(npc, pathComponent);
        }

        _logger.LogInformation(
            "Path set for entity {Entity} with {Count} waypoints (loop: {Loop})",
            npc.Id,
            waypoints.Length,
            loop
        );
    }

    public Point[]? GetNPCPath(Entity npc)
    {
        if (!_world.IsAlive(npc))
        {
            return null;
        }

        if (_world.Has<MovementRoute>(npc))
        {
            ref MovementRoute path = ref _world.Get<MovementRoute>(npc);
            return path.Waypoints;
        }

        return null;
    }

    public void ClearNPCPath(Entity npc)
    {
        if (!_world.IsAlive(npc))
        {
            return;
        }

        if (_world.Has<MovementRoute>(npc))
        {
            _world.Remove<MovementRoute>(npc);
            _logger.LogInformation("Path cleared for entity {Entity}", npc.Id);
        }
    }

    public void PauseNPCPath(Entity npc)
    {
        if (!_world.IsAlive(npc) || !_world.Has<MovementRoute>(npc))
        {
            return;
        }

        ref MovementRoute path = ref _world.Get<MovementRoute>(npc);

        // Set wait time to a very high value to effectively pause
        path.WaypointWaitTime = float.MaxValue;

        _logger.LogInformation("Path paused for entity {Entity}", npc.Id);
    }

    public void ResumeNPCPath(Entity npc, float waitTime = 0f)
    {
        if (!_world.IsAlive(npc) || !_world.Has<MovementRoute>(npc))
        {
            return;
        }

        ref MovementRoute path = ref _world.Get<MovementRoute>(npc);

        // Reset wait time to resume movement
        path.WaypointWaitTime = waitTime;
        path.CurrentWaitTime = 0f;

        _logger.LogInformation("Path resumed for entity {Entity}", npc.Id);
    }

    public bool IsNPCMoving(Entity npc)
    {
        if (!_world.IsAlive(npc))
        {
            return false;
        }

        if (_world.Has<GridMovement>(npc))
        {
            ref GridMovement movement = ref _world.Get<GridMovement>(npc);
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
            ref GridMovement movement = ref _world.Get<GridMovement>(npc);
            movement.CompleteMovement();
        }

        // Clear any pending movement requests
        if (_world.Has<MovementRequest>(npc))
        {
            _world.Remove<MovementRequest>(npc);
        }
    }
}
