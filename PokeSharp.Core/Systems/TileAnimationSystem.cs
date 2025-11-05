using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components;

namespace PokeSharp.Core.Systems;

/// <summary>
///     System that updates animated tile frames based on time.
///     Handles Pokemon-style tile animations (water ripples, grass swaying, flowers).
///     Priority: 850 (after Animation:800, before Render:1000).
/// </summary>
public class TileAnimationSystem : BaseSystem
{
    /// <inheritdoc />
    public override int Priority => SystemPriority.TileAnimation;

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        if (!Enabled)
            return;

        // Query all entities with AnimatedTile and TileSprite components
        var query = new QueryDescription().WithAll<AnimatedTile, TileSprite>();

        world.Query(
            in query,
            (Entity entity, ref AnimatedTile animTile, ref TileSprite sprite) =>
            {
                UpdateTileAnimation(ref animTile, ref sprite, deltaTime);
            }
        );
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
            sprite.SourceRect = CalculateTileSourceRect(newFrameTileId, sprite);
        }
    }

    /// <summary>
    ///     Calculates the source rectangle for a tile ID using tileset info.
    ///     Falls back to assumptions if tileset info not found.
    /// </summary>
    private static Rectangle CalculateTileSourceRect(int tileGid, TileSprite sprite)
    {
        // Try to use existing SourceRect dimensions if valid
        if (sprite.SourceRect.Width > 0 && sprite.SourceRect.Height > 0)
        {
            // Just update the position based on new tile ID
            // Assume the dimensions stay the same (all tiles in set are same size)
            const int tilesPerRow = 16; // Fallback assumption
            var tileIndex = tileGid - 1;
            if (tileIndex < 0)
                tileIndex = 0;

            var tileX = tileIndex % tilesPerRow;
            var tileY = tileIndex / tilesPerRow;

            return new Rectangle(
                tileX * sprite.SourceRect.Width,
                tileY * sprite.SourceRect.Height,
                sprite.SourceRect.Width,
                sprite.SourceRect.Height
            );
    }

        // Fallback for invalid SourceRect
        const int fallbackTileSize = 16;
        const int fallbackTilesPerRow = 16;
        var fallbackIndex = tileGid - 1;
        if (fallbackIndex < 0)
            fallbackIndex = 0;

        var fallbackX = fallbackIndex % fallbackTilesPerRow;
        var fallbackY = fallbackIndex / fallbackTilesPerRow;

        return new Rectangle(
            fallbackX * fallbackTileSize,
            fallbackY * fallbackTileSize,
            fallbackTileSize,
            fallbackTileSize
        );
    }
}
