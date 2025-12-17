using Arch.Core;

namespace MonoBallFramework.Game.Ecs.Components.Relationships;

/// <summary>
///     Safe wrapper for entity references with validation and null-reference semantics.
/// </summary>
/// <remarks>
///     <para>
///         EntityRef provides a convenient wrapper around Entity that adds null-reference
///         semantics and validation helpers. Arch's Entity struct already includes generation
///         tracking internally, so IsValid() leverages Arch's built-in IsAlive() check to
///         detect stale references when entities are destroyed and recycled.
///     </para>
///     <para>
///         <b>Usage Example:</b>
///         <code>
/// // Create a safe entity reference
/// var entityRef = new EntityRef(targetEntity);
/// 
/// // Later, validate before use
/// if (entityRef.IsValid(world)) {
///     var entity = entityRef.Value;
///     // Safe to use entity
/// } else {
///     // Entity was destroyed or recycled
/// }
/// 
/// // Optional null-reference pattern
/// var nullRef = EntityRef.Null;
/// if (!nullRef.IsNull) { /* ... */ }
/// </code>
///     </para>
///     <para>
///         <b>Note:</b> This wrapper is primarily useful for optional entity references
///         and scenarios where null semantics are clearer than Entity.Null checks.
///         For most cases, direct Entity references with IsAlive checks are sufficient.
///     </para>
/// </remarks>
public struct EntityRef
{
    /// <summary>
    ///     Creates a new entity reference.
    /// </summary>
    /// <param name="entity">The entity to reference.</param>
    /// <remarks>
    ///     Generation tracking is handled internally by Arch's Entity struct.
    ///     Use <see cref="IsValid" /> to check if the entity is still alive.
    /// </remarks>
    public EntityRef(Entity entity)
    {
        Value = entity;
    }

    /// <summary>
    ///     Gets the underlying entity value.
    /// </summary>
    /// <remarks>
    ///     <b>Warning:</b> Always call <see cref="IsValid" /> before using this value
    ///     to ensure the entity is still alive and the reference hasn't been recycled.
    /// </remarks>
    public Entity Value { get; private set; }

    /// <summary>
    ///     Validates that the referenced entity is still alive.
    /// </summary>
    /// <param name="world">The world containing the entity.</param>
    /// <returns>True if the entity is alive and the reference is valid.</returns>
    /// <remarks>
    ///     This delegates to Arch's IsAlive() which performs generation validation internally.
    ///     A false result indicates the entity was destroyed or the ID was recycled.
    /// </remarks>
    public readonly bool IsValid(World world)
    {
        return world.IsAlive(Value);
    }

    /// <summary>
    ///     Attempts to get the entity if the reference is valid.
    /// </summary>
    /// <param name="world">The world containing the entity.</param>
    /// <param name="entity">The valid entity, if the reference is valid.</param>
    /// <returns>True if the reference is valid and entity is populated.</returns>
    public readonly bool TryGetEntity(World world, out Entity entity)
    {
        if (IsValid(world))
        {
            entity = Value;
            return true;
        }

        entity = default;
        return false;
    }

    /// <summary>
    ///     Creates an invalid/null entity reference.
    /// </summary>
    public static EntityRef Null => new() { Value = Entity.Null };

    /// <summary>
    ///     Checks if this is a null reference.
    /// </summary>
    public readonly bool IsNull => Value == Entity.Null;
}
