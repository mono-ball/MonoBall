namespace MonoBallFramework.Game.Engine.Systems.Queries;

/// <summary>
///     Centralized query descriptions for common entity query patterns.
/// </summary>
/// <remarks>
///     With Arch.Relationships, most relationship queries are handled through
///     the relationship extension methods (HasRelationship, GetRelationships, etc.)
///     rather than through component queries.
/// </remarks>
public static class RelationshipQueries
{
    // Note: With Arch.Relationships, we no longer need component-based queries
    // for relationships. Instead, use extension methods like:
    // - entity.HasRelationship<T>()
    // - entity.GetRelationships<T>()
    // - entity.AddRelationship<T>(target)
    // - entity.RemoveRelationship<T>(target)
    //
    // To find all entities with a specific relationship type, use:
    // world.Query(new QueryDescription(), entity => {
    //     if (entity.HasRelationship<ParentOf>()) {
    //         // Process entity with relationships
    //     }
    // });
}
