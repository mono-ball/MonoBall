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
/// Unified rendering system that renders tile layers in Tiled's order, with sprites
/// Y-sorted alongside the object layer. This follows standard Tiled conventions:
/// 1. Render ground layer
/// 2. Y-sort and render object layer tiles + all sprites together
/// 3. Render overhead layer (naturally appears on top due to layer order)
/// </summary>
public class ZOrderRenderSystem : BaseSystem
{
    private const int TileSize = 16;
    private const float MaxRenderDistance = 10000f; // Maximum Y coordinate for normalization
    
    // Layer indices where sprites should be rendered (between object and overhead layers)
    private const int SpriteRenderAfterLayer = 1; // Render sprites after layer index 1 (Objects)

    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly AssetManager _assetManager;
    private readonly ILogger<ZOrderRenderSystem>? _logger;
    private ulong _frameCounter = 0;

    /// <summary>
    /// Initializes a new instance of the ZOrderRenderSystem class.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device for rendering.</param>
    /// <param name="assetManager">Asset manager for texture loading.</param>
    /// <param name="logger">Optional logger for debug output.</param>
    public ZOrderRenderSystem(GraphicsDevice graphicsDevice, AssetManager assetManager, ILogger<ZOrderRenderSystem>? logger = null)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _assetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));
        _spriteBatch = new SpriteBatch(_graphicsDevice);
        _logger = logger;
    }

    /// <inheritdoc/>
    public override int Priority => SystemPriority.Render;

    /// <inheritdoc/>
    public override void Update(World world, float deltaTime)
    {
        try
        {
            EnsureInitialized();
            _frameCounter++;

            _logger?.LogDebug("═══════════════════════════════════════════════════");
            _logger?.LogDebug("ZOrderRenderSystem - Frame {FrameCounter} - Starting Z-order rendering", _frameCounter);
            _logger?.LogDebug("═══════════════════════════════════════════════════");

            // Get camera transform matrix (if camera exists)
            Matrix cameraTransform = Matrix.Identity;
            var cameraQuery = new QueryDescription().WithAll<Player, Camera>();
            world.Query(in cameraQuery, (ref Camera camera) =>
            {
                cameraTransform = camera.GetTransformMatrix();
                _logger?.LogDebug("Camera transform applied: Position=({X:F1}, {Y:F1}), Zoom={Zoom:F2}x",
                    camera.Position.X, camera.Position.Y, camera.Zoom);
            });

            // Begin sprite batch with BackToFront sorting for proper Z-ordering
            _spriteBatch.Begin(
                sortMode: SpriteSortMode.BackToFront,
                blendState: BlendState.AlphaBlend,
                samplerState: SamplerState.PointClamp,
                transformMatrix: cameraTransform);

            _logger?.LogDebug("SpriteBatch started (BackToFront, AlphaBlend, PointClamp)");

            // Render all tile maps
            int tileMapCount = 0;
            int totalTilesRendered = 0;
            var tileMapQuery = new QueryDescription().WithAll<TileMap>();
            
            world.Query(in tileMapQuery, (ref TileMap tileMap) =>
            {
                tileMapCount++;
                totalTilesRendered += RenderTileMap(ref tileMap);
            });

            _logger?.LogDebug("Rendered {TileMapCount} tile maps with {TotalTiles} tiles", tileMapCount, totalTilesRendered);

            // Render all sprites (player, NPCs, objects)
            int spriteCount = 0;
            
            // Query for entities WITH GridMovement (moving entities)
            var movingSpriteQuery = new QueryDescription().WithAll<Position, Sprite, GridMovement>();
            world.Query(in movingSpriteQuery, (ref Position position, ref Sprite sprite, ref GridMovement movement) =>
            {
                spriteCount++;
                RenderMovingSprite(ref position, ref sprite, ref movement);
            });
            
            // Query for entities WITHOUT GridMovement (static sprites)
            var staticSpriteQuery = new QueryDescription().WithAll<Position, Sprite>().WithNone<GridMovement>();
            world.Query(in staticSpriteQuery, (ref Position position, ref Sprite sprite) =>
            {
                spriteCount++;
                RenderStaticSprite(ref position, ref sprite);
            });

            _logger?.LogDebug("Rendered {SpriteCount} sprites", spriteCount);

            // End sprite batch
            _spriteBatch.End();

            _logger?.LogDebug("───────────────────────────────────────────────────");
            _logger?.LogDebug("ZOrderRenderSystem - Frame {FrameCounter} - Completed", _frameCounter);
            _logger?.LogDebug("═══════════════════════════════════════════════════");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ CRITICAL ERROR in ZOrderRenderSystem.Update (Frame {FrameCounter})", _frameCounter);
            throw;
        }
    }

    private int RenderTileMap(ref TileMap tileMap)
    {
        try
        {
            _logger?.LogDebug("  → TileMap: TilesetId='{TilesetId}', Size={Width}x{Height} tiles",
                tileMap.TilesetId, tileMap.Width, tileMap.Height);

            // Get tileset texture
            if (!_assetManager.HasTexture(tileMap.TilesetId))
            {
                _logger?.LogWarning("    ⚠️  Tileset '{TilesetId}' NOT FOUND in AssetManager - skipping", tileMap.TilesetId);
                return 0;
            }

            var tilesetTexture = _assetManager.GetTexture(tileMap.TilesetId);
            int tilesPerRow = tilesetTexture.Width / TileSize;
            int tilesRendered = 0;

            // Render layers following Tiled's layer order:
            // Layer 0 (Ground): flat, layerDepth = 0.9-1.0
            // Layer 1 (Objects): Y-sorted with sprites, layerDepth = 0.4-0.6
            // Layer 2 (Overhead): flat on top, layerDepth = 0.0-0.1

            // Layer 0: Ground (rendered flat)
            _logger?.LogDebug("    Rendering GROUND layer (flat, at back)...");
            tilesRendered += RenderFlatLayer(tileMap.GroundLayer, tileMap.Width, tileMap.Height, tilesetTexture, tilesPerRow, 0.95f, "Ground");

            // Layer 1: Objects (Y-sorted, will be rendered with sprites)
            _logger?.LogDebug("    Rendering OBJECT layer (Y-sorted with sprites)...");
            tilesRendered += RenderYSortedLayer(tileMap.ObjectLayer, tileMap.Width, tileMap.Height, tilesetTexture, tilesPerRow, "Object");

            // Layer 2: Overhead (rendered flat on top)
            _logger?.LogDebug("    Rendering OVERHEAD layer (flat, on top)...");
            tilesRendered += RenderFlatLayer(tileMap.OverheadLayer, tileMap.Width, tileMap.Height, tilesetTexture, tilesPerRow, 0.05f, "Overhead");

            _logger?.LogDebug("    ✓ TileMap rendered: {TilesRendered} tiles", tilesRendered);
            return tilesRendered;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "    ❌ ERROR rendering tilemap with TilesetId '{TilesetId}'", tileMap.TilesetId);
            return 0;
        }
    }

    private int RenderFlatLayer(int[,] layer, int mapWidth, int mapHeight, Texture2D tilesetTexture, int tilesPerRow, float layerDepth, string layerName)
    {
        int tilesRendered = 0;

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                int tileId = layer[y, x];
                if (tileId == 0) continue; // Skip empty tiles

                var sourceRect = GetTileSourceRect(tileId, TileSize, tilesPerRow);
                if (sourceRect.IsEmpty) continue;

                var position = new Vector2(x * TileSize, y * TileSize);

                // Render with fixed layer depth
                _spriteBatch.Draw(
                    texture: tilesetTexture,
                    position: position,
                    sourceRectangle: sourceRect,
                    color: Color.White,
                    rotation: 0f,
                    origin: Vector2.Zero,
                    scale: 1f,
                    effects: SpriteEffects.None,
                    layerDepth: layerDepth);

                tilesRendered++;
            }
        }

        _logger?.LogDebug("      {LayerName} layer: {Rendered} tiles rendered at layerDepth={LayerDepth:F2}",
            layerName, tilesRendered, layerDepth);

        return tilesRendered;
    }

    private int RenderYSortedLayer(int[,] layer, int mapWidth, int mapHeight, Texture2D tilesetTexture, int tilesPerRow, string layerName)
    {
        int tilesRendered = 0;

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                int tileId = layer[y, x];
                if (tileId == 0) continue; // Skip empty tiles

                var sourceRect = GetTileSourceRect(tileId, TileSize, tilesPerRow);
                if (sourceRect.IsEmpty)
                {
                    _logger?.LogWarning("      ⚠️  Invalid tile ID {TileId} at ({X}, {Y}) in {LayerName} layer",
                        tileId, x, y, layerName);
                    continue;
                }

                var position = new Vector2(x * TileSize, y * TileSize);
                
                // Calculate layer depth based on Y position (bottom of tile)
                // This makes tiles sort with sprites based on Y position
                float yBottom = position.Y + TileSize;
                float layerDepth = CalculateYSortDepth(yBottom);

                _spriteBatch.Draw(
                    texture: tilesetTexture,
                    position: position,
                    sourceRectangle: sourceRect,
                    color: Color.White,
                    rotation: 0f,
                    origin: Vector2.Zero,
                    scale: 1f,
                    effects: SpriteEffects.None,
                    layerDepth: layerDepth);

                tilesRendered++;

                // Log first few tiles for debugging
                if (tilesRendered <= 2)
                {
                    _logger?.LogDebug("      {LayerName} Tile: ID={TileId}, Pos=({X},{Y}), YBottom={YBottom}, LayerDepth={LayerDepth:F4}",
                        layerName, tileId, x, y, yBottom, layerDepth);
                }
            }
        }

        _logger?.LogDebug("      {LayerName} layer: {Rendered} tiles rendered (Y-sorted)",
            layerName, tilesRendered);

        return tilesRendered;
    }

    private void RenderMovingSprite(ref Position position, ref Sprite sprite, ref GridMovement movement)
    {
        try
        {
            // Get texture from AssetManager
            if (!_assetManager.HasTexture(sprite.TextureId))
            {
                _logger?.LogWarning("    ⚠️  Texture '{TextureId}' NOT FOUND in AssetManager - skipping sprite", sprite.TextureId);
                return;
            }

            var texture = _assetManager.GetTexture(sprite.TextureId);

            // Determine source rectangle
            var sourceRect = sprite.SourceRect;
            if (sourceRect.IsEmpty)
            {
                sourceRect = new Rectangle(0, 0, texture.Width, texture.Height);
            }

            // Calculate render position (visual interpolated position)
            var renderPosition = new Vector2(position.PixelX, position.PixelY);

            // BEST PRACTICE FOR MOVING ENTITIES: Use TARGET position for depth sorting
            // When moving/jumping, sort based on where the entity is going, not where they started.
            // This prevents flickering and ensures entities sort correctly during movement.
            // For example, when jumping over a fence, the player should sort as if they're
            // already on the other side of the fence.
            float groundY;
            if (movement.IsMoving)
            {
                // Use target grid position for sorting
                int targetGridY = (int)(movement.TargetPosition.Y / TileSize);
                groundY = (targetGridY + 1) * TileSize; // +1 for bottom of tile
            }
            else
            {
                // Use current grid position
                groundY = (position.Y + 1) * TileSize;
            }
            
            float layerDepth = CalculateYSortDepth(groundY);

            // Draw sprite
            _spriteBatch.Draw(
                texture: texture,
                position: renderPosition,
                sourceRectangle: sourceRect,
                color: sprite.Tint,
                rotation: sprite.Rotation,
                origin: sprite.Origin,
                scale: sprite.Scale,
                effects: SpriteEffects.None,
                layerDepth: layerDepth);

            _logger?.LogDebug("    Moving Sprite: TextureId='{TextureId}', RenderPos=({X:F1},{Y:F1}), GroundY={GroundY:F1}, LayerDepth={LayerDepth:F4}",
                sprite.TextureId, position.PixelX, position.PixelY, groundY, layerDepth);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "    ❌ ERROR rendering moving sprite with TextureId '{TextureId}' at position ({X}, {Y})",
                sprite.TextureId, position.PixelX, position.PixelY);
        }
    }

    private void RenderStaticSprite(ref Position position, ref Sprite sprite)
    {
        try
        {
            // Get texture from AssetManager
            if (!_assetManager.HasTexture(sprite.TextureId))
            {
                _logger?.LogWarning("    ⚠️  Texture '{TextureId}' NOT FOUND in AssetManager - skipping sprite", sprite.TextureId);
                return;
            }

            var texture = _assetManager.GetTexture(sprite.TextureId);

            // Determine source rectangle
            var sourceRect = sprite.SourceRect;
            if (sourceRect.IsEmpty)
            {
                sourceRect = new Rectangle(0, 0, texture.Width, texture.Height);
            }

            // Calculate render position
            var renderPosition = new Vector2(position.PixelX, position.PixelY);

            // BEST PRACTICE: Calculate layer depth based on entity's GRID position, not visual pixel position.
            // This ensures:
            // 1. Sprites sort correctly even during movement/jumping animations
            // 2. Sort order doesn't change mid-movement (would cause flickering)
            // 3. Entities sort based on which grid tile they occupy, not their interpolated visual position
            //
            // The grid position represents where the entity logically is for gameplay purposes.
            // The pixel position is just the visual interpolation for smooth movement.
            // For a 16x16 tile grid, the entity's ground Y is at the bottom of their grid tile.
            float groundY = (position.Y + 1) * TileSize; // +1 because we want bottom of tile
            float layerDepth = CalculateYSortDepth(groundY);

            // Draw sprite
            _spriteBatch.Draw(
                texture: texture,
                position: renderPosition,
                sourceRectangle: sourceRect,
                color: sprite.Tint,
                rotation: sprite.Rotation,
                origin: sprite.Origin,
                scale: sprite.Scale,
                effects: SpriteEffects.None,
                layerDepth: layerDepth);

            _logger?.LogDebug("    Sprite: TextureId='{TextureId}', RenderPos=({X:F1},{Y:F1}), GroundY={GroundY:F1}, LayerDepth={LayerDepth:F4}",
                sprite.TextureId, position.PixelX, position.PixelY, groundY, layerDepth);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "    ❌ ERROR rendering sprite with TextureId '{TextureId}' at position ({X}, {Y})",
                sprite.TextureId, position.PixelX, position.PixelY);
        }
    }

    /// <summary>
    /// Calculates layer depth for Y-sorting within the object layer range (0.4-0.6).
    /// Lower Y positions (top of screen) get higher layer depth (render first/behind).
    /// Higher Y positions (bottom of screen) get lower layer depth (render last/in front).
    /// This range allows object tiles and sprites to sort together while staying between
    /// ground layer (0.95) and overhead layer (0.05).
    /// </summary>
    /// <param name="yPosition">The Y position (typically bottom of sprite/tile).</param>
    /// <returns>Layer depth value between 0.4 (front) and 0.6 (back) for Y-sorting.</returns>
    private static float CalculateYSortDepth(float yPosition)
    {
        // Normalize Y position to 0.0-1.0 range
        float normalized = yPosition / MaxRenderDistance;
        
        // Map to Y-sort range: 0.6 (back/top) to 0.4 (front/bottom)
        // Lower Y = 0.6, Higher Y = 0.4
        float layerDepth = 0.6f - (normalized * 0.2f);
        
        // Clamp to Y-sort range
        return MathHelper.Clamp(layerDepth, 0.4f, 0.6f);
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

