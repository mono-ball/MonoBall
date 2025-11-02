using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Core.Components;
using PokeSharp.Core.Systems;
using PokeSharp.Rendering.Assets;
using PokeSharp.Rendering.Components;

namespace PokeSharp.Rendering.Systems;

/// <summary>
/// System responsible for rendering tile-based maps with multiple layers.
/// </summary>
public class MapRenderSystem : BaseSystem
{
    private const int TileSize = 16;

    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly AssetManager _assetManager;
    private readonly ILogger<MapRenderSystem>? _logger;
    private ulong _frameCounter = 0;
    private int _tileMapCount = 0;
    private int _totalTilesRendered = 0;

    /// <summary>
    /// Initializes a new instance of the MapRenderSystem class.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device for rendering.</param>
    /// <param name="assetManager">Asset manager for texture loading.</param>
    /// <param name="logger">Optional logger for debug output.</param>
    public MapRenderSystem(GraphicsDevice graphicsDevice, AssetManager assetManager, ILogger<MapRenderSystem>? logger = null)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _assetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));
        _spriteBatch = new SpriteBatch(_graphicsDevice);
        _logger = logger;
    }

    /// <inheritdoc/>
    public override int Priority => SystemPriority.MapRender;

    /// <inheritdoc/>
    public override void Update(World world, float deltaTime)
    {
        try
        {
            EnsureInitialized();
            _frameCounter++;
            _tileMapCount = 0;
            _totalTilesRendered = 0;

            _logger?.LogDebug("═══════════════════════════════════════════════════");
            _logger?.LogDebug("MapRenderSystem - Frame {FrameCounter} - Starting map rendering (RUNS FIRST - Priority {Priority})",
                _frameCounter, Priority);
            _logger?.LogDebug("═══════════════════════════════════════════════════");

            // Get camera transform matrix (if camera exists)
            Matrix cameraTransform = Matrix.Identity;
            var cameraQuery = new QueryDescription().WithAll<Player, Camera>();
            world.Query(in cameraQuery, (ref Camera camera) =>
            {
                cameraTransform = camera.GetTransformMatrix();
            });

            // Begin sprite batch for map rendering with camera transform
            _spriteBatch.Begin(
                sortMode: SpriteSortMode.Deferred,
                blendState: BlendState.AlphaBlend,
                samplerState: SamplerState.PointClamp,
                transformMatrix: cameraTransform);

            _logger?.LogDebug("SpriteBatch started (Deferred, AlphaBlend, PointClamp)");

            // Query and render all tile maps
            var query = new QueryDescription().WithAll<TileMap>();
            _logger?.LogDebug("Querying entities with TileMap component...");

            world.Query(in query, (ref TileMap tileMap) =>
            {
                _tileMapCount++;
                RenderTileMap(ref tileMap);
            });

            _spriteBatch.End();

            _logger?.LogDebug("SpriteBatch ended");
            _logger?.LogDebug("───────────────────────────────────────────────────");
            _logger?.LogDebug("MapRenderSystem - Frame {FrameCounter} - Completed. TileMaps: {TileMapCount}, Total tiles: {TotalTiles}",
                _frameCounter, _tileMapCount, _totalTilesRendered);
            _logger?.LogDebug("═══════════════════════════════════════════════════");

            if (_tileMapCount == 0)
            {
                _logger?.LogWarning("⚠️  WARNING: No tile maps found to render in frame {FrameCounter}", _frameCounter);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ CRITICAL ERROR in MapRenderSystem.Update (Frame {FrameCounter})", _frameCounter);
            throw;
        }
    }

    private void RenderTileMap(ref TileMap tileMap)
    {
        try
        {
            _logger?.LogDebug("  → TileMap #{TileMapNum}: TilesetId='{TilesetId}', Size={Width}x{Height} tiles",
                _tileMapCount, tileMap.TilesetId, tileMap.Width, tileMap.Height);

            // Get tileset texture
            if (!_assetManager.HasTexture(tileMap.TilesetId))
            {
                _logger?.LogWarning("    ⚠️  Tileset '{TilesetId}' NOT FOUND in AssetManager - skipping map", tileMap.TilesetId);
                return; // Tileset not loaded
            }

            var tilesetTexture = _assetManager.GetTexture(tileMap.TilesetId);
            int tilesPerRow = tilesetTexture.Width / TileSize;
            _logger?.LogDebug("    ✓ Tileset found: {Width}x{Height}px, {TilesPerRow} tiles per row",
                tilesetTexture.Width, tilesetTexture.Height, tilesPerRow);

            // Render ground layer
            _logger?.LogDebug("    Rendering GROUND layer...");
            int groundTiles = RenderLayer(tileMap.GroundLayer, tileMap.Width, tileMap.Height, tilesetTexture, tilesPerRow, "Ground");

            // Render object layer
            _logger?.LogDebug("    Rendering OBJECT layer...");
            int objectTiles = RenderLayer(tileMap.ObjectLayer, tileMap.Width, tileMap.Height, tilesetTexture, tilesPerRow, "Object");

            _logger?.LogDebug("    ✓ TileMap rendered: Ground={GroundTiles}, Object={ObjectTiles} tiles", groundTiles, objectTiles);

            // Note: Overhead layer should be rendered after sprites in a separate pass
            // For now, we skip it to keep rendering order simple
            _logger?.LogDebug("    (Overhead layer skipped - should render after sprites)");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "    ❌ ERROR rendering tilemap with TilesetId '{TilesetId}'", tileMap.TilesetId);
        }
    }

    private int RenderLayer(int[,] layer, int mapWidth, int mapHeight, Texture2D tilesetTexture, int tilesPerRow, string layerName)
    {
        int tilesRendered = 0;
        int tilesSkipped = 0;

        try
        {
            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    int tileId = layer[y, x];

                    // Skip empty tiles (ID 0)
                    if (tileId == 0)
                    {
                        tilesSkipped++;
                        continue;
                    }

                    // Calculate source rectangle from tile ID
                    var sourceRect = GetTileSourceRect(tileId, TileSize, tilesPerRow);

                    if (sourceRect.IsEmpty)
                    {
                        _logger?.LogWarning("      ⚠️  Invalid tile ID {TileId} at ({X}, {Y}) in {LayerName} layer",
                            tileId, x, y, layerName);
                        continue;
                    }

                    // Calculate world position
                    var position = new Vector2(x * TileSize, y * TileSize);

                    // Draw tile
                    _spriteBatch.Draw(
                        texture: tilesetTexture,
                        position: position,
                        sourceRectangle: sourceRect,
                        color: Color.White);

                    tilesRendered++;
                    _totalTilesRendered++;

                    // Log first few tiles for debugging
                    if (tilesRendered <= 3)
                    {
                        _logger?.LogDebug("      Tile: ID={TileId}, Grid=({X},{Y}), Pixel=({PixelX},{PixelY}), Source=({SrcX},{SrcY},{SrcW},{SrcH})",
                            tileId, x, y, position.X, position.Y, sourceRect.X, sourceRect.Y, sourceRect.Width, sourceRect.Height);
                    }
                }
            }

            _logger?.LogDebug("      {LayerName} layer: {Rendered} tiles rendered, {Skipped} empty tiles skipped",
                layerName, tilesRendered, tilesSkipped);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "      ❌ ERROR rendering {LayerName} layer", layerName);
        }

        return tilesRendered;
    }

    private static Rectangle GetTileSourceRect(int tileId, int tileSize, int tilesPerRow)
    {
        // Tiled uses 1-based tile IDs, we need 0-based
        int tileIndex = tileId - 1;

        if (tileIndex < 0)
        {
            return Rectangle.Empty;
        }

        int sourceX = (tileIndex % tilesPerRow) * tileSize;
        int sourceY = (tileIndex / tilesPerRow) * tileSize;

        return new Rectangle(sourceX, sourceY, tileSize, tileSize);
    }
}
