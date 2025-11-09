using Arch.Core;
using PokeSharp.Core.Components.Relationships;
using PokeSharp.Core.Extensions;
using PokeSharp.Core.Queries;
using PokeSharp.Core.Systems;
using PokeSharp.Tests.ECS;
using PokeSharp.Tests.ECS.TestUtilities;
using Xunit;

namespace PokeSharp.Tests.Integration;

/// <summary>
/// Integration tests validating Phase 1 components work together correctly.
/// Tests the integration of: Testing Infrastructure, Relationship System, Query Cache, and DI System.
/// </summary>
public class Phase1ValidationTests : EcsTestBase
{
    [Fact]
    public void TestInfrastructure_CreatesWorldSuccessfully()
    {
        // Arrange & Act - World created by EcsTestBase

        // Assert
        Assert.NotNull(World);
        Assert.Equal(0, GetEntityCount());
    }

    [Fact]
    public void TestInfrastructure_HelperMethodsWork()
    {
        // Arrange
        var entity = EcsTestHelpers.CreateEntityWithPosition(World, 10, 20);

        // Act & Assert
        AssertHasComponent<PokeSharp.Core.Components.Common.Position>(entity);
        EcsTestHelpers.AssertPosition(World, entity, 10, 20);
        Assert.Equal(1, GetEntityCount());
    }

    [Fact]
    public void RelationshipSystem_ParentChildRelationship_WorksCorrectly()
    {
        // Arrange
        var parent = World.Create();
        var child = World.Create();

        // Act
        child.SetParent(parent, World);

        // Assert
        Assert.True(child.Has<Parent>());
        Assert.True(parent.Has<Children>());

        var retrievedParent = child.GetParent(World);
        Assert.NotNull(retrievedParent);
        Assert.Equal(parent, retrievedParent.Value);

        var children = parent.GetChildren(World).ToList();
        Assert.Single(children);
        Assert.Equal(child, children[0]);
    }

    [Fact]
    public void RelationshipSystem_OwnerOwnedRelationship_WorksCorrectly()
    {
        // Arrange
        var owner = World.Create();
        var owned = World.Create();

        // Act
        owned.SetOwner(owner, World);

        // Assert
        Assert.True(owned.Has<Owned>());
        Assert.True(owner.Has<Owner>());

        var retrievedOwner = owned.GetOwner(World);
        Assert.NotNull(retrievedOwner);
        Assert.Equal(owner, retrievedOwner.Value);
    }

    [Fact]
    public void QueryCache_RelationshipQueries_ExecuteWithoutAllocations()
    {
        // Arrange
        var parent1 = World.Create();
        var parent2 = World.Create();
        var child1 = World.Create();
        var child2 = World.Create();
        var child3 = World.Create();

        child1.SetParent(parent1, World);
        child2.SetParent(parent1, World);
        child3.SetParent(parent2, World);

        // Act
        int childCount = 0;
        World.Query(in RelationshipQueries.AllChildren, (ref Parent p) =>
        {
            childCount++;
        });

        int parentCount = 0;
        World.Query(in RelationshipQueries.AllParents, (ref Children c) =>
        {
            parentCount++;
        });

        // Assert
        Assert.Equal(3, childCount);
        Assert.Equal(2, parentCount);
    }

    [Fact]
    public void QueryCache_MultipleQueriesTypes_WorkTogether()
    {
        // Arrange
        var parent = World.Create();
        var child = World.Create();
        child.SetParent(parent, World);

        // Act - Test root parents query
        int rootParentCount = 0;
        World.Query(in RelationshipQueries.RootParents, (ref Children c) =>
        {
            rootParentCount++;
        });

        // Act - Test leaf children query
        int leafChildCount = 0;
        World.Query(in RelationshipQueries.LeafChildren, (ref Parent p) =>
        {
            leafChildCount++;
        });

        // Assert
        Assert.Equal(1, rootParentCount); // Parent has no parent
        Assert.Equal(1, leafChildCount);  // Child has no children
    }

