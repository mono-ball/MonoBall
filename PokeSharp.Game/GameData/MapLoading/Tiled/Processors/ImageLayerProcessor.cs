using Arch.Core;
using Arch.Relationships;
using Microsoft.Extensions.Logging;
using PokeSharp.Game.Engine.Common.Logging;
using PokeSharp.Game.Engine.Core.Types;
using PokeSharp.Game.Engine.Rendering.Assets;
using PokeSharp.Game.Components.Relationships;
using PokeSharp.Game.Components.Rendering;
using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

namespace PokeSharp.Game.Data.MapLoading.Tiled.Processors;

/// <summary>
///     Handles creation of image layer entities from Tiled maps.
///     Processes image layers and creates ECS entities with ImageLayer components.
/// </summary>
public class ImageLayerProcessor
{
    private readonly IAssetProvider _assetManager;
    private readonly ILogger<ImageLayerProcessor>? _logger;

    public ImageLayerProcessor(
        IAssetProvider assetManager,
        ILogger<ImageLayerProcessor>? logger = null
    )
    {
        _assetManager = assetManager;
        _logger = logger;
    }

    /// <summary>
    ///     Creates image layer entities from the Tiled document.
    /// </summary>
    /// <param name="world">The ECS world to create entities in.</param>
    /// <param name="tmxDoc">The Tiled document.</param>
    /// <param name="mapInfoEntity">The map entity for establishing relationships.</param>
    /// <param name="mapId">The map runtime ID.</param>
    /// <param name="mapPath">Path to the map file (for resolving relative image paths).</param>
    /// <param name="totalLayerCount">Total number of layers (tile + image) for depth calculation.</param>
    /// <returns>Number of image layer entities created.</returns>
    public int CreateImageLayerEntities(
        World world,
        TmxDocument tmxDoc,
        Entity mapInfoEntity,
        MapRuntimeId mapId,
        string mapPath,
        int totalLayerCount
    )
    {
        if (tmxDoc.ImageLayers.Count == 0)
        {
            return 0;
        }

        int created = 0;
        string mapDirectory = Path.GetDirectoryName(mapPath) ?? string.Empty;

        // Process image layers in order
        for (int i = 0; i < tmxDoc.ImageLayers.Count; i++)
        {
            TmxImageLayer imageLayer = tmxDoc.ImageLayers[i];

            // Skip invisible layers
            if (!imageLayer.Visible || imageLayer.Image == null)
            {
                continue;
            }

            // Get image path
            string imagePath = imageLayer.Image.Source;
            if (string.IsNullOrEmpty(imagePath))
            {
                _logger?.LogOperationSkipped($"Image layer '{imageLayer.Name}'", "no image source");
                continue;
            }

            // Create texture ID from image filename
            string textureId = Path.GetFileNameWithoutExtension(imagePath);

            // Load texture if not already loaded
            if (!_assetManager.HasTexture(textureId))
            {
                try
                {
                    // Resolve relative path from map directory
                    string fullImagePath = Path.Combine(mapDirectory, imagePath);

                    // If using AssetManager, make path relative to Assets root
                    // Otherwise (e.g., in tests with stub), use the path directly
                    string pathForLoader;
                    if (_assetManager is AssetManager assetManager)
                    {
                        pathForLoader = Path.GetRelativePath(assetManager.AssetRoot, fullImagePath);
                    }
                    else
                    {
                        pathForLoader = imagePath;
                    }

                    _assetManager.LoadTexture(textureId, pathForLoader);
                }
                catch (Exception ex)
                {
                    _logger?.LogExceptionWithContext(
                        ex,
                        "Failed to load image layer texture '{TextureId}' from '{ImagePath}'",
                        textureId,
                        imagePath
                    );
                    continue;
                }
            }

            // Calculate layer depth based on position in layer stack
            // Image layers should interleave with tile layers
            // We need to determine where this image layer falls in the overall layer order
            float layerDepth = CalculateImageLayerDepth(imageLayer.Id, totalLayerCount);

            // Create entity with ImageLayer component
            var imageLayerComponent = new ImageLayer(
                textureId,
                imageLayer.X,
                imageLayer.Y,
                imageLayer.Opacity,
                layerDepth,
                imageLayer.Id
            );

            Entity entity = world.Create(imageLayerComponent);

            // Add ParentOf relationship - map is parent of image layers
            mapInfoEntity.AddRelationship(entity, new ParentOf());

            _logger?.LogDebug(
                "Created image layer '{LayerName}' with texture '{TextureId}' at ({X}, {Y}) depth {Depth:F2}",
                imageLayer.Name,
                textureId,
                imageLayer.X,
                imageLayer.Y,
                layerDepth
            );

            created++;
        }

        return created;
    }

    /// <summary>
    ///     Calculates the layer depth for an image layer based on its ID.
    ///     Lower IDs render behind, higher IDs render in front.
    /// </summary>
    /// <param name="layerId">The layer ID from Tiled.</param>
    /// <param name="totalLayerCount">Total number of layers in the map.</param>
    /// <returns>Layer depth value (0.0 to 1.0, where lower is in front).</returns>
    private static float CalculateImageLayerDepth(int layerId, int totalLayerCount)
    {
        // Map layer IDs to depth range 0.0 (front) to 1.0 (back)
        // Lower layer IDs (created first in Tiled) should render behind (higher depth)
        // Higher layer IDs (created later in Tiled) should render in front (lower depth)
        if (totalLayerCount <= 1)
        {
            return 0.5f;
        }

        float normalized = (float)layerId / totalLayerCount;
        return 1.0f - normalized; // Invert so lower IDs = higher depth (back)
    }
}
