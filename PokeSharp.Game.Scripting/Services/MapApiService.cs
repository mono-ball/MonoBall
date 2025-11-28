using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Core.Systems;
using PokeSharp.Engine.Core.Types;
using PokeSharp.Game.Components.Maps;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Api;
using EcsQueries = PokeSharp.Engine.Systems.Queries.Queries;

namespace PokeSharp.Game.Scripting.Services;

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

    public bool IsPositionWalkable(MapRuntimeId mapId, int x, int y)
    {
        if (_spatialQuery == null)
        {
            _logger.LogSystemUnavailable("SpatialQuery", "not initialized yet");
            return true; // Default to walkable if system not ready
        }

        IReadOnlyList<Entity> entities = _spatialQuery.GetEntitiesAt(mapId.Value, x, y);
        foreach (Entity entity in entities)
        {
            if (_world.Has<Collision>(entity))
            {
                ref Collision collision = ref _world.Get<Collision>(entity);
                if (collision.IsSolid)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public Entity[] GetEntitiesAt(MapRuntimeId mapId, int x, int y)
    {
        if (_spatialQuery == null)
        {
            _logger.LogSystemUnavailable("SpatialQuery", "not initialized yet");
            return [];
        }

        return [.. _spatialQuery.GetEntitiesAt(mapId.Value, x, y)];
    }

    public MapRuntimeId GetCurrentMapId()
    {
        Entity? playerEntity = GetPlayerEntity();
        if (playerEntity.HasValue && _world.Has<Position>(playerEntity.Value))
        {
            ref Position position = ref _world.Get<Position>(playerEntity.Value);
            return position.MapId;
        }

        return new MapRuntimeId(0);
    }

    public void TransitionToMap(MapRuntimeId mapId, int x, int y)
    {
        Entity? playerEntity = GetPlayerEntity();
        if (playerEntity.HasValue && _world.Has<Position>(playerEntity.Value))
        {
            ref Position position = ref _world.Get<Position>(playerEntity.Value);
            position.MapId = mapId;
            position.X = x;
            position.Y = y;
            int tileSize = GetTileSize(mapId);
            position.SyncPixelsToGrid(tileSize);
            _logger.LogInformation("Transitioned to map {MapId} at ({X}, {Y})", mapId.Value, x, y);
        }
    }

    public (int width, int height)? GetMapDimensions(MapRuntimeId mapId)
    {
        (int width, int height)? result = null;

        // Use centralized query for MapInfo
        _world.Query(
            in EcsQueries.MapInfo,
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

    public Direction GetDirectionTo(int fromX, int fromY, int toX, int toY)
    {
        int dx = toX - fromX;
        int dy = toY - fromY;

        // Prioritize horizontal movement over vertical
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            return dx > 0 ? Direction.East : Direction.West;
        }

        return dy > 0 ? Direction.South : Direction.North;
    }

    /// <summary>
    ///     Sets the spatial query service. This is called after initialization when SpatialHashSystem is available.
    /// </summary>
    public void SetSpatialQuery(ISpatialQuery spatialQuery)
    {
        _spatialQuery = spatialQuery ?? throw new ArgumentNullException(nameof(spatialQuery));
    }

    private Entity? GetPlayerEntity()
    {
        Entity? playerEntity = null;

        // Use centralized query for Player
        _world.Query(
            in EcsQueries.Player,
            entity =>
            {
                playerEntity = entity;
            }
        );

        return playerEntity;
    }

    private int GetTileSize(MapRuntimeId mapId)
    {
        int tileSize = 16;

        _world.Query(
            in EcsQueries.MapInfo,
            (ref MapInfo mapInfo) =>
            {
                if (mapInfo.MapId == mapId)
                {
                    tileSize = mapInfo.TileSize;
                }
            }
        );

        return tileSize;
    }
}
