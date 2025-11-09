using Arch.Core;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components.Common;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Components.Rendering;
using Xunit;

namespace PokeSharp.Tests.ECS.TestUtilities;

/// <summary>
///     Static helper methods for ECS testing.
///     Provides utilities for entity creation, component manipulation, and assertions.
/// </summary>
public static class EcsTestHelpers
{
    #region Entity Creation Helpers

    /// <summary>
    ///     Creates an entity with a Position component.
    /// </summary>
    /// <param name="world">The world instance.</param>
    /// <param name="x">Grid X coordinate.</param>
    /// <param name="y">Grid Y coordinate.</param>
    /// <param name="mapId">Map identifier.</param>
    /// <param name="tileSize">Tile size in pixels.</param>
    /// <returns>The created entity.</returns>
    public static Entity CreateEntityWithPosition(World world, int x = 0, int y = 0, int mapId = 0, int tileSize = 16)
    {
        var entity = world.Create(new Position(x, y, mapId, tileSize));
        return entity;
    }

    /// <summary>
    ///     Creates an entity with Position and GridMovement components.
    /// </summary>
    /// <param name="world">The world instance.</param>
    /// <param name="x">Grid X coordinate.</param>
    /// <param name="y">Grid Y coordinate.</param>
    /// <param name="speed">Movement speed.</param>
    /// <param name="mapId">Map identifier.</param>
    /// <returns>The created entity.</returns>
    public static Entity CreateMovableEntity(World world, int x = 0, int y = 0, float speed = 4.0f, int mapId = 0)
    {
        var entity = world.Create(
            new Position(x, y, mapId),
            new GridMovement(speed)
        );
        return entity;
    }

    /// <summary>
    ///     Creates an entity with Position, GridMovement, and Animation components.
    /// </summary>
    /// <param name="world">The world instance.</param>
    /// <param name="x">Grid X coordinate.</param>
    /// <param name="y">Grid Y coordinate.</param>
    /// <param name="animationName">Initial animation name.</param>
    /// <param name="speed">Movement speed.</param>
    /// <returns>The created entity.</returns>
    public static Entity CreateAnimatedMovableEntity(
        World world,
        int x = 0,
        int y = 0,
        string animationName = "idle_down",
        float speed = 4.0f)
    {
        var entity = world.Create(
            new Position(x, y),
            new GridMovement(speed),
            new Animation { CurrentAnimation = animationName }
        );
        return entity;
    }

    /// <summary>
    ///     Creates an entity with a Tag component.
    /// </summary>
    /// <param name="world">The world instance.</param>
    /// <param name="tag">The tag value.</param>
    /// <returns>The created entity.</returns>
    public static Entity CreateTaggedEntity(World world, string tag)
    {
        var entity = world.Create(new Tag(tag));
        return entity;
    }

    #endregion

    #region Component Manipulation

    /// <summary>
    ///     Adds a MovementRequest component to an entity.
    /// </summary>
    /// <param name="world">The world instance.</param>
    /// <param name="entity">The target entity.</param>
    /// <param name="direction">The requested direction.</param>
    public static void AddMovementRequest(World world, Entity entity, Direction direction)
    {
        world.Add(entity, new MovementRequest(direction));
    }

    /// <summary>
    ///     Sets the position of an entity.
    /// </summary>
    /// <param name="world">The world instance.</param>
    /// <param name="entity">The target entity.</param>
    /// <param name="x">Grid X coordinate.</param>
    /// <param name="y">Grid Y coordinate.</param>
    public static void SetPosition(World world, Entity entity, int x, int y)
    {
        ref var position = ref world.Get<Position>(entity);
        position.X = x;
        position.Y = y;
        position.SyncPixelsToGrid();
    }

    /// <summary>
    ///     Gets the position of an entity.
    /// </summary>
    /// <param name="world">The world instance.</param>
    /// <param name="entity">The target entity.</param>
    /// <returns>The position component.</returns>
    public static Position GetPosition(World world, Entity entity)
    {
        return world.Get<Position>(entity);
    }

    /// <summary>
    ///     Gets the grid movement component of an entity.
    /// </summary>
    /// <param name="world">The world instance.</param>
    /// <param name="entity">The target entity.</param>
    /// <returns>The grid movement component.</returns>
    public static GridMovement GetGridMovement(World world, Entity entity)
    {
        return world.Get<GridMovement>(entity);
    }

    #endregion

    #region Query Helpers

    /// <summary>
    ///     Gets all entities with a specific component.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="world">The world instance.</param>
    /// <returns>List of entities with the component.</returns>
    public static List<Entity> GetEntitiesWithComponent<T>(World world) where T : struct
    {
        var entities = new List<Entity>();
        var query = new QueryDescription().WithAll<T>();
        world.Query(in query, (Entity entity) => entities.Add(entity));
        return entities;
    }

    /// <summary>
    ///     Gets all entities with two specific components.
    /// </summary>
    /// <typeparam name="T1">First component type.</typeparam>
    /// <typeparam name="T2">Second component type.</typeparam>
    /// <param name="world">The world instance.</param>
    /// <returns>List of entities with both components.</returns>
    public static List<Entity> GetEntitiesWithComponents<T1, T2>(World world)
        where T1 : struct
        where T2 : struct
    {
        var entities = new List<Entity>();
        var query = new QueryDescription().WithAll<T1, T2>();
        world.Query(in query, (Entity entity) => entities.Add(entity));
        return entities;
    }

    /// <summary>
    ///     Counts entities with a specific component.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="world">The world instance.</param>
    /// <returns>Number of entities with the component.</returns>
    public static int CountEntitiesWithComponent<T>(World world) where T : struct
    {
        return world.CountEntities(new QueryDescription().WithAll<T>());
    }

