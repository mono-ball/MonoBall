using System.Diagnostics;
using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Core.Systems;
using PokeSharp.Engine.Rendering.Assets;
using PokeSharp.Engine.Rendering.Components;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.Player;
using PokeSharp.Game.Components.Rendering;
using PokeSharp.Game.Components.Tiles;
using AnimationComponent = PokeSharp.Game.Components.Rendering.Animation;

namespace PokeSharp.Engine.Rendering.Systems;

/// <summary>
/// Elevation-based rendering system using Pokemon Emerald's elevation model.
/// Renders all tiles and sprites sorted by: elevation (primary) + Y position (secondary).
/// This allows proper layering of bridges, overhead structures, and multi-level maps.
/// </summary>
/// <remarks>
/// <para>
/// <b>Render Order Formula:</b>
/// layerDepth = (elevation * 16) + (y / mapHeight)
/// </para>
/// <para>
/// <b>Elevation Levels (Pokemon Emerald):</b>
/// - 0: Ground level (water, pits)
/// - 3: Standard elevation (most tiles and objects)
/// - 6: Bridges, elevated platforms
/// - 9-12: Overhead structures
/// - 15: Maximum elevation
/// </para>
/// </remarks>
public class ElevationRenderSystem(
    GraphicsDevice graphicsDevice,
    AssetManager assetManager,
    ILogger<ElevationRenderSystem>? logger = null
) : SystemBase, IRenderSystem
{
    // Tile size is set from map data via SetTileSize()
    private int _tileSize = 16; // Default fallback, overridden by map data

    /// <summary>
    ///     Gets the tile size currently used for rendering.
    /// </summary>
    public int TileSize => _tileSize;
    private const float MapHeight = RenderingConstants.MaxRenderDistance;

    private readonly AssetManager _assetManager =
        assetManager ?? throw new ArgumentNullException(nameof(assetManager));

    /// <summary>
    /// Gets the AssetManager used by this render system.
    /// </summary>
    public AssetManager AssetManager => _assetManager;

    /// <summary>
    ///     Updates the tile size used by the renderer (falls back to 16 if invalid).
    /// </summary>
    public void SetTileSize(int tileSize)
    {
        var clamped = tileSize > 0 ? tileSize : 16; // Fallback to 16 if invalid
        if (_tileSize == clamped)
            return;

        _tileSize = clamped;
        _cachedCameraBounds = null;
        _cachedCameraTransform = Matrix.Identity;
        _logger?.LogInformation("Render tile size set to {TileSize}px", _tileSize);
    }

    /// <summary>
    /// Sets the sprite texture loader for lazy loading.
    /// </summary>
    public void SetSpriteTextureLoader(object loader)
    {
        _spriteTextureLoader = loader;
        _logger?.LogInformation("Sprite texture loader registered for lazy loading");
    }

    // Cache query descriptions to avoid allocation every frame
    private readonly QueryDescription _cameraQuery = QueryCache.Get<Player, Camera>();

    private readonly GraphicsDevice _graphicsDevice =
        graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));

    // Tile queries (with and without LayerOffset for parallax)
    private readonly QueryDescription _tileQuery = QueryCache.Get<
        TilePosition,
        TileSprite,
        Elevation
    >();

    private readonly QueryDescription _tileWithOffsetQuery = QueryCache.Get<
        TilePosition,
        TileSprite,
        LayerOffset,
        Elevation
    >();

    private readonly ILogger<ElevationRenderSystem>? _logger = logger;

    // Lazy sprite texture loader (set after initialization)
    private object? _spriteTextureLoader;

    // Sprite queries (moving and static)
    private readonly QueryDescription _movingSpriteQuery = QueryCache.Get<
        Position,
        Sprite,
        GridMovement,
        Elevation
    >();

    private readonly QueryDescription _staticSpriteQuery = QueryCache.Get<
        Position,
        Sprite,
        Elevation
    >();

    // Image layers (backgrounds, overlays)
    private readonly QueryDescription _imageLayerQuery = QueryCache.Get<ImageLayer>();

    private readonly SpriteBatch _spriteBatch = new(graphicsDevice);

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
        _tileTime,
        _spriteTime,
        _batchEndTime;

    /// <inheritdoc />
    public override int Priority => SystemPriority.Render;

    /// <summary>
    /// Gets the render order. Lower values render first.
    /// Order 1 renders entities after background (0) but before UI (2).
    /// </summary>
    public int RenderOrder => 1;

    /// <inheritdoc />
    /// <remarks>
    /// This is a render-only system. The Update method is not used.
    /// All rendering logic is in the Render method.
    /// </remarks>
    public override void Update(World world, float deltaTime)
    {
        // No-op: This is a render-only system. See Render() method.
    }

    /// <inheritdoc />
    public void Render(World world)
    {
        try
        {
            EnsureInitialized();
            _frameCounter++;

            // Only run detailed profiling when explicitly enabled (adds overhead)
            if (_enableDetailedProfiling)
            {
                RenderWithProfiling(world);
                return;
            }

            // Fast path - no profiling overhead
            UpdateCameraCache(world);

            _spriteBatch.Begin(
                SpriteSortMode.BackToFront, // Sort sprites by layerDepth for proper overlap
                BlendState.NonPremultiplied, // Use NonPremultiplied for PNG transparency
                SamplerState.PointClamp,
                DepthStencilState.None, // Disable depth buffer for 2D sprite transparency
                RasterizerState.CullNone,
                null,
                transformMatrix: _cachedCameraTransform
            );

            // Render image layers (they sort with everything via layerDepth)
            var imageLayerCount = RenderImageLayers(world);

            // Render all tiles (elevation + Y sorting via SpriteBatch)
            var totalTilesRendered = RenderAllTiles(world);

            // Render all sprites (elevation + Y sorting via SpriteBatch)
            var spriteCount = RenderAllSprites(world);

            if (_frameCounter % RenderingConstants.PerformanceLogInterval == 0)
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
                "Error in ElevationRenderSystem.Render (Frame {FrameCounter})",
                _frameCounter
            );
            throw;
        }
    }

    /// <summary>
    /// Render with detailed profiling enabled (slower, for diagnostics only).
    /// </summary>
    private void RenderWithProfiling(World world)
    {
        var swSetup = Stopwatch.StartNew();
        UpdateCameraCache(world);
        swSetup.Stop();
        _setupTime = swSetup.Elapsed.TotalMilliseconds;

        var swBatchBegin = Stopwatch.StartNew();
        _spriteBatch.Begin(
            SpriteSortMode.BackToFront, // Sort sprites by layerDepth for proper overlap
            BlendState.NonPremultiplied, // Use NonPremultiplied for PNG transparency
            SamplerState.PointClamp,
            DepthStencilState.None, // Disable depth buffer for 2D sprite transparency
            RasterizerState.CullNone,
            null,
            transformMatrix: _cachedCameraTransform
        );
        swBatchBegin.Stop();
        _batchBeginTime = swBatchBegin.Elapsed.TotalMilliseconds;

        // Render image layers
        var imageLayerCount = RenderImageLayers(world);

        var swTiles = Stopwatch.StartNew();
        var totalTilesRendered = RenderAllTiles(world);
        swTiles.Stop();
        _tileTime = swTiles.Elapsed.TotalMilliseconds;

        var swSprites = Stopwatch.StartNew();
        var spriteCount = RenderAllSprites(world);
        swSprites.Stop();
        _spriteTime = swSprites.Elapsed.TotalMilliseconds;

        var swBatchEnd = Stopwatch.StartNew();
        _spriteBatch.End();
        swBatchEnd.Stop();
        _batchEndTime = swBatchEnd.Elapsed.TotalMilliseconds;

        if (_frameCounter % 300 == 0)
        {
            var totalEntities = totalTilesRendered + spriteCount + imageLayerCount;
            _logger?.LogRenderStats(totalEntities, totalTilesRendered, spriteCount, _frameCounter);

            _logger?.LogInformation(
                "Render breakdown: Setup={0:F2}ms, Begin={1:F2}ms, Tiles={2:F2}ms, Sprites={3:F2}ms, End={4:F2}ms",
                _setupTime,
                _batchBeginTime,
                _tileTime,
                _spriteTime,
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
    /// Enables or disables detailed per-frame profiling breakdown.
    /// </summary>
    public void SetDetailedProfiling(bool enabled)
    {
        _enableDetailedProfiling = enabled;
        _logger?.LogInformation("Detailed profiling {State}", enabled ? "enabled" : "disabled");
    }

    /// <summary>
    /// Preloads all textures used by tiles in the world to avoid loading spikes during gameplay.
    /// Call this after loading a new map.
    /// </summary>
    public void PreloadMapAssets(World world)
    {
        var sw = Stopwatch.StartNew();
        var texturesNeeded = new HashSet<string>();

        // Gather all tile textures
        world.Query(
            in _tileQuery,
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
                var textureKey = GetSpriteTextureKey(sprite);
                texturesNeeded.Add(textureKey);
            }
        );

        world.Query(
            in _staticSpriteQuery,
            (ref Sprite sprite) =>
            {
                var textureKey = GetSpriteTextureKey(sprite);
                texturesNeeded.Add(textureKey);
            }
        );

        // With lazy loading, sprites will load on-demand during rendering
        // Just log what textures are expected
        _logger?.LogDebug(
            "Map requires {Count} textures (will load on-demand)",
            texturesNeeded.Count
        );

        // Optionally preload sprite textures if lazy loader is available
        if (_spriteTextureLoader != null)
        {
            foreach (var textureId in texturesNeeded)
            {
                if (!_assetManager.HasTexture(textureId) && textureId.StartsWith("sprites/"))
                {
                    // Parse sprite category/name from texture key
                    var parts = textureId.Split('/');
                    if (parts.Length >= 3)
                    {
                        var category = parts[1];
                        var spriteName = parts[2];
                        TryLazyLoadSprite(category, spriteName, textureId);
                    }
                }
            }
        }

        sw.Stop();
        _logger?.LogWorkflowStatus(
            "Texture preload complete",
            ("count", texturesNeeded.Count),
            ("timeMs", sw.Elapsed.TotalMilliseconds.ToString("F2"))
        );
    }

    /// <summary>
    /// Updates the cached camera transform and bounds once per frame.
    /// </summary>
    private void UpdateCameraCache(World world)
    {
        world.Query(
            in _cameraQuery,
            (ref Camera camera) =>
            {
                // Only recalculate if camera changed (dirty flag optimization)
                if (!camera.IsDirty && _cachedCameraTransform != Matrix.Identity)
                    return;

                _cachedCameraTransform = camera.GetTransformMatrix();

                // Calculate camera bounds for culling
                const int margin = 2;
                var left =
                    (int)(camera.Position.X / _tileSize)
                    - camera.Viewport.Width / 2 / _tileSize / (int)camera.Zoom
                    - margin;
                var top =
                    (int)(camera.Position.Y / _tileSize)
                    - camera.Viewport.Height / 2 / _tileSize / (int)camera.Zoom
                    - margin;
                var width = camera.Viewport.Width / _tileSize / (int)camera.Zoom + margin * 2;
                var height = camera.Viewport.Height / _tileSize / (int)camera.Zoom + margin * 2;

                _cachedCameraBounds = new Rectangle(left, top, width, height);

                // Reset dirty flag after recalculation
                camera.IsDirty = false;
            }
        );
    }

    /// <summary>
    /// Renders all tiles with elevation-based depth sorting.
    /// OPTIMIZED: Single unified query eliminates duplicate iteration and expensive Has() checks.
    /// </summary>
    private int RenderAllTiles(World world)
    {
        var tilesRendered = 0;
        var tilesCulled = 0;

        try
        {
            var cameraBounds = _cachedCameraBounds;

            // CRITICAL OPTIMIZATION: Use single query with optional LayerOffset
            // OLD: Two separate queries + expensive world.Has<LayerOffset>(entity) check per tile
            // NEW: Single query handles both cases, checking component in-place
            //
            // Performance improvement:
            // - Eliminates 200+ expensive Has() checks (was causing 11ms spikes)
            // - Single query iteration instead of two (better cache locality)
            // - Optional component pattern: query includes LayerOffset but we check if it exists
            world.Query(
                in _tileQuery,
                (
                    Entity entity,
                    ref TilePosition pos,
                    ref TileSprite sprite,
                    ref Elevation elevation
                ) =>
                {
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

                    // Get tileset texture (cached in AssetManager)
                    if (!_assetManager.HasTexture(sprite.TilesetId))
                    {
                        if (tilesRendered == 0) // Only warn once
                            _logger?.LogWarning(
                                "  WARNING: Tileset '{TilesetId}' NOT FOUND - skipping tiles",
                                sprite.TilesetId
                            );
                        return;
                    }

                    var texture = _assetManager.GetTexture(sprite.TilesetId);

                    // OPTIMIZATION: Check for LayerOffset inline (faster than separate query)
                    // TryGet is faster than Has() + Get() because it's a single lookup
                    Vector2 position;
                    if (world.TryGet(entity, out LayerOffset offset))
                    {
                        // Apply layer offset for parallax effect
                        // +1 to Y for bottom-left origin alignment
                        position = new Vector2(
                            pos.X * _tileSize + offset.X,
                            (pos.Y + 1) * _tileSize + offset.Y
                        );
                    }
                    else
                    {
                        // Standard positioning (+1 to Y for bottom-left origin alignment)
                        position = new Vector2(pos.X * _tileSize, (pos.Y + 1) * _tileSize);
                    }

                    // Calculate elevation-based layer depth
                    var layerDepth = CalculateElevationDepth(elevation.Value, position.Y);

                    // Apply flip flags from Tiled
                    var effects = SpriteEffects.None;
                    if (sprite.FlipHorizontally)
                        effects |= SpriteEffects.FlipHorizontally;
                    if (sprite.FlipVertically)
                        effects |= SpriteEffects.FlipVertically;

                    // Render tile
                    // Calculate tile origin (bottom-left for grid alignment)
                    var tileOrigin = new Vector2(0, sprite.SourceRect.Height);

                    _spriteBatch.Draw(
                        texture,
                        position,
                        sprite.SourceRect,
                        Color.White,
                        0f,
                        tileOrigin,
                        1f,
                        effects,
                        layerDepth
                    );

                    tilesRendered++;
                }
            );
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "  ERROR rendering tiles");
        }

        return tilesRendered;
    }

    /// <summary>
    /// Renders all sprites with elevation-based depth sorting.
    /// </summary>
    private int RenderAllSprites(World world)
    {
        var spriteCount = 0;

        try
        {
            // Render moving sprites
            world.Query(
                in _movingSpriteQuery,
                (
                    ref Position position,
                    ref Sprite sprite,
                    ref GridMovement movement,
                    ref Elevation elevation
                ) =>
                {
                    spriteCount++;
                    RenderMovingSprite(ref position, ref sprite, ref movement, ref elevation);
                }
            );

            // Render static sprites
            world.Query(
                in _staticSpriteQuery,
                (ref Position position, ref Sprite sprite, ref Elevation elevation) =>
                {
                    spriteCount++;
                    RenderStaticSprite(ref position, ref sprite, ref elevation);
                }
            );
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "  ERROR rendering sprites");
        }

        return spriteCount;
    }

    private void RenderMovingSprite(
        ref Position position,
        ref Sprite sprite,
        ref GridMovement movement,
        ref Elevation elevation
    )
    {
        try
        {
            // Get texture key from sprite (category/spriteName)
            var textureKey = GetSpriteTextureKey(sprite);

            // Lazy load texture if not already loaded
            if (!_assetManager.HasTexture(textureKey))
            {
                TryLazyLoadSprite(sprite.Category, sprite.SpriteName, textureKey);

                // If still not found after lazy load attempt, skip rendering
                if (!_assetManager.HasTexture(textureKey))
                {
                    _logger?.LogWarning(
                        "    WARNING: Texture '{TextureKey}' NOT FOUND - skipping sprite ({Category}/{SpriteName})",
                        textureKey,
                        sprite.Category,
                        sprite.SpriteName
                    );
                    return;
                }
            }

            var texture = _assetManager.GetTexture(textureKey);

            // Determine source rectangle
            var sourceRect = sprite.SourceRect;
            if (sourceRect.IsEmpty)
                sourceRect = new Rectangle(0, 0, texture.Width, texture.Height);

            // Calculate render position (visual interpolated position)
            // Add tile size to Y to align sprite feet with tile bottom
            var renderPosition = new Vector2(position.PixelX, position.PixelY + _tileSize);

            // BEST PRACTICE FOR MOVING ENTITIES: Use TARGET position for depth sorting
            // When moving/jumping, sort based on where the entity is going, not where they started.
            // This prevents flickering and ensures entities sort correctly during movement.
            // For example, when jumping over a fence, the player should sort as if they're
            // already on the other side of the fence.
            float groundY;
            if (movement.IsMoving)
            {
                // Use target grid position for sorting
                var targetGridY = (int)(movement.TargetPosition.Y / _tileSize);
                groundY = (targetGridY + 1) * _tileSize; // +1 for bottom of tile
            }
            else
            {
                // Use current grid position
                groundY = (position.Y + 1) * _tileSize;
            }

            var layerDepth = CalculateElevationDepth(elevation.Value, groundY);

            // Determine sprite effects (flip horizontal for left-facing)
            var effects = sprite.FlipHorizontal
                ? SpriteEffects.FlipHorizontally
                : SpriteEffects.None;

            // Draw sprite
            _spriteBatch.Draw(
                texture,
                renderPosition,
                sourceRect,
                sprite.Tint,
                sprite.Rotation,
                sprite.Origin,
                sprite.Scale,
                effects,
                layerDepth
            );
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "    ERROR rendering moving sprite '{Category}/{SpriteName}' at position ({X}, {Y})",
                sprite.Category,
                sprite.SpriteName,
                position.PixelX,
                position.PixelY
            );
        }
    }

    private void RenderStaticSprite(
        ref Position position,
        ref Sprite sprite,
        ref Elevation elevation
    )
    {
        try
        {
            // Get texture key from sprite (category/spriteName)
            var textureKey = GetSpriteTextureKey(sprite);

            // Lazy load texture if not already loaded
            if (!_assetManager.HasTexture(textureKey))
            {
                TryLazyLoadSprite(sprite.Category, sprite.SpriteName, textureKey);

                // If still not found after lazy load attempt, skip rendering
                if (!_assetManager.HasTexture(textureKey))
                {
                    _logger?.LogWarning(
                        "    WARNING: Texture '{TextureKey}' NOT FOUND - skipping sprite ({Category}/{SpriteName})",
                        textureKey,
                        sprite.Category,
                        sprite.SpriteName
                    );
                    return;
                }
            }

            var texture = _assetManager.GetTexture(textureKey);

            // Determine source rectangle
            var sourceRect = sprite.SourceRect;
            if (sourceRect.IsEmpty)
                sourceRect = new Rectangle(0, 0, texture.Width, texture.Height);

            // Calculate render position
            // Add tile size to Y to align sprite feet with tile bottom
            var renderPosition = new Vector2(position.PixelX, position.PixelY + _tileSize);

            // BEST PRACTICE: Calculate layer depth based on entity's GRID position, not visual pixel position.
            // This ensures:
            // 1. Sprites sort correctly even during movement/jumping animations
            // 2. Sort order doesn't change mid-movement (would cause flickering)
            // 3. Entities sort based on which grid tile they occupy, not their interpolated visual position
            //
            // The grid position represents where the entity logically is for gameplay purposes.
            // The pixel position is just the visual interpolation for smooth movement.
            // For a 16x16 tile grid, the entity's ground Y is at the bottom of their grid tile.
            float groundY = (position.Y + 1) * _tileSize; // +1 because we want bottom of tile
            var layerDepth = CalculateElevationDepth(elevation.Value, groundY);

            // Determine sprite effects (flip horizontal for left-facing)
            var effects = sprite.FlipHorizontal
                ? SpriteEffects.FlipHorizontally
                : SpriteEffects.None;

            // Draw sprite
            _spriteBatch.Draw(
                texture,
                renderPosition,
                sourceRect,
                sprite.Tint,
                sprite.Rotation,
                sprite.Origin,
                sprite.Scale,
                effects,
                layerDepth
            );
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "    ERROR rendering sprite '{Category}/{SpriteName}' at position ({X}, {Y})",
                sprite.Category,
                sprite.SpriteName,
                position.PixelX,
                position.PixelY
            );
        }
    }

    /// <summary>
    /// Gets the texture key for loading sprite sheets from the AssetManager.
    /// </summary>
    /// <param name="sprite">The sprite component.</param>
    /// <returns>Texture key in format "sprites/{category}/{spriteName}".</returns>
    private static string GetSpriteTextureKey(Sprite sprite)
    {
        return $"sprites/{sprite.Category}/{sprite.SpriteName}";
    }

    /// <summary>
    /// Attempts to lazy-load a sprite texture if a loader is registered.
    /// </summary>
    private void TryLazyLoadSprite(string category, string spriteName, string textureKey)
    {
        if (_spriteTextureLoader == null)
            return;

        try
        {
            // Use reflection to call LoadSpriteTexture on the loader
            var loaderType = _spriteTextureLoader.GetType();
            var method = loaderType.GetMethod("LoadSpriteTexture");
            if (method != null)
            {
                method.Invoke(_spriteTextureLoader, new object[] { category, spriteName });
                _logger?.LogDebug("Lazy-loaded sprite: {TextureKey}", textureKey);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to lazy-load sprite: {TextureKey}", textureKey);
        }
    }

    /// <summary>
    /// Calculates layer depth using Pokemon Emerald's elevation model.
    /// Formula: layerDepth = 1.0 - ((elevation * 16) + (y / mapHeight))
    /// This allows 16 Y-sorted positions per elevation level.
    /// </summary>
    /// <param name="elevation">Elevation level (0-15).</param>
    /// <param name="yPosition">Y position for sorting within elevation.</param>
    /// <returns>Layer depth value (0.0 = front, 1.0 = back).</returns>
    private static float CalculateElevationDepth(byte elevation, float yPosition)
    {
        // Normalize Y position to 0.0-1.0 range
        var normalizedY = yPosition / MapHeight;

        // Elevation contributes 16 units per level (allowing 16 Y-sorted positions per elevation)
        // Y position contributes fractional depth within elevation
        var depth = (elevation * 16.0f) + normalizedY;

        // Invert for SpriteBatch (0.0 = front, 1.0 = back)
        // Max depth is (15 * 16) + 1 = 241
        var layerDepth = 1.0f - (depth / 241.0f);

        // Clamp to valid range
        return MathHelper.Clamp(layerDepth, 0.0f, 1.0f);
    }

    /// <summary>
    /// Renders all image layers with proper Z-ordering and transparency.
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
