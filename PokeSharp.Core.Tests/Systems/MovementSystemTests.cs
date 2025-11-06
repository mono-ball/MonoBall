using Arch.Core;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components;
using PokeSharp.Core.Systems;

namespace PokeSharp.Core.Tests.Systems;

/// <summary>
///     Tests for MovementSystem with collision validation.
/// </summary>
public class MovementSystemTests
{
    [Fact]
    public void ProcessMovementRequest_WithValidMove_ShouldStartMovement()
    {
        // Arrange
        var world = World.Create();
        var spatialHashSystem = new SpatialHashSystem();
        spatialHashSystem.Initialize(world);

        var movementSystem = new MovementSystem();
        movementSystem.SetSpatialHashSystem(spatialHashSystem);
        movementSystem.Initialize(world);

        // Create NPC at (5, 5) with movement request
        var npc = world.Create(
            new Position(5, 5),
            new GridMovement(2.0f),
            new MovementRequest(Direction.Down) // Request move down
        );

        // Build spatial hash (empty - no blocking tiles)
        spatialHashSystem.Update(world, 0.016f);

        // Act
        movementSystem.Update(world, 0.016f);

        // Assert
        ref var movement = ref world.Get<GridMovement>(npc);
        Assert.True(movement.IsMoving, "NPC should have started moving");
        Assert.Equal(Direction.Down, movement.FacingDirection);
        Assert.False(
            world.Has<MovementRequest>(npc),
            "MovementRequest should be removed after processing"
        );
    }

    [Fact]
    public void ProcessMovementRequest_WithBlockedMove_ShouldNotStartMovement()
    {
        // Arrange
        var world = World.Create();
        var spatialHashSystem = new SpatialHashSystem();
        spatialHashSystem.Initialize(world);

        var movementSystem = new MovementSystem();
        movementSystem.SetSpatialHashSystem(spatialHashSystem);
        movementSystem.Initialize(world);

        // Create wall at (5, 6)
        var wall = world.Create(
            new TilePosition(5, 6),
            new TileSprite("tileset", 1, TileLayer.Ground, default),
            new Collision(true)
        );

        // Create NPC at (5, 5) with movement request to go down (into wall)
        var npc = world.Create(
            new Position(5, 5),
            new GridMovement(2.0f),
            new MovementRequest(Direction.Down)
        );

        // Build spatial hash
        spatialHashSystem.Update(world, 0.016f);

        // Act
        movementSystem.Update(world, 0.016f);

        // Assert
        ref var movement = ref world.Get<GridMovement>(npc);
        Assert.False(movement.IsMoving, "NPC should NOT have started moving (blocked by wall)");
        Assert.False(
            world.Has<MovementRequest>(npc),
            "MovementRequest should be removed even if blocked"
        );
    }

    [Fact]
    public void ProcessMovementRequest_WithLedge_ShouldJumpTwoTiles()
    {
        // Arrange
        var world = World.Create();
        var spatialHashSystem = new SpatialHashSystem();
        spatialHashSystem.Initialize(world);

        var movementSystem = new MovementSystem();
        movementSystem.SetSpatialHashSystem(spatialHashSystem);
        movementSystem.Initialize(world);

        // Create ledge at (5, 6) that allows jumping down
        var ledge = world.Create(
            new TilePosition(5, 6),
            new TileSprite("tileset", 8, TileLayer.Object, default),
            new Collision(true),
            new TileLedge(Direction.Down)
        );

        // Create NPC at (5, 5) requesting to move down
        var npc = world.Create(
            new Position(5, 5),
            new GridMovement(2.0f),
            new MovementRequest(Direction.Down)
        );

        // Build spatial hash
        spatialHashSystem.Update(world, 0.016f);

        // Act
        movementSystem.Update(world, 0.016f);

        // Assert
        ref var movement = ref world.Get<GridMovement>(npc);
        ref var position = ref world.Get<Position>(npc);

        Assert.True(movement.IsMoving, "NPC should be jumping");

        // Target should be 2 tiles down (5, 7) because of ledge jump
        var expectedTarget = new Vector2(5 * 16, 7 * 16);
        Assert.Equal(expectedTarget, movement.TargetPosition);
    }

    [Fact]
    public void ProcessMovementRequest_WithLedgeWrongDirection_ShouldNotJump()
    {
        // Arrange
        var world = World.Create();
        var spatialHashSystem = new SpatialHashSystem();
        spatialHashSystem.Initialize(world);

        var movementSystem = new MovementSystem();
        movementSystem.SetSpatialHashSystem(spatialHashSystem);
        movementSystem.Initialize(world);

        // Create ledge at (5, 6) that only allows jumping down
        var ledge = world.Create(
            new TilePosition(5, 6),
            new TileSprite("tileset", 8, TileLayer.Object, default),
            new Collision(true),
            new TileLedge(Direction.Down) // Can only jump down
        );

        // Create NPC at (5, 7) requesting to move up (try to climb ledge)
        var npc = world.Create(
            new Position(5, 7),
            new GridMovement(2.0f),
            new MovementRequest(Direction.Up) // Wrong direction!
        );

        // Build spatial hash
        spatialHashSystem.Update(world, 0.016f);

        // Act
        movementSystem.Update(world, 0.016f);

        // Assert
        ref var movement = ref world.Get<GridMovement>(npc);
        Assert.False(movement.IsMoving, "NPC should NOT move (can't climb up ledge)");
    }

    [Fact]
    public void MultiplNPCs_CanRequestMovementIndependently()
    {
        // Arrange
        var world = World.Create();
        var spatialHashSystem = new SpatialHashSystem();
        spatialHashSystem.Initialize(world);

        var movementSystem = new MovementSystem();
        movementSystem.SetSpatialHashSystem(spatialHashSystem);
        movementSystem.Initialize(world);

        // Create two NPCs
        var npc1 = world.Create(
            new Position(3, 3),
            new GridMovement(2.0f),
            new MovementRequest(Direction.Right)
        );

        var npc2 = world.Create(
            new Position(10, 8),
            new GridMovement(3.0f),
            new MovementRequest(Direction.Left)
        );

        // Build spatial hash
        spatialHashSystem.Update(world, 0.016f);

        // Act
        movementSystem.Update(world, 0.016f);

        // Assert
        ref var movement1 = ref world.Get<GridMovement>(npc1);
        ref var movement2 = ref world.Get<GridMovement>(npc2);

        Assert.True(movement1.IsMoving, "NPC1 should be moving");
        Assert.True(movement2.IsMoving, "NPC2 should be moving");
        Assert.Equal(Direction.Right, movement1.FacingDirection);
        Assert.Equal(Direction.Left, movement2.FacingDirection);
    }
}
