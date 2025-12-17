using System.Diagnostics;

namespace MonoBallFramework.Game.Engine.Core.Types;

/// <summary>
///     Strongly-typed identifier for font definitions.
///     Format: base:font:{category}/{name}
///     Examples:
///     - base:font:game/pokemon
///     - base:font:debug/mono
///     - base:font:ui/menu
///     - base:font:dialogue/standard
/// </summary>
/// <remarks>
///     <para>
///         Fonts define text rendering styles for different game contexts.
///         Categories typically include:
///         <list type="bullet">
///             <item>"game" - Main game fonts (dialogue, menus)</item>
///             <item>"debug" - Developer/debug overlay fonts</item>
///             <item>"ui" - User interface fonts</item>
///             <item>"dialogue" - Character dialogue fonts</item>
///         </list>
///     </para>
/// </remarks>
[DebuggerDisplay("{Value}")]
public sealed record GameFontId : EntityId
{
    private const string TypeName = "font";
    private const string DefaultCategory = "game";

    /// <summary>
    ///     Initializes a new GameFontId from a full ID string.
    /// </summary>
    /// <param name="value">The full ID string (e.g., "base:font:game/pokemon")</param>
    public GameFontId(string value) : base(value)
    {
        if (EntityType != TypeName)
        {
            throw new ArgumentException(
                $"Expected entity type '{TypeName}', got '{EntityType}'",
                nameof(value));
        }
    }

    /// <summary>
    ///     Initializes a new GameFontId from components.
    /// </summary>
    /// <param name="category">The font category (e.g., "game", "debug", "ui")</param>
    /// <param name="name">The font name (e.g., "pokemon", "mono")</param>
    /// <param name="ns">Optional namespace (defaults to "base")</param>
    /// <param name="subcategory">Optional subcategory</param>
    public GameFontId(string category, string name, string? ns = null, string? subcategory = null)
        : base(TypeName, category, name, ns, subcategory)
    {
    }

    /// <summary>
    ///     Whether this is a game font.
    /// </summary>
    public bool IsGameFont => Category == "game";

    /// <summary>
    ///     Whether this is a debug font.
    /// </summary>
    public bool IsDebugFont => Category == "debug";

    /// <summary>
    ///     Whether this is a UI font.
    /// </summary>
    public bool IsUiFont => Category == "ui";

    /// <summary>
    ///     Whether this is a dialogue font.
    /// </summary>
    public bool IsDialogueFont => Category == "dialogue";

    /// <summary>
    ///     Creates a GameFontId from just a name, using defaults.
    /// </summary>
    /// <param name="fontName">The font name</param>
    /// <param name="category">Optional category (defaults to "game")</param>
    /// <returns>A new GameFontId</returns>
    public static GameFontId Create(string fontName, string? category = null)
    {
        return new GameFontId(category ?? DefaultCategory, fontName);
    }

    /// <summary>
    ///     Creates a game font ID.
    /// </summary>
    /// <param name="name">The font name (e.g., "pokemon")</param>
    /// <returns>A new GameFontId with "game" category</returns>
    public static GameFontId CreateGameFont(string name)
    {
        return new GameFontId("game", name);
    }

    /// <summary>
    ///     Creates a debug font ID.
    /// </summary>
    /// <param name="name">The font name (e.g., "mono")</param>
    /// <returns>A new GameFontId with "debug" category</returns>
    public static GameFontId CreateDebugFont(string name)
    {
        return new GameFontId("debug", name);
    }

    /// <summary>
    ///     Creates a UI font ID.
    /// </summary>
    /// <param name="name">The font name (e.g., "menu")</param>
    /// <returns>A new GameFontId with "ui" category</returns>
    public static GameFontId CreateUiFont(string name)
    {
        return new GameFontId("ui", name);
    }

    /// <summary>
    ///     Creates a dialogue font ID.
    /// </summary>
    /// <param name="name">The font name (e.g., "standard")</param>
    /// <returns>A new GameFontId with "dialogue" category</returns>
    public static GameFontId CreateDialogueFont(string name)
    {
        return new GameFontId("dialogue", name);
    }

    /// <summary>
    ///     Tries to create a GameFontId from a string, returning null if invalid.
    ///     Only accepts the full format: base:font:{category}/{name}
    /// </summary>
    public static GameFontId? TryCreate(string? value)
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
            return new GameFontId(value);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Explicit conversion from string. Use TryCreate() for safe parsing.
    /// </summary>
    public static explicit operator GameFontId(string value)
    {
        return new GameFontId(value);
    }
}
