using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.Ecs.Components.GameState;

/// <summary>
///     Component storing boolean game flags.
///     Flags track game events like story progression, item collection, NPC visibility, etc.
/// </summary>
/// <remarks>
///     Flag IDs follow the unified format: base:flag:{category}/{name}
///     Examples:
///     - base:flag:visibility/littleroot_town_fat_man
///     - base:flag:item/route_102_potion
///     - base:flag:hidden_item/abandoned_ship_rm_1_key
/// </remarks>
public struct GameFlags
{
    /// <summary>
    ///     Dictionary storing flag ID to boolean value mappings.
    /// </summary>
    public Dictionary<string, bool> Flags { get; set; }

    /// <summary>
    ///     Creates a new GameFlags component with an empty flag dictionary.
    /// </summary>
    public GameFlags()
    {
        Flags = new Dictionary<string, bool>();
    }

    /// <summary>
    ///     Gets the value of a flag.
    /// </summary>
    /// <param name="flagId">The flag identifier.</param>
    /// <returns>True if the flag is set, false otherwise.</returns>
    public readonly bool GetFlag(GameFlagId flagId)
    {
        // GameFlagId has implicit conversion to string
        return Flags.TryGetValue(flagId, out bool value) && value;
    }

    /// <summary>
    ///     Sets the value of a flag.
    /// </summary>
    /// <param name="flagId">The flag identifier.</param>
    /// <param name="value">The value to set.</param>
    public void SetFlag(GameFlagId flagId, bool value)
    {
        // GameFlagId has implicit conversion to string
        Flags[flagId] = value;
    }

    /// <summary>
    ///     Checks if a flag exists (has been set at least once).
    /// </summary>
    /// <param name="flagId">The flag identifier.</param>
    /// <returns>True if the flag exists.</returns>
    public readonly bool FlagExists(GameFlagId flagId)
    {
        // GameFlagId has implicit conversion to string
        return Flags.ContainsKey(flagId);
    }

    /// <summary>
    ///     Gets all flag IDs that are currently set to true.
    /// </summary>
    /// <returns>Enumerable of active flag IDs.</returns>
    public readonly IEnumerable<string> GetActiveFlags()
    {
        return Flags.Where(kvp => kvp.Value).Select(kvp => kvp.Key);
    }
}
