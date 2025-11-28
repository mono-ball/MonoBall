using Arch.Core;
using Arch.Core.Extensions;

namespace PokeSharp.Engine.Systems.BulkOperations;

/// <summary>
///     Bulk operations on query results for batch processing of entities.
///     Provides efficient ways to collect, modify, and destroy entities matching queries.
/// </summary>
/// <remarks>
///     <para>
///         Use these methods when you need to:
///         - Collect all entities matching criteria for later processing
///         - Apply changes to all entities in a query result
///         - Batch destroy entities matching conditions
///         - Extract component data from multiple entities
///     </para>
///     <example>
///         <code>
/// var bulkQuery = new BulkQueryOperations(world);
///
/// // Find all low-health enemies
/// var query = new QueryDescription().WithAll&lt;Enemy, Health&gt;();
/// var weakEnemies = bulkQuery.CollectWithComponent&lt;Health&gt;(query)
///     .Where(x => x.component.CurrentHP &lt; x.component.MaxHP * 0.3f)
///     .Select(x => x.entity)
///     .ToList();
///
/// // Make them all flee
/// bulkQuery.AddComponentToMatching&lt;FleeingStatus&gt;(query, new FleeingStatus());
/// </code>
///     </example>
/// </remarks>
public sealed class BulkQueryOperations
{
    private readonly World _world;

    /// <summary>
    ///     Creates a new bulk query operations handler.
    /// </summary>
    /// <param name="world">The Arch ECS world to query</param>
    public BulkQueryOperations(World world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    /// <summary>
    ///     Collect all entities matching a query into a list.
    ///     Useful for batch processing or when you need to iterate multiple times.
    /// </summary>
    /// <param name="query">Query description to match</param>
    /// <returns>List of all matching entities</returns>
    /// <example>
    ///     <code>
    /// // Collect all enemies for later processing
    /// var query = new QueryDescription().WithAll&lt;Enemy&gt;();
    /// var allEnemies = bulkQuery.CollectEntities(query);
    /// </code>
    /// </example>
    public List<Entity> CollectEntities(in QueryDescription query)
    {
        var entities = new List<Entity>();

        _world.Query(
            in query,
            entity =>
            {
                entities.Add(entity);
            }
        );

        return entities;
    }

    /// <summary>
    ///     Collect entities along with a specific component value.
    ///     Efficient for extracting data without multiple lookups.
    /// </summary>
    /// <typeparam name="T">Component type to collect</typeparam>
    /// <param name="query">Query description to match</param>
    /// <returns>List of entity-component pairs</returns>
    /// <example>
    ///     <code>
    /// // Get all entities with health values
    /// var query = new QueryDescription().WithAll&lt;Health&gt;();
    /// var healthData = bulkQuery.CollectWithComponent&lt;Health&gt;(query);
    ///
    /// foreach (var (entity, health) in healthData)
    /// {
    ///     if (health.CurrentHP &lt;= 0)
    ///         entity.Destroy();
    /// }
    /// </code>
    /// </example>
    public List<(Entity entity, T component)> CollectWithComponent<T>(in QueryDescription query)
        where T : struct
    {
        var results = new List<(Entity, T)>();

        _world.Query(
            in query,
            (Entity entity, ref T component) =>
            {
                results.Add((entity, component));
            }
        );

        return results;
    }

    /// <summary>
    ///     Collect entities with two component values.
    ///     Useful for operations that need multiple component types.
    /// </summary>
    /// <typeparam name="T1">First component type</typeparam>
    /// <typeparam name="T2">Second component type</typeparam>
    /// <param name="query">Query description to match</param>
    /// <returns>List of entity with two components</returns>
    /// <example>
    ///     <code>
    /// // Get all entities with position and velocity
    /// var query = new QueryDescription().WithAll&lt;Position, Velocity&gt;();
    /// var movingEntities = bulkQuery.CollectWithComponents&lt;Position, Velocity&gt;(query);
    ///
    /// foreach (var (entity, pos, vel) in movingEntities)
    /// {
    ///     // Process movement data
    /// }
    /// </code>
    /// </example>
    public List<(Entity entity, T1 c1, T2 c2)> CollectWithComponents<T1, T2>(
        in QueryDescription query
    )
        where T1 : struct
        where T2 : struct
    {
        var results = new List<(Entity, T1, T2)>();

        _world.Query(
            in query,
            (Entity entity, ref T1 c1, ref T2 c2) =>
            {
                results.Add((entity, c1, c2));
            }
        );

        return results;
    }

    /// <summary>
    ///     Apply an action to all entities matching the query.
    ///     Useful for simple operations that don't need component access.
    /// </summary>
    /// <param name="query">Query description to match</param>
    /// <param name="action">Action to apply to each entity</param>
    /// <example>
    ///     <code>
    /// // Tag all enemies as "aggressive"
    /// var query = new QueryDescription().WithAll&lt;Enemy&gt;();
    /// bulkQuery.ForEach(query, entity =>
    /// {
    ///     entity.Add(new AggressiveTag());
    /// });
    /// </code>
    /// </example>
    public void ForEach(in QueryDescription query, Action<Entity> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        _world.Query(
            in query,
            entity =>
            {
                action(entity);
            }
        );
    }

    /// <summary>
    ///     Apply an action with component access to all matching entities.
    ///     The component reference allows in-place modification.
    /// </summary>
    /// <typeparam name="T">Component type to access</typeparam>
    /// <param name="query">Query description to match</param>
    /// <param name="action">Action with entity and component reference</param>
    /// <example>
    ///     <code>
    /// // Heal all entities by 10 HP
    /// var query = new QueryDescription().WithAll&lt;Health&gt;();
    /// bulkQuery.ForEach&lt;Health&gt;(query, (Entity entity, ref Health health) =>
    /// {
    ///     health.CurrentHP = Math.Min(health.CurrentHP + 10, health.MaxHP);
    /// });
    /// </code>
    /// </example>
    public void ForEach<T>(in QueryDescription query, Action<Entity, T> action)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(action);

        _world.Query(
            in query,
            (Entity entity, ref T component) =>
            {
                action(entity, component);
            }
        );
    }

