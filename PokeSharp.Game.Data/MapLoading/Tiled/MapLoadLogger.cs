using System.Linq;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

namespace PokeSharp.Game.Data.MapLoading.Tiled;

/// <summary>
///     Handles logging for map loading operations.
///     Provides structured logging for map loading summaries and statistics.
/// </summary>
public class MapLoadLogger
{
    private readonly ILogger<MapLoader>? _logger;

    public MapLoadLogger(ILogger<MapLoader>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Logs a summary of the map loading operation.
    /// </summary>
    public void LogLoadingSummary(
        string mapName,
        TmxDocument tmxDoc,
        int tilesCreated,
        int objectsCreated,
        int imageLayersCreated,
        int animatedTilesCreated,
        int mapId,
        string tilesetId
    )
    {
        _logger?.LogMapLoaded(mapName, tmxDoc.Width, tmxDoc.Height, tilesCreated, objectsCreated);

        if (imageLayersCreated > 0)
        {
            _logger?.LogDebug(
                "[dim]Image Layers:[/] [magenta]{ImageLayerCount}[/]",
                imageLayersCreated
            );
        }

        _logger?.LogDebug(
            "[dim]MapId:[/] [grey]{MapId}[/] [dim]|[/] [dim]Animated:[/] [yellow]{AnimatedCount}[/] [dim]|[/] [dim]Tileset:[/] [cyan]{TilesetId}[/]",
            mapId,
            animatedTilesCreated,
            tilesetId
        );
    }

    /// <summary>
    ///     Describes tilesets for logging purposes.
    /// </summary>
    public static string DescribeTilesetsForLog(IReadOnlyList<LoadedTileset> tilesets)
    {
        if (tilesets.Count == 0)
            return "none";

        if (tilesets.Count == 1)
            return tilesets[0].TilesetId;

        return string.Join(",", tilesets.Select(t => t.TilesetId));
    }
}