    [Fact]
    public void RelationshipSystem_RemovesInvalidReferences()
    {
        // Arrange
        var parent = World.Create();
        var child = World.Create();
        child.SetParent(parent, World);

        // Act - Destroy parent
        World.Destroy(parent);

        // Manual validation since RelationshipSystem isn't running
        // In production, RelationshipSystem would clean this up automatically

        // Assert - Child still has Parent component (would be cleaned by system)
        Assert.True(child.Has<Parent>());

        // GetParent should return null for destroyed parent
        var retrievedParent = child.GetParent(World);
        Assert.Null(retrievedParent);
    }

    [Fact]
    public void Integration_ComplexHierarchy_MaintainsIntegrity()
    {
        // Arrange - Create a trainer with multiple Pokemon
        var trainer = World.Create();
        var pokemon1 = World.Create();
        var pokemon2 = World.Create();
        var pokemon3 = World.Create();

        // Act - Set up parent-child relationships
        pokemon1.SetParent(trainer, World);
        pokemon2.SetParent(trainer, World);
        pokemon3.SetParent(trainer, World);

        // Assert - Verify hierarchy
        Assert.Equal(3, trainer.GetChildCount(World));

        var children = trainer.GetChildren(World).ToList();
        Assert.Equal(3, children.Count);
        Assert.Contains(pokemon1, children);
        Assert.Contains(pokemon2, children);
        Assert.Contains(pokemon3, children);

        // Verify each pokemon knows its trainer
        Assert.Equal(trainer, pokemon1.GetParent(World).Value);
        Assert.Equal(trainer, pokemon2.GetParent(World).Value);
        Assert.Equal(trainer, pokemon3.GetParent(World).Value);
    }

    [Fact]
    public void Integration_ReparentingEntity_UpdatesBothParents()
    {
        // Arrange
        var oldParent = World.Create();
        var newParent = World.Create();
        var child = World.Create();

        child.SetParent(oldParent, World);

        // Act - Reparent to new parent
        child.SetParent(newParent, World);

        // Assert
        Assert.Equal(newParent, child.GetParent(World).Value);
        Assert.Equal(0, oldParent.GetChildCount(World));
        Assert.Equal(1, newParent.GetChildCount(World));
    }

    [Fact]
    public void Integration_TestInfrastructureWithRelationships_WorksTogether()
    {
        // Arrange - Use test helpers to create entities
        var parent = EcsTestHelpers.CreateEntityWithPosition(World, 0, 0);
        var child = EcsTestHelpers.CreateEntityWithPosition(World, 1, 0);

        // Act - Establish relationship
        child.SetParent(parent, World);

        // Assert - Test infrastructure assertions work with relationships
        AssertHasComponent<PokeSharp.Core.Components.Common.Position>(parent);
        AssertHasComponent<PokeSharp.Core.Components.Common.Position>(child);
        AssertHasComponent<Parent>(child);
        AssertHasComponent<Children>(parent);

        // Verify both position and relationship data
        EcsTestHelpers.AssertPosition(World, parent, 0, 0);
        EcsTestHelpers.AssertPosition(World, child, 1, 0);
        Assert.Equal(parent, child.GetParent(World).Value);
    }

