using Arch.Core;
using Arch.Core.Extensions;
using Arch.Relationships;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Ecs.Components.Common;
using MonoBallFramework.Game.Ecs.Components.Maps;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Ecs.Components.Relationships;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Utilities;

namespace MonoBallFramework.Game.GameData.MapLoading.Tiled.Spawners;

/// <summary>
///     Spawns warp entities from Tiled "warp_event" objects.
///     Warps transport the player to another map location.
///
///     Required Tiled properties:
///     - type = "warp_event"
///     - warp.map (string): Target map ID (e.g., "base:map:hoenn/littleroot_town")
///     - warp.x (int): Target X tile coordinate
///     - warp.y (int): Target Y tile coordinate
/// </summary>
public sealed class WarpEntitySpawner : IEntitySpawner
{
    private readonly ILogger<WarpEntitySpawner>? _logger;

    public WarpEntitySpawner(ILogger<WarpEntitySpawner>? logger = null)
    {
        _logger = logger;
    }

    public string Name => "WarpEntitySpawner";

    public int Priority => 100;

    public bool CanSpawn(EntitySpawnContext context)
    {
        return context.TiledObject.Type == "warp_event";
    }

    public Entity Spawn(EntitySpawnContext context)
    {
        string errorContext = context.CreateErrorContext();
        var props = context.TiledObject.Properties;

        // Get the required "warp" nested object
        Dictionary<string, object> warpData = TiledPropertyParser.GetRequiredNestedObject(
            props, "warp", errorContext);

        // Parse required target map ID
        string targetMapStr = TiledPropertyParser.GetRequiredString(
            warpData, "map", $"warp property in {errorContext}");

        GameMapId? targetMapId = GameMapId.TryCreate(targetMapStr);
        if (targetMapId == null)
        {
            throw new InvalidDataException(
                $"Invalid target map ID format '{targetMapStr}'. " +
                $"Expected format: 'base:map:region/mapname'. Context: {errorContext}");
        }

        // Parse required target coordinates
        string warpContext = $"warp property in {errorContext}";
        int targetX = TiledPropertyParser.GetRequiredInt(warpData, "x", warpContext);
        int targetY = TiledPropertyParser.GetRequiredInt(warpData, "y", warpContext);

        // Get source tile position
        var (tileX, tileY) = context.GetTilePosition();

        // Create warp entity
        Entity warpEntity = context.World.Create(
            new Position(tileX, tileY, context.MapId, context.TileHeight),
            new WarpPoint(targetMapId, targetX, targetY)
        );

        // Add parent relationship - map owns this warp
        context.MapInfoEntity.AddRelationship(warpEntity, new ParentOf());

        // Register warp in spatial index for O(1) lookup during collision
        ref MapWarps mapWarps = ref context.MapInfoEntity.Get<MapWarps>();
        if (!mapWarps.AddWarp(tileX, tileY, warpEntity))
        {
            _logger?.LogWarning(
                "Warp at ({TileX}, {TileY}) overwrites existing warp - duplicate warp positions",
                tileX, tileY);
        }

        _logger?.LogDebug(
            "Created warp at ({TileX}, {TileY}) → {TargetMap} @ ({TargetX}, {TargetY})",
            tileX, tileY, targetMapStr, targetX, targetY);

        return warpEntity;
    }
}
