namespace PokeSharp.Game.Engine.Systems.Pooling;

/// <summary>
///     Centralized constants for entity pool names.
///     Use these instead of string literals to avoid typos and enable compile-time safety.
/// </summary>
/// <remarks>
///     <para>
///         Using constants instead of string literals provides several benefits:
///     </para>
///     <list type="bullet">
///         <item>Compile-time checking - typos caught immediately</item>
///         <item>IntelliSense support - easy to discover available pools</item>
///         <item>Refactoring support - rename all uses at once</item>
///         <item>Documentation - single place to document pool purposes</item>
///     </list>
///     <example>
///         <code>
/// // ❌ BAD: String literal (typo-prone)
/// Entity entity = poolManager.Acquire("playre");  // TYPO! Runtime error
///
/// // ✅ GOOD: Type-safe constant
/// Entity entity = poolManager.Acquire(PoolNames.Player);  // Compile-time safe
/// </code>
///     </example>
/// </remarks>
public static class PoolNames
{
    /// <summary>
    ///     Default pool for miscellaneous entities.
    ///     Used when no specific pool is configured.
    /// </summary>
    public const string Default = "default";

    /// <summary>
    ///     Pool for player entities.
    ///     Typically contains 1-4 entities (single player or co-op).
    /// </summary>
    /// <remarks>
    ///     Configuration: Small pool size, high importance.
    ///     Default: 5 initial, 20 max
    /// </remarks>
    public const string Player = "player";

    /// <summary>
    ///     Pool for NPC (Non-Player Character) entities.
    ///     Contains NPCs, trainers, gym leaders, shopkeepers, etc.
    /// </summary>
    /// <remarks>
    ///     Configuration: Medium pool size, moderate reuse.
    ///     Default: 50 initial, 200 max
    /// </remarks>
    public const string Npc = "npc";

    /// <summary>
    ///     Pool for tile entities.
    ///     Contains map tiles, ground, grass, water, walls, etc.
    /// </summary>
    /// <remarks>
    ///     Configuration: Large pool size, high reuse rate.
    ///     Default: 1000 initial, 5000 max
    ///     Tiles are frequently created/destroyed during map transitions.
    /// </remarks>
    public const string Tile = "tile";

    /// <summary>
    ///     Pool for projectile entities (attacks, thrown items, etc.).
    /// </summary>
    /// <remarks>
    ///     Not yet implemented in default configuration.
    ///     Recommended: 50 initial, 200 max
    /// </remarks>
    public const string Projectile = "projectile";

    /// <summary>
    ///     Pool for particle effect entities (animations, visual effects).
    /// </summary>
    /// <remarks>
    ///     Not yet implemented in default configuration.
    ///     Recommended: 100 initial, 500 max
    /// </remarks>
    public const string Particle = "particle";

    /// <summary>
    ///     Pool for UI entities (menus, dialogs, buttons).
    /// </summary>
    /// <remarks>
    ///     Not yet implemented in default configuration.
    ///     Recommended: 20 initial, 100 max
    /// </remarks>
    public const string UI = "ui";

    /// <summary>
    ///     Pool for item entities (items on ground, in inventory).
    /// </summary>
    /// <remarks>
    ///     Not yet implemented in default configuration.
    ///     Recommended: 50 initial, 200 max
    /// </remarks>
    public const string Item = "item";

    /// <summary>
    ///     Pool for Pokémon entities (party members, wild Pokémon, opponent Pokémon).
    /// </summary>
    /// <remarks>
    ///     Not yet implemented in default configuration.
    ///     Recommended: 20 initial, 100 max
    /// </remarks>
    public const string Pokemon = "pokemon";
}
