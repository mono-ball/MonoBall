using System;
using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components.Tiles;
using PokeSharp.Core.Logging;
using PokeSharp.Core.Queries;

namespace PokeSharp.Core.Systems;

/// <summary>
///     System that updates animated tile frames based on time.
///     Handles Pokemon-style tile animations (water ripples, grass swaying, flowers).
///     Priority: 850 (after Animation:800, before Render:1000).
/// </summary>
public class TileAnimationSystem(ILogger<TileAnimationSystem>? logger = null) : BaseSystem
{
    private readonly ILogger<TileAnimationSystem>? _logger = logger;
    private int _animatedTileCount = -1; // Track for logging on first update

    /// <inheritdoc />
    public override int Priority => SystemPriority.TileAnimation;

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        if (!Enabled)
            return;

        // Use centralized query for animated tiles
        var tileCount = 0;

        world.Query(
            in Queries.Queries.AnimatedTiles,
            (Entity entity, ref AnimatedTile animTile, ref TileSprite sprite) =>
            {
                UpdateTileAnimation(ref animTile, ref sprite, deltaTime);
                tileCount++;
            }
        );

        // Log animated tile count on first update
        if (_animatedTileCount < 0 && tileCount > 0)
        {
            _animatedTileCount = tileCount;
            _logger?.LogAnimatedTilesProcessed(_animatedTileCount);
        }
    }

    /// <summary>
    ///     Updates a single animated tile's frame timer and advances frames when needed.
    ///     Updates the TileSprite component's SourceRect to display the new frame.
    /// </summary>
    /// <param name="animTile">The animated tile data.</param>
    /// <param name="sprite">The tile sprite to update.</param>
    /// <param name="deltaTime">Time elapsed since last frame in seconds.</param>
    private static void UpdateTileAnimation(
        ref AnimatedTile animTile,
        ref TileSprite sprite,
        float deltaTime
    )
    {
        // Validate animation data
        if (
            animTile.FrameTileIds == null
            || animTile.FrameTileIds.Length == 0
            || animTile.FrameDurations == null
            || animTile.FrameDurations.Length == 0
        )
            return;

        // Update frame timer
        animTile.FrameTimer += deltaTime;

        // Get current frame duration
        var currentIndex = animTile.CurrentFrameIndex;
        if (currentIndex < 0 || currentIndex >= animTile.FrameDurations.Length)
        {
            currentIndex = 0;
            animTile.CurrentFrameIndex = 0;
        }

        var currentDuration = animTile.FrameDurations[currentIndex];

        // Check if we need to advance to next frame
        if (animTile.FrameTimer >= currentDuration)
        {
            // Advance to next frame
            animTile.CurrentFrameIndex = (currentIndex + 1) % animTile.FrameTileIds.Length;
            animTile.FrameTimer = 0f;

            // Update sprite's source rectangle for the new frame
            var newFrameTileId = animTile.FrameTileIds[animTile.CurrentFrameIndex];
            sprite.SourceRect = CalculateTileSourceRect(newFrameTileId, ref animTile, ref sprite);
        }
    }

    /// <summary>
    ///     Calculates the source rectangle for a tile ID using tileset info.
    ///     Falls back to assumptions if tileset info not found.
    /// </summary>
    private static Rectangle CalculateTileSourceRect(
        int tileGid,
        ref AnimatedTile animTile,
        ref TileSprite sprite
    )
    {
        if (animTile.TilesetFirstGid <= 0)
            throw new InvalidOperationException("AnimatedTile missing tileset first GID.");

        var firstGid = animTile.TilesetFirstGid;
        var localId = tileGid - firstGid;
        if (localId < 0)
            throw new InvalidOperationException(
                $"Tile GID {tileGid} is not part of tileset starting at {firstGid}."
            );

        var tileWidth = animTile.TileWidth;
        var tileHeight = animTile.TileHeight;
        var tilesPerRow = animTile.TilesPerRow;

        if (tileWidth <= 0 || tileHeight <= 0)
            throw new InvalidOperationException(
                $"AnimatedTile missing tile dimensions for TilesetFirstGid={animTile.TilesetFirstGid}"
            );

        if (tilesPerRow <= 0)
            throw new InvalidOperationException(
                $"AnimatedTile missing tiles-per-row for TilesetFirstGid={animTile.TilesetFirstGid}"
            );

        var spacing = Math.Max(0, animTile.TileSpacing);
        var margin = Math.Max(0, animTile.TileMargin);

        var tileX = localId % tilesPerRow;
        var tileY = localId / tilesPerRow;

        var sourceX = margin + tileX * (tileWidth + spacing);
        var sourceY = margin + tileY * (tileHeight + spacing);

        return new Rectangle(sourceX, sourceY, tileWidth, tileHeight);
    }
}
