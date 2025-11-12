using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

namespace PokeSharp.Game.Data.Validation;

/// <summary>
///     Validates layer data and properties.
/// </summary>
public class LayerValidator : IMapValidator
{
    public ValidationResult Validate(TmxDocument map, string mapPath)
    {
        var result = new ValidationResult();

        // Validate tile layers
        foreach (var layer in map.Layers)
        {
            // Check layer has data
            if (layer.Data == null)
            {
                result.AddWarning($"Layer '{layer.Name}' has no data");
                continue;
            }

            // Validate layer dimensions match map dimensions
            var expectedWidth = map.Width;
            var expectedHeight = map.Height;
            var actualHeight = layer.Data.GetLength(0);
            var actualWidth = layer.Data.GetLength(1);

            if (actualWidth != expectedWidth || actualHeight != expectedHeight)
            {
                result.AddError(
                    $"Layer '{layer.Name}' dimensions ({actualWidth}x{actualHeight}) don't match map dimensions ({expectedWidth}x{expectedHeight})");
            }

            // Validate tile GIDs are within tileset bounds
            ValidateTileGids(map, layer, result);
        }

        // Validate image layers
        foreach (var imageLayer in map.ImageLayers)
        {
            if (imageLayer.Image == null || string.IsNullOrWhiteSpace(imageLayer.Image.Source))
            {
                result.AddError($"Image layer '{imageLayer.Name}' has no image source");
                continue;
            }

            // Validate image path exists
            var mapDirectory = Path.GetDirectoryName(mapPath) ?? string.Empty;
            var imagePath = Path.Combine(mapDirectory, imageLayer.Image.Source);

            if (!File.Exists(imagePath))
            {
                result.AddWarning(
                    $"Image layer '{imageLayer.Name}' image not found at: {imagePath}");
            }
        }

        return result;
    }

    private void ValidateTileGids(TmxDocument map, TmxLayer layer, ValidationResult result)
    {
        // Build valid GID ranges from tilesets
        var validRanges = map.Tilesets
            .Select(ts => new
            {
                ts.FirstGid,
                // Calculate last GID based on tileset image dimensions
                LastGid = ts.FirstGid + ((ts.Image?.Width ?? 256) / ts.TileWidth)
                                      * ((ts.Image?.Height ?? 256) / ts.TileHeight) - 1
            })
            .ToList();

        const uint TILE_ID_MASK = 0x1FFFFFFF; // Mask out flip flags

        for (var y = 0; y < layer.Height; y++)
        {
            for (var x = 0; x < layer.Width; x++)
            {
                var index = y * layer.Width + x;
                var rawGid = (uint)layer.Data[index];
                var tileGid = (int)(rawGid & TILE_ID_MASK);

                // Skip empty tiles
                if (tileGid == 0)
                    continue;

                // Check if GID is within any valid tileset range
                var isValid = validRanges.Any(r => tileGid >= r.FirstGid && tileGid <= r.LastGid);

                if (!isValid)
                {
                    result.AddError(
                        $"Layer '{layer.Name}' has invalid tile GID {tileGid} at ({x}, {y})");
                }
            }
        }
    }
}
