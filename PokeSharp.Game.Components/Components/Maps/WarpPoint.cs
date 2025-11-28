using PokeSharp.Engine.Core.Types;

namespace PokeSharp.Game.Components.Maps;

/// <summary>
///     Represents a warp point that teleports entities to another map location.
///     Created from Tiled warp_event objects during map loading.
/// </summary>
/// <remarks>
///     <para>
///         WarpPoints are entities with Position and WarpPoint components.
///         They define a one-way teleport from their position to the target.
///     </para>
///     <para>
///         <b>Related Components:</b>
///         - Position: The warp's location on the current map
///         - BelongsToMap: Relationship to the parent map entity
///         - MapWarps: Spatial index on map entity for O(1) lookup
///     </para>
/// </remarks>
public struct WarpPoint
{
    /// <summary>
    ///     Gets or sets the target map identifier (type-safe).
    /// </summary>
    public MapIdentifier TargetMap { get; set; }

    /// <summary>
    ///     Gets or sets the target X tile coordinate on the destination map.
    /// </summary>
    public int TargetX { get; set; }

    /// <summary>
    ///     Gets or sets the target Y tile coordinate on the destination map.
    /// </summary>
    public int TargetY { get; set; }

    /// <summary>
    ///     Gets or sets the target elevation on the destination map.
    /// </summary>
    public byte TargetElevation { get; set; }

    /// <summary>
    ///     Creates a new WarpPoint with the specified destination.
    /// </summary>
    /// <param name="targetMap">Target map identifier.</param>
    /// <param name="targetX">Target X tile coordinate.</param>
    /// <param name="targetY">Target Y tile coordinate.</param>
    /// <param name="targetElevation">Target elevation (default: 3).</param>
    public WarpPoint(MapIdentifier targetMap, int targetX, int targetY, byte targetElevation = 3)
    {
        TargetMap = targetMap;
        TargetX = targetX;
        TargetY = targetY;
        TargetElevation = targetElevation;
    }

    /// <summary>
    ///     Creates a new WarpPoint from a string map name (convenience constructor).
    /// </summary>
    /// <param name="targetMapName">Target map name string.</param>
    /// <param name="targetX">Target X tile coordinate.</param>
    /// <param name="targetY">Target Y tile coordinate.</param>
    /// <param name="targetElevation">Target elevation (default: 3).</param>
    public WarpPoint(string targetMapName, int targetX, int targetY, byte targetElevation = 3)
        : this(new MapIdentifier(targetMapName), targetX, targetY, targetElevation)
    {
    }

    /// <inheritdoc />
    public override readonly string ToString()
    {
        return $"WarpPoint(→ {TargetMap.Value} @ {TargetX},{TargetY} elev:{TargetElevation})";
    }
}
