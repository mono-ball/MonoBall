using PokeSharp.Engine.Core.Components.Movement;
using PokeSharp.Engine.Core.Components.Rendering;
using PokeSharp.Engine.Core.Pooling;
using Xunit;

namespace PokeSharp.Tests.Pooling;

/// <summary>
///     Tests for component pooling system.
/// </summary>
public class ComponentPoolTests
{
    [Fact]
    public void ComponentPool_RentAndReturn_ReusesComponents()
    {
        // Arrange
        var pool = new ComponentPool<Position>(maxSize: 100);

        // Act - Rent and return to build up pool
        for (int i = 0; i < 50; i++)
        {
            var pos = pool.Rent();
            pool.Return(pos);
        }

        // Assert - Should have high reuse rate
        Assert.True(pool.ReuseRate > 0.9f, $"Expected reuse rate > 90%, got {pool.ReuseRate:P1}");
        Assert.Equal(50, pool.TotalRented);
        Assert.InRange(pool.TotalCreated, 1, 10); // Should create far fewer than rented
    }

    [Fact]
    public void ComponentPool_RentWhenEmpty_CreatesNew()
    {
        // Arrange
        var pool = new ComponentPool<Position>(maxSize: 100);

        // Act
        var position = pool.Rent();

        // Assert
        Assert.NotNull(position);
        Assert.Equal(1, pool.TotalCreated);
        Assert.Equal(1, pool.TotalRented);
        Assert.Equal(0, pool.Count); // Pool empty
    }

    [Fact]
    public void ComponentPool_ReturnWhenFull_DiscardsComponent()
    {
        // Arrange
        var pool = new ComponentPool<Position>(maxSize: 2);

        // Act - Fill pool to max
        var pos1 = pool.Rent();
        var pos2 = pool.Rent();
        pool.Return(pos1);
        pool.Return(pos2);

        Assert.Equal(2, pool.Count);

        // Act - Return one more (should be discarded)
        var pos3 = pool.Rent();
        pool.Return(pos3);

        // Assert - Count should still be max size
        Assert.Equal(2, pool.Count);
    }

    [Fact]
    public void ComponentPool_ResetsComponentToDefault()
    {
        // Arrange
        var pool = new ComponentPool<Position>(maxSize: 10);

        // Act - Modify and return
        var position = pool.Rent();
        position.X = 100;
        position.Y = 200;
        pool.Return(position);

        // Rent again
        var newPosition = pool.Rent();

        // Assert - Should be reset to default
        Assert.Equal(0, newPosition.X);
        Assert.Equal(0, newPosition.Y);
    }

    [Fact]
    public void ComponentPool_GetStatistics_ReturnsAccurateData()
    {
        // Arrange
        var pool = new ComponentPool<Position>(maxSize: 100);

        // Act
        for (int i = 0; i < 10; i++)
        {
            var pos = pool.Rent();
            if (i % 2 == 0) // Return every other one
                pool.Return(pos);
        }

        var stats = pool.GetStatistics();

        // Assert
        Assert.Equal("Position", stats.ComponentType);
        Assert.Equal(10, stats.TotalRented);
        Assert.InRange(stats.ReuseRate, 0f, 1f);
        Assert.InRange(stats.UtilizationRate, 0f, 1f);
    }

    [Fact]
    public void ComponentPool_ThreadSafety_HandlesParallelOperations()
    {
        // Arrange
        var pool = new ComponentPool<Position>(maxSize: 1000);
        var tasks = new List<Task>();

        // Act - Multiple threads renting and returning
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(
                Task.Run(() =>
                {
                    for (int j = 0; j < 100; j++)
                    {
                        var pos = pool.Rent();
                        Thread.Sleep(1); // Simulate work
                        pool.Return(pos);
                    }
                })
            );
        }

        Task.WaitAll(tasks.ToArray());

        // Assert - No exceptions, consistent counts
        Assert.Equal(1000, pool.TotalRented);
        Assert.Equal(1000, pool.TotalReturned);
    }
}

