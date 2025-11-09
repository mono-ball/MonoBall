using Arch.Core;
using Arch.Core.Extensions;
using PokeSharp.Core.Components.Relationships;
using PokeSharp.Core.Extensions;
using PokeSharp.Tests.ECS;
using System.Collections.Generic;
using Xunit;

namespace PokeSharp.Tests.ECS.Queries;

/// <summary>
/// Tests for querying relationship components.
/// </summary>
public class RelationshipQueryTests : EcsTestBase
{
    [Fact]
    public void QueryParentComponent_FindsAllChildren()
    {
        // Arrange
        var parent = World.Create();
        var child1 = World.Create();
        var child2 = World.Create();
        var child3 = World.Create();

        child1.SetParent(parent, World);
        child2.SetParent(parent, World);
        child3.SetParent(parent, World);

        // Act
        var query = new QueryDescription().WithAll<Parent>();
        var count = 0;
        World.Query(in query, (ref Parent p) =>
        {
            count++;
        });

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public void QueryChildrenComponent_FindsAllParents()
    {
        // Arrange
        var parent1 = World.Create();
        var parent2 = World.Create();
        var child1 = World.Create();
        var child2 = World.Create();

        child1.SetParent(parent1, World);
        child2.SetParent(parent2, World);

        // Act
        var query = new QueryDescription().WithAll<Children>();
        var count = 0;
        World.Query(in query, (ref Children c) =>
        {
            count++;
        });

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public void QueryOwnedComponent_FindsAllOwnedEntities()
    {
        // Arrange
        var owner1 = World.Create();
        var owner2 = World.Create();
        var owned1 = World.Create();
        var owned2 = World.Create();
        var owned3 = World.Create();

        owned1.SetOwner(owner1, World);
        owned2.SetOwner(owner1, World);
        owned3.SetOwner(owner2, World);

        // Act
        var query = new QueryDescription().WithAll<Owned>();
        var count = 0;
        World.Query(in query, (ref Owned o) =>
        {
            count++;
        });

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public void QueryOwnerComponent_FindsAllOwners()
    {
        // Arrange
        var owner1 = World.Create();
        var owner2 = World.Create();
        var owned1 = World.Create();
        var owned2 = World.Create();

        owned1.SetOwner(owner1, World);
        owned2.SetOwner(owner2, World);

        // Act
        var query = new QueryDescription().WithAll<Owner>();
        var count = 0;
        World.Query(in query, (ref Owner o) =>
        {
            count++;
        });

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public void QueryParentAndChildren_FindsEntitiesWithBoth()
    {
        // Arrange
        var grandparent = World.Create();
        var parent = World.Create();
        var child = World.Create();

        parent.SetParent(grandparent, World);
        child.SetParent(parent, World);

        // Act - Find entities that are both parents and children
        var query = new QueryDescription().WithAll<Parent, Children>();
        var count = 0;
        World.Query(in query, (Entity e, ref Parent p, ref Children c) =>
        {
            Assert.Equal(parent, e);
            count++;
        });

        // Assert
        Assert.Equal(1, count);
    }

    [Fact]
    public void QueryRootParents_FindsOnlyTopLevelParents()
    {
        // Arrange
        var grandparent = World.Create();
        var parent = World.Create();
        var child = World.Create();

        parent.SetParent(grandparent, World);
        child.SetParent(parent, World);

        // Act - Find entities that have children but no parent (root level)
        var query = new QueryDescription()
            .WithAll<Children>()
            .WithNone<Parent>();

        var roots = new List<Entity>();
        World.Query(in query, (Entity e, ref Children c) =>
        {
            roots.Add(e);
        });

        // Assert
        Assert.Single(roots);
        Assert.Equal(grandparent, roots[0]);
    }

    [Fact]
    public void QueryLeafChildren_FindsOnlyBottomLevelChildren()
    {
        // Arrange
        var grandparent = World.Create();
        var parent = World.Create();
        var child = World.Create();

        parent.SetParent(grandparent, World);
        child.SetParent(parent, World);

        // Act - Find entities that have a parent but no children (leaf level)
        var query = new QueryDescription()
            .WithAll<Parent>()
            .WithNone<Children>();

        var leaves = new List<Entity>();
        World.Query(in query, (Entity e, ref Parent p) =>
        {
            leaves.Add(e);
        });

        // Assert
        Assert.Single(leaves);
        Assert.Equal(child, leaves[0]);
    }

    [Fact]
    public void QueryByOwnershipType_FindsMatchingEntities()
    {
        // Arrange
        var owner = World.Create();
        var permanent = World.Create();
        var temporary = World.Create();

        permanent.SetOwner(owner, World, OwnershipType.Permanent);
        temporary.SetOwner(owner, World, OwnershipType.Temporary);

        // Act - Find all temporary ownership
        var query = new QueryDescription().WithAll<Owned>();
        var temporaryCount = 0;

        World.Query(in query, (Entity e, ref Owned o) =>
        {
            var type = e.GetOwnershipType(World);
            if (type == OwnershipType.Temporary)
            {
                temporaryCount++;
            }
        });

        // Assert
        Assert.Equal(1, temporaryCount);
    }

    [Fact]
    public void QueryWithEntityParameter_CanAccessEntityId()
    {
        // Arrange
        var parent = World.Create();
        var child1 = World.Create();
        var child2 = World.Create();

        child1.SetParent(parent, World);
        child2.SetParent(parent, World);

        // Act
        var query = new QueryDescription().WithAll<Parent>();
        var entities = new List<Entity>();

        World.Query(in query, (Entity e, ref Parent p) =>
        {
            entities.Add(e);
            Assert.Equal(parent, p.Value);
        });

        // Assert
        Assert.Equal(2, entities.Count);
        Assert.Contains(child1, entities);
        Assert.Contains(child2, entities);
    }

    [Fact]
    public void QueryComplexRelationships_FindsMatchingPatterns()
    {
        // Arrange - Create entities with both parent and owner relationships
        var parentOwner = World.Create();
        var childOwned1 = World.Create();
        var childOwned2 = World.Create();

        childOwned1.SetParent(parentOwner, World);
        childOwned1.SetOwner(parentOwner, World);

        childOwned2.SetParent(parentOwner, World);
        childOwned2.SetOwner(parentOwner, World);

        // Act - Find entities that have both relationships
        var query = new QueryDescription().WithAll<Parent, Owned>();
        var count = 0;

        World.Query(in query, (Entity e, ref Parent p, ref Owned o) =>
        {
            Assert.Equal(parentOwner, p.Value);
            Assert.Equal(parentOwner, o.OwnerEntity);
            count++;
        });

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public void QueryEmptyWorld_ReturnsNoResults()
    {
        // Act
        var parentQuery = new QueryDescription().WithAll<Parent>();
        var childrenQuery = new QueryDescription().WithAll<Children>();
        var ownedQuery = new QueryDescription().WithAll<Owned>();
        var ownerQuery = new QueryDescription().WithAll<Owner>();

        var parentCount = 0;
        var childrenCount = 0;
        var ownedCount = 0;
        var ownerCount = 0;

        World.Query(in parentQuery, (ref Parent p) => parentCount++);
        World.Query(in childrenQuery, (ref Children c) => childrenCount++);
        World.Query(in ownedQuery, (ref Owned o) => ownedCount++);
        World.Query(in ownerQuery, (ref Owner o) => ownerCount++);

        // Assert
        Assert.Equal(0, parentCount);
        Assert.Equal(0, childrenCount);
        Assert.Equal(0, ownedCount);
        Assert.Equal(0, ownerCount);
    }

    [Fact]
    public void QueryAfterDestroy_DoesNotIncludeDestroyedEntities()
    {
        // Arrange
        var parent = World.Create();
        var child1 = World.Create();
        var child2 = World.Create();

        child1.SetParent(parent, World);
        child2.SetParent(parent, World);

        // Destroy one child
        World.Destroy(child1);

        // Act
        var query = new QueryDescription().WithAll<Parent>();
        var count = 0;

        World.Query(in query, (ref Parent p) =>
        {
            count++;
        });

        // Assert
        Assert.Equal(1, count); // Only child2 should remain
    }
}
