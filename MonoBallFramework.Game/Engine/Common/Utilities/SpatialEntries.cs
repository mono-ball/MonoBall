using System.Runtime.InteropServices;
using Arch.Core;
using Microsoft.Xna.Framework;

namespace MonoBallFramework.Game.Engine.Common.Utilities;

/// <summary>
///     Flags for collision entry properties, packed into a single byte.
/// </summary>
[Flags]
public enum CollisionFlags : byte
{
    None = 0,
    Solid = 1 << 0,
    HasBehavior = 1 << 1
}

/// <summary>
///     Flags for dynamic entry properties, packed into a single byte.
/// </summary>
[Flags]
public enum DynamicFlags : byte
{
    None = 0,
    HasCollision = 1 << 0,
    Solid = 1 << 1
}

/// <summary>
///     Pre-computed collision data for spatial lookups.
///     Eliminates Has&lt;T&gt;() and Get&lt;T&gt;() calls during collision checks.
/// </summary>
/// <remarks>
///     This struct is populated during spatial hash indexing when we already have
///     access to component data via Arch queries. Storing the data here means
///     collision checks become pure data comparisons with zero ECS overhead.
///     Bools are packed into a flags byte for better memory density (12 bytes vs 16).
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public readonly struct CollisionEntry
{
    /// <summary>
    ///     The entity reference. Only needed if TileBehavior calls are required.
    /// </summary>
    public readonly Entity Entity;

    /// <summary>
    ///     Pre-computed elevation value. Collision only occurs at same elevation.
    /// </summary>
    public readonly byte Elevation;

    /// <summary>
    ///     Packed flags for collision properties.
    /// </summary>
    private readonly CollisionFlags _flags;

    /// <summary>
    ///     Whether this entity blocks movement (Collision.IsSolid).
    /// </summary>
    public bool IsSolid => (_flags & CollisionFlags.Solid) != 0;

    /// <summary>
    ///     Whether this entity has a TileBehavior component.
    ///     If true, caller may need to check behavior-specific blocking.
    /// </summary>
    public bool HasTileBehavior => (_flags & CollisionFlags.HasBehavior) != 0;

    public CollisionEntry(Entity entity, byte elevation, bool isSolid, bool hasTileBehavior)
    {
        Entity = entity;
        Elevation = elevation;
        _flags = (isSolid ? CollisionFlags.Solid : 0)
               | (hasTileBehavior ? CollisionFlags.HasBehavior : 0);
    }
}

/// <summary>
///     Pre-computed tile render data for spatial lookups.
///     Eliminates TryGet() calls during tile rendering.
/// </summary>
/// <remarks>
///     <para>
///         Contains all data needed to render a tile without any ECS access.
///         The only external dependency is the texture lookup by TilesetId.
///         For animated tiles (IsAnimated=true), SourceRect must be fetched from
///         the entity's TileSprite component at render time since it changes each frame.
///     </para>
///     <para>
///         <b>Note:</b> X, Y coordinates are NOT stored in this struct to save memory.
///         When iterating tiles, use GetTileRenderEntriesAt(x, y) and use the loop
///         coordinates directly for position calculations.
///     </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public readonly struct TileRenderEntry
{
    /// <summary>
    ///     Source rectangle in the tileset texture.
    ///     For animated tiles, this is the initial frame - use entity's TileSprite for current frame.
    /// </summary>
    public readonly Rectangle SourceRect;

    /// <summary>
    ///     Tileset identifier for texture lookup.
    /// </summary>
    public readonly string TilesetId;

    /// <summary>
    ///     Layer offset X (0 if no LayerOffset component).
    /// </summary>
    public readonly float OffsetX;

    /// <summary>
    ///     Layer offset Y (0 if no LayerOffset component).
    /// </summary>
    public readonly float OffsetY;

    /// <summary>
    ///     The entity reference. Needed for animated tiles to get current SourceRect.
    /// </summary>
    public readonly Entity Entity;

    /// <summary>
    ///     Elevation for depth sorting.
    /// </summary>
    public readonly byte Elevation;

    /// <summary>
    ///     Horizontal flip flag.
    /// </summary>
    public readonly bool FlipHorizontally;

    /// <summary>
    ///     Vertical flip flag.
    /// </summary>
    public readonly bool FlipVertically;

    /// <summary>
    ///     Whether this tile has animation. If true, SourceRect must be fetched
    ///     from entity's TileSprite component at render time.
    /// </summary>
    public readonly bool IsAnimated;

    public TileRenderEntry(
        Rectangle sourceRect,
        string tilesetId,
        float offsetX,
        float offsetY,
        Entity entity,
        byte elevation,
        bool flipHorizontally,
        bool flipVertically,
        bool isAnimated = false)
    {
        SourceRect = sourceRect;
        TilesetId = tilesetId;
        OffsetX = offsetX;
        OffsetY = offsetY;
        Entity = entity;
        Elevation = elevation;
        FlipHorizontally = flipHorizontally;
        FlipVertically = flipVertically;
        IsAnimated = isAnimated;
    }
}

/// <summary>
///     Pre-computed dynamic entity data (NPCs, player) for spatial lookups.
///     Bools are packed into a flags byte for better memory density (12 bytes vs 16).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct DynamicEntry
{
    /// <summary>
    ///     The entity reference.
    /// </summary>
    public readonly Entity Entity;

    /// <summary>
    ///     Pre-computed elevation value.
    /// </summary>
    public readonly byte Elevation;

    /// <summary>
    ///     Packed flags for dynamic entry properties.
    /// </summary>
    private readonly DynamicFlags _flags;

    /// <summary>
    ///     Whether this entity has a Collision component.
    /// </summary>
    public bool HasCollision => (_flags & DynamicFlags.HasCollision) != 0;

    /// <summary>
    ///     Whether this entity blocks movement (Collision.IsSolid).
    /// </summary>
    public bool IsSolid => (_flags & DynamicFlags.Solid) != 0;

    public DynamicEntry(Entity entity, byte elevation, bool hasCollision, bool isSolid)
    {
        Entity = entity;
        Elevation = elevation;
        _flags = (hasCollision ? DynamicFlags.HasCollision : 0)
               | (isSolid ? DynamicFlags.Solid : 0);
    }
}
