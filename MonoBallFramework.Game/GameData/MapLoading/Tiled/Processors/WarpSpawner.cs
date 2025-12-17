using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Ecs.Components.Maps;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Tmx;

namespace MonoBallFramework.Game.GameData.MapLoading.Tiled.Processors;

/// <summary>
///     Handles warp entity spawning from Tiled object layers.
///     Creates warp entities and manages the spatial index.
/// </summary>
public class WarpSpawner
{
    private readonly ILogger<WarpSpawner>? _logger;

    public WarpSpawner(ILogger<WarpSpawner>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Spawns a warp entity from a warp_event Tiled object.
    ///     Creates entity with Position, WarpPoint, and BelongsToMap components.
    ///     Registers warp in MapWarps spatial index for O(1) lookup.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="obj">The Tiled object with warp_event type.</param>
    /// <param name="mapInfoEntity">The map entity for relationship and spatial index.</param>
    /// <param name="mapId">The current map's game map ID.</param>
    /// <param name="tileWidth">Tile width for coordinate conversion.</param>
    /// <param name="tileHeight">Tile height for coordinate conversion.</param>
    /// <returns>True if warp entity was created successfully.</returns>
    public bool TrySpawnWarpEntity(
        World world,
        TmxObject obj,
        Entity mapInfoEntity,
        GameMapId mapId,
        int tileWidth,
        int tileHeight
    )
    {
        // Get the warp property (nested class object)
        if (!obj.Properties.TryGetValue("warp", out object? warpProp))
        {
            _logger?.LogWarning("warp_event '{Name}' missing 'warp' property, skipping", obj.Name);
            return false;
        }

        // Parse warp data from Dictionary (parsed from Tiled class property)
        if (warpProp is not Dictionary<string, object?> warpData)
        {
            _logger?.LogWarning(
                "warp_event '{Name}' has invalid 'warp' property format, expected Dictionary",
                obj.Name
            );
            return false;
        }

        // Extract warp destination data
        string? targetMap = warpData.TryGetValue("map", out object? mapVal)
            ? mapVal?.ToString()
            : null;

        if (string.IsNullOrEmpty(targetMap))
        {
            _logger?.LogWarning(
                "warp_event '{Name}' missing target map in warp property",
                obj.Name
            );
            return false;
        }

        int targetX =
            warpData.TryGetValue("x", out object? xVal) && xVal != null ? Convert.ToInt32(xVal) : 0;
        int targetY =
            warpData.TryGetValue("y", out object? yVal) && yVal != null ? Convert.ToInt32(yVal) : 0;

        // Convert pixel coordinates to tile coordinates
        int tileX = (int)Math.Floor(obj.X / tileWidth);
        int tileY = (int)Math.Floor(obj.Y / tileHeight);

        try
        {
            // Parse target map ID - TryCreate handles both full format and legacy short names
            var targetMapId = GameMapId.TryCreate(targetMap);
            if (targetMapId == null)
            {
                _logger?.LogWarning(
                    "Invalid warp target map ID format: {TargetMap}",
                    targetMap
                );
                return false;
            }

            // Create warp entity with Position and WarpPoint components
            Entity warpEntity = world.Create(
                new Position(tileX, tileY, mapId, tileHeight),
                new WarpPoint(targetMapId, targetX, targetY)
            );

            // Register warp in MapWarps spatial index for O(1) lookup
            ref MapWarps mapWarps = ref mapInfoEntity.Get<MapWarps>();
            if (!mapWarps.AddWarp(tileX, tileY, warpEntity))
            {
                _logger?.LogWarning(
                    "Warp at ({TileX}, {TileY}) overwrites existing warp - duplicate warp positions",
                    tileX,
                    tileY
                );
            }

            _logger?.LogDebug(
                "Created warp at ({TileX}, {TileY}) → {TargetMap} @ ({TargetX}, {TargetY})",
                tileX,
                tileY,
                targetMap,
                targetX,
                targetY
            );

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogExceptionWithContext(
                ex,
                "Failed to create warp entity for '{ObjectName}'",
                obj.Name
            );
            return false;
        }
    }
}
