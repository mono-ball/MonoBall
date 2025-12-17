using Arch.Core;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Ecs.Components.Common;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Ecs.Components.NPCs;
using MonoBallFramework.Game.Ecs.Components.Rendering;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Scripting.Api;
using EcsQueries = MonoBallFramework.Game.Engine.Systems.Queries.Queries;

namespace MonoBallFramework.Game.Scripting.Services;

/// <summary>
///     Entity spawning and lifecycle management service implementation.
///     Provides script-friendly entity creation directly using ECS components.
/// </summary>
public class EntityApiService(
    World world,
    NpcApiService npcService,
    IMapApi mapApi,
    ILogger<EntityApiService> logger
) : IEntityApi
{
    private readonly ILogger<EntityApiService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly IMapApi _mapApi =
        mapApi ?? throw new ArgumentNullException(nameof(mapApi));

    private readonly NpcApiService _npcService =
        npcService ?? throw new ArgumentNullException(nameof(npcService));

    private readonly World _world = world ?? throw new ArgumentNullException(nameof(world));

    #region Fluent NPC Builder

    /// <inheritdoc />
    public INpcSpawnBuilder CreateNpc(int x, int y)
    {
        return new NpcSpawnBuilder(_world, _npcService, _logger, x, y);
    }

    #endregion

    #region NPC Spawning

    /// <inheritdoc />
    public Entity SpawnNpcAt(
        int x,
        int y,
        GameSpriteId spriteId,
        GameBehaviorId? behaviorId = null,
        string? displayName = null
    )
    {
        ArgumentNullException.ThrowIfNull(spriteId);

        _logger.LogDebug(
            "Spawning generic NPC at ({X}, {Y}) with sprite {SpriteId}",
            x,
            y,
            spriteId
        );

        // Create entity with basic components directly
        Entity entity = _world.Create(
            new Position(x, y),
            new Sprite(spriteId),
            new GridMovement(),
            new Visible()
        );

        // Add behavior if specified
        if (behaviorId != null)
        {
            _world.Add(entity, new Behavior(behaviorId.ToString()));
        }

        // Add display name if specified
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            _world.Add(entity, new Name(displayName));
        }

        _logger.LogDebug("Created generic NPC entity {EntityId} at ({X}, {Y})", entity.Id, x, y);

        return entity;
    }

    #endregion

    #region Entity Lifecycle

    /// <inheritdoc />
    public void DestroyEntity(Entity entity)
    {
        if (!_world.IsAlive(entity))
        {
            _logger.LogWarning("Attempted to destroy non-existent entity {EntityId}", entity.Id);
            return;
        }

        _world.Destroy(entity);
        _logger.LogDebug("Destroyed entity {EntityId}", entity.Id);
    }

    /// <inheritdoc />
    public void DestroyEntityDelayed(Entity entity, float delaySeconds)
    {
        if (!_world.IsAlive(entity))
        {
            _logger.LogWarning(
                "Attempted to schedule destruction for non-existent entity {EntityId}",
                entity.Id
            );
            return;
        }

        // TODO: Implement proper delayed destruction with scheduler/component when available
        // For now, log warning and destroy immediately
        if (delaySeconds > 0)
        {
            _logger.LogWarning(
                "Delayed destruction requested for entity {EntityId} with delay {Delay}s, " +
                "but scheduler not yet implemented - destroying immediately",
                entity.Id,
                delaySeconds
            );
        }

        _world.Destroy(entity);
        _logger.LogDebug("Destroyed entity {EntityId} (delayed destruction not fully implemented)", entity.Id);
    }

    /// <inheritdoc />
    public bool IsAlive(Entity entity)
    {
        return _world.IsAlive(entity);
    }

    #endregion

    #region Entity Queries

    /// <inheritdoc />
    public Entity? FindEntityById(int entityId)
    {
        // Arch ECS doesn't directly support lookup by raw ID
        // This would need to be implemented via a component or lookup table
        _logger.LogDebug("Looking up entity by ID: {EntityId}", entityId);

        // TODO: Implement entity lookup
        return null;
    }

    /// <inheritdoc />
    public Entity[] FindEntitiesByTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return [];
        }

        _logger.LogDebug("Finding entities with tag: {Tag}", tag);

        // TODO: Query entities with EntityTag component matching tag
        return [];
    }

    /// <inheritdoc />
    public Entity[] FindNpcsInRadius(int centerX, int centerY, int radius)
    {
        var entities = new List<Entity>();
        int radiusSquared = radius * radius;

        _world.Query(
            in EcsQueries.Npcs,
            (Entity entity, ref Position position) =>
            {
                int dx = position.X - centerX;
                int dy = position.Y - centerY;
                int distanceSquared = (dx * dx) + (dy * dy);

                if (distanceSquared <= radiusSquared)
                {
                    entities.Add(entity);
                }
            }
        );

        _logger.LogDebug(
            "Found {Count} NPCs within {Radius} tiles of ({X}, {Y})",
            entities.Count,
            radius,
            centerX,
            centerY
        );

        return entities.ToArray();
    }

    /// <inheritdoc />
    public Entity[] FindEntitiesAt(int x, int y)
    {
        var entities = new List<Entity>();

        _world.Query(
            in EcsQueries.Movement,
            (Entity entity, ref Position position) =>
            {
                if (position.X == x && position.Y == y)
                {
                    entities.Add(entity);
                }
            }
        );

        _logger.LogDebug("Found {Count} entities at ({X}, {Y})", entities.Count, x, y);
        return entities.ToArray();
    }

    /// <inheritdoc />
    public GameNpcId? GetNpcId(Entity entity)
    {
        if (!_world.IsAlive(entity))
        {
            return null;
        }

        if (_world.Has<Npc>(entity))
        {
            ref Npc npc = ref _world.Get<Npc>(entity);
            return npc.NpcId;
        }

        return null;
    }

    #endregion
}
