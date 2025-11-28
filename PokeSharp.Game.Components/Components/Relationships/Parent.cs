using Arch.Core;

namespace PokeSharp.Game.Components.Relationships;

/// <summary>
///     Component representing a parent-child relationship.
///     Attached to the child entity to reference its parent.
/// </summary>
/// <remarks>
///     <para>
///         This component establishes a hierarchical relationship where one entity
///         is considered the child of another parent entity. Common uses include:
///         - Pokémon belonging to a trainer
///         - Items in a container
///         - Sub-entities in a composite structure
///     </para>
///     <para>
///         <b>Usage Example:</b>
///         <code>
/// // Create parent-child relationship
/// var trainer = world.Create();
/// var pokemon = world.Create();
///
/// pokemon.Add(new Parent {
///     Value = trainer,
///     EstablishedAt = DateTime.UtcNow
/// });
///
/// // Or use extension method
/// pokemon.SetParent(trainer, world);
/// </code>
///     </para>
///     <para>
///         <b>Important:</b> Always validate the parent entity is still alive before use.
///         The RelationshipSystem handles automatic cleanup of invalid references.
///     </para>
/// </remarks>
public struct Parent
{
    /// <summary>
    ///     The parent entity reference.
    /// </summary>
    /// <remarks>
    ///     This should be validated using <c>world.IsAlive(Value)</c> before use
    ///     to ensure the parent entity hasn't been destroyed.
    /// </remarks>
    public Entity Value;

    /// <summary>
    ///     UTC timestamp when this relationship was established.
    /// </summary>
    /// <remarks>
    ///     Useful for tracking relationship duration, debugging, and
    ///     implementing time-based relationship logic.
    /// </remarks>
    public DateTime EstablishedAt;

    /// <summary>
    ///     Whether this relationship is currently valid.
    ///     Set to false instead of removing the component to avoid expensive ECS structural changes.
    /// </summary>
    /// <remarks>
    ///     When false, the relationship should be considered broken and ignored by systems.
    ///     RelationshipSystem sets this to false when the parent entity is destroyed.
    /// </remarks>
    public bool IsValid;
}
