using Arch.Core;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Engine.Common.Utilities;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.Engine.Core.Systems;

/// <summary>
///     Interface for querying entities by spatial position with pre-computed component data.
///     Provides efficient tile-based lookups for collision detection, rendering, and AI.
/// </summary>
/// <remarks>
///     <para>
///         This interface provides zero-ECS-overhead spatial queries by returning pre-computed
///         component data that was extracted during spatial hash indexing. Instead of returning
///         raw Entity references that require Has&lt;T&gt;() and Get&lt;T&gt;() calls, methods return
///         specialized entry structs containing all relevant data.
///     </para>
///     <para>
///         <b>Performance Benefits:</b>
///     </para>
///     <list type="bullet">
///         <item>Zero ECS calls during collision checks</item>
///         <item>Zero ECS calls during tile rendering</item>
///         <item>ReadOnlySpan returns enable zero-allocation iteration</item>
///         <item>Data locality - all needed info in contiguous memory</item>
///     </list>
///     <para>
///         <b>Example Usage:</b>
///     </para>
///     <code>
///         // Collision check - zero ECS calls
///         foreach (ref readonly CollisionEntry entry in spatialQuery.GetCollisionEntriesAt(mapId, x, y))
///         {
///             if (entry.Elevation != entityElevation) continue;
///             if (entry.IsSolid) return false; // Blocked!
///         }
///
///         // Tile rendering - zero ECS calls
///         spatialQuery.GetTileRenderEntries(mapId, bounds, _buffer);
///         foreach (ref readonly TileRenderEntry tile in CollectionsMarshal.AsSpan(_buffer))
///         {
///             spriteBatch.Draw(GetTexture(tile.TilesetId), ...);
///         }
///     </code>
/// </remarks>
public interface ISpatialQuery
{
    // ============================================================================
    // COLLISION QUERIES
    // ============================================================================

    /// <summary>
    ///     Gets pre-computed collision data for all entities at the specified position.
    ///     Includes both static tiles and dynamic entities.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="x">The X tile coordinate.</param>
    /// <param name="y">The Y tile coordinate.</param>
    /// <returns>
    ///     ReadOnlySpan of collision entries. Iterate with:
    ///     <c>foreach (ref readonly CollisionEntry entry in span)</c>
    /// </returns>
    /// <remarks>
    ///     Each entry contains: Entity, Elevation, IsSolid, HasTileBehavior.
    ///     No ECS calls needed to check collision - all data is pre-computed.
    /// </remarks>
    ReadOnlySpan<CollisionEntry> GetCollisionEntriesAt(GameMapId mapId, int x, int y);

    /// <summary>
    ///     Gets pre-computed collision data for static tiles only at the specified position.
    ///     Does not include dynamic entities (NPCs, player).
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="x">The X tile coordinate.</param>
    /// <param name="y">The Y tile coordinate.</param>
    /// <returns>ReadOnlySpan of collision entries for static tiles only.</returns>
    ReadOnlySpan<CollisionEntry> GetStaticCollisionEntriesAt(GameMapId mapId, int x, int y);

    /// <summary>
    ///     Gets pre-computed collision data for dynamic entities only at the specified position.
    ///     Does not include static tiles.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="x">The X tile coordinate.</param>
    /// <param name="y">The Y tile coordinate.</param>
    /// <returns>ReadOnlySpan of collision entries for dynamic entities only.</returns>
    ReadOnlySpan<CollisionEntry> GetDynamicCollisionEntriesAt(GameMapId mapId, int x, int y);

    // ============================================================================
    // RENDER QUERIES
    // ============================================================================

