using System.Diagnostics;

namespace MonoBallFramework.Game.Engine.Core.Types;

/// <summary>
///     Strongly-typed identifier for behavior definitions.
///     Format: base:behavior:{category}/{name}
///     Examples:
///     - base:behavior:npc/patrol
///     - base:behavior:npc/stationary
///     - base:behavior:npc/trainer_approach
///     - base:behavior:npc/wander
///     - base:behavior:tile/ice_slide
///     - base:behavior:tile/jump_south
///     - base:behavior:tile/warp
/// </summary>
/// <remarks>
///     <para>
///         Behaviors define how NPCs and tiles act and respond to events.
///         Categories typically include:
///         <list type="bullet">
///             <item>"npc" - NPC movement and interaction behaviors</item>
///             <item>"tile" - Tile-triggered behaviors (warps, ice, ledges)</item>
///             <item>"trainer" - Trainer-specific battle behaviors</item>
///             <item>"custom" - User-defined/modded behaviors</item>
///         </list>
///     </para>
/// </remarks>
[DebuggerDisplay("{Value}")]
public sealed record GameBehaviorId : EntityId
{
    private const string TypeName = "behavior";
    private const string DefaultCategory = "npc";

    /// <summary>
    ///     Initializes a new GameBehaviorId from a full ID string.
    /// </summary>
    /// <param name="value">The full ID string (e.g., "base:behavior:npc/patrol")</param>
    public GameBehaviorId(string value) : base(value)
    {
        if (EntityType != TypeName)
        {
            throw new ArgumentException(
                $"Expected entity type '{TypeName}', got '{EntityType}'",
                nameof(value));
        }
    }

    /// <summary>
    ///     Initializes a new GameBehaviorId from components.
    /// </summary>
    /// <param name="category">The behavior category (e.g., "npc", "tile", "trainer")</param>
    /// <param name="name">The behavior name (e.g., "patrol", "ice_slide")</param>
    /// <param name="ns">Optional namespace (defaults to "base")</param>
    /// <param name="subcategory">Optional subcategory</param>
    public GameBehaviorId(string category, string name, string? ns = null, string? subcategory = null)
        : base(TypeName, category, name, ns, subcategory)
    {
    }

    /// <summary>
    ///     Whether this is an NPC behavior.
    /// </summary>
    public bool IsNpcBehavior => Category == "npc" || Category == "trainer";

    /// <summary>
    ///     Whether this is a tile behavior.
    /// </summary>
    public bool IsTileBehavior => Category == "tile";

    /// <summary>
    ///     Creates a GameBehaviorId from just a name, using defaults.
    /// </summary>
    /// <param name="behaviorName">The behavior name</param>
    /// <param name="category">Optional category (defaults to "npc")</param>
    /// <returns>A new GameBehaviorId</returns>
    public static GameBehaviorId Create(string behaviorName, string? category = null)
    {
        return new GameBehaviorId(category ?? DefaultCategory, behaviorName);
    }

    /// <summary>
    ///     Creates an NPC behavior ID.
    /// </summary>
    /// <param name="name">The behavior name (e.g., "patrol", "stationary")</param>
    /// <returns>A new GameBehaviorId with "npc" category</returns>
    public static GameBehaviorId CreateNpcBehavior(string name)
    {
        return new GameBehaviorId("npc", name);
    }

    /// <summary>
    ///     Creates a tile behavior ID.
    /// </summary>
    /// <param name="name">The behavior name (e.g., "ice_slide", "jump_south")</param>
    /// <returns>A new GameBehaviorId with "tile" category</returns>
    public static GameBehaviorId CreateTileBehavior(string name)
    {
        return new GameBehaviorId("tile", name);
    }

    /// <summary>
    ///     Creates a trainer behavior ID.
    /// </summary>
    /// <param name="name">The behavior name (e.g., "approach_player", "sight_battle")</param>
    /// <returns>A new GameBehaviorId with "trainer" category</returns>
    public static GameBehaviorId CreateTrainerBehavior(string name)
    {
        return new GameBehaviorId("trainer", name);
    }

    /// <summary>
    ///     Tries to create a GameBehaviorId from a string, returning null if invalid.
    ///     Only accepts the full format: base:behavior:{category}/{name}
    /// </summary>
    public static GameBehaviorId? TryCreate(string? value)
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
            return new GameBehaviorId(value);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Explicit conversion from string. Use TryCreate() for safe parsing.
    /// </summary>
    public static explicit operator GameBehaviorId(string value)
    {
        return new GameBehaviorId(value);
    }
}
