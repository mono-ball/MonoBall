using System.Diagnostics;

namespace PokeSharp.Game.Engine.Core.Types;

/// <summary>
///     Strongly-typed identifier for sprites (e.g., "may/walking", "boy_1").
///     Replaces primitive string obsession with type-safe value type.
///     Supports both "category/spriteName" and "spriteName" formats.
/// </summary>
[DebuggerDisplay("{Value}")]
public readonly record struct SpriteId
{
    /// <summary>
    ///     Initializes a new instance of the SpriteId struct.
    /// </summary>
    /// <param name="value">The sprite identifier string.</param>
    /// <exception cref="ArgumentException">Thrown when value is null, empty, or whitespace.</exception>
    public SpriteId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Sprite ID cannot be null, empty, or whitespace.",
                nameof(value)
            );
        }

        Value = value;

        // Parse category and sprite name
        string[] parts = value.Split('/', 2);
        if (parts.Length == 2)
        {
            Category = parts[0];
            SpriteName = parts[1];
        }
        else
        {
            Category = "generic";
            SpriteName = value;
        }
    }

    /// <summary>
    ///     Gets the underlying string value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    ///     Gets the category part of the sprite ID (e.g., "may" from "may/walking").
    ///     Returns "generic" if no category is specified.
    /// </summary>
    public string Category { get; }

    /// <summary>
    ///     Gets the sprite name part (e.g., "walking" from "may/walking").
    ///     Returns the full value if no category is specified.
    /// </summary>
    public string SpriteName { get; }

    /// <summary>
    ///     Implicit conversion from string to SpriteId.
    /// </summary>
    public static implicit operator SpriteId(string value)
    {
        return new SpriteId(value);
    }

    /// <summary>
    ///     Implicit conversion from SpriteId to string.
    /// </summary>
    public static implicit operator string(SpriteId id)
    {
        return id.Value;
    }

    /// <summary>
    ///     Returns the string representation of the sprite ID.
    /// </summary>
    public override string ToString()
    {
        return Value;
    }

    /// <summary>
    ///     Creates a SpriteId from a string, returning null if invalid.
    /// </summary>
    public static SpriteId? TryCreate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return new SpriteId(value);
    }

    /// <summary>
    ///     Creates a SpriteId from category and sprite name.
    /// </summary>
    public static SpriteId Create(string category, string spriteName)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException(
                "Category cannot be null, empty, or whitespace.",
                nameof(category)
            );
        }

        if (string.IsNullOrWhiteSpace(spriteName))
        {
            throw new ArgumentException(
                "Sprite name cannot be null, empty, or whitespace.",
                nameof(spriteName)
            );
        }

        return new SpriteId($"{category}/{spriteName}");
    }
}
