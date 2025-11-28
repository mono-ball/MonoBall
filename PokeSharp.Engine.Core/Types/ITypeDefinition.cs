namespace PokeSharp.Engine.Core.Types;

/// <summary>
///     Base interface for all moddable type definitions.
///     Type definitions are pure data - no behavior methods.
/// </summary>
/// <remarks>
///     All game data types (behaviors, weather, terrain, items, moves, abilities, etc.)
///     should implement this interface to be loadable from JSON and managed by TypeRegistry.
/// </remarks>
public interface ITypeDefinition
{
    /// <summary>
    ///     Unique identifier for this type (e.g., "rain", "lava", "warp_pad", "patrol").
    ///     Used as the key in TypeRegistry lookups.
    /// </summary>
    string TypeId { get; }

    /// <summary>
    ///     Display name shown to players and in editor tools.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    ///     Optional description for documentation, tooltips, and modder reference.
    /// </summary>
    string? Description { get; }
}
