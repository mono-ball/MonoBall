using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Components.Rendering;
using PokeSharp.Core.Systems;

namespace PokeSharp.Benchmarks;

/// <summary>
///     Benchmarks for system execution and update performance.
///     Tests the SystemManager and individual system update speeds.
/// </summary>
public class SystemBenchmarks : BenchmarkBase
{
    /// <summary>
    ///     Number of entities to test with.
    ///     Tests at 100 and 1000 entity scales.
    /// </summary>
    [Params(100, 1000)]
    public int EntityCount;

    private MovementSystem _movementSystem = null!;
    private CollisionSystem _collisionSystem = null!;
    private SpatialHashSystem _spatialHashSystem = null!;
    private TileAnimationSystem _tileAnimationSystem = null!;

    /// <summary>
    ///     Setup: Create systems and populate with test entities.
    /// </summary>
    [GlobalSetup]
    public override void Setup()
    {
        base.Setup();

        // Create system manager with null logger
        SystemManager = new SystemManager(NullLogger<SystemManager>.Instance);

        // Create and register core systems
        _spatialHashSystem = new SpatialHashSystem(NullLogger<SpatialHashSystem>.Instance);
        SystemManager.RegisterSystem(_spatialHashSystem);

        _collisionSystem = new CollisionSystem(NullLogger<CollisionSystem>.Instance);
        _collisionSystem.SetSpatialHashSystem(_spatialHashSystem);
        SystemManager.RegisterSystem(_collisionSystem);

        _movementSystem = new MovementSystem(NullLogger<MovementSystem>.Instance);
        _movementSystem.SetSpatialHashSystem(_spatialHashSystem);
        SystemManager.RegisterSystem(_movementSystem);

        _tileAnimationSystem = new TileAnimationSystem(NullLogger<TileAnimationSystem>.Instance);
        SystemManager.RegisterSystem(_tileAnimationSystem);

        // Initialize all systems
        SystemManager.Initialize(World);

        // Populate with entities
        for (int i = 0; i < EntityCount; i++)
        {
            World.Create(
                new Position { X = i % 100, Y = i / 100, MapId = 1 },
                new GridMovement(4.0f) { IsMoving = i % 2 == 0 },
                new Sprite(""),
                new Animation("idle"),
                new Collision { IsSolid = i % 3 == 0 }
            );
        }
    }

    /// <summary>
    ///     Baseline: Update all systems for one frame (60 FPS).
    /// </summary>
    [Benchmark(Baseline = true)]
    public void UpdateAllSystems_OneFrame()
    {
        SystemManager!.Update(World, 0.016f); // 60 FPS
    }

    /// <summary>
    ///     Update all systems for 10 frames.
    ///     Tests sustained performance.
    /// </summary>
    [Benchmark]
    public void UpdateAllSystems_TenFrames()
    {
        for (int i = 0; i < 10; i++)
        {
            SystemManager!.Update(World, 0.016f);
        }
    }

    /// <summary>
    ///     Update only MovementSystem.
    ///     Tests individual system performance.
    /// </summary>
    [Benchmark]
    public void UpdateMovementSystem_OneFrame()
    {
        _movementSystem.Update(World, 0.016f);
    }

    /// <summary>
    ///     Update only CollisionSystem.
    ///     Tests collision detection performance.
    /// </summary>
    [Benchmark]
    public void UpdateCollisionSystem_OneFrame()
    {
        _collisionSystem.Update(World, 0.016f);
    }

    /// <summary>
    ///     Update only SpatialHashSystem.
    ///     Tests spatial hashing performance.
    /// </summary>
    [Benchmark]
    public void UpdateSpatialHashSystem_OneFrame()
    {
        _spatialHashSystem.Update(World, 0.016f);
    }

    /// <summary>
    ///     Update only TileAnimationSystem.
    ///     Tests animation system performance.
    /// </summary>
    [Benchmark]
    public void UpdateTileAnimationSystem_OneFrame()
    {
        _tileAnimationSystem.Update(World, 0.016f);
    }

    /// <summary>
    ///     Simulate realistic game loop with variable delta time.
    /// </summary>
    [Benchmark]
    public void GameLoop_60FPS_OneSecond()
    {
        for (int i = 0; i < 60; i++)
        {
            SystemManager!.Update(World, 0.016f);
        }
    }

    /// <summary>
    ///     Simulate game loop with spike (30 FPS mixed with 60 FPS).
    /// </summary>
    [Benchmark]
    public void GameLoop_WithSpikes_OneSecond()
    {
        for (int i = 0; i < 45; i++)
        {
            // 30 slow frames, 15 fast frames
            float deltaTime = i < 30 ? 0.033f : 0.016f;
            SystemManager!.Update(World, deltaTime);
        }
    }
}
