using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Game.Components.Maps;
using PokeSharp.Game.Components.Tiles;
using PokeSharp.Game.Data.Entities;
using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

namespace PokeSharp.Game.Data.MapLoading.Tiled;

/// <summary>
///     Handles creation of map metadata entities (MapInfo and TilesetInfo).
///     Creates ECS entities that store map and tileset information for runtime use.
/// </summary>
public class MapMetadataFactory
{
    private readonly ILogger<MapMetadataFactory>? _logger;

    public MapMetadataFactory(ILogger<MapMetadataFactory>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Creates MapInfo and TilesetInfo metadata entities.
    /// </summary>
    public Entity CreateMapMetadata(
        World world,
        TmxDocument tmxDoc,
        string mapPath,
        int mapId,
        string mapName,
        IReadOnlyList<LoadedTileset> tilesets
    )
    {
        // Create MapInfo entity for map metadata
        var mapInfo = new MapInfo(mapId, mapName, tmxDoc.Width, tmxDoc.Height, tmxDoc.TileWidth);
        var mapInfoEntity = world.Create(mapInfo);

        foreach (var loadedTileset in tilesets)
        {
            var tileset = loadedTileset.Tileset;
            if (tileset.FirstGid <= 0)
                throw new InvalidOperationException(
                    $"Tileset '{tileset.Name ?? loadedTileset.TilesetId}' in '{mapPath}' has invalid firstgid {tileset.FirstGid}."
                );
            if (tileset.TileWidth <= 0 || tileset.TileHeight <= 0)
                throw new InvalidOperationException(
                    $"Tileset '{tileset.Name ?? loadedTileset.TilesetId}' in '{mapPath}' has invalid tile size {tileset.TileWidth}x{tileset.TileHeight}."
                );
            if (tileset.Image == null || tileset.Image.Width <= 0 || tileset.Image.Height <= 0)
                throw new InvalidOperationException(
                    $"Tileset '{tileset.Name ?? loadedTileset.TilesetId}' in '{mapPath}' is missing valid image dimensions."
                );

            var tilesetInfo = new TilesetInfo(
                loadedTileset.TilesetId,
                tileset.FirstGid,
                tileset.TileWidth,
                tileset.TileHeight,
                tileset.Image.Width,
                tileset.Image.Height
            );
            world.Create(tilesetInfo);
        }

        return mapInfoEntity;
    }

    /// <summary>
    ///     Creates MapInfo and TilesetInfo metadata entities from MapDefinition.
    ///     Used for definition-based map loading.
    /// </summary>
    public Entity CreateMapMetadataFromDefinition(
        World world,
        TmxDocument tmxDoc,
        MapDefinition mapDef,
        int mapId,
        IReadOnlyList<LoadedTileset> tilesets
    )
    {
        // Create MapInfo entity for map metadata (use MapDefinition display name)
        var mapInfo = new MapInfo(
            mapId,
            mapDef.DisplayName,
            tmxDoc.Width,
            tmxDoc.Height,
            tmxDoc.TileWidth
        );
        var mapInfoEntity = world.Create(mapInfo);

        // Create TilesetInfo if map has tilesets
        foreach (var loadedTileset in tilesets)
        {
            var tileset = loadedTileset.Tileset;

            // Validation
            if (tileset.FirstGid <= 0)
                throw new InvalidOperationException(
                    $"Tileset '{tileset.Name ?? loadedTileset.TilesetId}' in map '{mapDef.MapId}' has invalid firstgid {tileset.FirstGid}."
                );
            if (tileset.TileWidth <= 0 || tileset.TileHeight <= 0)
                throw new InvalidOperationException(
                    $"Tileset '{tileset.Name ?? loadedTileset.TilesetId}' in map '{mapDef.MapId}' has invalid tile size {tileset.TileWidth}x{tileset.TileHeight}."
                );
            if (tileset.Image == null || tileset.Image.Width <= 0 || tileset.Image.Height <= 0)
                throw new InvalidOperationException(
                    $"Tileset '{tileset.Name ?? loadedTileset.TilesetId}' in map '{mapDef.MapId}' is missing valid image dimensions."
                );

            var tilesetInfo = new TilesetInfo(
                loadedTileset.TilesetId,
                tileset.FirstGid,
                tileset.TileWidth,
                tileset.TileHeight,
                tileset.Image.Width,
                tileset.Image.Height
            );
            world.Create(tilesetInfo);
        }

        return mapInfoEntity;
    }
}

