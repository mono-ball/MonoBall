using Arch.Core;
using PokeSharp.Core.Components.Relationships;
using PokeSharp.Core.Extensions;
using PokeSharp.Tests.ECS;
using System;
using System.Linq;
using Xunit;

namespace PokeSharp.Tests.ECS.Components.Relationships;

/// <summary>
/// Tests for edge cases and error conditions in relationship system.
/// </summary>
public class EdgeCaseTests : EcsTestBase
{
    #region Parent-Child Edge Cases

    [Fact]
    public void GetChildren_ReturnsEmptyIfNoChildren()
    {
        // Arrange
        var parent = World.Create();

        // Act
        var children = parent.GetChildren(World).ToList();

        // Assert
        Assert.Empty(children);
    }

    [Fact]
    public void GetChildren_HandlesNullValuesList()
    {
        // Arrange
        var parent = World.Create();
        parent.Add(new Children { Values = null });

        // Act
        var children = parent.GetChildren(World).ToList();

        // Assert
        Assert.Empty(children);
    }

    [Fact]
    public void RemoveParent_SafelyHandlesEntityWithoutParent()
    {
        // Arrange
        var entity = World.Create();

        // Act - Should not throw
        entity.RemoveParent(World);

        // Assert
        Assert.False(entity.Has<Parent>());
    }

    [Fact]
    public void RemoveParent_SafelyHandlesDestroyedEntity()
    {
        // Arrange
        var parent = World.Create();
        var child = World.Create();
        child.SetParent(parent, World);
        World.Destroy(child);

        // Act - Should not throw
        child.RemoveParent(World);

        // Assert - Nothing to verify, just shouldn't throw
    }

    [Fact]
    public void GetParent_ReturnsNullWhenParentDestroyed()
    {
        // Arrange
        var parent = World.Create();
        var child = World.Create();
        child.SetParent(parent, World);
        World.Destroy(parent);

        // Act
        var result = child.GetParent(World);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void SetParent_HandlesMultipleUpdates()
    {
        // Arrange
        var parent1 = World.Create();
        var parent2 = World.Create();
        var parent3 = World.Create();
        var child = World.Create();

        // Act
        child.SetParent(parent1, World);
        child.SetParent(parent2, World);
        child.SetParent(parent3, World);

        // Assert
        Assert.Equal(parent3, child.GetParent(World));
        Assert.Empty(parent1.GetChildren(World));
        Assert.Empty(parent2.GetChildren(World));
        Assert.Single(parent3.GetChildren(World));
    }

    #endregion

    #region Owner-Owned Edge Cases

    [Fact]
    public void GetOwnedEntities_ReturnsEmptyWhenNoOwned()
    {
        // Arrange
        var owner = World.Create();

        // Act
        var owned = owner.GetOwnedEntities(World).ToList();

        // Assert
        Assert.Empty(owned);
    }

    [Fact]
    public void RemoveOwner_SafelyHandlesEntityWithoutOwner()
    {
        // Arrange
        var entity = World.Create();

        // Act - Should not throw
        entity.RemoveOwner(World);

        // Assert
        Assert.False(entity.Has<Owned>());
    }

    [Fact]
    public void RemoveOwner_SafelyHandlesDestroyedEntity()
    {
        // Arrange
        var owner = World.Create();
        var owned = World.Create();
        owned.SetOwner(owner, World);
        World.Destroy(owned);

        // Act - Should not throw
        owned.RemoveOwner(World);

        // Assert - Nothing to verify, just shouldn't throw
    }

    [Fact]
    public void GetOwner_ReturnsNullWhenOwnerDestroyed()
    {
        // Arrange
        var owner = World.Create();
        var owned = World.Create();
        owned.SetOwner(owner, World);
        World.Destroy(owner);

        // Act
        var result = owned.GetOwner(World);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetOwnershipType_ReturnsNullWhenOwnerComponentMissing()
    {
        // Arrange
        var owner = World.Create();
        var owned = World.Create();
        owned.SetOwner(owner, World);

        // Remove owner component but keep owned component (invalid state)
        owner.Remove<Owner>();

        // Act
        var type = owned.GetOwnershipType(World);

        // Assert
        Assert.Null(type);
    }

    [Fact]
    public void SetOwner_HandlesMultipleUpdates()
    {
        // Arrange
        var owner1 = World.Create();
        var owner2 = World.Create();
        var owner3 = World.Create();
        var owned = World.Create();

        // Act
        owned.SetOwner(owner1, World);
        owned.SetOwner(owner2, World);
        owned.SetOwner(owner3, World);

        // Assert
        Assert.Equal(owner3, owned.GetOwner(World));
    }

    #endregion

    #region Combined Relationships

    [Fact]
    public void Entity_CanHaveBothParentAndOwner()
    {
        // Arrange
        var parent = World.Create();
        var owner = World.Create();
        var entity = World.Create();

        // Act
        entity.SetParent(parent, World);
        entity.SetOwner(owner, World);

        // Assert
        Assert.True(entity.Has<Parent>());
        Assert.True(entity.Has<Owned>());
        Assert.Equal(parent, entity.GetParent(World));
        Assert.Equal(owner, entity.GetOwner(World));
    }

    [Fact]
    public void Entity_CanBeParentAndOwner()
    {
        // Arrange
        var parentOwner = World.Create();
        var childOwned = World.Create();

        // Act
        childOwned.SetParent(parentOwner, World);
        childOwned.SetOwner(parentOwner, World);

        // Assert
        Assert.Single(parentOwner.GetChildren(World));
        Assert.Single(parentOwner.GetOwnedEntities(World));
    }

    #endregion

    #region Performance Edge Cases

    [Fact]
    public void GetChildren_PerformanceWithManyChildren()
    {
        // Arrange
        var parent = World.Create();
        var children = new Entity[100];
        for (int i = 0; i < 100; i++)
        {
            children[i] = World.Create();
            children[i].SetParent(parent, World);
        }

        // Act
        var retrievedChildren = parent.GetChildren(World).ToList();

        // Assert
        Assert.Equal(100, retrievedChildren.Count);
    }

    [Fact]
    public void GetOwnedEntities_PerformanceWithManyOwned()
    {
        // Arrange
        var owner = World.Create();
        var ownedEntities = new Entity[100];
        for (int i = 0; i < 100; i++)
        {
            ownedEntities[i] = World.Create();
            ownedEntities[i].SetOwner(owner, World);
        }

        // Act
        var retrieved = owner.GetOwnedEntities(World).ToList();

        // Assert
        Assert.Equal(100, retrieved.Count);
    }

    #endregion

    #region Null Safety

    [Fact]
    public void GetChildCount_HandlesDestroyedParent()
    {
        // Arrange
        var parent = World.Create();
        var child = World.Create();
        child.SetParent(parent, World);
        World.Destroy(parent);

        // Act
        var count = parent.GetChildCount(World);

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public void ChildrenComponent_CountReturnsZeroWhenValuesNull()
    {
        // Arrange
        var children = new Children { Values = null };

        // Act
        var count = children.Count;

        // Assert
        Assert.Equal(0, count);
    }

    #endregion
}
