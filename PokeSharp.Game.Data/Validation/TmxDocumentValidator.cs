using Microsoft.Extensions.Logging;
using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

namespace PokeSharp.Game.Data.Validation;

/// <summary>
///     Validates TMX document structure and data integrity.
///     Validates required elements, bounds, and file references.
/// </summary>
/// <remarks>
///     This validator works with the current TMX structure where layer.Data is a uint[] array.
///     For layer data validation (dimensions, tile GIDs), see <see cref="LayerValidator" />.
/// </remarks>
public class TmxDocumentValidator : IMapValidator
{
    private readonly ILogger<TmxDocumentValidator> _logger;
    private readonly bool _validateFileReferences;

    public TmxDocumentValidator(
        ILogger<TmxDocumentValidator> logger,
        bool validateFileReferences = true
    )
    {
        _logger = logger;
        _validateFileReferences = validateFileReferences;
    }

    public ValidationResult Validate(TmxDocument map, string mapPath)
    {
        var result = new ValidationResult();

        ValidateRequiredElements(map, result);
        ValidateBounds(map, result);

        if (_validateFileReferences)
        {
            ValidateFileReferences(map, mapPath, result);
        }

        return result;
    }

    /// <summary>
    ///     Validates that required map elements are present
    /// </summary>
    private void ValidateRequiredElements(TmxDocument map, ValidationResult result)
    {
        // Check map dimensions
        if (map.Width <= 0)
        {
            result.AddError("Map width must be greater than 0", "Width");
        }

        if (map.Height <= 0)
        {
            result.AddError("Map height must be greater than 0", "Height");
        }

        if (map.TileWidth <= 0)
        {
            result.AddError("Tile width must be greater than 0", "TileWidth");
        }

        if (map.TileHeight <= 0)
        {
            result.AddError("Tile height must be greater than 0", "TileHeight");
        }

        // Check that map has at least one tileset
        if (map.Tilesets == null || map.Tilesets.Count == 0)
        {
            result.AddWarning("Map has no tilesets", "Tilesets");
        }

        // Check that map has at least one layer
        if (map.Layers == null || map.Layers.Count == 0)
        {
            result.AddWarning("Map has no layers", "Layers");
        }
    }

    /// <summary>
    ///     Validates map bounds and dimensions are reasonable
    /// </summary>
    private void ValidateBounds(TmxDocument map, ValidationResult result)
    {
        const int MaxDimension = 10000; // Reasonable limit for map size
        const int MaxTileSize = 512; // Reasonable limit for tile size

        if (map.Width > MaxDimension)
        {
            result.AddWarning(
                $"Map width {map.Width} exceeds recommended maximum {MaxDimension}",
                "Width"
            );
        }

        if (map.Height > MaxDimension)
        {
            result.AddWarning(
                $"Map height {map.Height} exceeds recommended maximum {MaxDimension}",
                "Height"
            );
        }

        if (map.TileWidth > MaxTileSize)
        {
            result.AddWarning(
                $"Tile width {map.TileWidth} exceeds recommended maximum {MaxTileSize}",
                "TileWidth"
            );
        }

        if (map.TileHeight > MaxTileSize)
        {
            result.AddWarning(
                $"Tile height {map.TileHeight} exceeds recommended maximum {MaxTileSize}",
                "TileHeight"
            );
        }
    }

    /// <summary>
    ///     Validates file references (tilesets, images) exist
    /// </summary>
    private void ValidateFileReferences(TmxDocument map, string mapPath, ValidationResult result)
    {
        string mapDirectory = Path.GetDirectoryName(mapPath) ?? string.Empty;

        // Validate tileset file references
        if (map.Tilesets != null)
        {
            for (int i = 0; i < map.Tilesets.Count; i++)
            {
                TmxTileset tileset = map.Tilesets[i];
                string location = $"Tileset[{i}] ({tileset.Name})";

                // Check external tileset file
                if (!string.IsNullOrEmpty(tileset.Source))
                {
                    string tilesetPath = Path.Combine(mapDirectory, tileset.Source);
                    if (!File.Exists(tilesetPath))
                    {
                        result.AddError(
                            $"External tileset file not found: {tileset.Source}",
                            $"{location}.Source"
                        );
                    }
                }

                // Check tileset image
                if (tileset.Image != null && !string.IsNullOrEmpty(tileset.Image.Source))
                {
                    string imagePath = Path.Combine(mapDirectory, tileset.Image.Source);
                    if (!File.Exists(imagePath))
                    {
                        result.AddWarning(
                            $"Tileset image not found: {tileset.Image.Source}",
                            $"{location}.Image.Source"
                        );
                    }
                }
            }
        }

        // Validate image layer references
        if (map.ImageLayers != null)
        {
            for (int i = 0; i < map.ImageLayers.Count; i++)
            {
                TmxImageLayer imageLayer = map.ImageLayers[i];
                if (imageLayer.Image != null && !string.IsNullOrEmpty(imageLayer.Image.Source))
                {
                    string imagePath = Path.Combine(mapDirectory, imageLayer.Image.Source);
                    if (!File.Exists(imagePath))
                    {
                        result.AddWarning(
                            $"Image layer image not found: {imageLayer.Image.Source}",
                            $"ImageLayer[{i}] ({imageLayer.Name}).Image.Source"
                        );
                    }
                }
            }
        }
    }
}
