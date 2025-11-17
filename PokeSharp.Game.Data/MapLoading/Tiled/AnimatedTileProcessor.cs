using System.Linq;
using Arch.Core;
using Arch.Core.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Game.Components.Rendering;
using PokeSharp.Game.Components.Tiles;
using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

namespace PokeSharp.Game.Data.MapLoading.Tiled;

/// <summary>
///     Handles creation of animated tile entities from tileset animations.
///     Uses optimized batch processing to add AnimatedTile components to existing tile entities.
/// </summary>
public class AnimatedTileProcessor
{
    private readonly ILogger<AnimatedTileProcessor>? _logger;

    public AnimatedTileProcessor(ILogger<AnimatedTileProcessor>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Creates animated tile entities for all tilesets in a map.
    /// </summary>
    public int CreateAnimatedTileEntities(
        World world,
        TmxDocument tmxDoc,
        IReadOnlyList<LoadedTileset> tilesets
    )
    {
        if (tilesets.Count == 0)
            return 0;

        var created = 0;
        foreach (var loadedTileset in tilesets)
        {
            created += CreateAnimatedTileEntitiesForTileset(world, loadedTileset.Tileset);
        }

        return created;
    }

    /// <summary>
    ///     Creates animated tile entities for a single tileset.
    ///     Uses optimized batch processing to minimize queries.
    /// </summary>
    private int CreateAnimatedTileEntitiesForTileset(World world, TmxTileset tileset)
    {
        if (tileset.Animations.Count == 0)
            return 0;

        var created = 0;

        var tilesPerRow = CalculateTilesPerRow(tileset);
        var tileWidth = tileset.TileWidth;
        var tileHeight = tileset.TileHeight;
        var tileSpacing = tileset.Spacing;
        var tileMargin = tileset.Margin;
        var firstGid = tileset.FirstGid;

        if (tileSpacing < 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has negative spacing value {tileSpacing}."
            );

        if (tileMargin < 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has negative margin value {tileMargin}."
            );

        if (firstGid <= 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has invalid firstgid {firstGid}."
            );

        // PERFORMANCE OPTIMIZATION: Build animation components dictionary BEFORE querying
        // This allows us to execute a SINGLE query instead of N queries (one per animation)
        // Reduces complexity from O(animations × tiles) to O(tiles + animations)
        var animationsByTileId = new Dictionary<int, AnimatedTile>(tileset.Animations.Count);

        foreach (var kvp in tileset.Animations)
        {
            var localTileId = kvp.Key;
            var animation = kvp.Value;

            // Convert local tile ID to global tile ID
            var globalTileId = tileset.FirstGid + localTileId;

            // Convert frame local IDs to global IDs
            var globalFrameIds = animation
                .FrameTileIds.Select(id => tileset.FirstGid + id)
                .ToArray();

            // PERFORMANCE OPTIMIZATION: Precalculate ALL source rectangles at load time
            // This eliminates expensive runtime calculations, dictionary lookups, and lock contention
            var frameSourceRects = globalFrameIds
                .Select(frameGid =>
                    CalculateTileSourceRect(
                        frameGid,
                        firstGid,
                        tileWidth,
                        tileHeight,
                        tilesPerRow,
                        tileSpacing,
                        tileMargin
                    )
                )
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
        if (animationsByTileId.Count > 0)
        {
            var tileQuery = QueryCache.Get<TileSprite>();
            world.Query(
                in tileQuery,
                (Entity entity, ref TileSprite sprite) =>
                {
                    // Check if this tile has an animation component
                    if (animationsByTileId.TryGetValue(sprite.TileGid, out var animatedTile))
                    {
                        world.Add(entity, animatedTile);
                        created++;
                    }
                }
            );
        }

        return created;
    }

    /// <summary>
    ///     Calculates the number of tiles per row in a tileset.
    /// </summary>
    private static int CalculateTilesPerRow(TmxTileset tileset)
    {
        if (tileset.TileWidth <= 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has invalid tile width {tileset.TileWidth}."
            );

        if (tileset.Image == null || tileset.Image.Width <= 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' is missing a valid image width."
            );

        var spacing = tileset.Spacing;
        var margin = tileset.Margin;

        if (spacing < 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has negative spacing value {spacing}."
            );
        if (margin < 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has negative margin value {margin}."
            );

        var usableWidth = tileset.Image.Width - margin * 2;
        if (usableWidth <= 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has unusable image width after margins."
            );

        var step = tileset.TileWidth + spacing;
        if (step <= 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has invalid step size {step}."
            );

        var tilesPerRow = (usableWidth + spacing) / step;
        if (tilesPerRow <= 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' produced non-positive tiles-per-row."
            );

        return tilesPerRow;
    }

    /// <summary>
    ///     Calculates the source rectangle for a tile in a tileset texture.
    /// </summary>
    private static Rectangle CalculateTileSourceRect(
        int tileGid,
        int firstGid,
        int tileWidth,
        int tileHeight,
        int tilesPerRow,
        int spacing,
        int margin
    )
    {
        var localId = tileGid - firstGid;
        if (localId < 0)
            throw new InvalidOperationException(
                $"Tile GID {tileGid} is not part of tileset starting at {firstGid}."
            );

        spacing = Math.Max(0, spacing);
        margin = Math.Max(0, margin);

        var tileX = localId % tilesPerRow;
        var tileY = localId / tilesPerRow;

        var sourceX = margin + tileX * (tileWidth + spacing);
        var sourceY = margin + tileY * (tileHeight + spacing);

        return new Rectangle(sourceX, sourceY, tileWidth, tileHeight);
    }
}

