namespace PokeSharp.Game.Components.Relationships;

/// <summary>
///     Relationship tag indicating a parent-child relationship.
///     Used with Arch.Relationships to establish hierarchical entity relationships.
/// </summary>
/// <remarks>
///     <para>
///         This tag is used with Arch.Relationships API:
///         <code>
/// // Create parent-child relationship
/// parent.AddRelationship&lt;ParentOf&gt;(child);
///
/// // Iterate all children
/// ref var children = ref parent.GetRelationships&lt;ParentOf&gt;();
/// foreach(var child in children) {
///     // Process child
/// }
///
/// // Check if relationship exists
/// if (parent.HasRelationship&lt;ParentOf&gt;(child)) {
///     // Relationship exists
/// }
/// </code>
///     </para>
///     <para>
///         Common uses include:
///         - Pokémon belonging to a trainer
///         - Items in a container
///         - Sub-entities in a composite structure
///     </para>
/// </remarks>
public struct ParentOf
{
    /// <summary>
    ///     UTC timestamp when this relationship was established.
    /// </summary>
    public DateTime EstablishedAt;

    /// <summary>
    ///     Optional metadata about the relationship.
    /// </summary>
    public string? Metadata;

    public ParentOf()
    {
        EstablishedAt = DateTime.UtcNow;
        Metadata = null;
    }

    public ParentOf(DateTime establishedAt, string? metadata = null)
    {
        EstablishedAt = establishedAt;
        Metadata = metadata;
    }
}

/// <summary>
///     Defines the type of ownership relationship between entities.
/// </summary>
public enum OwnershipType
{
    /// <summary>Permanent ownership that persists indefinitely.</summary>
    Permanent,

    /// <summary>Temporary ownership with expected transfer or expiration.</summary>
    Temporary,

    /// <summary>Conditional ownership based on specific criteria.</summary>
    Conditional,

    /// <summary>Shared ownership among multiple entities.</summary>
    Shared,
}

/// <summary>
///     Relationship tag indicating ownership of another entity.
///     Used with Arch.Relationships for ownership tracking.
/// </summary>
/// <remarks>
///     <para>
///         This tag is used with Arch.Relationships API:
///         <code>
/// // Create ownership relationship
/// owner.AddRelationship&lt;OwnerOf&gt;(owned, new OwnerOf(OwnershipType.Permanent));
///
/// // Iterate all owned entities
/// ref var owned = ref owner.GetRelationships&lt;OwnerOf&gt;();
/// foreach(var entity in owned) {
///     // Process owned entity
/// }
/// </code>
///     </para>
///     <para>
///         Common uses include:
///         - Trainer owning Pokémon
///         - Player owning items
///         - Entity possessing resources
///     </para>
/// </remarks>
public struct OwnerOf
{
    /// <summary>The type of ownership relationship.</summary>
    public OwnershipType Type;

    /// <summary>UTC timestamp when ownership was acquired.</summary>
    public DateTime AcquiredAt;

    /// <summary>Optional metadata about the ownership.</summary>
    public string? Metadata;

    public OwnerOf()
    {
        Type = OwnershipType.Permanent;
        AcquiredAt = DateTime.UtcNow;
        Metadata = null;
    }

    public OwnerOf(OwnershipType type, string? metadata = null)
    {
        Type = type;
        AcquiredAt = DateTime.UtcNow;
        Metadata = metadata;
    }
}