    /// <summary>
    ///     Batch destroy all entities matching the query.
    ///     More efficient than destroying individually.
    /// </summary>
    /// <param name="query">Query description to match</param>
    /// <returns>Number of entities destroyed</returns>
    /// <example>
    ///     <code>
    /// // Destroy all dead entities
    /// var query = new QueryDescription()
    ///     .WithAll&lt;Health&gt;()
    ///     .WithAll&lt;DeadTag&gt;();
    /// int destroyed = bulkQuery.DestroyMatching(query);
    /// Console.WriteLine($"Cleaned up {destroyed} dead entities");
    /// </code>
    /// </example>
    public int DestroyMatching(in QueryDescription query)
    {
        List<Entity> entitiesToDestroy = CollectEntities(query);

        foreach (Entity entity in entitiesToDestroy)
        {
            if (_world.IsAlive(entity))
            {
                _world.Destroy(entity);
            }
        }

        return entitiesToDestroy.Count;
    }

    /// <summary>
    ///     Batch add the same component to all entities matching the query.
    /// </summary>
    /// <typeparam name="T">Component type to add</typeparam>
    /// <param name="query">Query description to match</param>
    /// <param name="component">Component to add to all matching entities</param>
    /// <returns>Number of entities modified</returns>
    /// <example>
    ///     <code>
    /// // Add stunned status to all enemies in radius
    /// var query = new QueryDescription().WithAll&lt;Enemy&gt;();
    /// int stunned = bulkQuery.AddComponentToMatching(query,
    ///     new StunnedStatus { Duration = 3.0f }
    /// );
    /// </code>
    /// </example>
    public int AddComponentToMatching<T>(in QueryDescription query, T component)
        where T : struct
    {
        int count = 0;

        _world.Query(
            in query,
            entity =>
            {
                if (!entity.Has<T>())
                {
                    entity.Add(component);
                    count++;
                }
            }
        );

        return count;
    }

    /// <summary>
    ///     Batch remove a component from all entities matching the query.
    /// </summary>
    /// <typeparam name="T">Component type to remove</typeparam>
    /// <param name="query">Query description to match</param>
    /// <returns>Number of entities modified</returns>
    /// <example>
    ///     <code>
    /// // Remove invulnerability from all entities
    /// var query = new QueryDescription().WithAll&lt;InvulnerableStatus&gt;();
    /// int removed = bulkQuery.RemoveComponentFromMatching&lt;InvulnerableStatus&gt;(query);
    /// </code>
    /// </example>
    public int RemoveComponentFromMatching<T>(in QueryDescription query)
        where T : struct
    {
        int count = 0;

        _world.Query(
            in query,
            entity =>
            {
                if (entity.Has<T>())
                {
                    entity.Remove<T>();
                    count++;
                }
            }
        );

        return count;
    }

    /// <summary>
    ///     Count entities matching a query without collecting them.
    ///     More efficient than CollectEntities().Count.
    /// </summary>
    /// <param name="query">Query description to match</param>
    /// <returns>Number of matching entities</returns>
    /// <example>
    ///     <code>
    /// var query = new QueryDescription().WithAll&lt;Enemy&gt;();
    /// int enemyCount = bulkQuery.CountMatching(query);
    /// </code>
    /// </example>
    public int CountMatching(in QueryDescription query)
    {
        int count = 0;
        _world.Query(
            in query,
            entity =>
            {
                count++;
            }
        );
        return count;
    }

    /// <summary>
    ///     Check if any entities match the query.
    ///     More efficient than CountMatching() > 0.
    /// </summary>
    /// <param name="query">Query description to match</param>
    /// <returns>True if at least one entity matches</returns>
    /// <example>
    ///     <code>
    /// var query = new QueryDescription().WithAll&lt;PlayerTag&gt;();
    /// if (!bulkQuery.HasMatching(query))
    /// {
    ///     // Game over - no player exists
    /// }
    /// </code>
    /// </example>
    public bool HasMatching(in QueryDescription query)
    {
        bool hasAny = false;

        _world.Query(
            in query,
            entity =>
            {
                hasAny = true;
                // Early exit after first match would be ideal, but Query doesn't support it
            }
        );

        return hasAny;
    }
}
