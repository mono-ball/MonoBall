using System.Diagnostics;

namespace PokeSharp.Engine.Core.Types;

/// <summary>
///     Strongly-typed runtime identifier for loaded maps (0, 1, 2, ...).
///     Replaces primitive int obsession with type-safe value type.
/// </summary>
[DebuggerDisplay("{Value}")]
public readonly record struct MapRuntimeId
{
    /// <summary>
    ///     Initializes a new instance of the MapRuntimeId struct.
    /// </summary>
    /// <param name="value">The runtime map ID (must be non-negative).</param>
    /// <exception cref="ArgumentException">Thrown when value is negative.</exception>
    public MapRuntimeId(int value)
    {
        if (value < 0)
        {
            throw new ArgumentException("Map runtime ID cannot be negative.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    ///     Gets the underlying integer value.
    /// </summary>
    public int Value { get; }

    /// <summary>
    ///     Implicit conversion from int to MapRuntimeId.
    /// </summary>
    public static implicit operator MapRuntimeId(int value)
    {
        return new MapRuntimeId(value);
    }

    /// <summary>
    ///     Implicit conversion from MapRuntimeId to int.
    /// </summary>
    public static implicit operator int(MapRuntimeId id)
    {
        return id.Value;
    }

    /// <summary>
    ///     Returns the string representation of the runtime ID.
    /// </summary>
    public override string ToString()
    {
        return Value.ToString();
    }

    /// <summary>
    ///     Creates a MapRuntimeId from an int, returning null if invalid.
    /// </summary>
    public static MapRuntimeId? TryCreate(int value)
    {
        return value < 0 ? null : new MapRuntimeId(value);
    }
}
