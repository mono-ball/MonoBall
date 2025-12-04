using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

namespace PokeSharp.Game.Data.Validation;

/// <summary>
///     Validates tileset requirements and properties.
/// </summary>
public class TilesetValidator : IMapValidator
{
    public ValidationResult Validate(TmxDocument map, string mapPath)
    {
        var result = new ValidationResult();

        // Check if map has at least one tileset
        if (map.Tilesets.Count == 0)
        {
            result.AddError("Map has no tilesets defined");
            return result;
        }

        foreach (TmxTileset tileset in map.Tilesets)
        {
            // Validate tileset has an image
            if (tileset.Image == null)
            {
                result.AddError($"Tileset '{tileset.Name}' has no image");
                continue;
            }

            // Validate image source exists
            if (string.IsNullOrWhiteSpace(tileset.Image.Source))
            {
                result.AddError($"Tileset '{tileset.Name}' has empty image source");
                continue;
            }

            // Validate image dimensions
            if (tileset.Image.Width <= 0 || tileset.Image.Height <= 0)
            {
                result.AddError(
                    $"Tileset '{tileset.Name}' has invalid image dimensions: {tileset.Image.Width}x{tileset.Image.Height}"
                );
            }

            // Validate tile dimensions
            if (tileset.TileWidth <= 0 || tileset.TileHeight <= 0)
            {
                result.AddError(
                    $"Tileset '{tileset.Name}' has invalid tile dimensions: {tileset.TileWidth}x{tileset.TileHeight}"
                );
            }

            // Validate FirstGid
            if (tileset.FirstGid <= 0)
            {
                result.AddError(
                    $"Tileset '{tileset.Name}' has invalid FirstGid: {tileset.FirstGid}"
                );
            }

            // Validate image path exists (relative to map directory)
            string mapDirectory = Path.GetDirectoryName(mapPath) ?? string.Empty;
            string tilesetPath = Path.Combine(mapDirectory, tileset.Image.Source);

            if (!File.Exists(tilesetPath))
            {
                result.AddWarning($"Tileset '{tileset.Name}' image not found at: {tilesetPath}");
            }
        }

        return result;
    }
}