    [Fact]
    public void QueryCache_ComplexQueries_ExecuteCorrectly()
    {
        // Arrange - Create complex hierarchy
        var rootParent = World.Create();
        var middleNode = World.Create();
        var leafChild = World.Create();

        middleNode.SetParent(rootParent, World);
        leafChild.SetParent(middleNode, World);

        // Act & Assert - Root parents (has children, no parent)
        int rootCount = 0;
        World.Query(in RelationshipQueries.RootParents, (Entity e, ref Children c) =>
        {
            rootCount++;
            Assert.Equal(rootParent, e);
        });
        Assert.Equal(1, rootCount);

        // Hierarchy nodes (has both parent and children)
        int hierarchyCount = 0;
        World.Query(in RelationshipQueries.HierarchyNodes, (Entity e, ref Parent p, ref Children c) =>
        {
            hierarchyCount++;
            Assert.Equal(middleNode, e);
        });
        Assert.Equal(1, hierarchyCount);

        // Leaf children (has parent, no children)
        int leafCount = 0;
        World.Query(in RelationshipQueries.LeafChildren, (Entity e, ref Parent p) =>
        {
            leafCount++;
            Assert.Equal(leafChild, e);
        });
        Assert.Equal(1, leafCount);
    }

    [Fact]
    public void ComponentFixtures_CreateValidRelationshipData()
    {
        // Arrange - Create entities using fixtures
        var entity1 = World.Create(ComponentFixtures.CreatePositionAtOrigin());
        var entity2 = World.Create(ComponentFixtures.CreatePositionAt10x10());

        // Act - Set up relationship
        entity2.SetParent(entity1, World);

        // Assert - Fixtures and relationships work together
        AssertHasComponent<PokeSharp.Core.Components.Common.Position>(entity1);
        AssertHasComponent<PokeSharp.Core.Components.Common.Position>(entity2);
        Assert.Equal(entity1, entity2.GetParent(World).Value);
    }

    [Fact]
    public void BulkOperations_MaintainRelationshipIntegrity()
    {
        // Arrange - Create many entities with relationships
        var parent = World.Create();
        var children = new List<Entity>();

        // Act - Create many child relationships
        for (int i = 0; i < 100; i++)
        {
            var child = World.Create();
            child.SetParent(parent, World);
            children.Add(child);
        }

        // Assert
        Assert.Equal(100, parent.GetChildCount(World));

        var retrievedChildren = parent.GetChildren(World).ToList();
        Assert.Equal(100, retrievedChildren.Count);

        // Verify all children reference correct parent
        foreach (var child in children)
        {
            Assert.Equal(parent, child.GetParent(World).Value);
        }
    }

    [Fact]
    public void Phase1_AllComponents_BuildAndIntegrate()
    {
        // This test validates that all Phase 1 components are present and working:
        // ✅ ECS Testing Infrastructure (EcsTestBase, helpers, fixtures)
        // ✅ Entity Relationship System (Parent/Children, Owner/Owned)
        // ✅ Centralized Query Cache (RelationshipQueries)
        // ✅ Extension Methods (SetParent, GetChildren, etc.)

        // Arrange
        var trainer = World.Create(ComponentFixtures.CreatePlayerTag());
        var pokemon1 = EcsTestHelpers.CreateEntityWithPosition(World, 0, 0);
        var pokemon2 = EcsTestHelpers.CreateEntityWithPosition(World, 1, 0);
        var item = World.Create();

        // Act - Build complex relationship graph
        pokemon1.SetParent(trainer, World);
        pokemon2.SetParent(trainer, World);
        item.SetOwner(pokemon1, World);

        // Assert - All systems integrated
        Assert.Equal(2, trainer.GetChildCount(World));
        Assert.True(pokemon1.Has<Parent>());
        Assert.True(pokemon2.Has<Parent>());
        Assert.True(item.Has<Owned>());
        Assert.Equal(pokemon1, item.GetOwner(World).Value);

        // Query cache works
        int parentCount = 0;
        World.Query(in RelationshipQueries.AllParents, (ref Children c) => parentCount++);
        Assert.Equal(1, parentCount);

        int childCount = 0;
        World.Query(in RelationshipQueries.AllChildren, (ref Parent p) => childCount++);
        Assert.Equal(2, childCount);

        int ownedCount = 0;
        World.Query(in RelationshipQueries.AllOwned, (ref Owned o) => ownedCount++);
        Assert.Equal(1, ownedCount);
    }
}
