using Arch.Core;

namespace PokeSharp.Game.Components.Relationships;

/// <summary>
///     Component indicating this entity is owned by another entity.
///     Attached to the owned entity to reference its owner.
/// </summary>
/// <remarks>
///     <para>
///         This component establishes the reverse relationship of <see cref="Owner" />,
///         allowing owned entities to reference their owner. Common uses include:
///         - Pokémon referencing their trainer
///         - Item referencing its owner
///         - Resource referencing its possessor
///     </para>
///     <para>
///         <b>Usage Example:</b>
///         <code>
/// // Query for all entities owned by a trainer
/// var query = new QueryDescription().WithAll&lt;Owned&gt;();
/// world.Query(in query, (Entity entity, ref Owned owned) => {
///     if (owned.OwnerEntity == trainerEntity) {
///         // Process owned entity
///         var timeSinceAcquired = DateTime.UtcNow - owned.AcquiredAt;
///     }
/// });
///
/// // Or use extension method
/// var owner = pokemon.GetOwner(world);
/// if (owner.HasValue) {
///     // Process owner
/// }
/// </code>
///     </para>
///     <para>
///         <b>Important:</b> Always validate the owner entity is still alive.
///         The RelationshipSystem automatically cleans up components with invalid owners.
///     </para>
/// </remarks>
public struct Owned
{
    /// <summary>
    ///     The owner entity reference.
    /// </summary>
    /// <remarks>
    ///     Should be validated using <c>world.IsAlive(OwnerEntity)</c> before use.
    ///     Invalid references are automatically cleaned up by RelationshipSystem.
    /// </remarks>
    public Entity OwnerEntity;

    /// <summary>
    ///     UTC timestamp when ownership was acquired.
    /// </summary>
    /// <remarks>
    ///     Useful for tracking:
    ///     - Duration of ownership
    ///     - Ordering by acquisition time
    ///     - Implementing time-based ownership logic
    ///     - Debugging relationship issues
    /// </remarks>
    public DateTime AcquiredAt;

    /// <summary>
    ///     Whether this ownership relationship is currently valid.
    ///     Set to false instead of removing the component to avoid expensive ECS structural changes.
    /// </summary>
    /// <remarks>
    ///     When false, the relationship should be considered broken and ignored by systems.
    ///     RelationshipSystem sets this to false when the owner entity is destroyed.
    /// </remarks>
    public bool IsValid;
}
