using Arch.Core;
using PokeSharp.Core.Components.Relationships;

namespace PokeSharp.Core.Queries;

/// <summary>
/// Centralized query descriptions for relationship components.
/// Provides optimized, reusable queries for common relationship patterns.
/// </summary>
/// <remarks>
/// These queries are designed for performance and can be cached/reused
/// across multiple system updates to minimize query compilation overhead.
/// </remarks>
public static class RelationshipQueries
{
    #region Parent-Child Queries

    /// <summary>
    /// Query for all entities with a Parent component (child entities).
    /// </summary>
    public static QueryDescription AllChildren => new QueryDescription()
        .WithAll<Parent>();

    /// <summary>
    /// Query for all entities with a Children component (parent entities).
    /// </summary>
    public static QueryDescription AllParents => new QueryDescription()
        .WithAll<Children>();

    /// <summary>
    /// Query for entities that are both parents and children in hierarchy.
    /// </summary>
    /// <remarks>
    /// Useful for finding intermediate nodes in a hierarchical structure,
    /// such as a trainer who is part of a team.
    /// </remarks>
    public static QueryDescription HierarchyNodes => new QueryDescription()
        .WithAll<Parent, Children>();

    /// <summary>
    /// Query for root entities (parents with no parent themselves).
    /// </summary>
    /// <remarks>
    /// Identifies top-level entities in a hierarchy, such as the main player
    /// or team leaders.
    /// </remarks>
    public static QueryDescription RootParents => new QueryDescription()
        .WithAll<Children>()
        .WithNone<Parent>();

    /// <summary>
    /// Query for leaf entities (children with no children of their own).
    /// </summary>
    /// <remarks>
    /// Identifies bottom-level entities in a hierarchy, such as individual
    /// Pokémon or items that don't contain other entities.
    /// </remarks>
    public static QueryDescription LeafChildren => new QueryDescription()
        .WithAll<Parent>()
        .WithNone<Children>();

    #endregion

    #region Owner-Owned Queries

    /// <summary>
    /// Query for all entities with an Owner component (entities that own something).
    /// </summary>
    public static QueryDescription AllOwners => new QueryDescription()
        .WithAll<Owner>();

    /// <summary>
    /// Query for all entities with an Owned component (owned entities).
    /// </summary>
    public static QueryDescription AllOwned => new QueryDescription()
        .WithAll<Owned>();

    /// <summary>
    /// Query for entities that both own and are owned.
    /// </summary>
    /// <remarks>
    /// Useful for finding intermediate ownership chains, such as a Pokémon
    /// that is owned by a trainer but also owns items.
    /// </remarks>
    public static QueryDescription OwnershipChain => new QueryDescription()
        .WithAll<Owner, Owned>();

    /// <summary>
    /// Query for entities that own something but are not owned themselves.
    /// </summary>
    /// <remarks>
    /// Identifies top-level owners in ownership chains, such as the player
    /// or autonomous entities.
    /// </remarks>
    public static QueryDescription IndependentOwners => new QueryDescription()
        .WithAll<Owner>()
        .WithNone<Owned>();

    /// <summary>
    /// Query for owned entities that don't own anything else.
    /// </summary>
    /// <remarks>
    /// Identifies leaf nodes in ownership chains, such as basic items or
    /// resources that cannot own other entities.
    /// </remarks>
    public static QueryDescription PureOwned => new QueryDescription()
        .WithAll<Owned>()
        .WithNone<Owner>();

    #endregion

    #region Combined Relationship Queries

    /// <summary>
    /// Query for entities with any relationship component.
    /// </summary>
    /// <remarks>
    /// Useful for finding all entities involved in any kind of relationship
    /// when you need to process relationships generically.
    /// </remarks>
    public static QueryDescription AnyRelationship => new QueryDescription()
        .WithAny<Parent, Children, Owner, Owned>();

    /// <summary>
    /// Query for entities with both parent-child and ownership relationships.
    /// </summary>
    /// <remarks>
    /// Identifies entities that are deeply integrated into relationship systems,
    /// such as a Pokémon that is both part of a trainer's team (parent-child)
    /// and owns items (ownership).
    /// </remarks>
    public static QueryDescription FullyRelated => new QueryDescription()
        .WithAny<Parent, Children>()
        .WithAny<Owner, Owned>();

    /// <summary>
    /// Query for orphaned entities (have relationship components but invalid references).
    /// </summary>
    /// <remarks>
    /// Note: This query still requires validation of the actual entity references
    /// to determine if they're truly orphaned. Use in conjunction with
    /// IsAlive checks on the referenced entities.
    /// </remarks>
    public static QueryDescription PotentialOrphans => new QueryDescription()
        .WithAny<Parent, Owned>();

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a query for entities owned by a specific owner.
    /// </summary>
    /// <remarks>
    /// Note: This returns all Owned entities. You must filter by OwnerEntity
    /// in your query callback to find entities owned by a specific owner.
    /// <para>
    /// <b>Example:</b>
    /// <code>
    /// var query = RelationshipQueries.OwnedByEntity();
    /// world.Query(in query, (Entity entity, ref Owned owned) => {
    ///     if (owned.OwnerEntity == targetOwner) {
    ///         // Process owned entity
    ///     }
    /// });
    /// </code>
    /// </para>
    /// </remarks>
    public static QueryDescription OwnedByEntity() => AllOwned;

    /// <summary>
    /// Creates a query for children of a specific parent.
    /// </summary>
    /// <remarks>
    /// Note: This returns all child entities. You must filter by Parent.Value
    /// in your query callback to find children of a specific parent.
    /// <para>
    /// <b>Example:</b>
    /// <code>
    /// var query = RelationshipQueries.ChildrenOfEntity();
    /// world.Query(in query, (Entity entity, ref Parent parent) => {
    ///     if (parent.Value == targetParent) {
    ///         // Process child entity
    ///     }
    /// });
    /// </code>
    /// </para>
    /// </remarks>
    public static QueryDescription ChildrenOfEntity() => AllChildren;

    #endregion
}