    /// <summary>
    ///     Gets pre-computed tile render data at a specific position.
    ///     Returns a span for zero-allocation iteration.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="x">The X tile coordinate.</param>
    /// <param name="y">The Y tile coordinate.</param>
    /// <returns>
    ///     ReadOnlySpan of tile render entries at this position.
    ///     Iterate with: <c>foreach (ref readonly TileRenderEntry tile in span)</c>
    /// </returns>
    /// <remarks>
    ///     <para>
    ///         Each entry contains: SourceRect, TilesetId, OffsetX, OffsetY, Entity,
    ///         Elevation, FlipHorizontally, FlipVertically, IsAnimated.
    ///     </para>
    ///     <para>
    ///         <b>Note:</b> X, Y are NOT in the entry - use the coordinates you passed in.
    ///         This saves 8 bytes per entry and improves cache efficiency.
    ///     </para>
    ///     <para>
    ///         <b>Usage pattern:</b>
    ///         <code>
    ///         for (int y = bounds.Top; y &lt; bounds.Bottom; y++)
    ///         for (int x = bounds.Left; x &lt; bounds.Right; x++)
    ///         {
    ///             foreach (ref readonly var tile in GetTileRenderEntriesAt(mapId, x, y))
    ///             {
    ///                 float posX = (x * TileSize) + worldOrigin.X + tile.OffsetX;
    ///                 // ... render
    ///             }
    ///         }
    ///         </code>
    ///     </para>
    /// </remarks>
    ReadOnlySpan<TileRenderEntry> GetTileRenderEntriesAt(GameMapId mapId, int x, int y);

    /// <summary>
    ///     Gets pre-computed tile render data within the specified bounds.
    ///     Fills the caller-provided list to avoid allocation.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="bounds">The bounding rectangle in local tile coordinates.</param>
    /// <param name="results">List to populate with results.</param>
    /// <remarks>
    ///     <para>
    ///         <b>Prefer GetTileRenderEntriesAt</b> when iterating bounds, as it avoids
    ///         copying tiles to a buffer and doesn't require X, Y in each entry.
    ///     </para>
    ///     <para>
    ///         Each entry contains: SourceRect, TilesetId, OffsetX, OffsetY, Entity,
    ///         Elevation, FlipHorizontally, FlipVertically, IsAnimated.
    ///         No ECS calls needed to render - all data is pre-computed.
    ///     </para>
    /// </remarks>
    void GetTileRenderEntries(GameMapId mapId, Rectangle bounds, List<TileRenderEntry> results);

    // ============================================================================
    // DYNAMIC ENTITY QUERIES
    // ============================================================================

    /// <summary>
    ///     Gets pre-computed dynamic entity data at the specified position.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="x">The X tile coordinate.</param>
    /// <param name="y">The Y tile coordinate.</param>
    /// <returns>ReadOnlySpan of dynamic entity entries.</returns>
    ReadOnlySpan<DynamicEntry> GetDynamicEntriesAt(GameMapId mapId, int x, int y);

    // ============================================================================
    // GENERIC ENTITY QUERIES (for scripting/advanced use cases)
    // ============================================================================

    /// <summary>
    ///     Gets all entities at the specified position (both static and dynamic).
    ///     Use this when you need to access components not covered by specialized entries.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="x">The X tile coordinate.</param>
    /// <param name="y">The Y tile coordinate.</param>
    /// <returns>ReadOnlySpan of Entity references at this position.</returns>
    /// <remarks>
    ///     Prefer using specialized queries (GetCollisionEntriesAt, GetTileRenderEntries)
    ///     when possible to avoid ECS calls. Use this method only when you need to
    ///     access components not included in the pre-computed entries.
    /// </remarks>
    ReadOnlySpan<Entity> GetEntitiesAt(GameMapId mapId, int x, int y);

    /// <summary>
    ///     Gets all entities within the specified bounds.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="bounds">The bounding rectangle in tile coordinates.</param>
    /// <param name="results">List to populate with results.</param>
    void GetEntitiesInBounds(GameMapId mapId, Rectangle bounds, List<Entity> results);

    /// <summary>
    ///     Gets static tile entities within the specified bounds.
    ///     Use GetTileRenderEntries() for rendering (zero ECS calls).
    ///     Use this only when you need raw Entity references.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="bounds">The bounding rectangle in tile coordinates.</param>
    /// <returns>List of Entity references for static tiles.</returns>
    /// <remarks>
    ///     For rendering, prefer GetTileRenderEntries() which provides pre-computed
    ///     render data without requiring ECS calls.
    /// </remarks>
    IReadOnlyList<Entity> GetStaticEntitiesInBounds(GameMapId mapId, Rectangle bounds);
}
