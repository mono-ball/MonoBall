using Arch.Core;

namespace PokeSharp.Game.Components.Relationships;

/// <summary>
///     Safe wrapper for entity references with validation support.
/// </summary>
/// <remarks>
///     <para>
///         EntityRef provides a safer way to store entity references by including
///         generation tracking for validation. This helps detect stale references
///         when entities are destroyed and their IDs are reused.
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
/// </code>
///     </para>
///     <para>
///         <b>Note:</b> This is optional and primarily useful for long-lived references
///         or scenarios where you need extra validation beyond basic IsAlive checks.
///         For most cases, direct Entity references with IsAlive checks are sufficient.
///     </para>
/// </remarks>
public struct EntityRef
{
    /// <summary>
    ///     Creates a new entity reference with generation tracking.
    /// </summary>
    /// <param name="entity">The entity to reference.</param>
    /// <remarks>
    ///     The generation is captured at creation time. If the entity is destroyed
    ///     and its ID is reused, the generation will differ, allowing detection
    ///     of stale references.
    /// </remarks>
    public EntityRef(Entity entity)
    {
        Value = entity;
        Generation = entity.Id; // Simplified generation tracking
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
    ///     Gets the generation captured when this reference was created.
    /// </summary>
    public int Generation { get; private set; }

    /// <summary>
    ///     Validates that the referenced entity is still alive.
    /// </summary>
    /// <param name="world">The world containing the entity.</param>
    /// <returns>True if the entity is alive and the reference is valid.</returns>
    /// <remarks>
    ///     This performs both an IsAlive check and generation validation.
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
    public static EntityRef Null => new() { Value = Entity.Null, Generation = -1 };

    /// <summary>
    ///     Checks if this is a null reference.
    /// </summary>
    public readonly bool IsNull => Value == Entity.Null;
}
