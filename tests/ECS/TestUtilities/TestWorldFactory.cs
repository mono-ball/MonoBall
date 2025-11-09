using Arch.Core;
using PokeSharp.Core.Components.Maps;
using PokeSharp.Core.Systems;

namespace PokeSharp.Tests.ECS.TestUtilities;

/// <summary>
///     Factory for creating pre-configured test worlds with common setups.
/// </summary>
public static class TestWorldFactory
{
    /// <summary>
    ///     Creates a basic empty world.
    /// </summary>
    /// <returns>A new World instance.</returns>
    public static World CreateEmptyWorld()
    {
        return World.Create();
    }

    /// <summary>
    ///     Creates a world with a basic map setup.
    /// </summary>
    /// <param name="mapId">Map identifier.</param>
    /// <param name="width">Map width in tiles.</param>
    /// <param name="height">Map height in tiles.</param>
    /// <param name="tileSize">Tile size in pixels.</param>
    /// <returns>A World with MapInfo entity.</returns>
    public static World CreateWorldWithMap(int mapId = 0, int width = 20, int height = 20, int tileSize = 16)
    {
        var world = World.Create();

        // Create a map info entity
        world.Create(new MapInfo
        {
            MapId = mapId,
            Width = width,
            Height = height,
            TileSize = tileSize,
            Name = $"Test Map {mapId}"
        });

        return world;
    }

    /// <summary>
    ///     Creates a world with movement system configured.
    /// </summary>
    /// <param name="systemManager">Optional existing system manager.</param>
    /// <returns>Tuple containing World and SystemManager.</returns>
    public static (World World, SystemManager SystemManager) CreateWorldWithMovementSystem(
        SystemManager? systemManager = null)
    {
        var world = CreateWorldWithMap();
        var manager = systemManager ?? new SystemManager();

        var movementSystem = new MovementSystem();
        var spatialHashSystem = new SpatialHashSystem();

        manager.RegisterSystem(spatialHashSystem);
        manager.RegisterSystem(movementSystem);

        spatialHashSystem.Initialize(world);
        movementSystem.Initialize(world);
        movementSystem.SetSpatialHashSystem(spatialHashSystem);

        return (world, manager);
    }

    /// <summary>
    ///     Creates a world with collision system configured.
    /// </summary>
    /// <returns>Tuple containing World, SystemManager, and SpatialHashSystem.</returns>
    public static (World World, SystemManager SystemManager, SpatialHashSystem SpatialHash) CreateWorldWithCollisionSystem()
    {
        var world = CreateWorldWithMap();
        var manager = new SystemManager();
        var spatialHashSystem = new SpatialHashSystem();

        manager.RegisterSystem(spatialHashSystem);
        spatialHashSystem.Initialize(world);

        return (world, manager, spatialHashSystem);
    }

    /// <summary>
    ///     Creates a world with all core systems registered and initialized.
    /// </summary>
    /// <returns>Tuple containing World and SystemManager with all systems.</returns>
    public static (World World, SystemManager SystemManager) CreateWorldWithAllCoreSystems()
    {
        var world = CreateWorldWithMap();
        var manager = new SystemManager();

        // Register core systems
        var spatialHashSystem = new SpatialHashSystem();
        var movementSystem = new MovementSystem();
        var collisionSystem = new CollisionSystem();

        manager.RegisterSystem(spatialHashSystem);
        manager.RegisterSystem(movementSystem);
        manager.RegisterSystem(collisionSystem);

        // Initialize systems
        manager.Initialize(world);

        // Set up dependencies
        movementSystem.SetSpatialHashSystem(spatialHashSystem);
        collisionSystem.SetSpatialHashSystem(spatialHashSystem);

        return (world, manager);
    }

    /// <summary>
    ///     Creates a world populated with test entities.
    /// </summary>
    /// <param name="entityCount">Number of entities to create.</param>
    /// <returns>Tuple containing World and list of created entities.</returns>
    public static (World World, List<Entity> Entities) CreatePopulatedWorld(int entityCount = 10)
    {
        var world = CreateWorldWithMap();
        var entities = new List<Entity>(entityCount);

        for (int i = 0; i < entityCount; i++)
        {
            var entity = EcsTestHelpers.CreateMovableEntity(world, i % 10, i / 10);
            entities.Add(entity);
        }

        return (world, entities);
    }

    /// <summary>
    ///     Creates a world with multiple maps.
    /// </summary>
    /// <param name="mapCount">Number of maps to create.</param>
    /// <returns>World with multiple MapInfo entities.</returns>
    public static World CreateWorldWithMultipleMaps(int mapCount = 3)
    {
        var world = World.Create();

        for (int i = 0; i < mapCount; i++)
        {
            world.Create(new MapInfo
            {
                MapId = i,
                Width = 20 + i * 5,
                Height = 20 + i * 5,
                TileSize = 16,
                Name = $"Test Map {i}"
            });
        }

        return world;
    }

    /// <summary>
    ///     Creates a world configured for performance testing.
    /// </summary>
    /// <param name="entityCount">Number of entities to create.</param>
    /// <returns>Tuple containing World, SystemManager, and created entities.</returns>
    public static (World World, SystemManager SystemManager, List<Entity> Entities) CreatePerformanceTestWorld(
        int entityCount = 1000)
    {
        var (world, manager) = CreateWorldWithAllCoreSystems();
        var entities = EcsTestHelpers.CreateBulkEntities(world, entityCount);

        return (world, manager, entities);
    }

    /// <summary>
    ///     Creates a minimal world for unit testing specific components.
    /// </summary>
    /// <returns>Minimal World instance.</returns>
    public static World CreateMinimalWorld()
    {
        return World.Create();
    }

    /// <summary>
    ///     Creates a world with custom configuration.
    /// </summary>
    /// <param name="configure">Action to configure the world.</param>
    /// <returns>Configured World instance.</returns>
    public static World CreateCustomWorld(Action<World> configure)
    {
        var world = World.Create();
        configure(world);
        return world;
    }

    /// <summary>
    ///     Creates a world with custom system configuration.
    /// </summary>
    /// <param name="configure">Action to configure the SystemManager.</param>
    /// <returns>Tuple containing World and configured SystemManager.</returns>
    public static (World World, SystemManager SystemManager) CreateCustomWorldWithSystems(
        Action<World, SystemManager> configure)
    {
        var world = CreateWorldWithMap();
        var manager = new SystemManager();

        configure(world, manager);

        return (world, manager);
    }

    /// <summary>
    ///     Disposes a world safely.
    /// </summary>
    /// <param name="world">The world to dispose.</param>
    public static void DisposeWorld(World world)
    {
        world?.Dispose();
    }

    /// <summary>
    ///     Disposes multiple worlds safely.
    /// </summary>
    /// <param name="worlds">The worlds to dispose.</param>
    public static void DisposeWorlds(params World[] worlds)
    {
        foreach (var world in worlds)
        {
            world?.Dispose();
        }
    }
}
