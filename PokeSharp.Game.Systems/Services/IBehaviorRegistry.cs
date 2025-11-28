using PokeSharp.Engine.Core.Types;

namespace PokeSharp.Game.Systems.Services;

/// <summary>
///     Registry for managing NPC behavior definitions.
///     Provides storage and retrieval of behavior patterns that define how NPCs act and respond.
/// </summary>
/// <remarks>
///     <para>
///         This registry serves as a central repository for behavior definitions,
///         enabling dynamic behavior lookup and management at runtime.
///     </para>
///     <para>
///         Behaviors can be registered during initialization or added dynamically
///         through scripting or configuration systems.
///     </para>
/// </remarks>
public interface IBehaviorRegistry
{
    /// <summary>
    ///     Register a behavior definition with the specified identifier.
    /// </summary>
    /// <param name="behaviorId">Unique identifier for the behavior.</param>
    /// <param name="definition">The behavior definition to register.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when behaviorId or definition is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when a behavior with the same ID is already registered.
    /// </exception>
    void RegisterBehavior(string behaviorId, BehaviorDefinition definition);

    /// <summary>
    ///     Retrieve a behavior definition by its identifier.
    /// </summary>
    /// <param name="behaviorId">The behavior identifier to look up.</param>
    /// <returns>The behavior definition, or null if not found.</returns>
    BehaviorDefinition? GetBehavior(string behaviorId);

    /// <summary>
    ///     Check if a behavior is registered.
    /// </summary>
    /// <param name="behaviorId">The behavior identifier to check.</param>
    /// <returns>True if the behavior exists, false otherwise.</returns>
    bool HasBehavior(string behaviorId);

    /// <summary>
    ///     Remove a behavior from the registry.
    /// </summary>
    /// <param name="behaviorId">The behavior identifier to remove.</param>
    /// <returns>True if the behavior was removed, false if it didn't exist.</returns>
    bool RemoveBehavior(string behaviorId);

    /// <summary>
    ///     Get all registered behavior identifiers.
    /// </summary>
    /// <returns>Collection of all registered behavior IDs.</returns>
    IEnumerable<string> GetAllBehaviorIds();
}
