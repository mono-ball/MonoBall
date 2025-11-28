using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Core.Systems;
using PokeSharp.Game.Components.Tiles;
using EcsQueries = PokeSharp.Engine.Systems.Queries.Queries;

namespace PokeSharp.Game.Systems;

/// <summary>
///     System that updates animated tile frames based on time.
///     Handles Pokemon-style tile animations (water ripples, grass swaying, flowers).
///     Priority: 850 (after Animation:800, before Render:1000).
///     OPTIMIZED: Uses precalculated source rectangles for zero runtime overhead.
///     POKEMON-ACCURATE: Uses a global timer so all tile animations are synchronized.
/// </summary>
public class TileAnimationSystem(ILogger<TileAnimationSystem>? logger = null)
    : SystemBase,
        IUpdateSystem
{
    private readonly ILogger<TileAnimationSystem>? _logger = logger;
    private int _animatedTileCount = -1; // Track for logging on first update

    /// <summary>
    ///     Global animation timer shared by all tiles.
    ///     Pokemon synchronizes all tile animations to a global clock.
    /// </summary>
    private float _globalAnimationTimer;

    /// <summary>
    ///     Gets the priority for execution order. Lower values execute first.
    ///     Tile animation executes at priority 850, after animation (800) and camera follow (825).
    /// </summary>
    public override int Priority => SystemPriority.TileAnimation;

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        if (!Enabled)
        {
            return;
        }

        // Update global animation timer (shared by all tiles for Pokemon-accurate sync)
        _globalAnimationTimer += deltaTime;

        // Capture timer for lambda (avoid closure issues)
        float globalTimer = _globalAnimationTimer;

        // CRITICAL OPTIMIZATION: Use sequential query instead of ParallelQuery
        // For 100-200 tiles, parallel overhead (task scheduling, thread sync) is MORE EXPENSIVE
        // than just iterating sequentially. ParallelQuery only helps with 500+ entities.
        //
        // Performance comparison (116 tiles):
        // - ParallelQuery: 10-20ms peaks (thread overhead)
        // - Sequential Query: <1ms consistent (no overhead)
        world.Query(
            in EcsQueries.AnimatedTiles,
            (Entity entity, ref AnimatedTile animTile, ref TileSprite sprite) =>
            {
                UpdateTileAnimation(ref animTile, ref sprite, globalTimer);
            }
        );

        // Log animated tile count on first update (sequential count for logging)
        if (_animatedTileCount < 0)
        {
            int tileCount = 0;
            int precalcCount = 0;
            int nullRectCount = 0;

            world.Query(
                in EcsQueries.AnimatedTiles,
                (Entity entity, ref AnimatedTile tile) =>
                {
                    tileCount++;
                    if (tile.FrameSourceRects != null && tile.FrameSourceRects.Length > 0)
                    {
                        precalcCount++;
                    }
                    else
                    {
                        nullRectCount++;
                    }
                }
            );

            if (tileCount > 0)
            {
                _animatedTileCount = tileCount;
                _logger?.LogAnimatedTilesProcessed(_animatedTileCount);
                _logger?.LogWarning(
                    "PERFORMANCE CHECK: {PrecalcCount}/{TotalCount} tiles have precalculated rects. "
                        + "{NullCount} tiles missing precalc (OLD MAP DATA - RELOAD REQUIRED!)",
                    precalcCount,
                    tileCount,
                    nullRectCount
                );
            }
        }
    }

    /// <summary>
    ///     Updates a single animated tile's frame based on the global timer.
    ///     All tiles with the same animation timing will display the same frame (Pokemon-accurate sync).
    ///     OPTIMIZED: Uses precalculated source rectangles - zero dictionary lookups or calculations.
    /// </summary>
    /// <param name="animTile">The animated tile data.</param>
    /// <param name="sprite">The tile sprite to update.</param>
    /// <param name="globalTimer">Global animation timer shared by all tiles.</param>
    private static void UpdateTileAnimation(
        ref AnimatedTile animTile,
        ref TileSprite sprite,
        float globalTimer
    )
    {
        // Validate animation data
        if (
            animTile.FrameTileIds == null
            || animTile.FrameTileIds.Length == 0
            || animTile.FrameDurations == null
            || animTile.FrameDurations.Length == 0
            || animTile.FrameSourceRects == null
            || animTile.FrameSourceRects.Length == 0
        )
        {
            return;
        }

        // Calculate total animation cycle duration
        float totalCycleDuration = 0f;
        for (int i = 0; i < animTile.FrameDurations.Length; i++)
        {
            totalCycleDuration += animTile.FrameDurations[i];
        }

        if (totalCycleDuration <= 0f)
        {
            return;
        }

        // Calculate position within the animation cycle using global timer
        // This ensures all tiles with the same animation are perfectly synchronized
        float timeInCycle = globalTimer % totalCycleDuration;

        // Find which frame we should be displaying
        float accumulatedTime = 0f;
        int frameIndex = 0;
        for (int i = 0; i < animTile.FrameDurations.Length; i++)
        {
            accumulatedTime += animTile.FrameDurations[i];
            if (timeInCycle < accumulatedTime)
            {
                frameIndex = i;
                break;
            }
        }

        // Only update if frame changed (avoid unnecessary writes)
        if (animTile.CurrentFrameIndex != frameIndex)
        {
            animTile.CurrentFrameIndex = frameIndex;

            // PERFORMANCE CRITICAL: Direct array access to precalculated source rectangle
            // No calculations, no dictionary lookups, no lock contention - just a simple array index
            sprite.SourceRect = animTile.FrameSourceRects[frameIndex];
        }
    }
}
