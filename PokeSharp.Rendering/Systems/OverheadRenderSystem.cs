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
/// System responsible for rendering overhead tiles (trees, roofs, bridges) that appear above sprites.
/// This creates the Pokemon-authentic depth illusion where the player can walk under trees and roofs.
/// </summary>
public class OverheadRenderSystem : BaseSystem
{
    private const int TileSize = 16;

    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly AssetManager _assetManager;
    private readonly ILogger<OverheadRenderSystem>? _logger;
    private ulong _frameCounter = 0;
    private int _tileMapCount = 0;
    private int _totalTilesRendered = 0;

    /// <summary>
    /// Initializes a new instance of the OverheadRenderSystem class.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device for rendering.</param>
    /// <param name="assetManager">Asset manager for texture loading.</param>
    /// <param name="logger">Optional logger for debug output.</param>
    public OverheadRenderSystem(GraphicsDevice graphicsDevice, AssetManager assetManager, ILogger<OverheadRenderSystem>? logger = null)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _assetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));
        _spriteBatch = new SpriteBatch(_graphicsDevice);
        _logger = logger;
    }

    /// <inheritdoc/>
    public override int Priority => SystemPriority.Overhead;

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
            _logger?.LogDebug("OverheadRenderSystem - Frame {FrameCounter} - Starting overhead rendering (RUNS AFTER SPRITES - Priority {Priority})",
                _frameCounter, Priority);
            _logger?.LogDebug("═══════════════════════════════════════════════════");

            // Get camera transform matrix (if camera exists)
            Matrix cameraTransform = Matrix.Identity;
            var cameraQuery = new QueryDescription().WithAll<Player, Camera>();
            world.Query(in cameraQuery, (ref Camera camera) =>
            {
                cameraTransform = camera.GetTransformMatrix();
            });

            // Begin sprite batch for overhead layer rendering with camera transform
            _spriteBatch.Begin(
                sortMode: SpriteSortMode.Deferred,
                blendState: BlendState.AlphaBlend,
                samplerState: SamplerState.PointClamp,
                transformMatrix: cameraTransform);

            _logger?.LogDebug("SpriteBatch started (Deferred, AlphaBlend, PointClamp)");

            // Query and render overhead layer for all tile maps
            var query = new QueryDescription().WithAll<TileMap>();
            _logger?.LogDebug("Querying entities with TileMap component...");

            world.Query(in query, (ref TileMap tileMap) =>
            {
                _tileMapCount++;
                RenderOverheadLayer(ref tileMap);
            });

            _spriteBatch.End();

            _logger?.LogDebug("SpriteBatch ended");
            _logger?.LogDebug("───────────────────────────────────────────────────");
            _logger?.LogDebug("OverheadRenderSystem - Frame {FrameCounter} - Completed. TileMaps: {TileMapCount}, Overhead tiles: {TotalTiles}",
                _frameCounter, _tileMapCount, _totalTilesRendered);
            _logger?.LogDebug("═══════════════════════════════════════════════════");

            if (_tileMapCount == 0)
            {
                _logger?.LogWarning("⚠️  WARNING: No tile maps found to render in frame {FrameCounter}", _frameCounter);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ CRITICAL ERROR in OverheadRenderSystem.Update (Frame {FrameCounter})", _frameCounter);
            throw;
        }
    }

    private void RenderOverheadLayer(ref TileMap tileMap)
    {
        try
        {
            _logger?.LogDebug("  → TileMap #{TileMapNum}: TilesetId='{TilesetId}', Size={Width}x{Height} tiles",
                _tileMapCount, tileMap.TilesetId, tileMap.Width, tileMap.Height);

            // Get tileset texture
            if (!_assetManager.HasTexture(tileMap.TilesetId))
            {
                _logger?.LogWarning("    ⚠️  Tileset '{TilesetId}' NOT FOUND in AssetManager - skipping overhead layer", tileMap.TilesetId);
                return; // Tileset not loaded
            }

            var tilesetTexture = _assetManager.GetTexture(tileMap.TilesetId);
            int tilesPerRow = tilesetTexture.Width / TileSize;
            _logger?.LogDebug("    ✓ Tileset found: {Width}x{Height}px, {TilesPerRow} tiles per row",
                tilesetTexture.Width, tilesetTexture.Height, tilesPerRow);

            // Render overhead layer ONLY
            _logger?.LogDebug("    Rendering OVERHEAD layer (trees, roofs, bridges)...");
            int overheadTiles = RenderLayer(tileMap.OverheadLayer, tileMap.Width, tileMap.Height, tilesetTexture, tilesPerRow);

            _logger?.LogDebug("    ✓ Overhead layer rendered: {OverheadTiles} tiles", overheadTiles);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "    ❌ ERROR rendering overhead layer for tilemap with TilesetId '{TilesetId}'", tileMap.TilesetId);
        }
    }

    private int RenderLayer(int[,] layer, int mapWidth, int mapHeight, Texture2D tilesetTexture, int tilesPerRow)
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
                        _logger?.LogWarning("      ⚠️  Invalid tile ID {TileId} at ({X}, {Y}) in Overhead layer",
                            tileId, x, y);
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
                        _logger?.LogDebug("      Overhead Tile: ID={TileId}, Grid=({X},{Y}), Pixel=({PixelX},{PixelY}), Source=({SrcX},{SrcY},{SrcW},{SrcH})",
                            tileId, x, y, position.X, position.Y, sourceRect.X, sourceRect.Y, sourceRect.Width, sourceRect.Height);
                    }
                }
            }

            _logger?.LogDebug("      Overhead layer: {Rendered} tiles rendered, {Skipped} empty tiles skipped",
                tilesRendered, tilesSkipped);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "      ❌ ERROR rendering Overhead layer");
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
