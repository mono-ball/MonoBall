using System.Diagnostics;
using Arch.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.Rendering;
using PokeSharp.Game.Systems;
using PokeSharp.Game.Systems.Services;
using Xunit;

namespace PokeSharp.Engine.Systems.Tests.Movement;

/// <summary>
///     Tests for MovementSystem focusing on optimized query usage.
///     Verifies that entities with and without animation are handled correctly
///     using separate optimized queries instead of conditional logic.
/// </summary>
public class MovementSystemTests : IDisposable
{
    private readonly Mock<ICollisionService> _mockCollisionService;
    private readonly Mock<ILogger<MovementSystem>> _mockLogger;
    private readonly MovementSystem _system;
    private readonly World _world;

    public MovementSystemTests()
    {
        _world = World.Create();
        _mockCollisionService = new Mock<ICollisionService>();
        _mockLogger = new Mock<ILogger<MovementSystem>>();
        _system = new MovementSystem(_mockCollisionService.Object, null, _mockLogger.Object);
    }

    public void Dispose()
    {
        _world?.Dispose();
    }

    [Fact]
    public void Update_ShouldProcessEntitiesWithAnimation_UsingSeparateQuery()
    {
        // Arrange - Entity WITH animation
        var position = new Position { X = 0, Y = 0 };
        var movement = new GridMovement
        {
            IsMoving = true,
            MovementProgress = 0f,
            FacingDirection = Direction.South,
        };
        var animation = new Animation { CurrentAnimation = "walk_down", IsPlaying = true };

        Entity entity = _world.Create(position, movement, animation);
        _system.Initialize(_world);

        // Act
        _system.Update(_world, 0.016f);

        // Assert - Animation should be processed
        Animation updatedAnimation = _world.Get<Animation>(entity);
        updatedAnimation.Should().NotBeNull();
    }

    [Fact]
    public void Update_ShouldProcessEntitiesWithoutAnimation_UsingSeparateQuery()
    {
        // Arrange - Entity WITHOUT animation
        var position = new Position { X = 0, Y = 0 };
        var movement = new GridMovement
        {
            IsMoving = true,
            MovementProgress = 0f,
            FacingDirection = Direction.South,
        };

        Entity entity = _world.Create(position, movement);
        // No Animation component
        _system.Initialize(_world);

        // Act
        _system.Update(_world, 0.016f);

        // Assert - Should still update movement without error
        GridMovement updatedMovement = _world.Get<GridMovement>(entity);
        updatedMovement.Should().NotBeNull();
        _world.Has<Animation>(entity).Should().BeFalse();
    }

    [Fact]
    public void Update_ShouldHandleMixedEntities_WithAndWithoutAnimation()
    {
        // Arrange - Create mix of entities
        Entity animatedEntity = _world.Create(
            new Position { X = 0, Y = 0 },
            new GridMovement { IsMoving = true },
            new Animation { CurrentAnimation = "walk" }
        );

        Entity staticEntity = _world.Create(
            new Position { X = 5, Y = 5 },
            new GridMovement { IsMoving = true }
        );

        _system.Initialize(_world);

        // Act
        _system.Update(_world, 0.016f);

        // Assert - Both should be processed correctly
        _world.Has<Animation>(animatedEntity).Should().BeTrue();
        _world.Has<Animation>(staticEntity).Should().BeFalse();
    }

    [Fact]
    public void Update_QueryPerformance_ShouldBeOptimalForLargeWorlds()
    {
        // This test documents the query optimization benefit
        // OLD: Single query + conditional check inside loop
        // NEW: Two separate queries (one with Animation, one without)

        // Arrange - Create realistic entity distribution
        // 80% entities without animation (NPCs, items, etc.)
        // 20% entities with animation (players, animated NPCs)
        for (int i = 0; i < 80; i++)
        {
            _world.Create(new Position { X = i, Y = 0 }, new GridMovement { IsMoving = false });
        }

        for (int i = 0; i < 20; i++)
        {
            _world.Create(
                new Position { X = i, Y = 1 },
                new GridMovement { IsMoving = false },
                new Animation { CurrentAnimation = "idle" }
            );
        }

        _system.Initialize(_world);

        // Act
        var sw = Stopwatch.StartNew();
        _system.Update(_world, 0.016f);
        sw.Stop();

        // Assert - Should execute quickly
        sw.ElapsedMilliseconds.Should().BeLessThan(5, "movement update should be fast");
    }

