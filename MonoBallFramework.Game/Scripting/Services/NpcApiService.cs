using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Ecs.Components.Common;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Ecs.Components.NPCs;
using MonoBallFramework.Game.Ecs.Components.Rendering;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Scripting.Api;

namespace MonoBallFramework.Game.Scripting.Services;

/// <summary>
///     NPC management service implementation.
/// </summary>
public class NpcApiService(World world, ILogger<NpcApiService> logger) : INpcApi
{
    private readonly ILogger<NpcApiService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly World _world = world ?? throw new ArgumentNullException(nameof(world));

    #region Fluent API

    /// <inheritdoc />
    public INpcContext For(Entity npc)
    {
        return new NpcContext(npc, this);
    }

    #endregion

    #region Movement

    public void MoveNpc(Entity npc, Direction direction)
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

    public Point GetNpcPosition(Entity npc)
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

    public void SetNpcPath(Entity npc, Point[] waypoints, bool loop)
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

    public Point[]? GetNpcPath(Entity npc)
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

    public void ClearNpcPath(Entity npc)
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

    public void PauseNpcPath(Entity npc)
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

    public void ResumeNpcPath(Entity npc, float waitTime = 0f)
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

    public bool IsNpcMoving(Entity npc)
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

    public void StopNpc(Entity npc)
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

    #endregion

    #region Sprite Management

    /// <inheritdoc />
    public void SetNpcSprite(Entity npc, GameSpriteId spriteId)
    {
        ArgumentNullException.ThrowIfNull(spriteId);

        if (!_world.IsAlive(npc))
        {
            _logger.LogEntityOperationInvalid("NPC", "set sprite", "entity is dead or invalid");
            return;
        }

        if (_world.Has<Sprite>(npc))
        {
            // Get the existing sprite component and create a new one with updated SpriteId
            ref Sprite sprite = ref _world.Get<Sprite>(npc);
            var updatedSprite = new Sprite(spriteId)
            {
                CurrentFrame = sprite.CurrentFrame,
                FlipHorizontal = sprite.FlipHorizontal,
                SourceRect = sprite.SourceRect,
                Origin = sprite.Origin,
                Rotation = sprite.Rotation,
                Tint = sprite.Tint,
                Scale = sprite.Scale
            };
            _world.Set(npc, updatedSprite);
        }
        else
        {
            // Add new sprite component if it doesn't exist
            _world.Add(npc, new Sprite(spriteId));
        }

        _logger.LogDebug("Set sprite {SpriteId} on entity {EntityId}", spriteId, npc.Id);
    }

    /// <inheritdoc />
    public GameSpriteId? GetNpcSprite(Entity npc)
    {
        if (!_world.IsAlive(npc))
        {
            return null;
        }

        if (_world.Has<Sprite>(npc))
        {
            ref Sprite sprite = ref _world.Get<Sprite>(npc);
            return sprite.SpriteId;
        }

        return null;
    }

    #endregion

    #region Behavior Management

    /// <inheritdoc />
    public void SetNpcBehavior(Entity npc, GameBehaviorId behaviorId)
    {
        ArgumentNullException.ThrowIfNull(behaviorId);

        if (!_world.IsAlive(npc))
        {
            _logger.LogEntityOperationInvalid("NPC", "set behavior", "entity is dead or invalid");
            return;
        }

        if (_world.Has<Behavior>(npc))
        {
            ref Behavior behavior = ref _world.Get<Behavior>(npc);
            behavior.BehaviorTypeId = behaviorId.Value;
            behavior.IsInitialized = false; // Force re-initialization
            behavior.IsActive = true;
        }
        else
        {
            _world.Add(npc, new Behavior(behaviorId.Value));
        }

        _logger.LogDebug("Set behavior {BehaviorId} on entity {EntityId}", behaviorId, npc.Id);
    }

    /// <inheritdoc />
    public GameBehaviorId? GetNpcBehavior(Entity npc)
    {
        if (!_world.IsAlive(npc) || !_world.Has<Behavior>(npc))
        {
            return null;
        }

        ref Behavior behavior = ref _world.Get<Behavior>(npc);
        return GameBehaviorId.TryCreate(behavior.BehaviorTypeId);
    }

    /// <inheritdoc />
    public void ActivateBehavior(Entity npc)
    {
        if (!_world.IsAlive(npc) || !_world.Has<Behavior>(npc))
        {
            return;
        }

        ref Behavior behavior = ref _world.Get<Behavior>(npc);
        behavior.IsActive = true;
        _logger.LogDebug("Activated behavior on entity {EntityId}", npc.Id);
    }

    /// <inheritdoc />
    public void DeactivateBehavior(Entity npc)
    {
        if (!_world.IsAlive(npc) || !_world.Has<Behavior>(npc))
        {
            return;
        }

        ref Behavior behavior = ref _world.Get<Behavior>(npc);
        behavior.IsActive = false;
        _logger.LogDebug("Deactivated behavior on entity {EntityId}", npc.Id);
    }

    /// <inheritdoc />
    public bool IsBehaviorActive(Entity npc)
    {
        if (!_world.IsAlive(npc) || !_world.Has<Behavior>(npc))
        {
            return false;
        }

        ref Behavior behavior = ref _world.Get<Behavior>(npc);
        return behavior.IsActive;
    }

    #endregion

    #region Visibility

    /// <inheritdoc />
    public void SetNpcVisible(Entity npc, bool visible)
    {
        if (!_world.IsAlive(npc))
        {
            _logger.LogEntityOperationInvalid("NPC", "set visibility", "entity is dead or invalid");
            return;
        }

        if (visible)
        {
            // Add Visible component if not present
            if (!_world.Has<Visible>(npc))
            {
                _world.Add<Visible>(npc);
            }
        }
        else
        {
            // Remove Visible component if present
            if (_world.Has<Visible>(npc))
            {
                _world.Remove<Visible>(npc);
            }
        }

        _logger.LogDebug("Set visibility {Visible} on entity {EntityId}", visible, npc.Id);
    }

    /// <inheritdoc />
    public bool IsNpcVisible(Entity npc)
    {
        if (!_world.IsAlive(npc))
        {
            return false;
        }

        return _world.Has<Visible>(npc);
    }

    #endregion

    #region Identity

    /// <inheritdoc />
    public GameNpcId? GetNpcId(Entity npc)
    {
        if (!_world.IsAlive(npc) || !_world.Has<Npc>(npc))
        {
            return null;
        }

        ref Npc npcComponent = ref _world.Get<Npc>(npc);
        return npcComponent.NpcId;
    }

    /// <inheritdoc />
    public string? GetNpcDisplayName(Entity npc)
    {
        if (!_world.IsAlive(npc))
        {
            return null;
        }

        if (_world.Has<Name>(npc))
        {
            ref Name name = ref _world.Get<Name>(npc);
            return name.DisplayName;
        }

        return null;
    }

    /// <inheritdoc />
    public void SetNpcDisplayName(Entity npc, string displayName)
    {
        if (!_world.IsAlive(npc))
        {
            _logger.LogEntityOperationInvalid("NPC", "set display name", "entity is dead or invalid");
            return;
        }

        if (_world.Has<Name>(npc))
        {
            ref Name name = ref _world.Get<Name>(npc);
            name.DisplayName = displayName;
        }
        else
        {
            _world.Add(npc, new Name(displayName));
        }

        _logger.LogDebug("Set display name '{Name}' on entity {EntityId}", displayName, npc.Id);
    }

    #endregion
}
