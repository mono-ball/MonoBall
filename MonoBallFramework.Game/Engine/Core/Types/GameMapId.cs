using System.Diagnostics;

namespace MonoBallFramework.Game.Engine.Core.Types;

/// <summary>
///     Strongly-typed identifier for map definitions.
///     Format: base:map:{region}/{name}
///     Examples:
///     - base:map:hoenn/littleroot_town
///     - base:map:hoenn/route_101
///     - mymod:map:custom/my_town
/// </summary>
[DebuggerDisplay("{Value}")]
public sealed record GameMapId : EntityId
{
    private const string TypeName = "map";
    private const string DefaultRegion = "hoenn";

    /// <summary>
    ///     Initializes a new GameMapId from a full ID string.
    /// </summary>
    /// <param name="value">The full ID string (e.g., "base:map:hoenn/littleroot_town")</param>
    public GameMapId(string value) : base(value)
    {
        if (EntityType != TypeName)
        {
            throw new ArgumentException(
                $"Expected entity type '{TypeName}', got '{EntityType}'",
                nameof(value));
        }
    }

    /// <summary>
    ///     Initializes a new GameMapId from components.
    /// </summary>
    /// <param name="region">The region (e.g., "hoenn", "kanto")</param>
    /// <param name="name">The map name (e.g., "littleroot_town")</param>
    /// <param name="ns">Optional namespace (defaults to "base")</param>
    /// <param name="subcategory">Optional subcategory (e.g., "indoor" for indoor maps)</param>
    public GameMapId(string region, string name, string? ns = null, string? subcategory = null)
        : base(TypeName, region, name, ns, subcategory)
    {
    }

    /// <summary>
    ///     Constructs a GameMapId from map name and region components.
    ///     Use this when you have the individual components, not a formatted string.
    /// </summary>
    /// <param name="mapName">The map name (e.g., "littleroot_town")</param>
    /// <param name="region">Optional region (defaults to "hoenn")</param>
    /// <returns>A new GameMapId</returns>
    public static GameMapId FromComponents(string mapName, string? region = null)
    {
        return new GameMapId(region ?? DefaultRegion, mapName);
    }

    /// <summary>
    ///     Tries to parse a GameMapId from a string, returning null if invalid.
    ///     Only accepts the full format: base:map:{region}/{name}
    /// </summary>
    /// <param name="value">The ID string to parse</param>
    public static GameMapId? TryCreate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // Only accept full format
        if (!IsValidFormat(value) || !value.Contains($":{TypeName}:"))
        {
            return null;
        }

        try
        {
            return new GameMapId(value);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Explicit conversion from string. Use TryCreate() for safe parsing.
    /// </summary>
    public static explicit operator GameMapId(string value)
    {
        return new GameMapId(value);
    }
}
