using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Systems;
using PokeSharp.Tests.ECS.Systems;
using PokeSharp.Tests.ECS.TestUtilities;
using Xunit;

namespace PokeSharp.Tests.ECS.Components;

/// <summary>
///     Example tests demonstrating how to use the ECS testing infrastructure.
///     Tests the MovementSystem to show testing patterns.
/// </summary>
public class MovementSystemTests : SystemTestBase<MovementSystem>
{
    private SpatialHashSystem? _spatialHashSystem;

    protected override MovementSystem CreateSystem()
    {
        return new MovementSystem();
    }

    protected override void ConfigureAdditionalSystems(SystemManager manager)
    {
        // MovementSystem depends on SpatialHashSystem
        _spatialHashSystem = new SpatialHashSystem();
        manager.RegisterSystem(_spatialHashSystem);
    }

    [Fact]
    public void System_Should_Initialize_Successfully()
    {
        // Act
        InitializeSystem();

        // Assert
        AssertSystemEnabled();
        Assert.True(IsInitialized);
    }

    [Fact]
    public void Entity_Should_Complete_Movement_After_Multiple_Updates()
    {
        // Arrange
        InitializeSystemViaManager();
        var entity = EcsTestHelpers.CreateMovableEntity(World, 5, 5);

        // Connect systems
        System.SetSpatialHashSystem(_spatialHashSystem!);

        // Add movement request
        EcsTestHelpers.AddMovementRequest(World, entity, Direction.Right);

        // Act - Update for one frame to start movement
        UpdateAllSystems(0.016f);

        // Assert - Entity should be moving
        EcsTestHelpers.AssertIsMoving(World, entity);
        EcsTestHelpers.AssertFacingDirection(World, entity, Direction.Right);

        // Act - Update for enough frames to complete movement
        // Movement speed is 4.0 tiles/sec, so at 60fps (0.016s per frame):
        // Time to move 1 tile = 1/4 = 0.25 seconds = 15.625 frames
        UpdateAllSystemsForFrames(20, 0.016f);

        // Assert - Entity should have completed movement
        EcsTestHelpers.AssertIsNotMoving(World, entity);
        EcsTestHelpers.AssertPosition(World, entity, 6, 5);
    }

    [Fact]
    public void Entity_Should_Interpolate_Position_During_Movement()
    {
        // Arrange
        InitializeSystemViaManager();
        var entity = EcsTestHelpers.CreateMovableEntity(World, 0, 0);
        System.SetSpatialHashSystem(_spatialHashSystem!);

        EcsTestHelpers.AddMovementRequest(World, entity, Direction.Right);
        UpdateAllSystems(0.016f); // Start movement

        // Act - Update halfway through movement
        UpdateAllSystemsForFrames(8, 0.016f); // ~50% progress

        // Assert - Pixel position should be interpolated
        var position = EcsTestHelpers.GetPosition(World, entity);
        Assert.True(position.PixelX > 0 && position.PixelX < 16,
            $"Expected pixel position between 0 and 16, got {position.PixelX}");

        // Grid position should already be at target
        Assert.Equal(1, position.X);
    }

    [Fact]
    public void Entity_Should_Not_Move_When_System_Is_Disabled()
    {
        // Arrange
        InitializeSystemViaManager();
        var entity = EcsTestHelpers.CreateMovableEntity(World, 0, 0);
        System.SetSpatialHashSystem(_spatialHashSystem!);

        EcsTestHelpers.AddMovementRequest(World, entity, Direction.Right);

        // Act - Disable system and update
        DisableSystem();
        UpdateAllSystemsForFrames(20, 0.016f);

        // Assert - Entity should not have moved
        EcsTestHelpers.AssertPosition(World, entity, 0, 0);
    }

    [Fact]
    public void System_Performance_Should_Be_Acceptable()
    {
        // Arrange
        InitializeSystemViaManager();
        var entities = new List<Arch.Core.Entity>();

        // Create 100 moving entities
        for (int i = 0; i < 100; i++)
        {
            var entity = EcsTestHelpers.CreateMovableEntity(World, i % 10, i / 10);
            entities.Add(entity);
        }

        // Start all entities moving
        foreach (var entity in entities)
        {
            EcsTestHelpers.AddMovementRequest(World, entity, Direction.Right);
        }

        // Act - Update multiple times
        UpdateAllSystemsForFrames(30, 0.016f);

        // Assert - System should complete in reasonable time (< 5ms average)
        AssertSystemHasProcessedEntities();
        AssertSystemPerformance(5.0);
    }

    [Fact]
    public void Multiple_Entities_Should_Move_Independently()
    {
        // Arrange
        InitializeSystemViaManager();
        System.SetSpatialHashSystem(_spatialHashSystem!);

        var entity1 = EcsTestHelpers.CreateMovableEntity(World, 0, 0);
        var entity2 = EcsTestHelpers.CreateMovableEntity(World, 5, 5);

        EcsTestHelpers.AddMovementRequest(World, entity1, Direction.Right);
        EcsTestHelpers.AddMovementRequest(World, entity2, Direction.Up);

        // Act
        UpdateAllSystemsForFrames(20, 0.016f);

        // Assert
        EcsTestHelpers.AssertPosition(World, entity1, 1, 0);
        EcsTestHelpers.AssertPosition(World, entity2, 5, 4);
    }

    [Fact]
    public void Locked_Movement_Should_Prevent_New_Movement()
    {
        // Arrange
        InitializeSystemViaManager();
        System.SetSpatialHashSystem(_spatialHashSystem!);

        var entity = EcsTestHelpers.CreateMovableEntity(World, 0, 0);

        // Lock movement
        ref var movement = ref World.Get<GridMovement>(entity);
        movement.MovementLocked = true;

        // Act - Try to move
        EcsTestHelpers.AddMovementRequest(World, entity, Direction.Right);
        UpdateAllSystemsForFrames(20, 0.016f);

        // Assert - Entity should not have moved
        EcsTestHelpers.AssertPosition(World, entity, 0, 0);
    }
}
