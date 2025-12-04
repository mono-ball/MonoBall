namespace PokeSharp.Game.Components.Rendering;

/// <summary>
///     Elevation level for render order and collision detection.
///     Based on Pokemon Emerald's elevation system.
/// </summary>
/// <remarks>
///     <para>
///         Elevation determines:
///         - Render order (higher elevation renders on top, with Y-sorting within elevation)
///         - Collision (can only interact with objects at same elevation)
///         - Bridges, ledges, and multi-level maps
///     </para>
///     <para>
///         <b>Render Order Formula:</b>
///         layerDepth = (elevation * 16) + (y / mapHeight)
///         This allows proper Y-sorting within each elevation level.
///     </para>
///     <para>
///         <b>Typical Values (Pokemon Emerald):</b>
///         - 0: Ground level (water, pits, lower terrain)
///         - 3: Standard elevation (most tiles and objects)
///         - 4-6: Elevated platforms, bridges
///         - 9-12: Tall objects, overhead structures
///         - 15: Maximum elevation
///     </para>
///     <para>
///         <b>Examples:</b>
///         - Bridge over water: Water at elevation 0, bridge at elevation 6
///         - Tall grass: Grass at elevation 3, player can walk behind it with Y-sorting
///         - Multi-level cave: Different floors at different elevations
///     </para>
/// </remarks>
public struct Elevation
{
    /// <summary>
    ///     Elevation level (0-15).
    ///     Higher values render on top of lower values.
    /// </summary>
    public byte Value { get; set; }

    /// <summary>
    ///     Default elevation for most ground tiles and objects.
    /// </summary>
    public const byte Default = 3;

    /// <summary>
    ///     Ground level (water, pits, lower terrain).
    /// </summary>
    public const byte Ground = 0;

    /// <summary>
    ///     Bridge level (walkways over water/ground).
    /// </summary>
    public const byte Bridge = 6;

    /// <summary>
    ///     Overhead level (tall trees, building roofs).
    /// </summary>
    public const byte Overhead = 9;

    /// <summary>
    ///     Maximum elevation level.
    /// </summary>
    public const byte Max = 15;

    /// <summary>
    ///     Initializes a new elevation with the specified value.
    /// </summary>
    /// <param name="value">Elevation level (0-15, clamped if out of range).</param>
    public Elevation(byte value = Default)
    {
        Value = value > Max ? Max : value; // Clamp to 0-15
    }

    /// <summary>
    ///     Implicit conversion from Elevation to byte.
    /// </summary>
    public static implicit operator byte(Elevation elevation)
    {
        return elevation.Value;
    }

    /// <summary>
    ///     Implicit conversion from byte to Elevation.
    /// </summary>
    public static implicit operator Elevation(byte value)
    {
        return new Elevation(value);
    }

    /// <summary>
    ///     Checks if this elevation is at ground level.
    /// </summary>
    public readonly bool IsGroundLevel => Value == Ground;

    /// <summary>
    ///     Checks if this elevation is at or above bridge level.
    /// </summary>
    public readonly bool IsBridgeOrHigher => Value >= Bridge;

    /// <summary>
    ///     Checks if this elevation is at or above overhead level.
    /// </summary>
    public readonly bool IsOverhead => Value >= Overhead;

    public override readonly string ToString()
    {
        return $"Elevation({Value})";
    }
}
