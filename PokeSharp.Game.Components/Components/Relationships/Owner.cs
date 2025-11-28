using Arch.Core;

namespace PokeSharp.Game.Components.Relationships;

/// <summary>
///     Defines the type of ownership relationship between entities.
/// </summary>
public enum OwnershipType
{
    /// <summary>
    ///     Permanent ownership that persists indefinitely.
    ///     Example: A trainer's starter Pokémon.
    /// </summary>
    Permanent,

    /// <summary>
    ///     Temporary ownership with expected transfer or expiration.
    ///     Example: A borrowed Pokémon, rental item.
    /// </summary>
    Temporary,

    /// <summary>
    ///     Conditional ownership based on specific criteria.
    ///     Example: Item held only during battle, event-specific ownership.
    /// </summary>
    Conditional,

    /// <summary>
    ///     Shared ownership among multiple entities.
    ///     Example: Team resources, shared inventory.
    /// </summary>
    Shared,
}

/// <summary>
///     Component representing ownership of other entities.
///     Attached to the owner entity to track what it owns.
/// </summary>
/// <remarks>
///     <para>
///         This component establishes an ownership relationship, typically used in
///         conjunction with the <see cref="Owned" /> component on owned entities.
///         Common uses include:
///         - Trainer owning Pokémon
///         - Player owning items
///         - Entity possessing resources
///     </para>
///     <para>
///         <b>Usage Example:</b>
///         <code>
/// // Create ownership relationship
/// var trainer = world.Create();
/// var pokemon = world.Create();
///
/// trainer.Add(new Owner {
///     Value = pokemon,
///     Type = OwnershipType.Permanent
/// });
///
/// pokemon.Add(new Owned {
///     OwnerEntity = trainer,
///     AcquiredAt = DateTime.UtcNow
/// });
///
/// // Or use extension method
/// pokemon.SetOwner(trainer, world);
/// </code>
///     </para>
///     <para>
///         <b>Note:</b> For tracking multiple owned entities, use the <see cref="Children" />
///         component pattern or create multiple Owner components (if supported by your
///         component architecture).
///     </para>
/// </remarks>
public struct Owner
{
    /// <summary>
    ///     The owned entity reference.
    /// </summary>
    /// <remarks>
    ///     Should be validated using <c>world.IsAlive(Value)</c> before use.
    ///     The RelationshipSystem handles automatic cleanup of invalid references.
    /// </remarks>
    public Entity Value;

    /// <summary>
    ///     The type of ownership relationship.
    /// </summary>
    /// <remarks>
    ///     Determines the semantics and behavior of the ownership relationship.
    ///     Can be used to implement different lifecycle rules, transfer restrictions,
    ///     or special handling for different ownership types.
    /// </remarks>
    public OwnershipType Type;

    /// <summary>
    ///     Whether this ownership relationship is currently valid.
    ///     Set to false instead of removing the component to avoid expensive ECS structural changes.
    /// </summary>
    /// <remarks>
    ///     When false, the ownership should be considered broken and ignored by systems.
    ///     RelationshipSystem sets this to false when the owned entity is destroyed.
    /// </remarks>
    public bool IsValid;
}
