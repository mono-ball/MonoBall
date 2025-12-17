using System.Diagnostics;

namespace MonoBallFramework.Game.Engine.Core.Types;

/// <summary>
///     Strongly-typed identifier for tile behavior definitions.
///     Format: base:tile_behavior:{category}/{name}
///     Examples:
///     - base:tile_behavior:movement/jump_south
///     - base:tile_behavior:movement/ice
///     - base:tile_behavior:terrain/tall_grass
///     - base:tile_behavior:special/warp
/// </summary>
/// <remarks>
///     <para>
///         Tile behaviors define how tiles respond to player movement and interaction.
///         Categories typically include:
///         <list type="bullet">
///             <item>"movement" - Movement-related behaviors (ice, ledges, jumps)</item>
///             <item>"terrain" - Terrain effects (tall grass, water)</item>
///             <item>"special" - Special interactions (warps, signs, doors)</item>
///             <item>"custom" - User-defined/modded behaviors</item>
///         </list>
///     </para>
/// </remarks>
[DebuggerDisplay("{Value}")]
public sealed record GameTileBehaviorId : EntityId
{
    private const string TypeName = "tile_behavior";
    private const string DefaultCategory = "movement";

    /// <summary>
    ///     Initializes a new GameTileBehaviorId from a full ID string.
    /// </summary>
    /// <param name="value">The full ID string (e.g., "base:tile_behavior:movement/ice")</param>
    public GameTileBehaviorId(string value) : base(value)
    {
        if (EntityType != TypeName)
        {
            throw new ArgumentException(
                $"Expected entity type '{TypeName}', got '{EntityType}'",
                nameof(value));
        }
    }

    /// <summary>
    ///     Initializes a new GameTileBehaviorId from components.
    /// </summary>
    /// <param name="category">The tile behavior category (e.g., "movement", "terrain", "special")</param>
    /// <param name="name">The tile behavior name (e.g., "ice", "tall_grass")</param>
    /// <param name="ns">Optional namespace (defaults to "base")</param>
    /// <param name="subcategory">Optional subcategory</param>
    public GameTileBehaviorId(string category, string name, string? ns = null, string? subcategory = null)
        : base(TypeName, category, name, ns, subcategory)
    {
    }

    /// <summary>
    ///     Creates a GameTileBehaviorId from just a name, using defaults.
    /// </summary>
    /// <param name="behaviorName">The tile behavior name</param>
    /// <param name="category">Optional category (defaults to "movement")</param>
    /// <returns>A new GameTileBehaviorId</returns>
    public static GameTileBehaviorId Create(string behaviorName, string? category = null)
    {
        return new GameTileBehaviorId(category ?? DefaultCategory, behaviorName);
    }

    /// <summary>
    ///     Creates a movement tile behavior ID.
    /// </summary>
    /// <param name="name">The behavior name (e.g., "ice", "jump_south")</param>
    /// <returns>A new GameTileBehaviorId with "movement" category</returns>
    public static GameTileBehaviorId CreateMovement(string name)
    {
        return new GameTileBehaviorId("movement", name);
    }

    /// <summary>
    ///     Creates a terrain tile behavior ID.
    /// </summary>
    /// <param name="name">The behavior name (e.g., "tall_grass", "water")</param>
    /// <returns>A new GameTileBehaviorId with "terrain" category</returns>
    public static GameTileBehaviorId CreateTerrain(string name)
    {
        return new GameTileBehaviorId("terrain", name);
    }

    /// <summary>
    ///     Creates a special tile behavior ID.
    /// </summary>
    /// <param name="name">The behavior name (e.g., "warp", "door")</param>
    /// <returns>A new GameTileBehaviorId with "special" category</returns>
    public static GameTileBehaviorId CreateSpecial(string name)
    {
        return new GameTileBehaviorId("special", name);
    }

    /// <summary>
    ///     Tries to create a GameTileBehaviorId from a string, returning null if invalid.
    ///     Only accepts the full format: base:tile_behavior:{category}/{name}
    /// </summary>
    public static GameTileBehaviorId? TryCreate(string? value)
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
            return new GameTileBehaviorId(value);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Explicit conversion from string. Use TryCreate() for safe parsing.
    /// </summary>
    public static explicit operator GameTileBehaviorId(string value)
    {
        return new GameTileBehaviorId(value);
    }
}
