using Microsoft.Xna.Framework;
using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

namespace PokeSharp.Game.Data.MapLoading.Tiled.Utilities;

/// <summary>
///     Utility methods for tileset calculations.
///     Shared utilities for calculating source rectangles and tiles per row.
/// </summary>
public static class TilesetUtilities
{
    /// <summary>
    ///     Calculates the number of tiles per row in a tileset.
    /// </summary>
    /// <param name="tileset">The tileset to calculate for.</param>
    /// <returns>Number of tiles per row.</returns>
    /// <exception cref="InvalidOperationException">If tileset has invalid dimensions or spacing.</exception>
    public static int CalculateTilesPerRow(TmxTileset tileset)
    {
        if (tileset.TileWidth <= 0)
        {
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has invalid tile width {tileset.TileWidth}."
            );
        }

        if (tileset.Image == null || tileset.Image.Width <= 0)
        {
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' is missing a valid image width."
            );
        }

        int spacing = tileset.Spacing;
        int margin = tileset.Margin;

        if (spacing < 0)
        {
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has negative spacing value {spacing}."
            );
        }

        if (margin < 0)
        {
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has negative margin value {margin}."
            );
        }

        int usableWidth = tileset.Image.Width - (margin * 2);
        if (usableWidth <= 0)
        {
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has unusable image width after margins."
            );
        }

        int step = tileset.TileWidth + spacing;
        if (step <= 0)
        {
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has invalid step size {step}."
            );
        }

        int tilesPerRow = (usableWidth + spacing) / step;
        if (tilesPerRow <= 0)
        {
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' produced non-positive tiles-per-row."
            );
        }

        return tilesPerRow;
    }

    /// <summary>
    ///     Calculates the source rectangle for a tile in a tileset texture.
    /// </summary>
    /// <param name="tileGid">The global tile ID.</param>
    /// <param name="tileset">The tileset containing the tile.</param>
    /// <returns>Source rectangle for the tile.</returns>
    /// <exception cref="InvalidOperationException">If tileset has invalid dimensions or spacing.</exception>
    public static Rectangle CalculateSourceRect(int tileGid, TmxTileset tileset)
    {
        // Convert global ID to local ID
        int localTileId = tileGid - tileset.FirstGid;

        // Get tileset dimensions
        int tileWidth = tileset.TileWidth;
        int tileHeight = tileset.TileHeight;

        // Validate tile dimensions to prevent division by zero
        if (tileWidth <= 0 || tileHeight <= 0)
        {
            throw new InvalidOperationException(
                $"Invalid tile dimensions: {tileWidth}x{tileHeight}"
            );
        }

        if (tileset.Image == null || tileset.Image.Width <= 0 || tileset.Image.Height <= 0)
        {
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' is missing valid image dimensions."
            );
        }

        int spacing = tileset.Spacing;
        int margin = tileset.Margin;

        if (spacing < 0)
        {
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has negative spacing value {spacing}."
            );
        }

        if (margin < 0)
        {
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has negative margin value {margin}."
            );
        }

        int usableWidth = tileset.Image.Width - (margin * 2);
        if (usableWidth <= 0)
        {
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has unusable image width after margins."
            );
        }

        int step = tileWidth + spacing;
        if (step <= 0)
        {
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has invalid step size {step}."
            );
        }

        int tilesPerRow = CalculateTilesPerRow(tileset);

        // Calculate tile position in the grid
        int tileX = localTileId % tilesPerRow;
        int tileY = localTileId / tilesPerRow;

        // Calculate source rect with spacing and margin
        int sourceX = margin + (tileX * (tileWidth + spacing));
        int sourceY = margin + (tileY * (tileHeight + spacing));

        return new Rectangle(sourceX, sourceY, tileWidth, tileHeight);
    }
}
