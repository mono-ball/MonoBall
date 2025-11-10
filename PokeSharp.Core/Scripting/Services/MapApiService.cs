using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Core.Components.Maps;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Components.Player;
using PokeSharp.Core.Logging;
using PokeSharp.Core.ScriptingApi;
using PokeSharp.Core.Systems;

namespace PokeSharp.Core.Scripting.Services;

/// <summary>
///     Map query service implementation.
/// </summary>
public class MapApiService(
    World world,
    ILogger<MapApiService> logger,
    ISpatialQuery? spatialQuery = null
) : IMapApi
{
    private readonly ILogger<MapApiService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly World _world = world ?? throw new ArgumentNullException(nameof(world));
    private ISpatialQuery? _spatialQuery = spatialQuery;

    public bool IsPositionWalkable(int mapId, int x, int y)
    {
        if (_spatialQuery == null)
        {
            _logger.LogSystemUnavailable("SpatialQuery", "not initialized yet");
            return true; // Default to walkable if system not ready
        }

        var entities = _spatialQuery.GetEntitiesAt(mapId, x, y);
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
        if (_spatialQuery == null)
        {
            _logger.LogSystemUnavailable("SpatialQuery", "not initialized yet");
            return [];
        }

        return [.. _spatialQuery.GetEntitiesAt(mapId, x, y)];
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
        (int width, int height)? result = null;

        // Use centralized query for MapInfo
        _world.Query(
            in Queries.Queries.MapInfo,
            (ref MapInfo mapInfo) =>
            {
                if (mapInfo.MapId == mapId)
                    result = (mapInfo.Width, mapInfo.Height);
            }
        );

        return result;
    }

    public Direction GetDirectionTo(int fromX, int fromY, int toX, int toY)
    {
        var dx = toX - fromX;
        var dy = toY - fromY;

        // Prioritize horizontal movement over vertical
        if (Math.Abs(dx) > Math.Abs(dy))
            return dx > 0 ? Direction.Right : Direction.Left;

        return dy > 0 ? Direction.Down : Direction.Up;
    }

    /// <summary>
    ///     Sets the spatial query service. This is called after initialization when SpatialHashSystem is available.
    /// </summary>
    public void SetSpatialQuery(ISpatialQuery spatialQuery)
    {
        _spatialQuery =
            spatialQuery ?? throw new ArgumentNullException(nameof(spatialQuery));
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