/// <summary>
///     Tests for ComponentPoolManager.
/// </summary>
public class ComponentPoolManagerTests
{
    [Fact]
    public void ComponentPoolManager_Initialize_CreatesDefaultPools()
    {
        // Arrange & Act
        var manager = new ComponentPoolManager(logger: null, enableStatistics: true);

        // Assert - Should have pre-configured pools
        var stats = manager.GetAllStatistics();
        Assert.Equal(5, stats.Count); // Position, GridMovement, Velocity, Sprite, Animation

        Assert.Contains(stats, s => s.ComponentType == "Position");
        Assert.Contains(stats, s => s.ComponentType == "GridMovement");
        Assert.Contains(stats, s => s.ComponentType == "Velocity");
        Assert.Contains(stats, s => s.ComponentType == "Sprite");
        Assert.Contains(stats, s => s.ComponentType == "Animation");
    }

    [Fact]
    public void ComponentPoolManager_RentAndReturn_WorksForAllTypes()
    {
        // Arrange
        var manager = new ComponentPoolManager();

        // Act & Assert - Position
        var pos = manager.RentPosition();
        Assert.NotNull(pos);
        manager.ReturnPosition(pos);

        // Act & Assert - GridMovement
        var movement = manager.RentGridMovement();
        Assert.NotNull(movement);
        manager.ReturnGridMovement(movement);

        // Act & Assert - Velocity
        var velocity = manager.RentVelocity();
        Assert.NotNull(velocity);
        manager.ReturnVelocity(velocity);

        // Act & Assert - Sprite
        var sprite = manager.RentSprite();
        Assert.NotNull(sprite);
        manager.ReturnSprite(sprite);

        // Act & Assert - Animation
        var animation = manager.RentAnimation();
        Assert.NotNull(animation);
        manager.ReturnAnimation(animation);
    }

    [Fact]
    public void ComponentPoolManager_GetPool_ReturnsExistingOrCreatesNew()
    {
        // Arrange
        var manager = new ComponentPoolManager();

        // Act - Get existing pool
        var positionPool = manager.GetPool<Position>();
        Assert.NotNull(positionPool);

        // Get same pool again
        var positionPool2 = manager.GetPool<Position>();
        Assert.Same(positionPool, positionPool2); // Should be same instance

        // Get custom pool (not pre-configured)
        var customPool = manager.GetPool<TestComponent>(maxSize: 500);
        Assert.NotNull(customPool);
    }

    [Fact]
    public void ComponentPoolManager_GenerateReport_ReturnsFormattedStatistics()
    {
        // Arrange
        var manager = new ComponentPoolManager();

        // Act - Use some pools
        for (int i = 0; i < 100; i++)
        {
            var pos = manager.RentPosition();
            manager.ReturnPosition(pos);
        }

        var report = manager.GenerateReport();

        // Assert
        Assert.Contains("Component Pool Statistics", report);
        Assert.Contains("Position", report);
        Assert.Contains("Reuse Rate", report);
        Assert.Contains("Overall Summary", report);
        Assert.Contains("Estimated Memory Saved", report);
    }

    [Fact]
    public void ComponentPoolManager_ClearAll_RemovesAllPooledComponents()
    {
        // Arrange
        var manager = new ComponentPoolManager();

        // Act - Build up pools
        for (int i = 0; i < 50; i++)
        {
            manager.ReturnPosition(manager.RentPosition());
            manager.ReturnGridMovement(manager.RentGridMovement());
        }

        var statsBefore = manager.GetAllStatistics();
        var totalBefore = statsBefore.Sum(s => s.AvailableCount);
        Assert.True(totalBefore > 0);

        // Clear all
        manager.ClearAll();

        // Assert - All pools should be empty
        var statsAfter = manager.GetAllStatistics();
        var totalAfter = statsAfter.Sum(s => s.AvailableCount);
        Assert.Equal(0, totalAfter);
    }

