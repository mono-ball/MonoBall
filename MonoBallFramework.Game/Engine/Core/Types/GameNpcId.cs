using System.Diagnostics;

namespace MonoBallFramework.Game.Engine.Core.Types;

/// <summary>
///     Strongly-typed identifier for NPC definitions.
///     Format: base:npc:{category}/{name}
///     Or with subcategory: base:npc:{category}/{subcategory}/{name}
///     Examples:
///     - base:npc:townfolk/prof_birch
///     - base:npc:shopkeeper/pokemart_clerk
///     - base:npc:generic/townfolk/boy_1 (with subcategory)
/// </summary>
[DebuggerDisplay("{Value}")]
public sealed record GameNpcId : EntityId
{
    private const string TypeName = "npc";
    private const string DefaultCategory = "generic";

    /// <summary>
    ///     Initializes a new GameNpcId from a full ID string.
    /// </summary>
    /// <param name="value">The full ID string (e.g., "base:npc:townfolk/prof_birch")</param>
    public GameNpcId(string value) : base(value)
    {
        if (EntityType != TypeName)
        {
            throw new ArgumentException(
                $"Expected entity type '{TypeName}', got '{EntityType}'",
                nameof(value));
        }
    }

    /// <summary>
    ///     Initializes a new GameNpcId from components.
    /// </summary>
    /// <param name="category">The NPC category (e.g., "townfolk", "shopkeeper")</param>
    /// <param name="name">The NPC name (e.g., "prof_birch")</param>
    /// <param name="ns">Optional namespace (defaults to "base")</param>
    /// <param name="subcategory">Optional subcategory</param>
    public GameNpcId(string category, string name, string? ns = null, string? subcategory = null)
        : base(TypeName, category, name, ns, subcategory)
    {
    }

    /// <summary>
    ///     Creates a GameNpcId from just a name, using defaults.
    /// </summary>
    /// <param name="npcName">The NPC name</param>
    /// <param name="category">Optional category (defaults to "generic")</param>
    /// <param name="subcategory">Optional subcategory</param>
    /// <returns>A new GameNpcId</returns>
    public static GameNpcId Create(string npcName, string? category = null, string? subcategory = null)
    {
        return new GameNpcId(category ?? DefaultCategory, npcName, subcategory: subcategory);
    }

    /// <summary>
    ///     Creates a townfolk NPC ID.
    /// </summary>
    /// <param name="name">The NPC name (e.g., "prof_birch")</param>
    /// <returns>A new GameNpcId with "townfolk" category</returns>
    public static GameNpcId CreateTownfolk(string name)
    {
        return new GameNpcId("townfolk", name);
    }

    /// <summary>
    ///     Creates a shopkeeper NPC ID.
    /// </summary>
    /// <param name="name">The NPC name (e.g., "pokemart_clerk")</param>
    /// <returns>A new GameNpcId with "shopkeeper" category</returns>
    public static GameNpcId CreateShopkeeper(string name)
    {
        return new GameNpcId("shopkeeper", name);
    }

    /// <summary>
    ///     Creates a story NPC ID.
    /// </summary>
    /// <param name="name">The NPC name (e.g., "rival_may")</param>
    /// <returns>A new GameNpcId with "story" category</returns>
    public static GameNpcId CreateStory(string name)
    {
        return new GameNpcId("story", name);
    }

    /// <summary>
    ///     Tries to create a GameNpcId from a string, returning null if invalid.
    ///     Only accepts the full format: base:npc:{category}/{name}
    /// </summary>
    public static GameNpcId? TryCreate(string? value)
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
            return new GameNpcId(value);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Explicit conversion from string. Use TryCreate() for safe parsing.
    /// </summary>
    public static explicit operator GameNpcId(string value)
    {
        return new GameNpcId(value);
    }
}
