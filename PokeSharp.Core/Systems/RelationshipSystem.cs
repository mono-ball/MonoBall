using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using PokeSharp.Core.Components.Relationships;
using PokeSharp.Core.Queries;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PokeSharp.Core.Systems;

/// <summary>
/// System responsible for validating and maintaining entity relationships.
/// Runs late in the update cycle to clean up broken references.
/// </summary>
/// <remarks>
/// <para>
/// This system performs critical maintenance tasks:
/// - Validates parent-child relationships
/// - Validates owner-owned relationships
/// - Removes references to destroyed entities
/// - Detects and optionally cleans up orphaned entities
/// - Logs relationship integrity issues
/// </para>
/// <para>
/// Runs with priority 950 (late update) to allow other systems to complete
/// their work before relationship validation occurs.
/// </para>
/// </remarks>
public class RelationshipSystem : SystemBase
{
    private readonly ILogger<RelationshipSystem> _logger;
    private QueryDescription _parentQuery;
    private QueryDescription _childrenQuery;
    private QueryDescription _ownerQuery;
    private QueryDescription _ownedQuery;

    // Statistics for monitoring
    private int _brokenParentsFixed;
    private int _brokenChildrenFixed;
    private int _brokenOwnersFixed;
    private int _brokenOwnedFixed;
    private int _orphansDetected;

    /// <summary>
    /// Gets or sets whether orphaned entities should be automatically destroyed.
    /// Default is false for safety.
    /// </summary>
    public bool AutoDestroyOrphans { get; set; } = false;

    /// <summary>
    /// Gets the priority of this system (950 - late update).
    /// </summary>
    public override int Priority => 950;

    public RelationshipSystem(ILogger<RelationshipSystem> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override void Initialize(World world)
    {
        base.Initialize(world);

        // Use centralized relationship queries for better semantics
        _parentQuery = RelationshipQueries.AllChildren;    // Entities with Parent component
        _childrenQuery = RelationshipQueries.AllParents;   // Entities with Children component
        _ownerQuery = RelationshipQueries.AllOwners;       // Entities with Owner component
        _ownedQuery = RelationshipQueries.AllOwned;        // Entities with Owned component

        _logger.LogInformation("RelationshipSystem initialized (Priority: {Priority})", Priority);
    }

    public override void Update(World world, float deltaTime)
    {
        // Reset statistics
        _brokenParentsFixed = 0;
        _brokenChildrenFixed = 0;
        _brokenOwnersFixed = 0;
        _brokenOwnedFixed = 0;
        _orphansDetected = 0;

        // Validate all relationship types
        ValidateParentRelationships(world);
        ValidateChildrenRelationships(world);
        ValidateOwnerRelationships(world);
        ValidateOwnedRelationships(world);

        // Log summary if any issues were found
        if (_brokenParentsFixed > 0 || _brokenChildrenFixed > 0 ||
            _brokenOwnersFixed > 0 || _brokenOwnedFixed > 0)
        {
            _logger.LogWarning(
                "Relationship cleanup: {Parents} parent(s), {Children} child refs, {Owners} owner(s), {Owned} owned refs fixed",
                _brokenParentsFixed, _brokenChildrenFixed, _brokenOwnersFixed, _brokenOwnedFixed);
        }

        if (_orphansDetected > 0)
        {
            _logger.LogWarning("Detected {Count} orphaned entities", _orphansDetected);
        }
    }

    /// <summary>
    /// Validates all Parent components and removes those referencing destroyed entities.
    /// </summary>
    private void ValidateParentRelationships(World world)
    {
        var entitiesToFix = new List<Entity>();

        world.Query(in _parentQuery, (Entity entity, ref Parent parent) =>
        {
            if (!world.IsAlive(parent.Value))
            {
                entitiesToFix.Add(entity);
                _orphansDetected++;
            }
        });

        foreach (var entity in entitiesToFix)
        {
            if (world.IsAlive(entity))
            {
                entity.Remove<Parent>();
                _brokenParentsFixed++;

                if (AutoDestroyOrphans)
                {
                    _logger.LogDebug("Destroying orphaned entity {Entity}", entity);
                    world.Destroy(entity);
                }
                else
                {
                    _logger.LogDebug("Removed invalid parent reference from entity {Entity}", entity);
                }
            }
        }
    }

    /// <summary>
    /// Validates all Children components and removes destroyed entities from child lists.
    /// </summary>
    private void ValidateChildrenRelationships(World world)
    {
        world.Query(in _childrenQuery, (Entity entity, ref Children children) =>
        {
            if (children.Values == null)
            {
                return;
            }

            var initialCount = children.Values.Count;
            children.Values.RemoveAll(child => !world.IsAlive(child));
            var removedCount = initialCount - children.Values.Count;

            if (removedCount > 0)
            {
                _brokenChildrenFixed += removedCount;
                _logger.LogDebug(
                    "Removed {Count} invalid child references from entity {Entity}",
                    removedCount, entity);
            }
        });
    }

    /// <summary>
    /// Validates all Owner components and removes those referencing destroyed entities.
    /// </summary>
    private void ValidateOwnerRelationships(World world)
    {
        var entitiesToFix = new List<Entity>();

        world.Query(in _ownerQuery, (Entity entity, ref Owner owner) =>
        {
            if (!world.IsAlive(owner.Value))
            {
                entitiesToFix.Add(entity);
            }
        });

        foreach (var entity in entitiesToFix)
        {
            if (world.IsAlive(entity))
            {
                entity.Remove<Owner>();
                _brokenOwnersFixed++;
                _logger.LogDebug("Removed invalid owner reference from entity {Entity}", entity);
            }
        }
    }

    /// <summary>
    /// Validates all Owned components and removes those referencing destroyed owners.
    /// </summary>
    private void ValidateOwnedRelationships(World world)
    {
        var entitiesToFix = new List<Entity>();

        world.Query(in _ownedQuery, (Entity entity, ref Owned owned) =>
        {
            if (!world.IsAlive(owned.OwnerEntity))
            {
                entitiesToFix.Add(entity);
                _orphansDetected++;
            }
        });

        foreach (var entity in entitiesToFix)
        {
            if (world.IsAlive(entity))
            {
                entity.Remove<Owned>();
                _brokenOwnedFixed++;

                if (AutoDestroyOrphans)
                {
                    _logger.LogDebug("Destroying entity {Entity} with invalid owner", entity);
                    world.Destroy(entity);
                }
                else
                {
                    _logger.LogDebug("Removed invalid owned reference from entity {Entity}", entity);
                }
            }
        }
    }

    /// <summary>
    /// Gets current relationship validation statistics.
    /// </summary>
    public RelationshipStats GetStats()
    {
        return new RelationshipStats
        {
            BrokenParentsFixed = _brokenParentsFixed,
            BrokenChildrenReferencesFixed = _brokenChildrenFixed,
            BrokenOwnersFixed = _brokenOwnersFixed,
            BrokenOwnedFixed = _brokenOwnedFixed,
            OrphansDetected = _orphansDetected
        };
    }
}

/// <summary>
/// Statistics about relationship validation performed in the last update.
/// </summary>
public struct RelationshipStats
{
    public int BrokenParentsFixed;
    public int BrokenChildrenReferencesFixed;
    public int BrokenOwnersFixed;
    public int BrokenOwnedFixed;
    public int OrphansDetected;
}
