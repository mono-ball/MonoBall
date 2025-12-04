using Arch.Core;
using Arch.Relationships;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Ecs.Components.Relationships;
using MonoBallFramework.Game.Engine.Core.Systems;
using MonoBallFramework.Game.Engine.Core.Systems.Base;

namespace MonoBallFramework.Game.GameSystems;

/// <summary>
///     System responsible for validating and maintaining entity relationships using Arch.Relationships.
///     Runs late in the update cycle to clean up broken references.
/// </summary>
/// <remarks>
///     <para>
///         With Arch.Relationships, most relationship management is automatic.
///         This system primarily validates that related entities are still alive
///         and removes relationships to destroyed entities.
///     </para>
///     <para>
///         Runs with priority 950 (late update) to allow other systems to complete
///         their work before relationship validation occurs.
///     </para>
/// </remarks>
public class RelationshipSystem : SystemBase, IUpdateSystem
{
    private readonly ILogger<RelationshipSystem> _logger;
    private int _brokenRelationshipsFixed;

    public RelationshipSystem(ILogger<RelationshipSystem> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Gets the priority of this system (950 - late update).
    /// </summary>
    public override int Priority => 950;

    public override void Initialize(World world)
    {
        base.Initialize(world);
        _logger.LogInformation(
            "RelationshipSystem initialized with Arch.Relationships (Priority: {Priority})",
            Priority
        );
    }

    public override void Update(World world, float deltaTime)
    {
        // Reset statistics
        _brokenRelationshipsFixed = 0;

        // Validate ParentOf relationships (now used for all hierarchies including maps)
        ValidateRelationships<ParentOf>(world);

        // Validate OwnerOf relationships
        ValidateRelationships<OwnerOf>(world);

        // Log summary if any issues were found
        if (_brokenRelationshipsFixed > 0)
        {
            _logger.LogWarning(
                "Relationship cleanup: {Count} broken relationship(s) fixed",
                _brokenRelationshipsFixed
            );
        }
    }

    /// <summary>
    ///     Validates relationships of a specific type and removes broken references.
    /// </summary>
    private void ValidateRelationships<T>(World world)
        where T : struct
    {
        var entitiesToClean = new List<(Entity parent, Entity child)>();

        // Query all entities and check if they have relationships of this type
        world.Query(
            new QueryDescription(),
            entity =>
            {
                if (!entity.HasRelationship<T>())
                {
                    return;
                }

                // Get the relationships and check if related entities are still alive
                ref Relationship<T> relationships = ref entity.GetRelationships<T>();
                foreach (KeyValuePair<Entity, T> kvp in relationships)
                {
                    Entity relatedEntity = kvp.Key;
                    if (!world.IsAlive(relatedEntity))
                    {
                        entitiesToClean.Add((entity, relatedEntity));
                    }
                }
            }
        );

        // Remove broken relationships
        foreach ((Entity parent, Entity child) in entitiesToClean)
        {
            if (world.IsAlive(parent))
            {
                parent.RemoveRelationship<T>(child);
                _brokenRelationshipsFixed++;
                _logger.LogDebug(
                    "Removed broken {RelationType} relationship from {Parent} to {Child}",
                    typeof(T).Name,
                    parent,
                    child
                );
            }
        }
    }

    /// <summary>
    ///     Gets current relationship validation statistics.
    /// </summary>
    public int GetBrokenRelationshipsFixed()
    {
        return _brokenRelationshipsFixed;
    }
}
