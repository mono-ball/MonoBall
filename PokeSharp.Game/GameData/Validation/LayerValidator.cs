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
        foreach (TmxLayer layer in map.Layers)
        {
            // Check layer has data
            if (layer.Data == null)
            {
                result.AddWarning($"Layer '{layer.Name}' has no data");
                continue;
            }

            // Validate layer dimensions match map dimensions
            int expectedWidth = map.Width;
            int expectedHeight = map.Height;
            int actualHeight = layer.Data.GetLength(0);
            int actualWidth = layer.Data.GetLength(1);

            if (actualWidth != expectedWidth || actualHeight != expectedHeight)
            {
                result.AddError(
                    $"Layer '{layer.Name}' dimensions ({actualWidth}x{actualHeight}) don't match map dimensions ({expectedWidth}x{expectedHeight})"
                );
            }

            // Validate tile GIDs are within tileset bounds
            ValidateTileGids(map, layer, result);
        }

        // Validate image layers
        foreach (TmxImageLayer imageLayer in map.ImageLayers)
        {
            if (imageLayer.Image == null || string.IsNullOrWhiteSpace(imageLayer.Image.Source))
            {
                result.AddError($"Image layer '{imageLayer.Name}' has no image source");
                continue;
            }

            // Validate image path exists
            string mapDirectory = Path.GetDirectoryName(mapPath) ?? string.Empty;
            string imagePath = Path.Combine(mapDirectory, imageLayer.Image.Source);

            if (!File.Exists(imagePath))
            {
                result.AddWarning(
                    $"Image layer '{imageLayer.Name}' image not found at: {imagePath}"
                );
            }
        }

        return result;
    }

    private void ValidateTileGids(TmxDocument map, TmxLayer layer, ValidationResult result)
    {
        // Guard against null data (should already be checked, but defensive programming)
        if (layer.Data == null)
        {
            return;
        }

        // Build valid GID ranges from tilesets
        var validRanges = map
            .Tilesets.Select(ts => new
            {
                ts.FirstGid,
                // Calculate last GID based on tileset image dimensions
                LastGid = ts.FirstGid
                    + (
                        (ts.Image?.Width ?? 256)
                        / ts.TileWidth
                        * ((ts.Image?.Height ?? 256) / ts.TileHeight)
                    )
                    - 1,
            })
            .ToList();

        const uint TILE_ID_MASK = 0x1FFFFFFF; // Mask out flip flags

        for (int y = 0; y < layer.Height; y++)
        for (int x = 0; x < layer.Width; x++)
        {
            int index = (y * layer.Width) + x;
            uint rawGid = layer.Data[index];
            int tileGid = (int)(rawGid & TILE_ID_MASK);

            // Skip empty tiles
            if (tileGid == 0)
            {
                continue;
            }

            // Check if GID is within any valid tileset range
            bool isValid = validRanges.Any(r => tileGid >= r.FirstGid && tileGid <= r.LastGid);

            if (!isValid)
            {
                result.AddError(
                    $"Layer '{layer.Name}' has invalid tile GID {tileGid} at ({x}, {y})"
                );
            }
        }
    }
}
