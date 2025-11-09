using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using Moq;
using PokeSharp.Core.Components.Relationships;
using PokeSharp.Core.Extensions;
using PokeSharp.Core.Systems;
using PokeSharp.Tests.ECS.Systems;
using System.Linq;
using Xunit;

namespace PokeSharp.Tests.ECS.Systems;

/// <summary>
/// Tests for RelationshipSystem validation and cleanup functionality.
/// </summary>
public class RelationshipSystemTests : SystemTestBase<RelationshipSystem>
{
    protected override RelationshipSystem CreateSystem()
    {
        var mockLogger = new Mock<ILogger<RelationshipSystem>>();
        return new RelationshipSystem(mockLogger.Object);
    }

    [Fact]
    public void Initialize_SetsUpQueriesSuccessfully()
    {
        // Act
        InitializeSystem();

        // Assert
        AssertSystemEnabled();
    }

    [Fact]
    public void Update_RemovesBrokenParentReferences()
    {
        // Arrange
        InitializeSystem();
        var parent = World.Create();
        var child = World.Create();
        child.SetParent(parent, World);

        // Destroy parent
        World.Destroy(parent);

        // Act
        UpdateSystem();

        // Assert
        Assert.False(child.Has<Parent>());
    }

    [Fact]
    public void Update_RemovesBrokenOwnerReferences()
    {
        // Arrange
        InitializeSystem();
        var owner = World.Create();
        var owned = World.Create();
        owned.SetOwner(owner, World);

        // Destroy owner
        World.Destroy(owner);

        // Act
        UpdateSystem();

        // Assert
        Assert.False(owned.Has<Owned>());
    }

    [Fact]
    public void Update_CleansUpChildrenCollection()
    {
        // Arrange
        InitializeSystem();
        var parent = World.Create();
        var child1 = World.Create();
        var child2 = World.Create();
        var child3 = World.Create();

        child1.SetParent(parent, World);
        child2.SetParent(parent, World);
        child3.SetParent(parent, World);

        // Destroy one child
        World.Destroy(child2);

        // Act
        UpdateSystem();

        // Assert
        var children = parent.GetChildren(World).ToList();
        Assert.Equal(2, children.Count);
        Assert.Contains(child1, children);
        Assert.DoesNotContain(child2, children);
        Assert.Contains(child3, children);
    }

    [Fact]
    public void Update_CleansUpMultipleBrokenReferences()
    {
        // Arrange
        InitializeSystem();
        var parent1 = World.Create();
        var parent2 = World.Create();
        var child1 = World.Create();
        var child2 = World.Create();

        child1.SetParent(parent1, World);
        child2.SetParent(parent2, World);

        // Destroy both parents
        World.Destroy(parent1);
        World.Destroy(parent2);

        // Act
        UpdateSystem();

        // Assert
        Assert.False(child1.Has<Parent>());
        Assert.False(child2.Has<Parent>());
    }

    [Fact]
    public void GetStats_ReturnsValidationMetrics()
    {
        // Arrange
        InitializeSystem();
        var parent = World.Create();
        var child = World.Create();
        child.SetParent(parent, World);

        World.Destroy(parent);
        UpdateSystem();

        // Act
        var stats = System.GetStats();

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(1, stats.BrokenParentsFixed);
        Assert.Equal(1, stats.OrphansDetected);
    }

    [Fact]
    public void AutoDestroyOrphans_WhenEnabled_DestroysOrphanedChildren()
    {
        // Arrange
        InitializeSystem();
        System.AutoDestroyOrphans = true;

        var parent = World.Create();
        var child = World.Create();
        child.SetParent(parent, World);

        // Destroy parent
        World.Destroy(parent);

        // Act
        UpdateSystem();

        // Assert
        Assert.False(World.IsAlive(child));
    }

    [Fact]
    public void AutoDestroyOrphans_WhenDisabled_KeepsOrphanedChildren()
    {
        // Arrange
        InitializeSystem();
        System.AutoDestroyOrphans = false;

        var parent = World.Create();
        var child = World.Create();
        child.SetParent(parent, World);

        // Destroy parent
        World.Destroy(parent);

        // Act
        UpdateSystem();

        // Assert
        Assert.True(World.IsAlive(child));
        Assert.False(child.Has<Parent>());
    }

    [Fact]
    public void AutoDestroyOrphans_WhenEnabled_DestroysOrphanedOwned()
    {
        // Arrange
        InitializeSystem();
        System.AutoDestroyOrphans = true;

        var owner = World.Create();
        var owned = World.Create();
        owned.SetOwner(owner, World);

        // Destroy owner
        World.Destroy(owner);

        // Act
        UpdateSystem();

        // Assert
        Assert.False(World.IsAlive(owned));
    }

