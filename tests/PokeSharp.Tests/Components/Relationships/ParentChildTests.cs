using Arch.Core;
using PokeSharp.Core.Components.Relationships;
using PokeSharp.Core.Extensions;
using PokeSharp.Tests.ECS;
using System;
using System.Linq;
using Xunit;

namespace PokeSharp.Tests.ECS.Components.Relationships;

/// <summary>
/// Tests for Parent-Child relationship functionality.
/// </summary>
public class ParentChildTests : EcsTestBase
{
    [Fact]
    public void SetParent_CreatesParentComponent()
    {
        // Arrange
        var parent = World.Create();
        var child = World.Create();

        // Act
        child.SetParent(parent, World);

        // Assert
        Assert.True(child.Has<Parent>());
        Assert.Equal(parent, child.Get<Parent>().Value);
    }

    [Fact]
    public void SetParent_AddsChildToParentCollection()
    {
        // Arrange
        var parent = World.Create();
        var child = World.Create();

        // Act
        child.SetParent(parent, World);

        // Assert
        Assert.True(parent.Has<Children>());
        var children = parent.GetChildren(World).ToList();
        Assert.Single(children);
        Assert.Equal(child, children[0]);
    }

    [Fact]
    public void RemoveParent_RemovesParentComponent()
    {
        // Arrange
        var parent = World.Create();
        var child = World.Create();
        child.SetParent(parent, World);

        // Act
        child.RemoveParent(World);

        // Assert
        Assert.False(child.Has<Parent>());
    }

    [Fact]
    public void RemoveParent_RemovesChildFromParentCollection()
    {
        // Arrange
        var parent = World.Create();
        var child = World.Create();
        child.SetParent(parent, World);

        // Act
        child.RemoveParent(World);

        // Assert
        var children = parent.GetChildren(World).ToList();
        Assert.Empty(children);
    }

    [Fact]
    public void GetChildren_ReturnsAllChildren()
    {
        // Arrange
        var parent = World.Create();
        var child1 = World.Create();
        var child2 = World.Create();
        var child3 = World.Create();

        // Act
        child1.SetParent(parent, World);
        child2.SetParent(parent, World);
        child3.SetParent(parent, World);

        // Assert
        var children = parent.GetChildren(World).ToList();
        Assert.Equal(3, children.Count);
        Assert.Contains(child1, children);
        Assert.Contains(child2, children);
        Assert.Contains(child3, children);
    }

    [Fact]
    public void SetParent_RecordsEstablishedTimestamp()
    {
        // Arrange
        var parent = World.Create();
        var child = World.Create();
        var before = DateTime.UtcNow;

        // Act
        child.SetParent(parent, World);

        // Assert
        var after = DateTime.UtcNow;
        var timestamp = child.Get<Parent>().EstablishedAt;
        Assert.True(timestamp >= before && timestamp <= after,
            $"Timestamp {timestamp} should be between {before} and {after}");
    }

    [Fact]
    public void GetParent_ReturnsParentEntity()
    {
        // Arrange
        var parent = World.Create();
        var child = World.Create();
        child.SetParent(parent, World);

        // Act
        var result = child.GetParent(World);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(parent, result.Value);
    }

    [Fact]
    public void GetParent_ReturnsNullWhenNoParent()
    {
        // Arrange
        var child = World.Create();

        // Act
        var result = child.GetParent(World);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetChildCount_ReturnsCorrectCount()
    {
        // Arrange
        var parent = World.Create();
        var child1 = World.Create();
        var child2 = World.Create();
        child1.SetParent(parent, World);
        child2.SetParent(parent, World);

        // Act
        var count = parent.GetChildCount(World);

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public void GetChildCount_ReturnsZeroWhenNoChildren()
    {
        // Arrange
        var parent = World.Create();

        // Act
        var count = parent.GetChildCount(World);

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public void SetParent_UpdatesExistingParent()
    {
        // Arrange
        var parent1 = World.Create();
        var parent2 = World.Create();
        var child = World.Create();
        child.SetParent(parent1, World);

        // Act
        child.SetParent(parent2, World);

        // Assert
        var currentParent = child.GetParent(World);
        Assert.Equal(parent2, currentParent);

        // Old parent should not have child
        var parent1Children = parent1.GetChildren(World).ToList();
        Assert.Empty(parent1Children);

        // New parent should have child
        var parent2Children = parent2.GetChildren(World).ToList();
        Assert.Single(parent2Children);
        Assert.Equal(child, parent2Children[0]);
    }

    [Fact]
    public void SetParent_ThrowsWhenChildNotAlive()
    {
        // Arrange
        var parent = World.Create();
        var child = World.Create();
        World.Destroy(child);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => child.SetParent(parent, World));
    }

    [Fact]
    public void SetParent_ThrowsWhenParentNotAlive()
    {
        // Arrange
        var parent = World.Create();
        var child = World.Create();
        World.Destroy(parent);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => child.SetParent(parent, World));
    }

    [Fact]
    public void GetChildren_FiltersDestroyedEntities()
    {
        // Arrange
        var parent = World.Create();
        var child1 = World.Create();
        var child2 = World.Create();
        var child3 = World.Create();

        child1.SetParent(parent, World);
        child2.SetParent(parent, World);
        child3.SetParent(parent, World);

        // Destroy middle child
        World.Destroy(child2);

        // Act
        var children = parent.GetChildren(World).ToList();

        // Assert
        Assert.Equal(2, children.Count);
        Assert.Contains(child1, children);
        Assert.DoesNotContain(child2, children);
        Assert.Contains(child3, children);
    }

    [Fact]
    public void ChildrenComponent_CountPropertyReturnsCorrectValue()
    {
        // Arrange
        var parent = World.Create();
        var child1 = World.Create();
        var child2 = World.Create();

        child1.SetParent(parent, World);
        child2.SetParent(parent, World);

        // Act
        var children = parent.Get<Children>();

        // Assert
        Assert.Equal(2, children.Count);
    }
}
