using System.Diagnostics;

namespace MonoBallFramework.Game.Engine.Core.Types;

/// <summary>
///     Strongly-typed identifier for popup theme definitions.
///     Format: base:theme:{category}/{name}
///     Examples:
///     - base:theme:popup/wood
///     - base:theme:popup/marble
///     - base:theme:popup/underwater
/// </summary>
[DebuggerDisplay("{Value}")]
public sealed record GameThemeId : EntityId
{
    private const string TypeName = "theme";
    private const string DefaultCategory = "popup";

    /// <summary>
    ///     Initializes a new GameThemeId from a full ID string.
    /// </summary>
    /// <param name="value">The full ID string (e.g., "base:theme:popup/wood")</param>
    public GameThemeId(string value) : base(value)
    {
        if (EntityType != TypeName)
        {
            throw new ArgumentException(
                $"Expected entity type '{TypeName}', got '{EntityType}'",
                nameof(value));
        }
    }

    /// <summary>
    ///     Initializes a new GameThemeId from components.
    /// </summary>
    /// <param name="category">The theme category (e.g., "popup")</param>
    /// <param name="name">The theme name (e.g., "wood", "marble")</param>
    /// <param name="ns">Optional namespace (defaults to "base")</param>
    /// <param name="subcategory">Optional subcategory</param>
    public GameThemeId(string category, string name, string? ns = null, string? subcategory = null)
        : base(TypeName, category, name, ns, subcategory)
    {
    }

    /// <summary>
    ///     Creates a GameThemeId from just a name, using defaults.
    /// </summary>
    public static GameThemeId Create(string themeName, string? category = null)
    {
        return new GameThemeId(category ?? DefaultCategory, themeName);
    }

    /// <summary>
    ///     Tries to create a GameThemeId from a string, returning null if invalid.
    ///     Only accepts the full format: base:theme:{category}/{name}
    /// </summary>
    public static GameThemeId? TryCreate(string? value)
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
            return new GameThemeId(value);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Creates a GameThemeId from legacy format.
    /// </summary>
    /// <param name="legacyTheme">Legacy ID (e.g., "wood" or "MAPPOPUP_THEME_WOOD")</param>
    public static GameThemeId FromLegacy(string legacyTheme)
    {
        string normalized = NormalizeComponent(legacyTheme);

        // Remove mappopup_theme_ prefix if present
        if (normalized.StartsWith("mappopup_theme_"))
        {
            normalized = normalized[15..];
        }

        return Create(normalized);
    }

    /// <summary>
    ///     Explicit conversion from string. Use TryCreate() for safe parsing.
    /// </summary>
    public static explicit operator GameThemeId(string value)
    {
        return new GameThemeId(value);
    }
}