    [Fact]
    public void Update_HandlesMultipleChildrenWithBrokenReferences()
    {
        // Arrange
        InitializeSystem();
        var parent1 = World.Create();
        var parent2 = World.Create();

        var child1 = World.Create();
        var child2 = World.Create();
        var child3 = World.Create();
        var child4 = World.Create();

        child1.SetParent(parent1, World);
        child2.SetParent(parent1, World);
        child3.SetParent(parent2, World);
        child4.SetParent(parent2, World);

        // Destroy first parent
        World.Destroy(parent1);

        // Act
        UpdateSystem();

        // Assert
        Assert.False(child1.Has<Parent>());
        Assert.False(child2.Has<Parent>());
        Assert.True(child3.Has<Parent>());
        Assert.True(child4.Has<Parent>());

        var stats = System.GetStats();
        Assert.Equal(2, stats.BrokenParentsFixed);
    }

    [Fact]
    public void Update_ValidatesOwnerComponentReferences()
    {
        // Arrange
        InitializeSystem();
        var owner = World.Create();
        var owned1 = World.Create();
        var owned2 = World.Create();

        owned1.SetOwner(owner, World);
        owned2.SetOwner(owner, World);

        // Destroy one owned entity
        World.Destroy(owned1);

        // Act
        UpdateSystem();

        // Assert
        // Owner component on owner should still be valid for owned2
        Assert.True(owner.Has<Owner>());
    }

    [Fact]
    public void Update_RunsMultipleTimesWithoutErrors()
    {
        // Arrange
        InitializeSystem();
        var parent = World.Create();
        var child = World.Create();
        child.SetParent(parent, World);

        // Act - Multiple updates should not cause issues
        UpdateSystemForFrames(10);

        // Assert
        Assert.True(parent.Has<Children>());
        Assert.True(child.Has<Parent>());
    }

    [Fact]
    public void GetStats_TracksChildrenReferencesFixed()
    {
        // Arrange
        InitializeSystem();
        var parent = World.Create();
        var child1 = World.Create();
        var child2 = World.Create();
        var child3 = World.Create();

        child1.SetParent(parent, World);
        child2.SetParent(parent, World);
        child3.SetParent(parent, World);

        // Destroy two children
        World.Destroy(child1);
        World.Destroy(child3);

        // Act
        UpdateSystem();
        var stats = System.GetStats();

        // Assert
        Assert.Equal(2, stats.BrokenChildrenReferencesFixed);
    }

    [Fact]
    public void System_HasCorrectPriority()
    {
        // Arrange & Act
        InitializeSystem();

        // Assert
        Assert.Equal(950, System.Priority);
    }

    [Fact]
    public void Update_HandlesEmptyWorld()
    {
        // Arrange
        InitializeSystem();

        // Act - Should not throw with no entities
        UpdateSystem();

        // Assert
        var stats = System.GetStats();
        Assert.Equal(0, stats.BrokenParentsFixed);
        Assert.Equal(0, stats.BrokenChildrenReferencesFixed);
        Assert.Equal(0, stats.BrokenOwnersFixed);
        Assert.Equal(0, stats.BrokenOwnedFixed);
    }

    [Fact]
    public void Update_HandlesComplexHierarchy()
    {
        // Arrange
        InitializeSystem();
        var grandparent = World.Create();
        var parent = World.Create();
        var child = World.Create();

        parent.SetParent(grandparent, World);
        child.SetParent(parent, World);

        // Destroy middle parent
        World.Destroy(parent);

        // Act
        UpdateSystem();

        // Assert
        Assert.False(child.Has<Parent>());
        var grandparentChildren = grandparent.GetChildren(World).ToList();
        Assert.Empty(grandparentChildren);
    }

    [Fact]
    public void GetStats_ResetsAfterEachUpdate()
    {
        // Arrange
        InitializeSystem();
        var parent = World.Create();
        var child = World.Create();
        child.SetParent(parent, World);

        World.Destroy(parent);
        UpdateSystem();
        var stats1 = System.GetStats();

        // Act - Second update should reset counters
        UpdateSystem();
        var stats2 = System.GetStats();

        // Assert
        Assert.Equal(1, stats1.BrokenParentsFixed);
        Assert.Equal(0, stats2.BrokenParentsFixed);
    }
}
