using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

namespace PokeSharp.Game.Data.Validation;

/// <summary>
///     Validates map dimensions and basic structure.
/// </summary>
public class MapDimensionsValidator : IMapValidator
{
    private const int MinMapSize = 1;
    private const int MaxMapSize = 10000; // Prevent extremely large maps
    private const int MinTileSize = 1;
    private const int MaxTileSize = 256;

    public ValidationResult Validate(TmxDocument map, string mapPath)
    {
        var result = new ValidationResult();

        // Validate map dimensions
        if (map.Width < MinMapSize || map.Width > MaxMapSize)
        {
            result.AddError(
                $"Map width {map.Width} is outside valid range ({MinMapSize}-{MaxMapSize})"
            );
        }

        if (map.Height < MinMapSize || map.Height > MaxMapSize)
        {
            result.AddError(
                $"Map height {map.Height} is outside valid range ({MinMapSize}-{MaxMapSize})"
            );
        }

        // Validate tile dimensions
        if (map.TileWidth < MinTileSize || map.TileWidth > MaxTileSize)
        {
            result.AddError(
                $"Tile width {map.TileWidth} is outside valid range ({MinTileSize}-{MaxTileSize})"
            );
        }

        if (map.TileHeight < MinTileSize || map.TileHeight > MaxTileSize)
        {
            result.AddError(
                $"Tile height {map.TileHeight} is outside valid range ({MinTileSize}-{MaxTileSize})"
            );
        }

        // Warn about very large maps (performance concern)
        int totalTiles = map.Width * map.Height;
        if (totalTiles > 100000)
        {
            result.AddWarning(
                $"Map has {totalTiles} tiles which may impact performance. Consider splitting into smaller maps."
            );
        }

        // Validate version string
        if (string.IsNullOrWhiteSpace(map.Version))
        {
            result.AddWarning("Map has no version specified");
        }

        return result;
    }
}
