using Arch.Core;
using Microsoft.Xna.Framework;

namespace MonoBallFramework.Game.Engine.Core.Systems;

/// <summary>
///     Interface for querying entities by spatial position.
///     Provides efficient tile-based lookups for collision detection, pathfinding, and AI.
/// </summary>
/// <remarks>
///     <para>
///         This interface abstracts spatial querying to reduce coupling between systems.
///         Systems that need spatial data depend on this interface rather than the concrete
///         SpatialHashSystem implementation, improving testability and modularity.
///     </para>
///     <para>
///         <b>Benefits:</b>
///     </para>
///     <list type="bullet">
///         <item>Easier unit testing (can mock spatial queries)</item>
///         <item>Reduced coupling (Dependency Inversion Principle)</item>
///         <item>Flexibility to swap implementations if needed</item>
///         <item>Clearer system dependencies</item>
///     </list>
///     <para>
///         <b>Example Usage:</b>
///     </para>
///     <code>
/// public class MySystem
/// {
///     private readonly ISpatialQuery _spatialQuery;
///
///     public MySystem(ISpatialQuery spatialQuery)
///     {
///         _spatialQuery = spatialQuery;
///     }
///
///     public void CheckCollision(int mapId, int x, int y)
///     {
///         // Query all entities at a tile position
///         var entities = _spatialQuery.GetEntitiesAt(mapId, x, y);
///         foreach (var entity in entities)
///         {
///             // Check collision component...
///         }
///     }
///
///     public void FindEntitiesInArea(int mapId, Rectangle bounds)
///     {
///         // Query entities in a rectangular area
///         var entities = _spatialQuery.GetEntitiesInBounds(mapId, bounds);
///         // Process entities...
///     }
/// }
///     </code>
/// </remarks>
public interface ISpatialQuery
{
    /// <summary>
    ///     Gets all entities at the specified tile position.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="x">The X tile coordinate.</param>
    /// <param name="y">The Y tile coordinate.</param>
    /// <returns>Collection of entities at this position. May be empty but never null.</returns>
    /// <example>
    ///     <code>
    /// var entities = spatialQuery.GetEntitiesAt(mapId: 1, x: 10, y: 5);
    /// foreach (var entity in entities)
    /// {
    ///     if (entity.Has&lt;Collision&gt;())
    ///     {
    ///         // Handle collision...
    ///     }
    /// }
    /// </code>
    /// </example>
    IReadOnlyList<Entity> GetEntitiesAt(int mapId, int x, int y);

    /// <summary>
    ///     Gets all entities within the specified rectangular bounds.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="bounds">The bounding rectangle in tile coordinates.</param>
    /// <returns>Collection of entities within the bounds. May be empty but never null.</returns>
    /// <example>
    ///     <code>
    /// var searchArea = new Rectangle(x: 0, y: 0, width: 10, height: 10);
    /// var entities = spatialQuery.GetEntitiesInBounds(mapId: 1, searchArea);
    /// // Process all entities in 10x10 tile area...
    /// </code>
    /// </example>
    IReadOnlyList<Entity> GetEntitiesInBounds(int mapId, Rectangle bounds);
}
