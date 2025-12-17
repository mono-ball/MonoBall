using System.Diagnostics;

namespace MonoBallFramework.Game.Engine.Core.Types;

/// <summary>
///     Strongly-typed identifier for map section definitions (MAPSEC).
///     Used for region map highlighting and popup themes.
///     Format: base:mapsec:{region}/{name}
///     Examples:
///     - base:mapsec:hoenn/littleroot_town
///     - base:mapsec:hoenn/route_101
///     - base:mapsec:hoenn/petalburg_city
/// </summary>
[DebuggerDisplay("{Value}")]
public sealed record GameMapSectionId : EntityId
{
    private const string TypeName = "mapsec";
    private const string DefaultRegion = "hoenn";

    /// <summary>
    ///     Initializes a new GameMapSectionId from a full ID string.
    /// </summary>
    /// <param name="value">The full ID string (e.g., "base:mapsec:hoenn/littleroot_town")</param>
    public GameMapSectionId(string value) : base(value)
    {
        if (EntityType != TypeName)
        {
            throw new ArgumentException(
                $"Expected entity type '{TypeName}', got '{EntityType}'",
                nameof(value));
        }
    }

    /// <summary>
    ///     Initializes a new GameMapSectionId from components.
    /// </summary>
    /// <param name="region">The region (e.g., "hoenn")</param>
    /// <param name="name">The section name (e.g., "littleroot_town")</param>
    /// <param name="ns">Optional namespace (defaults to "base")</param>
    /// <param name="subcategory">Optional subcategory</param>
    public GameMapSectionId(string region, string name, string? ns = null, string? subcategory = null)
        : base(TypeName, region, name, ns, subcategory)
    {
    }

    /// <summary>
    ///     Creates a GameMapSectionId from just a name, using defaults.
    /// </summary>
    public static GameMapSectionId Create(string sectionName, string? region = null)
    {
        return new GameMapSectionId(region ?? DefaultRegion, sectionName);
    }

    /// <summary>
    ///     Tries to create a GameMapSectionId from a string, returning null if invalid.
    ///     Only accepts the full format: base:mapsec:{region}/{name}
    /// </summary>
    public static GameMapSectionId? TryCreate(string? value)
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
            return new GameMapSectionId(value);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Creates a GameMapSectionId from legacy MAPSEC format.
    /// </summary>
    /// <param name="legacyMapsec">Legacy ID (e.g., "MAPSEC_LITTLEROOT_TOWN")</param>
    /// <param name="region">Optional region override</param>
    public static GameMapSectionId FromLegacy(string legacyMapsec, string? region = null)
    {
        string normalized = NormalizeComponent(legacyMapsec);

        // Remove mapsec_ prefix if present
        if (normalized.StartsWith("mapsec_"))
        {
            normalized = normalized[7..];
        }

        return Create(normalized, region);
    }

    /// <summary>
    ///     Explicit conversion from string. Use TryCreate() for safe parsing.
    /// </summary>
    public static explicit operator GameMapSectionId(string value)
    {
        return new GameMapSectionId(value);
    }
}
