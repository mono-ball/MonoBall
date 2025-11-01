using Arch.Core;
using Arch.Core.Extensions;
using PokeSharp.Core.Components;

namespace PokeSharp.Core.Systems;

/// <summary>
/// System that updates animated tile frames based on time.
/// Handles Pokemon-style tile animations (water ripples, grass swaying, flowers).
/// Priority: 850 (after Animation:800, before MapRender:900).
/// </summary>
public class TileAnimationSystem : BaseSystem
{
    /// <inheritdoc/>
    public override int Priority => SystemPriority.TileAnimation;

    private QueryDescription _tileMapQuery;

    /// <inheritdoc/>
    public override void Initialize(World world)
    {
        base.Initialize(world);
        _tileMapQuery = new QueryDescription().WithAll<TileMap, AnimatedTile>();
    }

    /// <inheritdoc/>
    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        if (!Enabled)
        {
            return;
        }

        // Query all entities with TileMap component
        var query = new QueryDescription().WithAll<TileMap>();

        world.Query(in query, (ref TileMap tileMap) =>
        {
            // Check if this map has animated tiles
            if (tileMap.AnimatedTiles == null || tileMap.AnimatedTiles.Length == 0)
            {
                return;
            }

            // Update each animated tile in the array
            for (int i = 0; i < tileMap.AnimatedTiles!.Length; i++)
            {
                UpdateTileAnimation(ref tileMap, ref tileMap.AnimatedTiles[i], deltaTime);
            }
        });
    }

    /// <summary>
    /// Updates a single animated tile's frame timer and advances frames when needed.
    /// </summary>
    /// <param name="tileMap">The tile map containing the animated tiles.</param>
    /// <param name="animatedTile">The animated tile data.</param>
    /// <param name="deltaTime">Time elapsed since last frame in seconds.</param>
    private static void UpdateTileAnimation(ref TileMap tileMap, ref AnimatedTile animatedTile, float deltaTime)
    {
        // Validate animation data
        if (animatedTile.FrameTileIds == null || animatedTile.FrameTileIds.Length == 0 ||
            animatedTile.FrameDurations == null || animatedTile.FrameDurations.Length == 0)
        {
            return;
        }

        // Update frame timer
        animatedTile.FrameTimer += deltaTime;

        // Get current frame duration
        int currentIndex = animatedTile.CurrentFrameIndex;
        if (currentIndex < 0 || currentIndex >= animatedTile.FrameDurations.Length)
        {
            currentIndex = 0;
            animatedTile.CurrentFrameIndex = 0;
        }

        float currentDuration = animatedTile.FrameDurations[currentIndex];

        // Check if we need to advance to next frame
        if (animatedTile.FrameTimer >= currentDuration)
        {
            // Advance to next frame
            animatedTile.CurrentFrameIndex = (currentIndex + 1) % animatedTile.FrameTileIds.Length;
            animatedTile.FrameTimer = 0f;

            // Update tile map data to show the new frame
            UpdateTileMapFrames(ref tileMap, animatedTile);
        }
    }

    /// <summary>
    /// Updates all instances of the animated tile in the tile map layers.
    /// </summary>
    /// <param name="tileMap">The tile map to update.</param>
    /// <param name="animatedTile">The animated tile data.</param>
    private static void UpdateTileMapFrames(ref TileMap tileMap, AnimatedTile animatedTile)
    {
        int currentFrameTileId = animatedTile.FrameTileIds[animatedTile.CurrentFrameIndex];

        // Get previous frame index (wrap around)
        int prevIndex = animatedTile.CurrentFrameIndex - 1;
        if (prevIndex < 0)
        {
            prevIndex = animatedTile.FrameTileIds.Length - 1;
        }
        int previousFrameTileId = animatedTile.FrameTileIds[prevIndex];

        // Update ground layer - replace PREVIOUS frame with CURRENT frame
        UpdateLayer(tileMap.GroundLayer, previousFrameTileId, currentFrameTileId);

        // Update object layer
        UpdateLayer(tileMap.ObjectLayer, previousFrameTileId, currentFrameTileId);

        // Update overhead layer
        UpdateLayer(tileMap.OverheadLayer, previousFrameTileId, currentFrameTileId);
    }

    /// <summary>
    /// Updates a single layer, replacing all instances of the old tile ID with the new tile ID.
    /// </summary>
    /// <param name="layer">The layer data to update.</param>
    /// <param name="oldTileId">The tile ID to find and replace.</param>
    /// <param name="newTileId">The new tile ID to replace with.</param>
    private static void UpdateLayer(int[,] layer, int oldTileId, int newTileId)
    {
        if (layer == null)
        {
            return;
        }

        int height = layer.GetLength(0);
        int width = layer.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Replace previous frame with current frame
                if (layer[y, x] == oldTileId)
                {
                    layer[y, x] = newTileId;
                }
            }
        }
    }
}
