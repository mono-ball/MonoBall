using System.Diagnostics;

namespace MonoBallFramework.Game.Engine.Core.Types;

/// <summary>
///     Strongly-typed identifier for sprite definitions.
///
///     Format: base:sprite:{category}/{name}
///     Examples:
///     - base:sprite:players/may
///     - base:sprite:generic/prof_birch
///     - base:sprite:gym_leaders/brawly
/// </summary>
[DebuggerDisplay("{Value}")]
public sealed record GameSpriteId : EntityId
{
    private const string TypeName = "sprite";
    private const string DefaultCategory = "generic";

    /// <summary>
    ///     Initializes a new GameSpriteId from a full ID string.
    /// </summary>
    /// <param name="value">The full ID string (e.g., "base:sprite:player/may")</param>
    public GameSpriteId(string value) : base(value)
    {
        if (EntityType != TypeName)
            throw new ArgumentException(
                $"Expected entity type '{TypeName}', got '{EntityType}'",
                nameof(value));
    }

    /// <summary>
    ///     Initializes a new GameSpriteId from components.
    /// </summary>
    /// <param name="category">The sprite category (e.g., "player", "npc", "generic")</param>
    /// <param name="name">The sprite name (e.g., "may", "twin", "boy_1")</param>
    /// <param name="ns">Optional namespace (defaults to "base")</param>
    public GameSpriteId(string category, string name, string? ns = null)
        : base(TypeName, category, name, ns)
    {
    }

    /// <summary>
    ///     The sprite category (shortcut for Category).
    /// </summary>
    public string SpriteCategory => Category;

    /// <summary>
    ///     The sprite name (shortcut for Name).
    /// </summary>
    public string SpriteName => Name;

    /// <summary>
    ///     Gets the texture key for AssetManager lookup.
    ///     Format: "sprites/{category}/{name}"
    /// </summary>
    public string TextureKey => $"sprites/{Category}/{Name}";

    /// <summary>
    ///     Creates a GameSpriteId from just a name, using defaults.
    /// </summary>
    /// <param name="spriteName">The sprite name</param>
    /// <param name="category">Optional category (defaults to "generic")</param>
    /// <returns>A new GameSpriteId</returns>
    public static GameSpriteId Create(string spriteName, string? category = null)
    {
        return new GameSpriteId(category ?? DefaultCategory, spriteName);
    }

    /// <summary>
    ///     Creates a player sprite ID.
    /// </summary>
    /// <param name="name">The sprite name (e.g., "may", "brendan")</param>
    /// <returns>A new GameSpriteId with "players" category</returns>
    public static GameSpriteId CreatePlayer(string name)
    {
        return new GameSpriteId("players", name);
    }

    /// <summary>
    ///     Creates an NPC sprite ID.
    /// </summary>
    /// <param name="name">The sprite name (e.g., "boy_1", "girl_2")</param>
    /// <returns>A new GameSpriteId with "npcs" category</returns>
    public static GameSpriteId CreateNpc(string name)
    {
        return new GameSpriteId("npcs", name);
    }

    /// <summary>
    ///     Creates a gym leader sprite ID.
    /// </summary>
    /// <param name="name">The sprite name (e.g., "roxanne", "brawly")</param>
    /// <returns>A new GameSpriteId with "gym_leaders" category</returns>
    public static GameSpriteId CreateGymLeader(string name)
    {
        return new GameSpriteId("gym_leaders", name);
    }

    /// <summary>
    ///     Tries to create a GameSpriteId from a string, returning null if invalid.
    ///     Only accepts the full format: base:sprite:{category}/{name}
    /// </summary>
    public static GameSpriteId? TryCreate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Only accept full format
        if (!IsValidFormat(value) || !value.Contains($":{TypeName}:"))
            return null;

        try
        {
            return new GameSpriteId(value);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Explicit conversion from string. Use TryCreate() for safe parsing.
    /// </summary>
    public static explicit operator GameSpriteId(string value) => new(value);
}
