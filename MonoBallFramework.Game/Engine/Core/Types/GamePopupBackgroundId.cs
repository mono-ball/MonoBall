using System.Diagnostics;

namespace MonoBallFramework.Game.Engine.Core.Types;

/// <summary>
///     Strongly-typed identifier for popup background definitions.
///     Format: base:popup:background/{name}
///     Examples:
///     - base:popup:background/wood
///     - base:popup:background/stone
///     - base:popup:background/metal
/// </summary>
/// <remarks>
///     <para>
///         Popup backgrounds define the visual style of map region/location popups.
///         These are bitmap backgrounds rendered behind popup text.
///     </para>
/// </remarks>
[DebuggerDisplay("{Value}")]
public sealed record GamePopupBackgroundId : EntityId
{
    private const string TypeName = "popup";
    private const string DefaultCategory = "background";

    /// <summary>
    ///     Initializes a new GamePopupBackgroundId from a full ID string.
    /// </summary>
    /// <param name="value">The full ID string (e.g., "base:popup:background/wood")</param>
    public GamePopupBackgroundId(string value) : base(value)
    {
        if (EntityType != TypeName)
        {
            throw new ArgumentException(
                $"Expected entity type '{TypeName}', got '{EntityType}'",
                nameof(value));
        }

        if (Category != DefaultCategory)
        {
            throw new ArgumentException(
                $"Expected category '{DefaultCategory}', got '{Category}'",
                nameof(value));
        }
    }

    /// <summary>
    ///     Initializes a new GamePopupBackgroundId from components.
    /// </summary>
    /// <param name="name">The background name (e.g., "wood", "stone")</param>
    /// <param name="ns">Optional namespace (defaults to "base")</param>
    public GamePopupBackgroundId(string name, string? ns = null)
        : base(TypeName, DefaultCategory, name, ns)
    {
    }

    /// <summary>
    ///     Creates a GamePopupBackgroundId from just a name.
    /// </summary>
    /// <param name="name">The background name</param>
    /// <returns>A new GamePopupBackgroundId</returns>
    public static GamePopupBackgroundId Create(string name)
    {
        // Explicitly use 2-param constructor to BUILD an ID from components
        // not the 1-param constructor which PARSES a full ID string
        return new GamePopupBackgroundId(name, null);
    }

    /// <summary>
    ///     Tries to create a GamePopupBackgroundId from a string, returning null if invalid.
    ///     Only accepts the full format: base:popup:background/{name}
    /// </summary>
    public static GamePopupBackgroundId? TryCreate(string? value)
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
            return new GamePopupBackgroundId(value);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Explicit conversion from string. Use TryCreate() for safe parsing.
    /// </summary>
    public static explicit operator GamePopupBackgroundId(string value)
    {
        return new GamePopupBackgroundId(value);
    }
}