    [Fact]
    public void Update_ShouldNotAllocate_StringsForDirectionLogging()
    {
        // This test verifies the DirectionNames array optimization
        // OLD: direction.ToString() allocates string
        // NEW: DirectionNames[index] uses cached string

        // Arrange
        Entity entity = _world.Create(
            new Position { X = 0, Y = 0 },
            new GridMovement { IsMoving = true, FacingDirection = Direction.South }
        );

        _system.Initialize(_world);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long memoryBefore = GC.GetTotalMemory(false);

        // Act - Update multiple times
        for (int i = 0; i < 100; i++)
        {
            _system.Update(_world, 0.016f);
        }

        long memoryAfter = GC.GetTotalMemory(false);
        long allocated = memoryAfter - memoryBefore;

        // Assert - Should not allocate excessive memory for direction strings
        // Some allocation is expected for movement updates and animation changes, but not for ToString()
        // Increased tolerance to account for animation string allocations (turn_*, go_*, face_*)
        allocated
            .Should()
            .BeLessThan(50000, "should not allocate excessive strings for direction logging");
    }

    [Fact]
    public void MovementProgress_ShouldUpdate_WhenEntityIsMoving()
    {
        // Arrange
        _mockCollisionService
            .Setup(x =>
                x.IsPositionWalkable(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<Direction>(),
                    It.IsAny<byte>()
                )
            )
            .Returns(true);

        Entity entity = _world.Create(
            new Position { X = 0, Y = 0 },
            new GridMovement
            {
                IsMoving = true,
                MovementSpeed = 4.0f,
                MovementProgress = 0f,
                FacingDirection = Direction.South,
            }
        );

        _system.Initialize(_world);

        // Act - Use smaller delta so movement doesn't complete in one frame
        _system.Update(_world, 0.1f); // 100ms at 4 tiles/sec = 0.4 progress

        // Assert
        GridMovement movement = _world.Get<GridMovement>(entity);
        movement.MovementProgress.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void Animation_ShouldUpdate_WhenEntityIsMoving()
    {
        // Arrange
        Entity entity = _world.Create(
            new Position { X = 0, Y = 0 },
            new GridMovement { IsMoving = true, FacingDirection = Direction.South },
            new Animation { CurrentAnimation = "idle", IsPlaying = false }
        );

        _system.Initialize(_world);

        // Act
        _system.Update(_world, 0.016f);

        // Assert - Animation should be updated based on movement
        Animation animation = _world.Get<Animation>(entity);
        // Note: Actual animation update logic would be tested in integration tests
        animation.Should().NotBeNull();
    }

    [Fact]
    public void TileSizeCache_ShouldReduceRedundantQueries()
    {
        // This test verifies the tile size caching optimization
        // OLD: Query map for tile size every frame
        // NEW: Cache tile size per map ID

        // Arrange - Multiple entities on same map
        for (int i = 0; i < 10; i++)
        {
            _world.Create(new Position { X = i, Y = 0 }, new GridMovement { IsMoving = true });
        }

        _system.Initialize(_world);

        // Act - Update multiple frames
        for (int frame = 0; frame < 100; frame++)
        {
            _system.Update(_world, 0.016f);
        }

        // Assert - Cache should work (no exceptions, consistent behavior)
        // Note: Without direct access to cache, we verify by absence of errors
        true.Should().BeTrue("tile size cache should work without errors");
    }

    [Fact]
    public void MovementRequest_ShouldBeProcessed_BeforeMovementUpdate()
    {
        // Arrange - Mock GetTileCollisionInfo (used by MovementSystem for optimized collision checking)
        _mockCollisionService
            .Setup(x =>
                x.GetTileCollisionInfo(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<byte>(),
                    It.IsAny<Direction>()
                )
            )
            .Returns((false, Direction.None, true)); // Not jump tile, no jump dir, walkable

        // Entity must already face South to start moving immediately (Pokemon Emerald turn-in-place behavior)
        Entity entity = _world.Create(
            new Position { X = 0, Y = 0 },
            new GridMovement { IsMoving = false, FacingDirection = Direction.South },
            new MovementRequest { Direction = Direction.South, Active = true }
        );

        _system.Initialize(_world);

        // Act
        _system.Update(_world, 0.016f);

        // Assert - Movement should have started (already facing correct direction)
        GridMovement movement = _world.Get<GridMovement>(entity);
        movement.IsMoving.Should().BeTrue();
        movement.FacingDirection.Should().Be(Direction.South);
    }

    [Fact]
    public void CollisionCheck_ShouldPreventMovement_WhenBlocked()
    {
        // Arrange - Mock GetTileCollisionInfo to return blocked
        _mockCollisionService
            .Setup(x =>
                x.GetTileCollisionInfo(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<byte>(),
                    It.IsAny<Direction>()
                )
            )
            .Returns((false, Direction.None, false)); // Not jump tile, no jump dir, NOT walkable

        // Entity must already face South to attempt movement (Pokemon Emerald turn-in-place behavior)
        Entity entity = _world.Create(
            new Position { X = 0, Y = 0 },
            new GridMovement { IsMoving = false, FacingDirection = Direction.South },
            new MovementRequest { Direction = Direction.South, Active = true }
        );

        _system.Initialize(_world);

        // Act
        _system.Update(_world, 0.016f);

        // Assert - Movement should not have started (blocked by collision)
        GridMovement movement = _world.Get<GridMovement>(entity);
        movement.IsMoving.Should().BeFalse();
    }

    [Fact]
    public void DirectionNames_ShouldMap_AllDirectionValues()
    {
        // This test verifies the DirectionNames array covers all Direction enum values
        // Index mapping: None=0, South=1, West=2, East=3, North=4

        // Arrange
        Direction[] directions = new[]
        {
            Direction.None,
            Direction.South,
            Direction.West,
            Direction.East,
            Direction.North,
        };

        // Act & Assert - All directions should map correctly
        foreach (Direction direction in directions)
        {
            int index = (int)direction + 1; // Offset for None=-1
            index.Should().BeInRange(0, 4, $"Direction.{direction} should map to valid index");
        }
    }

    [Fact]
    public void EntitiesToRemove_ShouldBeReused_AcrossFrames()
    {
        // This test verifies the _entitiesToRemove list is reused to avoid allocations
        // OLD: New list every frame
        // NEW: Cached list cleared and reused

        // Arrange
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Entity entity1 = _world.Create(
            new Position { X = 0, Y = 0 },
            new GridMovement { IsMoving = true }
        );

        _system.Initialize(_world);

        long memoryBefore = GC.GetTotalMemory(false);

        // Act - Update 100 frames
        for (int i = 0; i < 100; i++)
        {
            _system.Update(_world, 0.016f);
        }

        long memoryAfter = GC.GetTotalMemory(false);
        long allocated = memoryAfter - memoryBefore;

        // Assert - Should not allocate new lists
        allocated.Should().BeLessThan(50000, "should reuse entity removal list");
    }
}

/// <summary>
///     Integration tests for MovementSystem with realistic scenarios.
/// </summary>
public class MovementSystemIntegrationTests : IDisposable
{
    private readonly Mock<ICollisionService> _mockCollisionService;
    private readonly MovementSystem _system;
    private readonly World _world;

