using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components;
using PokeSharp.Core.Systems;

namespace PokeSharp.Core.Scripting;

/// <summary>
///     Concrete implementation of IWorldApi that provides script access to game systems.
///     Routes API calls to appropriate ECS systems and components.
/// </summary>
public class WorldApiImplementation : IWorldApi
{
    private readonly Dictionary<string, bool> _flags = new();
    private readonly ILogger _logger;
    private readonly SpatialHashSystem? _spatialHashSystem;
    private readonly Dictionary<string, string> _variables = new();
    private readonly World _world;

    public WorldApiImplementation(
        World world,
        ILogger logger,
        SpatialHashSystem? spatialHashSystem = null
    )
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _spatialHashSystem = spatialHashSystem;
    }

    // ============================================================================
    // IPlayerApi Implementation
    // ============================================================================

    public string GetPlayerName()
    {
        var playerEntity = GetPlayerEntity();
        if (playerEntity.HasValue && _world.Has<Player>(playerEntity.Value))
            // TODO: Add PlayerName field to Player component
            return "PLAYER"; // Placeholder
        return "PLAYER";
    }

    public int GetMoney()
    {
        // TODO: Implement when Player component has Money field
        return 0;
    }

    public void GiveMoney(int amount)
    {
        if (amount < 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));

        // TODO: Implement when Player component has Money field
        _logger.LogInformation("GiveMoney: {Amount}", amount);
    }

    public bool TakeMoney(int amount)
    {
        var currentMoney = GetMoney();
        if (currentMoney >= amount)
        {
            // TODO: Implement when Player component has Money field
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
            // TODO: Add CanMove or MovementLocked field to GridMovement or create separate component
            // For now, we'll use IsMoving as a workaround - setting it to true effectively locks movement
            _logger.LogInformation("Player movement {Status}", locked ? "locked" : "unlocked");
    }

    public bool IsPlayerMovementLocked()
    {
        // TODO: Implement when GridMovement has CanMove or MovementLocked field
        return false;
    }

    // ============================================================================
    // IMapApi Implementation
    // ============================================================================

    public bool IsPositionWalkable(int mapId, int x, int y)
    {
        if (_spatialHashSystem == null)
        {
            _logger.LogWarning("SpatialHashSystem not available for walkability check");
            return true; // Default to walkable if system unavailable
        }

        var entities = _spatialHashSystem.GetEntitiesAt(mapId, x, y);
        foreach (var entity in entities)
            if (_world.Has<Collision>(entity))
            {
                ref var collision = ref _world.Get<Collision>(entity);
                if (collision.IsSolid)
                    return false;
            }

        return true;
    }

    public Entity[] GetEntitiesAt(int mapId, int x, int y)
    {
        if (_spatialHashSystem == null)
        {
            _logger.LogWarning("SpatialHashSystem not available for entity query");
            return Array.Empty<Entity>();
        }

        return _spatialHashSystem.GetEntitiesAt(mapId, x, y).ToArray();
    }

    public int GetCurrentMapId()
    {
        var playerEntity = GetPlayerEntity();
        if (playerEntity.HasValue && _world.Has<Position>(playerEntity.Value))
        {
            ref var position = ref _world.Get<Position>(playerEntity.Value);
            return position.MapId;
        }

        return 0;
    }

    public void TransitionToMap(int mapId, int x, int y)
    {
        var playerEntity = GetPlayerEntity();
        if (playerEntity.HasValue && _world.Has<Position>(playerEntity.Value))
        {
            ref var position = ref _world.Get<Position>(playerEntity.Value);
            position.MapId = mapId;
            position.X = x;
            position.Y = y;
            position.SyncPixelsToGrid();
            _logger.LogInformation("Transitioned to map {MapId} at ({X}, {Y})", mapId, x, y);
        }
    }

    public (int width, int height)? GetMapDimensions(int mapId)
    {
        // Query for MapInfo component
        var query = new QueryDescription().WithAll<MapInfo>();
        (int width, int height)? result = null;

        _world.Query(
            in query,
            (ref MapInfo mapInfo) =>
            {
                if (mapInfo.MapId == mapId)
                    result = (mapInfo.Width, mapInfo.Height);
            }
        );

        return result;
    }

    // ============================================================================
    // INpcApi Implementation
    // ============================================================================

    public void MoveNpc(Entity npc, Direction direction)
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
            direction = dx > 0 ? Direction.Right : Direction.Left;
        else if (Math.Abs(dy) > 0)
            direction = dy > 0 ? Direction.Down : Direction.Up;
        else
            direction = Direction.Down; // Default if at same position

        FaceDirection(npc, direction);
    }

    public Point GetNpcPosition(Entity npc)
    {
        if (!_world.IsAlive(npc))
            return Point.Zero;

        if (_world.Has<Position>(npc))
        {
            ref var position = ref _world.Get<Position>(npc);
            return new Point(position.X, position.Y);
        }

        return Point.Zero;
    }

    public void SetNpcPath(Entity npc, Point[] waypoints, bool loop)
    {
        if (!_world.IsAlive(npc))
        {
            _logger.LogWarning("Attempted to set path on dead/invalid NPC entity");
            return;
        }

        // TODO: Implement when PathComponent is created
        _logger.LogInformation(
            "SetNpcPath called for entity {Entity} with {Count} waypoints",
            npc.Id,
            waypoints.Length
        );
    }

    public bool IsNpcMoving(Entity npc)
    {
        if (!_world.IsAlive(npc))
            return false;

        if (_world.Has<GridMovement>(npc))
        {
            ref var movement = ref _world.Get<GridMovement>(npc);
            return movement.IsMoving;
        }

        return false;
    }

    public void StopNpc(Entity npc)
    {
        if (!_world.IsAlive(npc))
            return;

        if (_world.Has<GridMovement>(npc))
        {
            ref var movement = ref _world.Get<GridMovement>(npc);
            movement.CompleteMovement();
        }

        // Clear any pending movement requests
        if (_world.Has<MovementRequest>(npc))
            _world.Remove<MovementRequest>(npc);
    }

    // ============================================================================
    // IGameStateApi Implementation
    // ============================================================================

    public bool GetFlag(string flagId)
    {
        if (string.IsNullOrWhiteSpace(flagId))
            return false;

        return _flags.TryGetValue(flagId, out var value) && value;
    }

    public void SetFlag(string flagId, bool value)
    {
        if (string.IsNullOrWhiteSpace(flagId))
            throw new ArgumentException("Flag ID cannot be null or empty", nameof(flagId));

        _flags[flagId] = value;
        _logger.LogDebug("Flag {FlagId} set to {Value}", flagId, value);
    }

    public bool FlagExists(string flagId)
    {
        if (string.IsNullOrWhiteSpace(flagId))
            return false;

        return _flags.ContainsKey(flagId);
    }

    public string? GetVariable(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        return _variables.TryGetValue(key, out var value) ? value : null;
    }

    public void SetVariable(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Variable key cannot be null or empty", nameof(key));

        _variables[key] = value;
        _logger.LogDebug("Variable {Key} set to {Value}", key, value);
    }

    public bool VariableExists(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        return _variables.ContainsKey(key);
    }

    public void DeleteVariable(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        _variables.Remove(key);
        _logger.LogDebug("Variable {Key} deleted", key);
    }

    public IEnumerable<string> GetActiveFlags()
    {
        return _flags.Where(kvp => kvp.Value).Select(kvp => kvp.Key);
    }

    public IEnumerable<string> GetVariableKeys()
    {
        return _variables.Keys;
    }

    // ============================================================================
    // Helper Methods
    // ============================================================================

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
