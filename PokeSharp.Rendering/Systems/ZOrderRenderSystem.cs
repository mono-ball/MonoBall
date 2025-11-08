using System.Diagnostics;
using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Components.Player;
using PokeSharp.Core.Components.Rendering;
using PokeSharp.Core.Components.Tiles;
using PokeSharp.Core.Logging;
using PokeSharp.Core.Systems;
using PokeSharp.Rendering.Assets;
using PokeSharp.Rendering.Components;

namespace PokeSharp.Rendering.Systems;

/// <summary>
///     Unified rendering system that renders tile layers in Tiled's order, with sprites
///     Y-sorted alongside the object layer. This follows standard Tiled conventions:
///     1. Render ground layer
///     2. Y-sort and render object layer tiles + all sprites together
///     3. Render overhead layer (naturally appears on top due to layer order)
/// </summary>
public class ZOrderRenderSystem(
    GraphicsDevice graphicsDevice,
    AssetManager assetManager,
    ILogger<ZOrderRenderSystem>? logger = null
) : BaseSystem
{
    private const int TileSize = 16;
    private const float MaxRenderDistance = 10000f; // Maximum Y coordinate for normalization

    // Layer indices where sprites should be rendered (between object and overhead layers)
    private const int SpriteRenderAfterLayer = 1; // Render sprites after layer index 1 (Objects)

    private readonly AssetManager _assetManager =
        assetManager ?? throw new ArgumentNullException(nameof(assetManager));

    // Cache query descriptions to avoid allocation every frame
    private readonly QueryDescription _cameraQuery = new QueryDescription().WithAll<
        Player,
        Camera
    >();

    private readonly GraphicsDevice _graphicsDevice =
        graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));

    private readonly QueryDescription _groundTileQuery = new QueryDescription().WithAll<
        TilePosition,
        TileSprite
    >();

    private readonly QueryDescription _groundTileWithOffsetQuery = new QueryDescription().WithAll<
        TilePosition,
        TileSprite,
        LayerOffset
    >();

    private readonly ILogger<ZOrderRenderSystem>? _logger = logger;

    private readonly QueryDescription _movingSpriteQuery = new QueryDescription().WithAll<
        Position,
        Sprite,
        GridMovement
    >();

    private readonly QueryDescription _objectTileQuery = new QueryDescription().WithAll<
        TilePosition,
        TileSprite
    >();

    private readonly QueryDescription _overheadTileQuery = new QueryDescription().WithAll<
        TilePosition,
        TileSprite
    >();

    private readonly QueryDescription _imageLayerQuery = new QueryDescription().WithAll<ImageLayer>();

    private readonly SpriteBatch _spriteBatch = new(graphicsDevice);

    private readonly QueryDescription _staticSpriteQuery = new QueryDescription()
        .WithAll<Position, Sprite>()
        .WithNone<GridMovement>();

    private Rectangle? _cachedCameraBounds;

    // Cache camera transform to avoid recalculating
    private Matrix _cachedCameraTransform = Matrix.Identity;

    // Performance profiling
    private bool _enableDetailedProfiling;

    private ulong _frameCounter;
    private int _lastEntityCount;
    private int _lastSpriteCount;
    private int _lastTileCount;

    private double _setupTime,
        _batchBeginTime,
        _groundTime,
        _objectTime,
        _spriteTime,
        _overheadTime,
        _batchEndTime;

    /// <inheritdoc />
    public override int Priority => SystemPriority.Render;

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        try
        {
            EnsureInitialized();
            _frameCounter++;

            // Only run detailed profiling when explicitly enabled (adds overhead)
            if (_enableDetailedProfiling)
            {
                UpdateWithProfiling(world);
                return;
            }

            // Fast path - no profiling overhead
            UpdateCameraCache(world);

            _spriteBatch.Begin(
                SpriteSortMode.BackToFront,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                transformMatrix: _cachedCameraTransform
            );

            var totalTilesRendered = 0;
            totalTilesRendered += RenderTileLayer(world, TileLayer.Ground);
            totalTilesRendered += RenderTileLayer(world, TileLayer.Object);

            // Render image layers (they sort with everything via layerDepth)
            var imageLayerCount = RenderImageLayers(world);

            var spriteCount = 0;
            world.Query(
                in _movingSpriteQuery,
                (ref Position position, ref Sprite sprite, ref GridMovement movement) =>
                {
                    spriteCount++;
                    RenderMovingSprite(ref position, ref sprite, ref movement);
                }
            );

            world.Query(
                in _staticSpriteQuery,
                (ref Position position, ref Sprite sprite) =>
                {
                    spriteCount++;
                    RenderStaticSprite(ref position, ref sprite);
                }
            );

            totalTilesRendered += RenderTileLayer(world, TileLayer.Overhead);

            if (_frameCounter % 300 == 0)
            {
                var totalEntities = totalTilesRendered + spriteCount + imageLayerCount;
                _logger?.LogRenderStats(
                    totalEntities,
                    totalTilesRendered,
                    spriteCount,
                    _frameCounter
                );
                if (imageLayerCount > 0)
                {
                    _logger?.LogDebug(
                        "[dim]Image Layers Rendered:[/] [magenta]{ImageLayerCount}[/]",
                        imageLayerCount
                    );
                }
            }

            _lastEntityCount = totalTilesRendered + spriteCount + imageLayerCount;
            _lastTileCount = totalTilesRendered;
            _lastSpriteCount = spriteCount;

            _spriteBatch.End();
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "Error in ZOrderRenderSystem.Update (Frame {FrameCounter})",
                _frameCounter
            );
            throw;
        }
    }

    /// <summary>
    ///     Update with detailed profiling enabled (slower, for diagnostics only).
    /// </summary>
    private void UpdateWithProfiling(World world)
    {
        var swSetup = Stopwatch.StartNew();
        UpdateCameraCache(world);
        swSetup.Stop();
        _setupTime = swSetup.Elapsed.TotalMilliseconds;

        var swBatchBegin = Stopwatch.StartNew();
        _spriteBatch.Begin(
            SpriteSortMode.BackToFront,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            transformMatrix: _cachedCameraTransform
        );
        swBatchBegin.Stop();
        _batchBeginTime = swBatchBegin.Elapsed.TotalMilliseconds;

        var totalTilesRendered = 0;

        var swGround = Stopwatch.StartNew();
        totalTilesRendered += RenderTileLayer(world, TileLayer.Ground);
        swGround.Stop();
        _groundTime = swGround.Elapsed.TotalMilliseconds;

        var swObject = Stopwatch.StartNew();
        totalTilesRendered += RenderTileLayer(world, TileLayer.Object);
        swObject.Stop();
        _objectTime = swObject.Elapsed.TotalMilliseconds;

        // Render image layers
        var imageLayerCount = RenderImageLayers(world);

        var swSprites = Stopwatch.StartNew();
        var spriteCount = 0;

        world.Query(
            in _movingSpriteQuery,
            (ref Position position, ref Sprite sprite, ref GridMovement movement) =>
            {
                spriteCount++;
                RenderMovingSprite(ref position, ref sprite, ref movement);
            }
        );

        world.Query(
            in _staticSpriteQuery,
            (ref Position position, ref Sprite sprite) =>
            {
                spriteCount++;
                RenderStaticSprite(ref position, ref sprite);
            }
        );
        swSprites.Stop();
        _spriteTime = swSprites.Elapsed.TotalMilliseconds;

        var swOverhead = Stopwatch.StartNew();
        totalTilesRendered += RenderTileLayer(world, TileLayer.Overhead);
        swOverhead.Stop();
        _overheadTime = swOverhead.Elapsed.TotalMilliseconds;

        var swBatchEnd = Stopwatch.StartNew();
        _spriteBatch.End();
        swBatchEnd.Stop();
        _batchEndTime = swBatchEnd.Elapsed.TotalMilliseconds;

        if (_frameCounter % 300 == 0)
        {
            var totalEntities = totalTilesRendered + spriteCount + imageLayerCount;
            _logger?.LogRenderStats(totalEntities, totalTilesRendered, spriteCount, _frameCounter);

            _logger?.LogInformation(
                "Render breakdown: Setup={0:F2}ms, Begin={1:F2}ms, Ground={2:F2}ms, Object={3:F2}ms, Sprites={4:F2}ms, Overhead={5:F2}ms, End={6:F2}ms",
                _setupTime,
                _batchBeginTime,
                _groundTime,
                _objectTime,
                _spriteTime,
                _overheadTime,
                _batchEndTime
            );
            if (imageLayerCount > 0)
            {
                _logger?.LogDebug(
                    "[dim]Image Layers Rendered:[/] [magenta]{ImageLayerCount}[/]",
                    imageLayerCount
                );
            }
        }

        _lastEntityCount = totalTilesRendered + spriteCount + imageLayerCount;
        _lastTileCount = totalTilesRendered;
        _lastSpriteCount = spriteCount;
    }

    /// <summary>
    ///     Enables or disables detailed per-frame profiling breakdown.
    /// </summary>
    public void SetDetailedProfiling(bool enabled)
    {
        _enableDetailedProfiling = enabled;
        _logger?.LogInformation("Detailed profiling {State}", enabled ? "enabled" : "disabled");
    }

    /// <summary>
    ///     Preloads all textures used by tiles in the world to avoid loading spikes during gameplay.
    ///     Call this after loading a new map.
    /// </summary>
    public void PreloadMapAssets(World world)
    {
        var sw = Stopwatch.StartNew();
        var texturesNeeded = new HashSet<string>();

        // Gather all tile textures
        world.Query(
            in _groundTileQuery,
            (ref TileSprite sprite) =>
            {
                texturesNeeded.Add(sprite.TilesetId);
            }
        );

        // Gather all sprite textures
        world.Query(
            in _movingSpriteQuery,
            (ref Sprite sprite) =>
            {
                texturesNeeded.Add(sprite.TextureId);
            }
        );

        world.Query(
            in _staticSpriteQuery,
            (ref Sprite sprite) =>
            {
                texturesNeeded.Add(sprite.TextureId);
            }
        );

        // Preload all textures
        foreach (var textureId in texturesNeeded)
            if (!_assetManager.HasTexture(textureId))
            {
                _logger?.LogDebug("Preloading texture: {TextureId}", textureId);
                _assetManager.GetTexture(textureId); // Force load now
            }

        sw.Stop();
        _logger?.LogInformation(
            "Preloaded {Count} textures in {TimeMs:F2}ms",
            texturesNeeded.Count,
            sw.Elapsed.TotalMilliseconds
        );
    }

    /// <summary>
    ///     Updates the cached camera transform and bounds once per frame.
    /// </summary>
    private void UpdateCameraCache(World world)
    {
        _cachedCameraTransform = Matrix.Identity;
        _cachedCameraBounds = null;

        world.Query(
            in _cameraQuery,
            (ref Camera camera) =>
            {
                _cachedCameraTransform = camera.GetTransformMatrix();

                // Calculate camera bounds for culling
                const int margin = 2;
                var left =
                    (int)(camera.Position.X / TileSize)
                    - camera.Viewport.Width / 2 / TileSize / (int)camera.Zoom
                    - margin;
                var top =
                    (int)(camera.Position.Y / TileSize)
                    - camera.Viewport.Height / 2 / TileSize / (int)camera.Zoom
                    - margin;
                var width = camera.Viewport.Width / TileSize / (int)camera.Zoom + margin * 2;
                var height = camera.Viewport.Height / TileSize / (int)camera.Zoom + margin * 2;

                _cachedCameraBounds = new Rectangle(left, top, width, height);
            }
        );
    }

    private int RenderTileLayer(World world, TileLayer layer)
    {
        var tilesRendered = 0;
        var tilesCulled = 0;

        try
        {
            // Use cached camera bounds instead of querying every time
            var cameraBounds = _cachedCameraBounds;

            // Render tiles with layer offsets (parallax scrolling)
            world.Query(
                in _groundTileWithOffsetQuery,
                (ref TilePosition pos, ref TileSprite sprite, ref LayerOffset offset) =>
                {
                    // Filter by layer (unavoidable - can't query by component field)
                    if (sprite.Layer != layer)
                        return;

                    // Viewport culling: skip tiles outside camera bounds
                    if (cameraBounds.HasValue)
                        if (
                            pos.X < cameraBounds.Value.Left
                            || pos.X >= cameraBounds.Value.Right
                            || pos.Y < cameraBounds.Value.Top
                            || pos.Y >= cameraBounds.Value.Bottom
                        )
                        {
                            tilesCulled++;
                            return;
                        }

                    // Get tileset texture
                    if (!_assetManager.HasTexture(sprite.TilesetId))
                    {
                        if (tilesRendered == 0) // Only warn once per layer
                            _logger?.LogWarning(
                                "  WARNING: Tileset '{TilesetId}' NOT FOUND - skipping tiles",
                                sprite.TilesetId
                            );
                        return;
                    }

                    var texture = _assetManager.GetTexture(sprite.TilesetId);

                    // Apply layer offset to position for parallax effect
                    var position = new Vector2(
                        pos.X * TileSize + offset.X,
                        pos.Y * TileSize + offset.Y
                    );

                    // Calculate layer depth based on layer type
                    var layerDepth = layer switch
                    {
                        TileLayer.Ground => 0.95f, // Back
                        TileLayer.Object => CalculateYSortDepth(position.Y + TileSize), // Y-sorted
                        TileLayer.Overhead => 0.05f, // Front
                        _ => 0.5f,
                    };

                    // Apply flip flags from Tiled
                    var effects = SpriteEffects.None;
                    if (sprite.FlipHorizontally)
                        effects |= SpriteEffects.FlipHorizontally;
                    if (sprite.FlipVertically)
                        effects |= SpriteEffects.FlipVertically;

                    // Note: Diagonal flip (rotate 90° then flip) is not directly supported by MonoGame's SpriteEffects.
                    // For full diagonal flip support, we would need to use rotation parameter.
                    // This is rarely used in practice, so we skip it for now.

                    // Render tile
                    _spriteBatch.Draw(
                        texture,
                        position,
                        sprite.SourceRect,
                        Color.White,
                        0f,
                        Vector2.Zero,
                        1f,
                        effects,
                        layerDepth
                    );

                    tilesRendered++;
                }
            );

            // Render tiles without layer offsets (standard positioning)
            world.Query(
                in _groundTileQuery, // All queries are the same, just reusing
                (Entity entity, ref TilePosition pos, ref TileSprite sprite) =>
                {
                    // Skip if this tile has a LayerOffset component (already rendered above)
                    if (world.Has<LayerOffset>(entity))
                        return;

                    // Filter by layer (unavoidable - can't query by component field)
                    if (sprite.Layer != layer)
                        return;

                    // Viewport culling: skip tiles outside camera bounds
                    if (cameraBounds.HasValue)
                        if (
                            pos.X < cameraBounds.Value.Left
                            || pos.X >= cameraBounds.Value.Right
                            || pos.Y < cameraBounds.Value.Top
                            || pos.Y >= cameraBounds.Value.Bottom
                        )
                        {
                            tilesCulled++;
                            return;
                        }

                    // Get tileset texture
                    if (!_assetManager.HasTexture(sprite.TilesetId))
                    {
                        if (tilesRendered == 0) // Only warn once per layer
                            _logger?.LogWarning(
                                "  WARNING: Tileset '{TilesetId}' NOT FOUND - skipping tiles",
                                sprite.TilesetId
                            );
                        return;
                    }

                    var texture = _assetManager.GetTexture(sprite.TilesetId);
                    var position = new Vector2(pos.X * TileSize, pos.Y * TileSize);

                    // Calculate layer depth based on layer type
                    var layerDepth = layer switch
                    {
                        TileLayer.Ground => 0.95f, // Back
                        TileLayer.Object => CalculateYSortDepth(position.Y + TileSize), // Y-sorted
                        TileLayer.Overhead => 0.05f, // Front
                        _ => 0.5f,
                    };

                    // Apply flip flags from Tiled
                    var effects = SpriteEffects.None;
                    if (sprite.FlipHorizontally)
                        effects |= SpriteEffects.FlipHorizontally;
                    if (sprite.FlipVertically)
                        effects |= SpriteEffects.FlipVertically;

                    // Note: Diagonal flip (rotate 90° then flip) is not directly supported by MonoGame's SpriteEffects.
                    // For full diagonal flip support, we would need to use rotation parameter.
                    // This is rarely used in practice, so we skip it for now.

                    // Render tile
                    _spriteBatch.Draw(
                        texture,
                        position,
                        sprite.SourceRect,
                        Color.White,
                        0f,
                        Vector2.Zero,
                        1f,
                        effects,
                        layerDepth
                    );

                    tilesRendered++;
                }
            );

            // Removed per-frame layer logging - see periodic stats in Update()
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "  ERROR rendering {Layer} layer", layer);
        }

        return tilesRendered;
    }

    private void RenderMovingSprite(
        ref Position position,
        ref Sprite sprite,
        ref GridMovement movement
    )
    {
        try
        {
            // Get texture from AssetManager
            if (!_assetManager.HasTexture(sprite.TextureId))
            {
                _logger?.LogWarning(
                    "    WARNING: Texture '{TextureId}' NOT FOUND in AssetManager - skipping sprite",
                    sprite.TextureId
                );
                return;
            }

            var texture = _assetManager.GetTexture(sprite.TextureId);

            // Determine source rectangle
            var sourceRect = sprite.SourceRect;
            if (sourceRect.IsEmpty)
                sourceRect = new Rectangle(0, 0, texture.Width, texture.Height);

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
                var targetGridY = (int)(movement.TargetPosition.Y / TileSize);
                groundY = (targetGridY + 1) * TileSize; // +1 for bottom of tile
            }
            else
            {
                // Use current grid position
                groundY = (position.Y + 1) * TileSize;
            }

            var layerDepth = CalculateYSortDepth(groundY);

            // Draw sprite
            _spriteBatch.Draw(
                texture,
                renderPosition,
                sourceRect,
                sprite.Tint,
                sprite.Rotation,
                sprite.Origin,
                sprite.Scale,
                SpriteEffects.None,
                layerDepth
            );

            // Removed per-entity logging - too verbose for rendering loop
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "    ERROR rendering moving sprite with TextureId '{TextureId}' at position ({X}, {Y})",
                sprite.TextureId,
                position.PixelX,
                position.PixelY
            );
        }
    }

    private void RenderStaticSprite(ref Position position, ref Sprite sprite)
    {
        try
        {
            // Get texture from AssetManager
            if (!_assetManager.HasTexture(sprite.TextureId))
            {
                _logger?.LogWarning(
                    "    WARNING: Texture '{TextureId}' NOT FOUND in AssetManager - skipping sprite",
                    sprite.TextureId
                );
                return;
            }

            var texture = _assetManager.GetTexture(sprite.TextureId);

            // Determine source rectangle
            var sourceRect = sprite.SourceRect;
            if (sourceRect.IsEmpty)
                sourceRect = new Rectangle(0, 0, texture.Width, texture.Height);

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
            var layerDepth = CalculateYSortDepth(groundY);

            // Draw sprite
            _spriteBatch.Draw(
                texture,
                renderPosition,
                sourceRect,
                sprite.Tint,
                sprite.Rotation,
                sprite.Origin,
                sprite.Scale,
                SpriteEffects.None,
                layerDepth
            );

            // Removed per-entity logging - too verbose for rendering loop
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "    ERROR rendering sprite with TextureId '{TextureId}' at position ({X}, {Y})",
                sprite.TextureId,
                position.PixelX,
                position.PixelY
            );
        }
    }

    /// <summary>
    ///     Calculates layer depth for Y-sorting within the object layer range (0.4-0.6).
    ///     Lower Y positions (top of screen) get higher layer depth (render first/behind).
    ///     Higher Y positions (bottom of screen) get lower layer depth (render last/in front).
    ///     This range allows object tiles and sprites to sort together while staying between
    ///     ground layer (0.95) and overhead layer (0.05).
    /// </summary>
    /// <param name="yPosition">The Y position (typically bottom of sprite/tile).</param>
    /// <returns>Layer depth value between 0.4 (front) and 0.6 (back) for Y-sorting.</returns>
    private static float CalculateYSortDepth(float yPosition)
    {
        // Normalize Y position to 0.0-1.0 range
        var normalized = yPosition / MaxRenderDistance;

        // Map to Y-sort range: 0.6 (back/top) to 0.4 (front/bottom)
        // Lower Y = 0.6, Higher Y = 0.4
        var layerDepth = 0.6f - normalized * 0.2f;

        // Clamp to Y-sort range
        return MathHelper.Clamp(layerDepth, 0.4f, 0.6f);
    }

    /// <summary>
    ///     Renders all image layers with proper Z-ordering and transparency.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <returns>Number of image layers rendered.</returns>
    private int RenderImageLayers(World world)
    {
        var rendered = 0;

        try
        {
            world.Query(
                in _imageLayerQuery,
                (ref ImageLayer imageLayer) =>
                {
                    // Get texture from AssetManager
                    if (!_assetManager.HasTexture(imageLayer.TextureId))
                    {
                        if (rendered == 0) // Only warn once
                            _logger?.LogWarning(
                                "  WARNING: Image layer texture '{TextureId}' NOT FOUND - skipping",
                                imageLayer.TextureId
                            );
                        return;
                    }

                    var texture = _assetManager.GetTexture(imageLayer.TextureId);

                    // Render the full image at the specified position
                    _spriteBatch.Draw(
                        texture,
                        imageLayer.Position,
                        null, // No source rect - use full texture
                        imageLayer.TintColor, // Apply opacity via tint
                        0f,
                        Vector2.Zero,
                        1f,
                        SpriteEffects.None,
                        imageLayer.LayerDepth
                    );

                    rendered++;
                }
            );
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "  ERROR rendering image layers");
        }

        return rendered;
    }
}