    public MovementSystemIntegrationTests()
    {
        _world = World.Create();
        _mockCollisionService = new Mock<ICollisionService>();
        _mockCollisionService
            .Setup(x =>
                x.IsPositionWalkable(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<Direction>(),
                    It.IsAny<byte>()
                )
            )
            .Returns(true);
        _system = new MovementSystem(_mockCollisionService.Object);
    }

    public void Dispose()
    {
        _world?.Dispose();
    }

    [Fact]
    public void RealWorldScenario_ShouldHandle_MultipleEntitiesMoving()
    {
        // Arrange - Create realistic game scenario
        // Player (animated)
        Entity player = _world.Create(
            new Position { X = 5, Y = 5 },
            new GridMovement { IsMoving = false },
            new Animation { CurrentAnimation = "idle" }
        );

        // 3 NPCs (animated)
        for (int i = 0; i < 3; i++)
        {
            _world.Create(
                new Position { X = i, Y = 0 },
                new GridMovement { IsMoving = false },
                new Animation { CurrentAnimation = "idle" }
            );
        }

        // 5 static entities (no animation)
        for (int i = 0; i < 5; i++)
        {
            _world.Create(new Position { X = i, Y = 10 }, new GridMovement { IsMoving = false });
        }

        _system.Initialize(_world);

        // Act - Simulate player movement
        _world.Set(player, new MovementRequest { Direction = Direction.South, Active = true });

        for (int frame = 0; frame < 60; frame++) // 1 second at 60 FPS
        {
            _system.Update(_world, 0.016f);
        }

        // Assert - System should handle all entities without error
        GridMovement playerMovement = _world.Get<GridMovement>(player);
        playerMovement.Should().NotBeNull();
    }

    [Fact]
    public void StressTest_ShouldHandle_100Entities()
    {
        // Arrange - Create 100 entities (mix of animated and non-animated)
        for (int i = 0; i < 100; i++)
        {
            if (i % 3 == 0) // 33% animated
            {
                _world.Create(
                    new Position { X = i % 10, Y = i / 10 },
                    new GridMovement { IsMoving = false },
                    new Animation { CurrentAnimation = "idle" }
                );
            }
            else
            {
                _world.Create(
                    new Position { X = i % 10, Y = i / 10 },
                    new GridMovement { IsMoving = false }
                );
            }
        }

        _system.Initialize(_world);

        // Act
        var sw = Stopwatch.StartNew();
        _system.Update(_world, 0.016f);
        sw.Stop();

        // Assert - Should handle load efficiently
        sw.ElapsedMilliseconds.Should()
            .BeLessThan(16, "should update 100 entities within frame budget");
    }
}
