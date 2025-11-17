using System.Diagnostics;

namespace PokeSharp.Engine.Core.Types;

/// <summary>
///     Strongly-typed identifier for map definitions (e.g., "littleroot_town", "route_101").
///     Replaces primitive string obsession with type-safe value type.
/// </summary>
[DebuggerDisplay("{Value}")]
public readonly record struct MapIdentifier
{
    /// <summary>
    ///     Gets the underlying string value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    ///     Initializes a new instance of the MapIdentifier struct.
    /// </summary>
    /// <param name="value">The map identifier string.</param>
    /// <exception cref="ArgumentException">Thrown when value is null, empty, or whitespace.</exception>
    public MapIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Map identifier cannot be null, empty, or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    ///     Implicit conversion from string to MapIdentifier.
    /// </summary>
    public static implicit operator MapIdentifier(string value) => new(value);

    /// <summary>
    ///     Implicit conversion from MapIdentifier to string.
    /// </summary>
    public static implicit operator string(MapIdentifier id) => id.Value;

    /// <summary>
    ///     Returns the string representation of the identifier.
    /// </summary>
    public override string ToString() => Value;

    /// <summary>
    ///     Creates a MapIdentifier from a string, returning null if invalid.
    /// </summary>
    public static MapIdentifier? TryCreate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return new MapIdentifier(value);
    }
}

