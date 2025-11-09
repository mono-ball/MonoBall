using Arch.Core;
using PokeSharp.Core.Components.Relationships;
using PokeSharp.Core.Extensions;
using PokeSharp.Tests.ECS;
using System;
using System.Linq;
using Xunit;

namespace PokeSharp.Tests.ECS.Components.Relationships;

/// <summary>
/// Tests for Owner-Owned relationship functionality.
/// </summary>
public class OwnerOwnedTests : EcsTestBase
{
    [Fact]
    public void SetOwner_CreatesOwnedComponent()
    {
        // Arrange
        var owner = World.Create();
        var owned = World.Create();

        // Act
        owned.SetOwner(owner, World, OwnershipType.Permanent);

        // Assert
        Assert.True(owned.Has<Owned>());
        Assert.Equal(owner, owned.Get<Owned>().OwnerEntity);
    }

    [Fact]
    public void SetOwner_CreatesOwnerComponent()
    {
        // Arrange
        var owner = World.Create();
        var owned = World.Create();

        // Act
        owned.SetOwner(owner, World, OwnershipType.Permanent);

        // Assert
        Assert.True(owner.Has<Owner>());
        Assert.Equal(owned, owner.Get<Owner>().Value);
    }

    [Fact]
    public void SetOwner_AddsToOwnerCollection()
    {
        // Arrange
        var owner = World.Create();
        var item1 = World.Create();
        var item2 = World.Create();

        // Act
        item1.SetOwner(owner, World);
        item2.SetOwner(owner, World);

        // Assert
        var ownedEntities = owner.GetOwnedEntities(World).ToList();
        Assert.Equal(2, ownedEntities.Count);
        Assert.Contains(item1, ownedEntities);
        Assert.Contains(item2, ownedEntities);
    }

    [Theory]
    [InlineData(OwnershipType.Permanent)]
    [InlineData(OwnershipType.Temporary)]
    [InlineData(OwnershipType.Conditional)]
    [InlineData(OwnershipType.Shared)]
    public void SetOwner_SupportsAllOwnershipTypes(OwnershipType type)
    {
        // Arrange
        var owner = World.Create();
        var owned = World.Create();

        // Act
        owned.SetOwner(owner, World, type);

        // Assert
        var actualType = owned.GetOwnershipType(World);
        Assert.NotNull(actualType);
        Assert.Equal(type, actualType.Value);
    }

    [Fact]
    public void RemoveOwner_RemovesOwnedComponent()
    {
        // Arrange
        var owner = World.Create();
        var owned = World.Create();
        owned.SetOwner(owner, World);

        // Act
        owned.RemoveOwner(World);

        // Assert
        Assert.False(owned.Has<Owned>());
    }

    [Fact]
    public void RemoveOwner_RemovesOwnerComponent()
    {
        // Arrange
        var owner = World.Create();
        var owned = World.Create();
        owned.SetOwner(owner, World);

        // Act
        owned.RemoveOwner(World);

        // Assert
        Assert.False(owner.Has<Owner>());
    }

    [Fact]
    public void GetOwner_ReturnsOwnerEntity()
    {
        // Arrange
        var owner = World.Create();
        var owned = World.Create();
        owned.SetOwner(owner, World);

        // Act
        var result = owned.GetOwner(World);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(owner, result.Value);
    }

    [Fact]
    public void GetOwner_ReturnsNullWhenNoOwner()
    {
        // Arrange
        var owned = World.Create();

        // Act
        var result = owned.GetOwner(World);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetOwnedEntities_ReturnsAllOwnedEntities()
    {
        // Arrange
        var owner = World.Create();
        var item1 = World.Create();
        var item2 = World.Create();
        var item3 = World.Create();

        item1.SetOwner(owner, World);
        item2.SetOwner(owner, World);
        item3.SetOwner(owner, World);

        // Act
        var ownedEntities = owner.GetOwnedEntities(World).ToList();

        // Assert
        Assert.Equal(3, ownedEntities.Count);
        Assert.Contains(item1, ownedEntities);
        Assert.Contains(item2, ownedEntities);
        Assert.Contains(item3, ownedEntities);
    }

    [Fact]
    public void GetOwnedEntities_ReturnsEmptyWhenNoOwned()
    {
        // Arrange
        var owner = World.Create();

        // Act
        var ownedEntities = owner.GetOwnedEntities(World).ToList();

        // Assert
        Assert.Empty(ownedEntities);
    }

    [Fact]
    public void GetOwnershipType_ReturnsCorrectType()
    {
        // Arrange
        var owner = World.Create();
        var owned = World.Create();
        owned.SetOwner(owner, World, OwnershipType.Temporary);

        // Act
        var type = owned.GetOwnershipType(World);

        // Assert
        Assert.NotNull(type);
        Assert.Equal(OwnershipType.Temporary, type.Value);
    }

    [Fact]
    public void GetOwnershipType_ReturnsNullWhenNoOwner()
    {
        // Arrange
        var owned = World.Create();

        // Act
        var type = owned.GetOwnershipType(World);

        // Assert
        Assert.Null(type);
    }

    [Fact]
    public void SetOwner_RecordsAcquiredTimestamp()
    {
        // Arrange
        var owner = World.Create();
        var owned = World.Create();
        var before = DateTime.UtcNow;

        // Act
        owned.SetOwner(owner, World);

        // Assert
        var after = DateTime.UtcNow;
        var timestamp = owned.Get<Owned>().AcquiredAt;
        Assert.True(timestamp >= before && timestamp <= after,
            $"Timestamp {timestamp} should be between {before} and {after}");
    }

    [Fact]
    public void SetOwner_ThrowsWhenOwnedNotAlive()
    {
        // Arrange
        var owner = World.Create();
        var owned = World.Create();
        World.Destroy(owned);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => owned.SetOwner(owner, World));
    }

    [Fact]
    public void SetOwner_ThrowsWhenOwnerNotAlive()
    {
        // Arrange
        var owner = World.Create();
        var owned = World.Create();
        World.Destroy(owner);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => owned.SetOwner(owner, World));
    }

    [Fact]
    public void SetOwner_ReplacesExistingOwner()
    {
        // Arrange
        var owner1 = World.Create();
        var owner2 = World.Create();
        var owned = World.Create();
        owned.SetOwner(owner1, World);

        // Act
        owned.SetOwner(owner2, World);

        // Assert
        var currentOwner = owned.GetOwner(World);
        Assert.Equal(owner2, currentOwner);
    }

    [Fact]
    public void GetOwnedEntities_FiltersDestroyedEntities()
    {
        // Arrange
        var owner = World.Create();
        var item1 = World.Create();
        var item2 = World.Create();
        var item3 = World.Create();

        item1.SetOwner(owner, World);
        item2.SetOwner(owner, World);
        item3.SetOwner(owner, World);

        // Destroy middle item
        World.Destroy(item2);

        // Act
        var ownedEntities = owner.GetOwnedEntities(World).ToList();

        // Assert
        Assert.Equal(2, ownedEntities.Count);
        Assert.Contains(item1, ownedEntities);
        Assert.DoesNotContain(item2, ownedEntities);
        Assert.Contains(item3, ownedEntities);
    }

    [Fact]
    public void SetOwner_DefaultsToPermamentOwnership()
    {
        // Arrange
        var owner = World.Create();
        var owned = World.Create();

        // Act - No ownership type specified
        owned.SetOwner(owner, World);

        // Assert
        var type = owned.GetOwnershipType(World);
        Assert.Equal(OwnershipType.Permanent, type);
    }
}