    [Fact]
    public void ComponentPoolManager_HighFrequencyUsage_MaintainsGoodReuseRate()
    {
        // Arrange
        var manager = new ComponentPoolManager();

        // Act - Simulate game loop with frequent position updates
        for (int frame = 0; frame < 100; frame++)
        {
            for (int entity = 0; entity < 50; entity++)
            {
                var pos = manager.RentPosition();
                pos.X = entity;
                pos.Y = frame;
                // Simulate usage
                var _ = pos.X + pos.Y;
                manager.ReturnPosition(pos);
            }
        }

        // Assert
        var stats = manager.GetAllStatistics();
        var positionStats = stats.First(s => s.ComponentType == "Position");

        Assert.Equal(5000, positionStats.TotalRented);
        Assert.True(
            positionStats.ReuseRate > 0.95f,
            $"Expected reuse rate > 95%, got {positionStats.ReuseRate:P1}"
        );
        Assert.InRange(positionStats.TotalCreated, 1, 100); // Should create far fewer
    }

    // Test component for custom pool testing
    private struct TestComponent
    {
        public int Value;
    }
}

/// <summary>
///     Integration tests with component types.
/// </summary>
public class ComponentPoolIntegrationTests
{
    [Fact]
    public void Position_PoolingWorkflow_MaintainsCorrectState()
    {
        // Arrange
        var manager = new ComponentPoolManager();

        // Act - Simulate movement calculation
        var startPos = manager.RentPosition();
        startPos.X = 10;
        startPos.Y = 20;
        startPos.PixelX = 160;
        startPos.PixelY = 320;

        var endPos = manager.RentPosition();
        endPos.X = 12;
        endPos.Y = 22;
        endPos.PixelX = 192;
        endPos.PixelY = 352;

        // Calculate interpolated position
        var interpolated = manager.RentPosition();
        interpolated.X = (startPos.X + endPos.X) / 2;
        interpolated.Y = (startPos.Y + endPos.Y) / 2;
        interpolated.PixelX = (startPos.PixelX + endPos.PixelX) / 2;
        interpolated.PixelY = (startPos.PixelY + endPos.PixelY) / 2;

        // Assert
        Assert.Equal(11, interpolated.X);
        Assert.Equal(21, interpolated.Y);
        Assert.Equal(176, interpolated.PixelX);
        Assert.Equal(336, interpolated.PixelY);

        // Cleanup
        manager.ReturnPosition(startPos);
        manager.ReturnPosition(endPos);
        manager.ReturnPosition(interpolated);
    }

    [Fact]
    public void Animation_PoolingWithReferenceTypes_HandlesHashSetCorrectly()
    {
        // Arrange
        var manager = new ComponentPoolManager();

        // Act
        var anim = manager.RentAnimation();
        anim.CurrentAnimation = "walk_down";
        anim.CurrentFrame = 5;
        anim.FrameTimer = 0.5f;
        anim.IsPlaying = true;

        // Add some triggered frames using bit operations
        anim.TriggeredEventFrames |= (1UL << 1); // Set bit 1
        anim.TriggeredEventFrames |= (1UL << 3); // Set bit 3

        // Return to pool (should reset)
        manager.ReturnAnimation(anim);

        // Rent again
        var newAnim = manager.RentAnimation();

        // Assert - Should be reset
        Assert.Equal(default(string), newAnim.CurrentAnimation);
        Assert.Equal(0, newAnim.CurrentFrame);
        Assert.False(newAnim.IsPlaying);
    }

    [Fact]
    public void GridMovement_PoolingForComplexStruct_MaintainsStructure()
    {
        // Arrange
        var manager = new ComponentPoolManager();

        // Act
        var movement = manager.RentGridMovement();
        movement.IsMoving = true;
        movement.MovementSpeed = 4.5f;
        movement.FacingDirection = Direction.East;
        movement.MovementProgress = 0.75f;

        var movementSpeed = movement.MovementSpeed;
        var direction = movement.FacingDirection;

        // Return and re-rent
        manager.ReturnGridMovement(movement);
        var newMovement = manager.RentGridMovement();

        // Assert - Should be reset to default
        Assert.False(newMovement.IsMoving);
        Assert.Equal(0f, newMovement.MovementSpeed);
        Assert.Equal(Direction.None, newMovement.FacingDirection);
    }
}
