using System.Diagnostics;

namespace MonoBallFramework.Game.Engine.Core.Types;

/// <summary>
///     Strongly-typed identifier for game flags (boolean state variables).
///
///     Format: base:flag:{category}/{name}
///     Examples:
///     - base:flag:hide/rival_oak_lab
///     - base:flag:story/defeated_brock
///     - base:flag:event/got_running_shoes
/// </summary>
/// <remarks>
///     <para>
///         Flags are used for tracking game state and controlling visibility:
///         <list type="bullet">
///             <item>FLAG_HIDE_* pattern: Hide NPCs when flag is true</item>
///             <item>FLAG_SHOW_* pattern: Show NPCs when flag is true</item>
///             <item>Story progression flags</item>
///             <item>Event triggers and one-time events</item>
///         </list>
///     </para>
/// </remarks>
[DebuggerDisplay("{Value}")]
public sealed record GameFlagId : EntityId
{
    private const string TypeName = "flag";
    private const string DefaultCategory = "misc";

    /// <summary>
    ///     Initializes a new GameFlagId from a full ID string.
    /// </summary>
    /// <param name="value">The full ID string (e.g., "base:flag:hide/rival_oak_lab")</param>
    public GameFlagId(string value) : base(value)
    {
        if (EntityType != TypeName)
            throw new ArgumentException(
                $"Expected entity type '{TypeName}', got '{EntityType}'",
                nameof(value));
    }

    /// <summary>
    ///     Initializes a new GameFlagId from components.
    /// </summary>
    /// <param name="category">The flag category (e.g., "hide", "story", "event")</param>
    /// <param name="name">The flag name (e.g., "rival_oak_lab", "defeated_brock")</param>
    /// <param name="ns">Optional namespace (defaults to "base")</param>
    public GameFlagId(string category, string name, string? ns = null)
        : base(TypeName, category, name, ns)
    {
    }

    /// <summary>
    ///     The flag category (shortcut for Category).
    /// </summary>
    public string FlagCategory => Category;

    /// <summary>
    ///     The flag name (shortcut for Name).
    /// </summary>
    public string FlagName => Name;

    /// <summary>
    ///     Creates a GameFlagId from just a name, using defaults.
    /// </summary>
    /// <param name="flagName">The flag name</param>
    /// <param name="category">Optional category (defaults to "misc")</param>
    /// <returns>A new GameFlagId</returns>
    public static GameFlagId Create(string flagName, string? category = null)
    {
        return new GameFlagId(category ?? DefaultCategory, flagName);
    }

    /// <summary>
    ///     Creates a hide flag ID (FLAG_HIDE_* pattern).
    /// </summary>
    /// <param name="name">The flag name (e.g., "rival_oak_lab")</param>
    /// <returns>A new GameFlagId with "hide" category</returns>
    public static GameFlagId CreateHideFlag(string name)
    {
        return new GameFlagId("hide", name);
    }

    /// <summary>
    ///     Creates a show flag ID (FLAG_SHOW_* pattern).
    /// </summary>
    /// <param name="name">The flag name (e.g., "rival_after_battle")</param>
    /// <returns>A new GameFlagId with "show" category</returns>
    public static GameFlagId CreateShowFlag(string name)
    {
        return new GameFlagId("show", name);
    }

    /// <summary>
    ///     Creates a story progression flag ID.
    /// </summary>
    /// <param name="name">The flag name (e.g., "defeated_brock")</param>
    /// <returns>A new GameFlagId with "story" category</returns>
    public static GameFlagId CreateStoryFlag(string name)
    {
        return new GameFlagId("story", name);
    }

    /// <summary>
    ///     Tries to create a GameFlagId from a string, returning null if invalid.
    ///     Only accepts the full format: base:flag:{category}/{name}
    /// </summary>
    public static GameFlagId? TryCreate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Only accept full format
        if (!IsValidFormat(value) || !value.Contains($":{TypeName}:"))
            return null;

        try
        {
            return new GameFlagId(value);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Explicit conversion from string. Use TryCreate() for safe parsing.
    /// </summary>
    public static explicit operator GameFlagId(string value) => new(value);
}
