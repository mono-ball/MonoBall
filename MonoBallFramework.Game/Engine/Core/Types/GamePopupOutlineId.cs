using System.Diagnostics;

namespace MonoBallFramework.Game.Engine.Core.Types;

/// <summary>
///     Strongly-typed identifier for popup outline/border definitions.
///     Format: base:popup:outline/{name}
///     Examples:
///     - base:popup:outline/stone_outline
///     - base:popup:outline/wood_outline
///     - base:popup:outline/metal_outline
/// </summary>
/// <remarks>
///     <para>
///         Popup outlines define the border style of map region/location popups.
///         Supports both tile sheet rendering (GBA-accurate) and legacy 9-slice rendering.
///     </para>
/// </remarks>
[DebuggerDisplay("{Value}")]
public sealed record GamePopupOutlineId : EntityId
{
    private const string TypeName = "popup";
    private const string DefaultCategory = "outline";

    /// <summary>
    ///     Initializes a new GamePopupOutlineId from a full ID string.
    /// </summary>
    /// <param name="value">The full ID string (e.g., "base:popup:outline/stone_outline")</param>
    public GamePopupOutlineId(string value) : base(value)
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
    ///     Initializes a new GamePopupOutlineId from components.
    /// </summary>
    /// <param name="name">The outline name (e.g., "stone_outline", "wood_outline")</param>
    /// <param name="ns">Optional namespace (defaults to "base")</param>
    public GamePopupOutlineId(string name, string? ns = null)
        : base(TypeName, DefaultCategory, name, ns)
    {
    }

    /// <summary>
    ///     Creates a GamePopupOutlineId from just a name.
    /// </summary>
    /// <param name="name">The outline name</param>
    /// <returns>A new GamePopupOutlineId</returns>
    public static GamePopupOutlineId Create(string name)
    {
        // Explicitly use 2-param constructor to BUILD an ID from components
        // not the 1-param constructor which PARSES a full ID string
        return new GamePopupOutlineId(name, null);
    }

    /// <summary>
    ///     Tries to create a GamePopupOutlineId from a string, returning null if invalid.
    ///     Only accepts the full format: base:popup:outline/{name}
    /// </summary>
    public static GamePopupOutlineId? TryCreate(string? value)
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
            return new GamePopupOutlineId(value);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Explicit conversion from string. Use TryCreate() for safe parsing.
    /// </summary>
    public static explicit operator GamePopupOutlineId(string value)
    {
        return new GamePopupOutlineId(value);
    }
}