    #endregion

    #region Assertion Helpers

    /// <summary>
    ///     Asserts that an entity is at a specific grid position.
    /// </summary>
    /// <param name="world">The world instance.</param>
    /// <param name="entity">The entity to check.</param>
    /// <param name="expectedX">Expected X coordinate.</param>
    /// <param name="expectedY">Expected Y coordinate.</param>
    public static void AssertPosition(World world, Entity entity, int expectedX, int expectedY)
    {
        var position = world.Get<Position>(entity);
        Assert.Equal(expectedX, position.X);
        Assert.Equal(expectedY, position.Y);
    }

    /// <summary>
    ///     Asserts that an entity is at a specific pixel position (with tolerance).
    /// </summary>
    /// <param name="world">The world instance.</param>
    /// <param name="entity">The entity to check.</param>
    /// <param name="expectedPixelX">Expected pixel X coordinate.</param>
    /// <param name="expectedPixelY">Expected pixel Y coordinate.</param>
    /// <param name="tolerance">Acceptable difference (default: 0.01f).</param>
    public static void AssertPixelPosition(
        World world,
        Entity entity,
        float expectedPixelX,
        float expectedPixelY,
        float tolerance = 0.01f)
    {
        var position = world.Get<Position>(entity);
        Assert.Equal(expectedPixelX, position.PixelX, tolerance);
        Assert.Equal(expectedPixelY, position.PixelY, tolerance);
    }

    /// <summary>
    ///     Asserts that an entity is currently moving.
    /// </summary>
    /// <param name="world">The world instance.</param>
    /// <param name="entity">The entity to check.</param>
    public static void AssertIsMoving(World world, Entity entity)
    {
        var movement = world.Get<GridMovement>(entity);
        Assert.True(movement.IsMoving, "Entity should be moving");
    }

    /// <summary>
    ///     Asserts that an entity is not moving.
    /// </summary>
    /// <param name="world">The world instance.</param>
    /// <param name="entity">The entity to check.</param>
    public static void AssertIsNotMoving(World world, Entity entity)
    {
        var movement = world.Get<GridMovement>(entity);
        Assert.False(movement.IsMoving, "Entity should not be moving");
    }

    /// <summary>
    ///     Asserts that an entity is facing a specific direction.
    /// </summary>
    /// <param name="world">The world instance.</param>
    /// <param name="entity">The entity to check.</param>
    /// <param name="expectedDirection">Expected facing direction.</param>
    public static void AssertFacingDirection(World world, Entity entity, Direction expectedDirection)
    {
        var movement = world.Get<GridMovement>(entity);
        Assert.Equal(expectedDirection, movement.FacingDirection);
    }

    /// <summary>
    ///     Asserts that an animation is playing.
    /// </summary>
    /// <param name="world">The world instance.</param>
    /// <param name="entity">The entity to check.</param>
    /// <param name="expectedAnimation">Expected animation name.</param>
    public static void AssertAnimation(World world, Entity entity, string expectedAnimation)
    {
        var animation = world.Get<Animation>(entity);
        Assert.Equal(expectedAnimation, animation.CurrentAnimation);
    }

    /// <summary>
    ///     Asserts that an entity has a specific tag.
    /// </summary>
    /// <param name="world">The world instance.</param>
    /// <param name="entity">The entity to check.</param>
    /// <param name="expectedTag">Expected tag value.</param>
    public static void AssertTag(World world, Entity entity, string expectedTag)
    {
        var tag = world.Get<Tag>(entity);
        Assert.Equal(expectedTag, tag.Value);
    }

    #endregion

    #region Performance Helpers

    /// <summary>
    ///     Measures the time taken to execute an action.
    /// </summary>
    /// <param name="action">The action to measure.</param>
    /// <returns>Elapsed time in milliseconds.</returns>
    public static double MeasureExecutionTime(Action action)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        action();
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    /// <summary>
    ///     Creates a large number of test entities for performance testing.
    /// </summary>
    /// <param name="world">The world instance.</param>
    /// <param name="count">Number of entities to create.</param>
    /// <returns>List of created entities.</returns>
    public static List<Entity> CreateBulkEntities(World world, int count)
    {
        var entities = new List<Entity>(count);
        for (int i = 0; i < count; i++)
        {
            entities.Add(world.Create(new Position(i % 100, i / 100)));
        }
        return entities;
    }

    #endregion

    #region Math Helpers

    /// <summary>
    ///     Checks if two Vector2 values are approximately equal.
    /// </summary>
    /// <param name="a">First vector.</param>
    /// <param name="b">Second vector.</param>
    /// <param name="tolerance">Tolerance for comparison.</param>
    /// <returns>True if approximately equal.</returns>
    public static bool ApproximatelyEqual(Vector2 a, Vector2 b, float tolerance = 0.01f)
    {
        return Math.Abs(a.X - b.X) <= tolerance && Math.Abs(a.Y - b.Y) <= tolerance;
    }

    /// <summary>
    ///     Calculates the distance between two positions.
    /// </summary>
    /// <param name="world">The world instance.</param>
    /// <param name="entity1">First entity.</param>
    /// <param name="entity2">Second entity.</param>
    /// <returns>Distance in grid units.</returns>
    public static float GetDistance(World world, Entity entity1, Entity entity2)
    {
        var pos1 = world.Get<Position>(entity1);
        var pos2 = world.Get<Position>(entity2);
        var dx = pos2.X - pos1.X;
        var dy = pos2.Y - pos1.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    #endregion
}
