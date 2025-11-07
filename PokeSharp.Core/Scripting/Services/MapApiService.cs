using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Core.Components;
using PokeSharp.Core.Components.Maps;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Components.Player;
using PokeSharp.Core.ScriptingApi;
using PokeSharp.Core.Systems;

namespace PokeSharp.Core.Scripting.Services;

/// <summary>
///     Map query service implementation.
/// </summary>
public class MapApiService(
    World world,
    ILogger<MapApiService> logger,
    SpatialHashSystem? spatialHashSystem = null) : IMapApi
{
    private readonly World _world = world ?? throw new ArgumentNullException(nameof(world));
    private readonly ILogger<MapApiService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly SpatialHashSystem? _spatialHashSystem = spatialHashSystem;

    public bool IsPositionWalkable(int mapId, int x, int y)
    {
        if (_spatialHashSystem == null)
        {
            _logger.LogWarning("SpatialHashSystem not available for walkability check");
            return true; // Default to walkable if system unavailable
        }

        var entities = _spatialHashSystem.GetEntitiesAt(mapId, x, y);
        foreach (var entity in entities)
        {
            if (_world.Has<Collision>(entity))
            {
                ref var collision = ref _world.Get<Collision>(entity);
                if (collision.IsSolid)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public Entity[] GetEntitiesAt(int mapId, int x, int y)
    {
        if (_spatialHashSystem == null)
        {
            _logger.LogWarning("SpatialHashSystem not available for entity query");
            return [];
        }

        return [.. _spatialHashSystem.GetEntitiesAt(mapId, x, y)];
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
                {
                    result = (mapInfo.Width, mapInfo.Height);
                }
            }
        );

        return result;
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

