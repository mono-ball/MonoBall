using Arch.Core;
using PokeSharp.Engine.Core.Types;

namespace PokeSharp.Game.Components.Relationships;

/// <summary>
///     Relationship component linking an entity to its parent map entity.
///     Used for warps, NPCs, and other map-spawned entities.
/// </summary>
/// <remarks>
///     <para>
///         This component establishes a relationship between entities spawned on a map
///         (warps, NPCs, items, triggers) and the map entity itself. Benefits include:
///         - Automatic cleanup: when a map is unloaded, all related entities can be found
///         - Query optimization: find all entities belonging to a specific map
///         - Debugging: trace an entity back to its source map
///     </para>
///     <para>
///         <b>Usage Example:</b>
///         <code>
///         // When spawning a warp entity
///         Entity warpEntity = world.Create(
///             new Position(tileX, tileY, mapId, tileSize),
///             new WarpPoint(targetMap, targetX, targetY),
///             new BelongsToMap(mapEntity, mapId)
///         );
///         </code>
///     </para>
///     <para>
///         Note: This is a simpler, more specific version of the Parent relationship.
///         Use Parent/Children for general hierarchies; use BelongsToMap for map ownership.
///     </para>
/// </remarks>
public struct BelongsToMap
{
    /// <summary>
    ///     The map entity this entity belongs to.
    ///     Should be validated with world.IsAlive() before use.
    /// </summary>
    public Entity MapEntity { get; set; }

    /// <summary>
    ///     The map's runtime ID for quick spatial queries without entity lookup.
    ///     Cached for performance - avoids frequent MapInfo component access.
    /// </summary>
    public MapRuntimeId MapId { get; set; }

    /// <summary>
    ///     Whether this relationship is currently valid.
    ///     Set to false when the map entity is destroyed.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    ///     Creates a new BelongsToMap relationship.
    /// </summary>
    /// <param name="mapEntity">The parent map entity.</param>
    /// <param name="mapId">The map's runtime ID.</param>
    public BelongsToMap(Entity mapEntity, MapRuntimeId mapId)
    {
        MapEntity = mapEntity;
        MapId = mapId;
        IsValid = true;
    }

    /// <inheritdoc />
    public override readonly string ToString()
    {
        return IsValid
            ? $"BelongsToMap(MapId:{MapId.Value}, Entity:{MapEntity.Id})"
            : $"BelongsToMap(Invalid - MapId:{MapId.Value})";
    }
}

