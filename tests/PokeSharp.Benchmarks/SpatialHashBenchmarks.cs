using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Systems;

namespace PokeSharp.Benchmarks;

/// <summary>
///     Benchmarks for spatial hash system performance.
///     Tests entity lookup, collision queries, and spatial partitioning.
/// </summary>
public class SpatialHashBenchmarks : BenchmarkBase
{
    [Params(100, 500, 1000)]
    public int EntityCount;

    private SpatialHashSystem _spatialHashSystem = null!;

    /// <summary>
    ///     Setup: Create spatial hash system and populate with entities.
    /// </summary>
    [GlobalSetup]
    public override void Setup()
    {
        base.Setup();

        _spatialHashSystem = new SpatialHashSystem(NullLogger<SpatialHashSystem>.Instance);
        _spatialHashSystem.Initialize(World);

        // Populate world with entities in a grid pattern
        int gridSize = (int)Math.Sqrt(EntityCount);
        for (int i = 0; i < EntityCount; i++)
        {
            int x = (i % gridSize) * 16; // 16 pixels apart
            int y = (i / gridSize) * 16;

            World.Create(
                new Position { X = x, Y = y, MapId = 1 },
                new Collision { IsSolid = i % 2 == 0 }
            );
        }

        // Initial hash build
        _spatialHashSystem.Update(World, 0.0f);
    }

    /// <summary>
    ///     Baseline: Update spatial hash (rebuild).
    /// </summary>
    [Benchmark(Baseline = true)]
    public void UpdateSpatialHash()
    {
        _spatialHashSystem.Update(World, 0.016f);
    }

    /// <summary>
    ///     Query entities at specific position.
    /// </summary>
    [Benchmark]
    public void QueryEntitiesAtPosition()
    {
        int hits = 0;
        for (int i = 0; i < 100; i++)
        {
            var results = _spatialHashSystem.GetEntitiesAt(1, i * 16, i * 16);
            hits += results.Count();
        }
    }

    /// <summary>
    ///     Query entities in area (range query).
    /// </summary>
    [Benchmark]
    public void QueryEntitiesInArea()
    {
        int totalFound = 0;
        for (int i = 0; i < 10; i++)
        {
            // Query 32x32 area (2x2 tiles at 16px each)
            var results = _spatialHashSystem.GetEntitiesInBounds(
                1,
                new Rectangle(i * 64, i * 64, 32, 32)
            );
            totalFound += results.Count();
        }
    }

    /// <summary>
    ///     Simulate entity movement and hash updates.
    /// </summary>
    [Benchmark]
    public void SimulateMovement_UpdateHash()
    {
        // Move entities
        World.Query(in Core.Queries.Queries.AllPositioned, (ref Position pos) =>
        {
            pos.X += 1;
        });

        // Update hash
        _spatialHashSystem.Update(World, 0.016f);
    }

    /// <summary>
    ///     Realistic game scenario: Movement + hash update.
    /// </summary>
    [Benchmark]
    public void RealisticScenario_MovementUpdate()
    {
        // Phase 1: Move entities
        World.Query(in Core.Queries.Queries.Movement, (ref Position pos, ref GridMovement mov) =>
        {
            if (mov.IsMoving)
            {
                pos.X += (int)mov.MovementSpeed;
            }
        });

        // Phase 2: Update spatial hash
        _spatialHashSystem.Update(World, 0.016f);
    }
}
