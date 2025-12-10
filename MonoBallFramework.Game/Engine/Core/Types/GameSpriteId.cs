using System.Diagnostics;

namespace MonoBallFramework.Game.Engine.Core.Types;

/// <summary>
///     Strongly-typed identifier for sprite definitions.
///
///     Format: base:sprite:{category}/{name}
///     Or with subcategory: base:sprite:{category}/{subcategory}/{name}
///
///     Examples:
///     - base:sprite:players/may
///     - base:sprite:npcs/prof_birch
///     - base:sprite:npcs/generic/boy_1 (with subcategory)
///     - base:sprite:npcs/gym_leaders/roxanne (with subcategory)
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
    /// <param name="category">The sprite category (e.g., "players", "npcs")</param>
    /// <param name="name">The sprite name (e.g., "may", "boy_1")</param>
    /// <param name="ns">Optional namespace (defaults to "base")</param>
    /// <param name="subcategory">Optional subcategory (e.g., "generic", "trainers")</param>
    public GameSpriteId(string category, string name, string? ns = null, string? subcategory = null)
        : base(TypeName, category, name, ns, subcategory)
    {
    }

    /// <summary>
    ///     Gets the texture key for AssetManager lookup.
    ///     Format: "sprites/{category}/{name}" or "sprites/{category}/{subcategory}/{name}"
    /// </summary>
    public string TextureKey => Subcategory != null
        ? $"sprites/{Category}/{Subcategory}/{Name}"
        : $"sprites/{Category}/{Name}";

    /// <summary>
    ///     Creates a GameSpriteId from just a name, using defaults.
    /// </summary>
    /// <param name="spriteName">The sprite name</param>
    /// <param name="category">Optional category (defaults to "generic")</param>
    /// <param name="subcategory">Optional subcategory</param>
    /// <returns>A new GameSpriteId</returns>
    public static GameSpriteId Create(string spriteName, string? category = null, string? subcategory = null)
    {
        return new GameSpriteId(category ?? DefaultCategory, spriteName, subcategory: subcategory);
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
    ///     Creates an NPC sprite ID with optional subcategory.
    /// </summary>
    /// <param name="name">The sprite name (e.g., "boy_1", "girl_2")</param>
    /// <param name="subcategory">Optional subcategory (e.g., "generic", "townfolk")</param>
    /// <returns>A new GameSpriteId with "npcs" category</returns>
    /// <example>
    ///     GameSpriteId.CreateNpc("boy_1", "generic") → base:sprite:npcs/generic/boy_1
    ///     GameSpriteId.CreateNpc("prof_birch") → base:sprite:npcs/prof_birch
    /// </example>
    public static GameSpriteId CreateNpc(string name, string? subcategory = null)
    {
        return new GameSpriteId("npcs", name, subcategory: subcategory);
    }

    /// <summary>
    ///     Creates a gym leader sprite ID.
    /// </summary>
    /// <param name="name">The sprite name (e.g., "roxanne", "brawly")</param>
    /// <returns>A new GameSpriteId with "npcs" category and "gym_leaders" subcategory</returns>
    public static GameSpriteId CreateGymLeader(string name)
    {
        return new GameSpriteId("npcs", name, subcategory: "gym_leaders");
    }

    /// <summary>
    ///     Creates a generic NPC sprite ID.
    /// </summary>
    /// <param name="name">The sprite name (e.g., "boy_1", "girl_2", "tuber_f")</param>
    /// <returns>A new GameSpriteId with "npcs" category and "generic" subcategory</returns>
    /// <example>
    ///     GameSpriteId.CreateGenericNpc("boy_1") → base:sprite:npcs/generic/boy_1
    /// </example>
    public static GameSpriteId CreateGenericNpc(string name)
    {
        return new GameSpriteId("npcs", name, subcategory: "generic");
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
