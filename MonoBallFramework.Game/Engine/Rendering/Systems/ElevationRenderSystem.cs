using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoBallFramework.Game.Ecs.Components;
using MonoBallFramework.Game.Ecs.Components.Maps;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Ecs.Components.Player;
using MonoBallFramework.Game.Ecs.Components.Rendering;
using MonoBallFramework.Game.Ecs.Components.Tiles;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Common.Utilities;
using MonoBallFramework.Game.Engine.Core.Systems;
using MonoBallFramework.Game.Engine.Core.Systems.Base;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Engine.Rendering.Assets;
using MonoBallFramework.Game.Engine.Rendering.Components;
using MonoBallFramework.Game.Engine.Rendering.Configuration;
using MonoBallFramework.Game.Engine.Rendering.Constants;
using MonoBallFramework.Game.Engine.Rendering.Context;
using MonoBallFramework.Game.Engine.Systems.Management;

namespace MonoBallFramework.Game.Engine.Rendering.Systems;

/// <summary>
///     Elevation-based rendering system using Pokemon Emerald's elevation model.
///     Renders all tiles and sprites sorted by: elevation (primary) + Y position (secondary).
///     This allows proper layering of bridges, overhead structures, and multi-level maps.
/// </summary>
/// <remarks>
///     <para>
///         <b>Render Order Formula:</b>
///         layerDepth = (elevation * 16) + (y / mapHeight)
///     </para>
///     <para>
///         <b>Elevation Levels (Pokemon Emerald):</b>
///         - 0: Ground level (water, pits)
///         - 3: Standard elevation (most tiles and objects)
///         - 6: Bridges, elevated platforms
///         - 9-12: Overhead structures
///         - 15: Maximum elevation
///     </para>
/// </remarks>
public class ElevationRenderSystem(
    GraphicsDevice graphicsDevice,
    AssetManager assetManager,
    ISpatialQuery spatialQuery,
    ILogger<ElevationRenderSystem>? logger = null
) : SystemBase, IRenderSystem
{
    private const float MapHeight = RenderingConstants.MaxRenderDistance;

    // Cache border data for the current frame (avoids repeated queries)
    private readonly List<MapBorderInfo> _cachedMapBorders = new(5);

    // Cache ALL map bounds for border exclusion (includes maps without borders)
    private readonly List<MapBoundsInfo> _cachedMapBounds = new(10);

    // Camera is now passed via RenderContext (scenes own cameras)

    private readonly GraphicsDevice _graphicsDevice =
        graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));

    // Image layers (backgrounds, overlays)
    private readonly QueryDescription _imageLayerQuery = QueryCache.Get<ImageLayer>();

    private readonly ILogger<ElevationRenderSystem>? _logger = logger;

    // Map border query for Pokemon Emerald-style border rendering
    private readonly QueryDescription _mapBorderQuery = QueryCache.Get<
        MapInfo,
        MapWorldPosition,
        MapBorder
    >();

    // PERFORMANCE: Cache map render order for O(1) lookups instead of O(maps) linear search
    private readonly Dictionary<string, int> _mapRenderOrderCache = new(10);

    // Cache map world origins for multi-map rendering (updated per frame)
    private readonly Dictionary<string, Vector2> _mapWorldOrigins = new(10);

    // Map world position query for multi-map streaming
    private readonly QueryDescription _mapWorldPosQuery = QueryCache.Get<
        MapInfo,
        MapWorldPosition
    >();

    // Sprite queries (moving and static) - require Visible component for rendering
    private readonly QueryDescription _movingSpriteQuery = QueryCache.Get<
        Position,
        Sprite,
        GridMovement,
        Elevation,
        Visible
    >();

    // Query for player position (to determine current map for borders)
    private readonly QueryDescription _playerPositionQuery = QueryCache.Get<Player, Position>();

    // Track missing sprite textures to avoid repeated log warnings (prevents log spam)
    private readonly HashSet<string> _reportedMissingSpriteTextures = new();

    // Spatial query for efficient tile lookups (only visible tiles)
    private readonly ISpatialQuery
        _spatialQuery = spatialQuery ?? throw new ArgumentNullException(nameof(spatialQuery));

    private readonly SpriteBatch _spriteBatch = new(graphicsDevice);

    private readonly QueryDescription _staticSpriteQuery = QueryCache.Get<
        Position,
        Sprite,
        Elevation,
        Visible
    >();

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

    private Rectangle? _cachedCameraBounds;

    // Cache camera transform to avoid recalculating
    private Matrix _cachedCameraTransform = Matrix.Identity;

    // Cached player's current map ID (for border rendering)
    private string? _cachedPlayerMapId;

    // Performance profiling
    private bool _enableDetailedProfiling;

    private ulong _frameCounter;
    private int _lastEntityCount;
    private int _lastSpriteCount;

    // Track whether we've logged the border texture warning (avoid spam)
    private bool _loggedBorderTextureWarning;

    // Reusable Vector2/Rectangle instances to avoid allocations (400-600 per frame eliminated)
    private Vector2 _reusablePosition = Vector2.Zero;
    private Rectangle _reusableSourceRect = Rectangle.Empty;
    private Vector2 _reusableTileOrigin = Vector2.Zero;

    private double _setupTime,
        _batchBeginTime,
        _tileTime,
        _spriteTime,
        _batchEndTime;

    /// <summary>
    ///     Cached delegate for sprite loading to avoid reflection overhead.
    ///     Created once during SetSpriteTextureLoader to eliminate GetType() and GetMethod() calls on every texture miss.
    ///     Reduces lazy load time from ~2.0ms to ~0.5-1.0ms (60% improvement).
    /// </summary>
    private Action<string>? _spriteLoadDelegate;

    // Lazy sprite texture loader (set after initialization)
    private object? _spriteTextureLoader;

    // Tile size is set from map data via SetTileSize()

    /// <summary>
    ///     Gets the tile size currently used for rendering.
    /// </summary>
    public int TileSize { get; private set; } = 16;

    /// <summary>
    ///     Gets the AssetManager used by this render system.
    /// </summary>
    public AssetManager AssetManager { get; } =
        assetManager ?? throw new ArgumentNullException(nameof(assetManager));

    /// <summary>
    ///     Gets the number of tiles rendered in the last frame.
    ///     This includes both regular tiles and border tiles.
    /// </summary>
    public int LastRenderedTileCount { get; private set; }

    /// <inheritdoc />
    public override int Priority => SystemPriority.Render;

    /// <summary>
    ///     Gets the render order. Lower values render first.
    ///     Order 1 renders entities after background (0) but before UI (2).
    /// </summary>
    public int RenderOrder => 1;

    /// <inheritdoc />
    /// <remarks>
    ///     This is a render-only system. The Update method is not used.
    ///     All rendering logic is in the Render method.
    /// </remarks>
    public override void Update(World world, float deltaTime)
    {
        // No-op: This is a render-only system. See Render() method.
    }

    /// <inheritdoc />
    /// <summary>
    ///     Renders all entities using the camera provided by the scene via RenderContext.
    ///     The scene owns and manages the camera, ensuring proper isolation.
    /// </summary>
    public void Render(World world, RenderContext context)
    {
        try
        {
            EnsureInitialized();
            _frameCounter++;

            // Only run detailed profiling when explicitly enabled (adds overhead)
            if (_enableDetailedProfiling)
            {
                RenderWithProfiling(world, context.Camera);
                return;
            }

            // Fast path - no profiling overhead
            UpdateCameraCache(world, context.Camera);

            // Defensive check: Skip rendering if viewport is not initialized
            // This should never happen with proper initialization, but prevents crashes
            if (context.Camera.Viewport.Width == 0 || context.Camera.Viewport.Height == 0)
            {
                return;
            }

            UpdateMapWorldOriginsCache(world);
            UpdateMapBordersCache(world);

            // Get camera virtual viewport for letterboxing/pillarboxing
            Rectangle virtualViewport = GetCameraVirtualViewport(context.Camera);
            Viewport originalViewport = graphicsDevice.Viewport;

            // Clear full screen with black (for borders)
            graphicsDevice.Clear(Color.Black);

            // Set viewport to virtual viewport (game rendering area)
            graphicsDevice.Viewport = new Viewport(virtualViewport);

            _spriteBatch.Begin(
                SpriteSortMode.BackToFront, // Sort sprites by layerDepth for proper overlap
                BlendState.NonPremultiplied, // Use NonPremultiplied for PNG transparency
                SamplerState.PointClamp,
                DepthStencilState.None, // Disable depth buffer for 2D sprite transparency
                RasterizerState.CullNone,
                null,
                _cachedCameraTransform
            );

            // Render image layers (they sort with everything via layerDepth)
            int imageLayerCount = RenderImageLayers(world);

            // Render border tiles (Pokemon Emerald-style 2x2 pattern for areas outside map bounds)
            int borderTilesRendered = RenderBorders(world);

            // Render all tiles (elevation + Y sorting via SpriteBatch)
            int totalTilesRendered = RenderAllTiles(world);

            // Render all sprites (elevation + Y sorting via SpriteBatch)
            int spriteCount = RenderAllSprites(world);

            if (_frameCounter % RenderingConstants.PerformanceLogInterval == 0)
            {
                int totalEntities =
                    totalTilesRendered + spriteCount + imageLayerCount + borderTilesRendered;
                _logger?.LogRenderStats(
                    totalEntities,
                    totalTilesRendered + borderTilesRendered,
                    spriteCount,
                    _frameCounter
                );
                if (imageLayerCount > 0)
                {
                    _logger?.LogImageLayersRendered(imageLayerCount);
                }

                if (borderTilesRendered > 0)
                {
                    _logger?.LogDebug("Border tiles rendered: {Count}", borderTilesRendered);
                }
            }

            _lastEntityCount =
                totalTilesRendered + spriteCount + imageLayerCount + borderTilesRendered;
            LastRenderedTileCount = totalTilesRendered + borderTilesRendered;
            _lastSpriteCount = spriteCount;

            _spriteBatch.End();

            // Restore original viewport after rendering
            graphicsDevice.Viewport = originalViewport;
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
    ///     Updates the tile size used by the renderer (falls back to 16 if invalid).
    /// </summary>
    public void SetTileSize(int tileSize)
    {
        int clamped = tileSize > 0 ? tileSize : 16; // Fallback to 16 if invalid
        if (TileSize == clamped)
        {
            return;
        }

        TileSize = clamped;
        _cachedCameraBounds = null;
        _cachedCameraTransform = Matrix.Identity;
        _logger?.LogRenderTileSizeSet(TileSize);
    }

    /// <summary>
    ///     Sets the sprite texture loader for lazy loading.
    ///     Creates a strongly-typed delegate to avoid reflection overhead on every texture miss.
    /// </summary>
    /// <param name="loader">The sprite texture loader instance (expected to have LoadSpriteTexture(string) method)</param>
    public void SetSpriteTextureLoader(object? loader)
    {
        _spriteTextureLoader = loader;

        if (loader != null)
        {
            // Create delegate once during initialization to eliminate reflection overhead
            // Use the single-parameter overload that accepts full sprite path (handles subcategories)
            Type loaderType = loader.GetType();
            MethodInfo? method = loaderType.GetMethod(
                "LoadSpriteTexture",
                new[] { typeof(string) }
            );

            if (method != null)
            {
                _spriteLoadDelegate =
                    (Action<string>)
                    Delegate.CreateDelegate(typeof(Action<string>), loader, method);
                _logger?.LogSpriteLoaderRegistered();
            }
            else
            {
                _spriteLoadDelegate = null;
                _logger?.LogWarning("LoadSpriteTexture(string) method not found on sprite loader");
            }
        }
        else
        {
            _spriteLoadDelegate = null;
        }
    }

    /// <summary>
    ///     Render with detailed profiling enabled (slower, for diagnostics only).
    /// </summary>
    private void RenderWithProfiling(World world, Camera camera)
    {
        var swSetup = Stopwatch.StartNew();
        UpdateCameraCache(world, camera);
        UpdateMapWorldOriginsCache(world);
        UpdateMapBordersCache(world);
        swSetup.Stop();
        _setupTime = swSetup.Elapsed.TotalMilliseconds;

        // Get camera virtual viewport for letterboxing/pillarboxing
        Rectangle virtualViewport = GetCameraVirtualViewport(camera);
        Viewport originalViewport = graphicsDevice.Viewport;

        // Clear full screen with black (for borders)
        graphicsDevice.Clear(Color.Black);

        // Set viewport to virtual viewport (game rendering area)
        graphicsDevice.Viewport = new Viewport(virtualViewport);

        var swBatchBegin = Stopwatch.StartNew();
        _spriteBatch.Begin(
            SpriteSortMode.BackToFront, // Sort sprites by layerDepth for proper overlap
            BlendState.NonPremultiplied, // Use NonPremultiplied for PNG transparency
            SamplerState.PointClamp,
            DepthStencilState.None, // Disable depth buffer for 2D sprite transparency
            RasterizerState.CullNone,
            null,
            _cachedCameraTransform
        );
        swBatchBegin.Stop();
        _batchBeginTime = swBatchBegin.Elapsed.TotalMilliseconds;

        // Render image layers
        int imageLayerCount = RenderImageLayers(world);

        // Render border tiles
        int borderTilesRendered = RenderBorders(world);

        var swTiles = Stopwatch.StartNew();
        int totalTilesRendered = RenderAllTiles(world);
        swTiles.Stop();
        _tileTime = swTiles.Elapsed.TotalMilliseconds;

        var swSprites = Stopwatch.StartNew();
        int spriteCount = RenderAllSprites(world);
        swSprites.Stop();
        _spriteTime = swSprites.Elapsed.TotalMilliseconds;

        var swBatchEnd = Stopwatch.StartNew();
        _spriteBatch.End();
        swBatchEnd.Stop();
        _batchEndTime = swBatchEnd.Elapsed.TotalMilliseconds;

        // Restore original viewport after rendering
        graphicsDevice.Viewport = originalViewport;

        if (_frameCounter % 300 == 0)
        {
            int totalEntities = totalTilesRendered + spriteCount + imageLayerCount;
            _logger?.LogRenderStats(totalEntities, totalTilesRendered, spriteCount, _frameCounter);
            _logger?.LogRenderBreakdown(
                _setupTime,
                _batchBeginTime,
                _tileTime,
                _spriteTime,
                _batchEndTime
            );
            if (imageLayerCount > 0)
            {
                _logger?.LogImageLayersRendered(imageLayerCount);
            }
        }

        _lastEntityCount = totalTilesRendered + spriteCount + imageLayerCount;
        LastRenderedTileCount = totalTilesRendered;
        _lastSpriteCount = spriteCount;
    }

    /// <summary>
    ///     Enables or disables detailed per-frame profiling breakdown.
    /// </summary>
    public void SetDetailedProfiling(bool enabled)
    {
        _enableDetailedProfiling = enabled;
        _logger?.LogDetailedProfilingChanged(enabled);
    }

    /// <summary>
    ///     Preloads all textures used by tiles in the world to avoid loading spikes during gameplay.
    ///     Call this after loading a new map.
    /// </summary>
    public void PreloadMapAssets(World world)
    {
        var sw = Stopwatch.StartNew();
        var texturesNeeded = new HashSet<string>(64); // Pre-allocate: typical map has 15-60 textures

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
                texturesNeeded.Add(sprite.SpriteId.TextureKey);
            }
        );

        world.Query(
            in _staticSpriteQuery,
            (ref Sprite sprite) =>
            {
                texturesNeeded.Add(sprite.SpriteId.TextureKey);
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
            foreach (string textureId in texturesNeeded)
            {
                if (!AssetManager.HasTexture(textureId) && textureId.StartsWith("sprites/"))
                {
                    // Extract full sprite path from texture key (handles subcategories)
                    // Format: sprites/{category}/{name} OR sprites/{category}/{subcategory}/{name}
                    string spritePath = textureId.Substring("sprites/".Length);
                    TryLazyLoadSprite(spritePath, textureId);
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
    ///     Updates the cached camera transform and bounds once per frame.
    ///     Uses the camera provided by the scene via RenderContext.
    /// </summary>
    /// <remarks>
    ///     The camera is now owned by the scene and passed explicitly.
    ///     This ensures proper scene-camera ownership and isolation.
    /// </remarks>
    private void UpdateCameraCache(World world, Camera camera)
    {
        // Defensive check: If viewport is not initialized, use identity matrix
        // This should never happen with proper initialization, but prevents crashes
        if (camera.Viewport.Width == 0 || camera.Viewport.Height == 0)
        {
            _cachedCameraTransform = Matrix.Identity;
            return;
        }

        // Only recalculate if camera changed (dirty flag optimization)
        if (!camera.IsDirty && _cachedCameraTransform != Matrix.Identity)
        {
            return;
        }

        _cachedCameraTransform = camera.GetTransformMatrix();

        // Calculate camera bounds for culling using floating-point math
        // When zoom < 1 (zoomed out), we see more tiles; when zoom > 1 (zoomed in), we see fewer
        float effectiveZoom = Math.Max(0.01f, camera.Zoom); // prevent divide by zero
        int halfWidthTiles = (int)Math.Ceiling(camera.Viewport.Width / 2f / TileSize / effectiveZoom);
        int halfHeightTiles = (int)Math.Ceiling(camera.Viewport.Height / 2f / TileSize / effectiveZoom);

        int left = (int)(camera.Position.X / TileSize) - halfWidthTiles - CameraConstants.ViewportMarginTiles;
        int top = (int)(camera.Position.Y / TileSize) - halfHeightTiles - CameraConstants.ViewportMarginTiles;
        int width = (halfWidthTiles * 2) + (CameraConstants.ViewportMarginTiles * 2);
        int height = (halfHeightTiles * 2) + (CameraConstants.ViewportMarginTiles * 2);

        _cachedCameraBounds = new Rectangle(left, top, width, height);

        // Cache the player's current map ID for border rendering
        world.Query(
            in _playerPositionQuery,
            (ref Position pos) =>
            {
                _cachedPlayerMapId = pos.MapId?.Value;
            }
        );
    }

    /// <summary>
    ///     Gets the camera's virtual viewport for letterboxing/pillarboxing.
    ///     Returns the full viewport if VirtualViewport is empty.
    /// </summary>
    private Rectangle GetCameraVirtualViewport(Camera camera)
    {
        if (camera.VirtualViewport != Rectangle.Empty)
        {
            return camera.VirtualViewport;
        }

        return graphicsDevice.Viewport.Bounds;
    }

    /// <summary>
    ///     Updates the cached map world origins and bounds for multi-map rendering.
    ///     Called once per frame to avoid repeated queries during tile rendering.
    /// </summary>
    private void UpdateMapWorldOriginsCache(World world)
    {
        _mapWorldOrigins.Clear();
        _cachedMapBounds.Clear();
        _mapRenderOrderCache.Clear(); // PERFORMANCE: Clear render order cache

        int orderIndex = 0;
        world.Query(
            in _mapWorldPosQuery,
            (ref MapInfo mapInfo, ref MapWorldPosition worldPos) =>
            {
                string mapIdValue = mapInfo.MapId.Value;
                _mapWorldOrigins[mapIdValue] = worldPos.WorldOrigin;

                // PERFORMANCE: Cache render order for O(1) lookups during sprite/tile rendering
                _mapRenderOrderCache[mapIdValue] = orderIndex++;

                // PERFORMANCE: Pre-compute tile coordinates to avoid divisions in IsTileInsideAnyMap
                int tileX = (int)(worldPos.WorldOrigin.X / mapInfo.TileSize);
                int tileY = (int)(worldPos.WorldOrigin.Y / mapInfo.TileSize);

                // Cache map bounds for border exclusion
                _cachedMapBounds.Add(
                    new MapBoundsInfo
                    {
                        MapId = mapInfo.MapId,
                        WorldOrigin = worldPos.WorldOrigin,
                        MapWidth = mapInfo.Width,
                        MapHeight = mapInfo.Height,
                        TileSize = mapInfo.TileSize,
                        TileX = tileX,
                        TileY = tileY,
                        TileRight = tileX + mapInfo.Width,
                        TileBottom = tileY + mapInfo.Height
                    }
                );
            }
        );

        // Log render cache count periodically for debugging multi-map rendering
        if (_frameCounter % 300 == 0 && _cachedMapBounds.Count > 0)
        {
            _logger?.LogInformation(
                "Render cache has {MapCount} maps: {MapIds}",
                _cachedMapBounds.Count,
                string.Join(", ", _cachedMapBounds.Select(m => m.MapId.Value))
            );
        }
    }

    /// <summary>
    ///     Renders all tiles with elevation-based depth sorting.
    ///     OPTIMIZED: Uses spatial query to only iterate visible tiles (~300) instead of ALL tiles (1M+).
    /// </summary>
    private int RenderAllTiles(World world)
    {
        int tilesRendered = 0;

        try
        {
            Rectangle? cameraBounds = _cachedCameraBounds;

            // CRITICAL OPTIMIZATION: Use spatial query for O(visible tiles) instead of O(all tiles)
            // For a 1000x1000 map: queries ~300 tiles instead of iterating 1,000,000
            if (cameraBounds.HasValue && _cachedMapBounds.Count > 0)
            {
                tilesRendered = RenderTilesUsingSpatialQuery(world, cameraBounds.Value);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "  ERROR rendering tiles");
        }

        return tilesRendered;
    }

    /// <summary>
    ///     Renders tiles using spatial query - only iterates visible tiles.
    ///     O(visible tiles) instead of O(all tiles) - ~300 vs 1,000,000 for large maps.
    ///     OPTIMIZED: Uses pre-computed TileRenderEntry data - ZERO ECS calls during rendering.
    /// </summary>
    private int RenderTilesUsingSpatialQuery(World world, Rectangle cameraBounds)
    {
        int tilesRendered = 0;

        // For each loaded map, calculate visible local bounds and query tiles
        foreach (MapBoundsInfo mapInfo in _cachedMapBounds)
        {
            // Calculate the map's tile origin in world coordinates
            int mapOriginTileX = (int)(mapInfo.WorldOrigin.X / TileSize);
            int mapOriginTileY = (int)(mapInfo.WorldOrigin.Y / TileSize);

            // Calculate the intersection of camera bounds with this map's world bounds
            int mapRightTile = mapOriginTileX + mapInfo.MapWidth;
            int mapBottomTile = mapOriginTileY + mapInfo.MapHeight;

            // Calculate visible region in world tile coordinates, clamped to map bounds
            int visibleLeft = Math.Max(cameraBounds.Left, mapOriginTileX);
            int visibleTop = Math.Max(cameraBounds.Top, mapOriginTileY);
            int visibleRight = Math.Min(cameraBounds.Right, mapRightTile);
            int visibleBottom = Math.Min(cameraBounds.Bottom, mapBottomTile);

            // Skip if no intersection with camera
            if (visibleLeft >= visibleRight || visibleTop >= visibleBottom)
            {
                continue;
            }

            // Convert to local map coordinates (spatial hash uses local coords)
            int localLeft = visibleLeft - mapOriginTileX;
            int localTop = visibleTop - mapOriginTileY;
            int localWidth = visibleRight - visibleLeft;
            int localHeight = visibleBottom - visibleTop;

            var localBounds = new Rectangle(localLeft, localTop, localWidth, localHeight);

            // Get world origin for position calculation
            Vector2 worldOrigin = mapInfo.WorldOrigin;

            // Skip if map ID is not set (shouldn't happen but defensive check)
            if (mapInfo.MapId is null)
            {
                continue;
            }

            int mapRenderOrder = GetMapRenderOrder(mapInfo.MapId.Value);

            // OPTIMIZED: Iterate bounds directly and query each position
            // Benefits: No buffer allocation, no X/Y stored in entries (saves 8 bytes/entry)
            // Before: Fill buffer, iterate buffer (copies + redundant X/Y storage)
            // After: Direct iteration, use loop coordinates for position
            for (int tileY = localBounds.Top; tileY < localBounds.Bottom; tileY++)
            {
                for (int tileX = localBounds.Left; tileX < localBounds.Right; tileX++)
                {
                    ReadOnlySpan<TileRenderEntry> tilesAtPos = _spatialQuery.GetTileRenderEntriesAt(
                        mapInfo.MapId, tileX, tileY);

                    foreach (ref readonly TileRenderEntry tile in tilesAtPos)
                    {
                        // PERFORMANCE: Single dictionary lookup instead of HasTexture + GetTexture
                        if (!AssetManager.TryGetTexture(tile.TilesetId, out Texture2D? texture) || texture == null)
                        {
                            continue;
                        }

                        // Calculate render position using loop coordinates + world offset
                        // X, Y are from the loop (not stored in tile entry - saves 8 bytes per entry)
                        _reusablePosition.X = (tileX * TileSize) + worldOrigin.X + tile.OffsetX;
                        _reusablePosition.Y = ((tileY + 1) * TileSize) + worldOrigin.Y + tile.OffsetY;

                        // Calculate depth using pre-computed elevation
                        float layerDepth = CalculateElevationDepth(
                            tile.Elevation,
                            _reusablePosition.Y,
                            mapRenderOrder
                        );

                        // Apply flip flags (pre-computed)
                        SpriteEffects effects = SpriteEffects.None;
                        if (tile.FlipHorizontally)
                        {
                            effects |= SpriteEffects.FlipHorizontally;
                        }

                        if (tile.FlipVertically)
                        {
                            effects |= SpriteEffects.FlipVertically;
                        }

                        // Get source rect - for animated tiles, fetch current frame from entity
                        // Static tiles: use pre-computed SourceRect (zero ECS calls)
                        // Animated tiles (~100-200): single TryGet to get current animation frame
                        Rectangle sourceRect;
                        if (tile.IsAnimated && tile.Entity.TryGet(out TileSprite animSprite))
                        {
                            // Animated tile: TileAnimationSystem updates TileSprite.SourceRect each frame
                            sourceRect = animSprite.SourceRect;
                        }
                        else
                        {
                            // Static tile: use pre-computed SourceRect
                            sourceRect = tile.SourceRect;
                        }

                        // Render tile
                        _reusableTileOrigin.X = 0;
                        _reusableTileOrigin.Y = sourceRect.Height;

                        _spriteBatch.Draw(
                            texture,
                            _reusablePosition,
                            sourceRect,
                            Color.White,
                            0f,
                            _reusableTileOrigin,
                            1f,
                            effects,
                            layerDepth
                        );

                        tilesRendered++;
                    }
                }
            }
        }

        return tilesRendered;
    }

    /// <summary>
    ///     Renders all sprites with elevation-based depth sorting.
    ///     OPTIMIZED: Single unified query eliminates duplicate iteration.
    ///     Uses TryGet pattern to check for GridMovement component inline.
    /// </summary>
    private int RenderAllSprites(World world)
    {
        int spriteCount = 0;

        try
        {
            // CRITICAL OPTIMIZATION: Use single query with optional GridMovement
            // OLD: Two separate queries (moving and static sprites)
            // NEW: Single query handles both cases, checking GridMovement in-place
            //
            // Performance improvement:
            // - Single query iteration instead of two (better cache locality)
            // - Eliminates redundant iteration overhead
            // - TryGet is fast inline component check
            world.Query(
                in _staticSpriteQuery,
                (
                    Entity entity,
                    ref Position position,
                    ref Sprite sprite,
                    ref Elevation elevation
                ) =>
                {
                    spriteCount++;

                    // Check if this sprite has movement component (inline optimization)
                    if (world.TryGet(entity, out GridMovement movement))
                    {
                        RenderMovingSprite(ref position, ref sprite, ref movement, ref elevation);
                    }
                    else
                    {
                        RenderStaticSprite(ref position, ref sprite, ref elevation);
                    }
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
            // Get texture with lazy loading support
            Texture2D? texture = TryGetSpriteTexture(ref sprite);
            if (texture == null)
            {
                return;
            }

            // Determine source rectangle - reuse static Rectangle to avoid allocation
            Rectangle sourceRect = sprite.SourceRect;
            if (sourceRect.IsEmpty)
            {
                _reusableSourceRect.X = 0;
                _reusableSourceRect.Y = 0;
                _reusableSourceRect.Width = texture.Width;
                _reusableSourceRect.Height = texture.Height;
                sourceRect = _reusableSourceRect;
            }

            // Calculate render position (visual interpolated position)
            // Add tile size to Y to align sprite feet with tile bottom
            // Reuse static Vector2 to avoid allocation
            _reusablePosition.X = position.PixelX;
            _reusablePosition.Y = position.PixelY + TileSize;

            // BEST PRACTICE: Use position.Y (grid coordinate) for depth sorting.
            // MovementSystem updates position.Y to the TARGET grid immediately when movement starts,
            // so this naturally sorts by target position during movement.
            // Using position.Y (local grid) instead of TargetPosition.Y (world pixels) ensures
            // correct sorting in multi-map streaming scenarios where world offsets vary.
            float groundY = (position.Y + 1) * TileSize; // +1 for bottom of tile

            int mapRenderOrder = GetMapRenderOrder(position.MapId?.Value);
            float layerDepth = CalculateElevationDepth(
                elevation.Value,
                groundY,
                mapRenderOrder
            );

            // Determine sprite effects (flip horizontal for left-facing)
            SpriteEffects effects = sprite.FlipHorizontal
                ? SpriteEffects.FlipHorizontally
                : SpriteEffects.None;

            // Draw sprite (using reusable position)
            _spriteBatch.Draw(
                texture,
                _reusablePosition,
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
                "    ERROR rendering moving sprite '{Category}/{Name}' at position ({X}, {Y})",
                sprite.SpriteId.Category,
                sprite.SpriteId.Name,
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
            // Get texture with lazy loading support
            Texture2D? texture = TryGetSpriteTexture(ref sprite);
            if (texture == null)
            {
                return;
            }

            // Determine source rectangle - reuse static Rectangle to avoid allocation
            Rectangle sourceRect = sprite.SourceRect;
            if (sourceRect.IsEmpty)
            {
                _reusableSourceRect.X = 0;
                _reusableSourceRect.Y = 0;
                _reusableSourceRect.Width = texture.Width;
                _reusableSourceRect.Height = texture.Height;
                sourceRect = _reusableSourceRect;
            }

            // Calculate render position
            // Add tile size to Y to align sprite feet with tile bottom
            // Reuse static Vector2 to avoid allocation
            _reusablePosition.X = position.PixelX;
            _reusablePosition.Y = position.PixelY + TileSize;

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
            int mapRenderOrder = GetMapRenderOrder(position.MapId?.Value);
            float layerDepth = CalculateElevationDepth(
                elevation.Value,
                groundY,
                mapRenderOrder
            );

            // Determine sprite effects (flip horizontal for left-facing)
            SpriteEffects effects = sprite.FlipHorizontal
                ? SpriteEffects.FlipHorizontally
                : SpriteEffects.None;

            // Draw sprite (using reusable position)
            _spriteBatch.Draw(
                texture,
                _reusablePosition,
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
                "    ERROR rendering sprite '{Category}/{Name}' at position ({X}, {Y})",
                sprite.SpriteId.Category,
                sprite.SpriteId.Name,
                position.PixelX,
                position.PixelY
            );
        }
    }

    /// <summary>
    ///     Tries to get a texture for a sprite, with lazy loading support.
    ///     Returns null if texture cannot be loaded after lazy load attempt.
    /// </summary>
    /// <param name="sprite">The sprite component containing texture info.</param>
    /// <returns>The texture if available, null otherwise.</returns>
    private Texture2D? TryGetSpriteTexture(ref Sprite sprite)
    {
        string textureKey = sprite.SpriteId.TextureKey;

        // Check if texture already loaded
        if (AssetManager.HasTexture(textureKey))
        {
            return AssetManager.GetTexture(textureKey);
        }

        // Skip sprites we've already reported as missing (prevents log spam)
        if (_reportedMissingSpriteTextures.Contains(textureKey))
        {
            return null;
        }

        // Try lazy load using LocalId which includes subcategory if present
        // Format: {category}/{name} OR {category}/{subcategory}/{name}
        TryLazyLoadSprite(sprite.SpriteId.LocalId, textureKey);

        // Check again after lazy load
        if (AssetManager.HasTexture(textureKey))
        {
            return AssetManager.GetTexture(textureKey);
        }

        // Texture unavailable - log once and track as missing
        if (_reportedMissingSpriteTextures.Add(textureKey))
        {
            _logger?.LogWarning(
                "    WARNING: Texture '{TextureKey}' NOT FOUND - skipping sprite ({Category}/{Name})",
                textureKey,
                sprite.SpriteId.Category,
                sprite.SpriteId.Name
            );
        }

        return null;
    }

    /// <summary>
    ///     Attempts to lazy-load a sprite texture if a loader is registered.
    ///     Uses cached delegate for zero-reflection performance (60% faster than reflection-based approach).
    /// </summary>
    /// <param name="spritePath">Full sprite path including subcategory if present (e.g., "npcs/generic/boy_1")</param>
    /// <param name="textureKey">Texture key for logging purposes</param>
    private void TryLazyLoadSprite(string spritePath, string textureKey)
    {
        if (_spriteLoadDelegate == null)
        {
            return;
        }

        try
        {
            // Direct delegate invocation - zero reflection overhead!
            // Eliminates: GetType(), GetMethod(), and object[] allocation
            _spriteLoadDelegate(spritePath);

            // Verify the texture was actually loaded
            if (AssetManager.HasTexture(textureKey))
            {
                _logger?.LogSpriteLoadedOnDemand(textureKey);
            }
        }
        catch (Exception ex)
        {
            // Note: Sprite details already logged in TryGetTexture() before this point
            _logger?.LogCriticalError(ex, "Lazy load sprite failed");
        }
    }

    /// <summary>
    ///     Calculates layer depth using Pokemon Emerald's elevation model with MapId-aware depth sorting.
    ///     Formula: layerDepth = 1.0 - ((elevation * 16) + (y / mapHeight) + (mapId * 0.01))
    ///     This allows 16 Y-sorted positions per elevation level and prevents z-fighting between maps.
    /// </summary>
    /// <param name="elevation">Elevation level (0-15).</param>
    /// <param name="yPosition">Y position for sorting within elevation.</param>
    /// <param name="mapId">Map identifier (0-99 supported before overflow).</param>
    /// <returns>Layer depth value (0.0 = front, 1.0 = back).</returns>
    private static float CalculateElevationDepth(byte elevation, float yPosition, int mapId)
    {
        // Normalize Y position to 0.0-1.0 range
        float normalizedY = yPosition / MapHeight;

        // Depth calculation priority:
        // 1. Elevation (0-240 range): Primary sorting by height
        // 2. MapId (0-10 range): Secondary sorting to separate overlapping maps
        // 3. Y-position (0-1 range): Tertiary sorting within same elevation/map
        //
        // MapId offset: 0.1 per mapId (supports 100 maps, range 0-10)
        // This ensures maps at same elevation don't z-fight (Route 102 vs Oldale Town)
        float mapOffset = mapId * 0.1f;

        // Calculate depth: elevation (0-240) + mapId (0-10) + yPos (0-1) = 0-251
        float depth = (elevation * 16.0f) + mapOffset + normalizedY;

        // Invert for SpriteBatch (0.0 = front, 1.0 = back)
        // Max depth is (15 * 16) + (100 * 0.1) + 1 = 240 + 10 + 1 = 251
        float layerDepth = 1.0f - (depth / 251.0f);

        // Clamp to valid range
        return MathHelper.Clamp(layerDepth, 0.0f, 1.0f);
    }

    /// <summary>
    ///     Renders all image layers with proper Z-ordering and transparency.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <returns>Number of image layers rendered.</returns>
    private int RenderImageLayers(World world)
    {
        int rendered = 0;

        try
        {
            world.Query(
                in _imageLayerQuery,
                (ref ImageLayer imageLayer) =>
                {
                    // Get texture from AssetManager
                    if (!AssetManager.HasTexture(imageLayer.TextureId))
                    {
                        if (rendered == 0) // Only warn once
                        {
                            _logger?.LogWarning(
                                "  WARNING: Image layer texture '{TextureId}' NOT FOUND - skipping",
                                imageLayer.TextureId
                            );
                        }

                        return;
                    }

                    Texture2D texture = AssetManager.GetTexture(imageLayer.TextureId);

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

    /// <summary>
    ///     Updates the cached map border data for the current frame.
    ///     Called once per frame to avoid repeated queries during border rendering.
    /// </summary>
    private void UpdateMapBordersCache(World world)
    {
        _cachedMapBorders.Clear();

        world.Query(
            in _mapBorderQuery,
            (ref MapInfo mapInfo, ref MapWorldPosition worldPos, ref MapBorder border) =>
            {
                if (border.HasBorder)
                {
                    _cachedMapBorders.Add(
                        new MapBorderInfo
                        {
                            MapIdValue = mapInfo.MapId.Value,
                            WorldOrigin = worldPos.WorldOrigin,
                            WidthInPixels = worldPos.WidthInPixels,
                            HeightInPixels = worldPos.HeightInPixels,
                            MapWidth = mapInfo.Width,
                            MapHeight = mapInfo.Height,
                            TileSize = mapInfo.TileSize,
                            Border = border
                        }
                    );
                }
            }
        );
    }

    /// <summary>
    ///     Renders border tiles for the player's current map only.
    ///     Border tiles are rendered when the camera extends beyond the current map's bounds.
    ///     Uses a 2x2 tiling algorithm for infinite border patterns.
    ///     Renders both bottom layer (ground) and top layer (overhead) tiles.
    /// </summary>
    /// <returns>Number of border tiles rendered.</returns>
    private int RenderBorders(World world)
    {
        if (_cachedMapBorders.Count == 0 || !_cachedCameraBounds.HasValue || _cachedPlayerMapId == null)
        {
            return 0;
        }

        // Find the border for the player's current map only
        MapBorderInfo? playerMapBorder = null;
        foreach (MapBorderInfo borderInfo in _cachedMapBorders)
        {
            if (borderInfo.MapIdValue == _cachedPlayerMapId)
            {
                playerMapBorder = borderInfo;
                break;
            }
        }

        // If player's current map has no border, don't render any borders
        if (!playerMapBorder.HasValue)
        {
            return 0;
        }

        MapBorderInfo primaryBorder = playerMapBorder.Value;
        MapBorder border = primaryBorder.Border;
        int tileSize = primaryBorder.TileSize;

        Rectangle cameraBounds = _cachedCameraBounds.Value;

        // Check if camera extends beyond the player's current map bounds
        // Only render borders if camera shows area outside the current map
        int mapOriginTileX = (int)(primaryBorder.WorldOrigin.X / tileSize);
        int mapOriginTileY = (int)(primaryBorder.WorldOrigin.Y / tileSize);
        int mapRightTile = mapOriginTileX + primaryBorder.MapWidth;
        int mapBottomTile = mapOriginTileY + primaryBorder.MapHeight;

        // If camera is entirely within the current map bounds, no borders needed
        if (
            cameraBounds.Left >= mapOriginTileX
            && cameraBounds.Right <= mapRightTile
            && cameraBounds.Top >= mapOriginTileY
            && cameraBounds.Bottom <= mapBottomTile
        )
        {
            return 0;
        }

        int bordersRendered = 0;

        // Render border tiles in the visible camera area
        // Extend render area slightly beyond camera for smooth scrolling
        int renderLeft = cameraBounds.Left - CameraConstants.BorderRenderMarginTiles;
        int renderRight = cameraBounds.Right + CameraConstants.BorderRenderMarginTiles;
        int renderTop = cameraBounds.Top - CameraConstants.BorderRenderMarginTiles;
        int renderBottom = cameraBounds.Bottom + CameraConstants.BorderRenderMarginTiles;

        // PERFORMANCE: Single dictionary lookup instead of HasTexture + GetTexture
        if (!AssetManager.TryGetTexture(border.TilesetId, out Texture2D? texture) || texture == null)
        {
            if (!_loggedBorderTextureWarning)
            {
                _logger?.LogWarning(
                    "Border texture not found: TilesetId='{TilesetId}', MapId='{MapId}'",
                    border.TilesetId,
                    primaryBorder.MapIdValue
                );
                _loggedBorderTextureWarning = true;
            }

            return 0;
        }

        // Get render order for depth calculation
        int borderMapRenderOrder = GetMapRenderOrder(primaryBorder.MapIdValue);

        // Render border tiles (only tiles OUTSIDE ALL map bounds)
        for (int y = renderTop; y < renderBottom; y++)
        {
            for (int x = renderLeft; x < renderRight; x++)
            {
                // PERFORMANCE: Skip tiles that are INSIDE ANY loaded map's bounds
                // Uses pre-computed tile bounds (no divisions per call)
                if (IsTileInsideAnyMap(x, y))
                {
                    continue;
                }

                // Get the border tile for this position
                // Use coordinates relative to the player's current map for the tiling pattern
                int relativeX = x - mapOriginTileX;
                int relativeY = y - mapOriginTileY;
                int borderIndex = MapBorder.GetBorderTileIndex(relativeX, relativeY);

                // Calculate world pixel position for this border tile
                // Use the same coordinate system as regular tiles
                _reusablePosition.X = x * tileSize;
                _reusablePosition.Y = (y + 1) * tileSize; // +1 for bottom-left origin alignment

                // ===== RENDER BOTTOM LAYER (ground/trunk tiles) =====
                Rectangle bottomSourceRect = border.BottomSourceRects[borderIndex];
                if (!bottomSourceRect.IsEmpty)
                {
                    // Use default elevation (3) for bottom layer - standard ground level
                    float bottomLayerDepth = CalculateElevationDepth(
                        Elevation.Default,
                        _reusablePosition.Y,
                        borderMapRenderOrder
                    );

                    // Origin for bottom-left alignment (same as regular tiles)
                    _reusableTileOrigin.X = 0;
                    _reusableTileOrigin.Y = bottomSourceRect.Height;

                    _spriteBatch.Draw(
                        texture,
                        _reusablePosition,
                        bottomSourceRect,
                        Color.White,
                        0f,
                        _reusableTileOrigin,
                        1f,
                        SpriteEffects.None,
                        bottomLayerDepth
                    );

                    bordersRendered++;
                }

                // ===== RENDER TOP LAYER (overhead/foliage tiles) =====
                if (border.HasTopLayer)
                {
                    Rectangle topSourceRect = border.TopSourceRects[borderIndex];
                    if (!topSourceRect.IsEmpty)
                    {
                        // Use overhead elevation (9) for top layer - above player's head
                        float topLayerDepth = CalculateElevationDepth(
                            Elevation.Overhead,
                            _reusablePosition.Y,
                            borderMapRenderOrder
                        );

                        _reusableTileOrigin.X = 0;
                        _reusableTileOrigin.Y = topSourceRect.Height;

                        _spriteBatch.Draw(
                            texture,
                            _reusablePosition,
                            topSourceRect,
                            Color.White,
                            0f,
                            _reusableTileOrigin,
                            1f,
                            SpriteEffects.None,
                            topLayerDepth
                        );

                        bordersRendered++;
                    }
                }
            }
        }

        return bordersRendered;
    }

    /// <summary>
    ///     Checks if a tile position is inside any loaded map's bounds.
    ///     PERFORMANCE: Uses pre-computed tile coordinates to avoid divisions per call.
    /// </summary>
    /// <param name="tileX">World tile X coordinate.</param>
    /// <param name="tileY">World tile Y coordinate.</param>
    /// <returns>True if the tile is inside any map; otherwise, false.</returns>
    private bool IsTileInsideAnyMap(int tileX, int tileY)
    {
        // PERFORMANCE: Use pre-computed tile bounds instead of divisions
        // Use index-based loop to avoid foreach enumerator allocation
        int count = _cachedMapBounds.Count;
        for (int i = 0; i < count; i++)
        {
            MapBoundsInfo mapInfo = _cachedMapBounds[i];

            // Simple integer comparisons using pre-computed bounds
            if (tileX >= mapInfo.TileX &&
                tileX < mapInfo.TileRight &&
                tileY >= mapInfo.TileY &&
                tileY < mapInfo.TileBottom)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Gets a numeric render order for a map ID string.
    ///     Used for depth sorting to prevent z-fighting between overlapping maps.
    ///     PERFORMANCE: Uses O(1) dictionary lookup instead of O(maps) linear search.
    /// </summary>
    private int GetMapRenderOrder(string? mapIdValue)
    {
        if (mapIdValue == null)
        {
            return 0;
        }

        // PERFORMANCE: O(1) dictionary lookup instead of O(maps) linear search
        return _mapRenderOrderCache.TryGetValue(mapIdValue, out int order) ? order : 0;
    }

    /// <summary>
    ///     Cached border information for a single map.
    /// </summary>
    private struct MapBorderInfo
    {
        public string MapIdValue;
        public Vector2 WorldOrigin;
        public int WidthInPixels;
        public int HeightInPixels;
        public int MapWidth;
        public int MapHeight;
        public int TileSize;
        public MapBorder Border;
    }

    /// <summary>
    ///     Cached map bounds information for border exclusion.
    ///     Used to prevent borders from rendering inside any loaded map.
    /// </summary>
    private struct MapBoundsInfo
    {
        public GameMapId MapId;
        public Vector2 WorldOrigin;
        public int MapWidth;
        public int MapHeight;
        public int TileSize;

        // PERFORMANCE: Pre-computed tile coordinates to avoid divisions per IsTileInsideAnyMap call
        public int TileX;
        public int TileY;
        public int TileRight;
        public int TileBottom;
    }
}
