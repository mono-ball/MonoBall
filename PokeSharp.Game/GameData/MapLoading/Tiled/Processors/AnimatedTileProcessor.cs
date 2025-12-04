using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Game.Engine.Systems.Management;
using PokeSharp.Game.Components.Tiles;
using PokeSharp.Game.Data.MapLoading.Tiled.Services;
using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;
using PokeSharp.Game.Data.MapLoading.Tiled.Utilities;

namespace PokeSharp.Game.Data.MapLoading.Tiled.Processors;

/// <summary>
///     Handles creation of animated tile entities from tileset animations.
///     Uses optimized batch processing to add AnimatedTile components to existing tile entities.
/// </summary>
public class AnimatedTileProcessor : IAnimatedTileProcessor
{
    private readonly ILogger<AnimatedTileProcessor>? _logger;

    public AnimatedTileProcessor(ILogger<AnimatedTileProcessor>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Creates animated tile entities for all tilesets in a map.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="tmxDoc">The TMX document.</param>
    /// <param name="mapInfoEntity">The map entity (unused - tiles already have BelongsToMap).</param>
    /// <param name="tilesets">Loaded tilesets.</param>
    /// <param name="mapId">The map ID to filter tiles by (prevents cross-map corruption).</param>
    public int CreateAnimatedTileEntities(
        World world,
        TmxDocument tmxDoc,
        Entity mapInfoEntity,
        IReadOnlyList<LoadedTileset> tilesets,
        int mapId
    )
    {
        if (tilesets.Count == 0)
        {
            return 0;
        }

        int created = 0;
        foreach (LoadedTileset loadedTileset in tilesets)
        {
            created += CreateAnimatedTileEntitiesForTileset(world, loadedTileset.Tileset, mapId);
        }

        return created;
    }

    /// <summary>
    ///     Creates animated tile entities for a single tileset.
    ///     Uses optimized batch processing to minimize queries.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="tileset">The tileset to process.</param>
    /// <param name="mapId">The map ID to filter tiles by (prevents cross-map corruption).</param>
    private int CreateAnimatedTileEntitiesForTileset(World world, TmxTileset tileset, int mapId)
    {
        if (tileset.Animations.Count == 0)
        {
            return 0;
        }

        int created = 0;

        int tilesPerRow = TilesetUtilities.CalculateTilesPerRow(tileset);
        int tileWidth = tileset.TileWidth;
        int tileHeight = tileset.TileHeight;
        int tileSpacing = tileset.Spacing;
        int tileMargin = tileset.Margin;
        int firstGid = tileset.FirstGid;

        if (tileSpacing < 0)
        {
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has negative spacing value {tileSpacing}."
            );
        }

        if (tileMargin < 0)
        {
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has negative margin value {tileMargin}."
            );
        }

        if (firstGid <= 0)
        {
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has invalid firstgid {firstGid}."
            );
        }

        // PERFORMANCE OPTIMIZATION: Build animation components dictionary BEFORE querying
        // This allows us to execute a SINGLE query instead of N queries (one per animation)
        // Reduces complexity from O(animations × tiles) to O(tiles + animations)
        var animationsByTileId = new Dictionary<int, AnimatedTile>(tileset.Animations.Count);

        foreach (KeyValuePair<int, TmxTileAnimation> kvp in tileset.Animations)
        {
            int localTileId = kvp.Key;
            TmxTileAnimation animation = kvp.Value;

            // Convert local tile ID to global tile ID
            int globalTileId = tileset.FirstGid + localTileId;

            // Convert frame local IDs to global IDs
            int[] globalFrameIds = animation
                .FrameTileIds.Select(id => tileset.FirstGid + id)
                .ToArray();

            // PERFORMANCE OPTIMIZATION: Precalculate ALL source rectangles at load time
            // This eliminates expensive runtime calculations, dictionary lookups, and lock contention
            Rectangle[] frameSourceRects = globalFrameIds
                .Select(frameGid => TilesetUtilities.CalculateSourceRect(frameGid, tileset))
                .ToArray();

            // Create AnimatedTile component with precalculated source rects
            var animatedTile = new AnimatedTile(
                globalTileId,
                globalFrameIds,
                animation.FrameDurations,
                frameSourceRects, // CRITICAL: Precalculated for zero runtime overhead
                firstGid,
                tilesPerRow,
                tileWidth,
                tileHeight,
                tileSpacing,
                tileMargin
            );

            // Store animation component for batch processing
            animationsByTileId[globalTileId] = animatedTile;
        }

        // PERFORMANCE CRITICAL: Execute SINGLE query to process all animations
        // This replaces N individual queries (one per animation) with ONE batch operation
        // For maps with 50 animations × 10,000 tiles, this eliminates 500,000 unnecessary iterations
        //
        // CRITICAL FIX: Filter by MapId to prevent cross-map animation corruption!
        // Without this filter, tiles from OTHER maps with matching GIDs would get
        // incorrect AnimatedTile components, causing visual corruption.
        if (animationsByTileId.Count > 0)
        {
            QueryDescription tileQuery = QueryCache.Get<TilePosition, TileSprite>();
            world.Query(
                in tileQuery,
                (Entity entity, ref TilePosition pos, ref TileSprite sprite) =>
                {
                    // CRITICAL: Only process tiles belonging to THIS map
                    if (pos.MapId.Value != mapId)
                    {
                        return;
                    }

                    // Check if this tile has an animation component
                    if (
                        animationsByTileId.TryGetValue(
                            sprite.TileGid,
                            out AnimatedTile animatedTile
                        )
                    )
                    {
                        world.Add(entity, animatedTile);
                        created++;
                    }
                }
            );
        }

        return created;
    }
}
