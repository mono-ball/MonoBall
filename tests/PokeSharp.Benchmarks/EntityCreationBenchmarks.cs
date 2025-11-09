using Arch.Core;
using BenchmarkDotNet.Attributes;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Components.Rendering;

namespace PokeSharp.Benchmarks;

/// <summary>
///     Benchmarks for entity creation and component attachment performance.
///     Tests various scenarios from single components to complex entity compositions.
/// </summary>
public class EntityCreationBenchmarks : BenchmarkBase
{
    /// <summary>
    ///     Baseline: Create entity with single Position component.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void CreateSingleEntity_Position()
    {
        World.Create(new Position { X = 10, Y = 20, MapId = 1 });
    }

    /// <summary>
    ///     Create entity with Position and Sprite components.
    /// </summary>
    [Benchmark]
    public void CreateSingleEntity_PositionAndSprite()
    {
        World.Create(
            new Position { X = 10, Y = 20, MapId = 1 },
            new Sprite()
        );
    }

    /// <summary>
    ///     Create entity with three components (Position, GridMovement, Sprite).
    /// </summary>
    [Benchmark]
    public void CreateSingleEntity_ThreeComponents()
    {
        World.Create(
            new Position { X = 10, Y = 20, MapId = 1 },
            new GridMovement(4.0f) { IsMoving = false },
            new Sprite("")
        );
    }

    /// <summary>
    ///     Create entity with all common gameplay components.
    /// </summary>
    [Benchmark]
    public void CreateSingleEntity_AllComponents()
    {
        World.Create(
            new Position { X = 10, Y = 20, MapId = 1 },
            new GridMovement(4.0f) { IsMoving = false },
            new Sprite(""),
            new Animation("idle"),
            new Collision { IsSolid = true }
        );
    }

    /// <summary>
    ///     Create 100 entities with basic components (Position + Sprite).
    /// </summary>
    [Benchmark]
    public void CreateBatch100Entities_Basic()
    {
        for (int i = 0; i < 100; i++)
        {
            World.Create(
                new Position { X = i, Y = i, MapId = 1 },
                new Sprite("")
            );
        }
    }

    /// <summary>
    ///     Create 100 entities with full component set.
    /// </summary>
    [Benchmark]
    public void CreateBatch100Entities_Full()
    {
        for (int i = 0; i < 100; i++)
        {
            World.Create(
                new Position { X = i, Y = i, MapId = 1 },
                new GridMovement(4.0f),
                new Sprite(""),
                new Animation("idle"),
                new Collision { IsSolid = true }
            );
        }
    }

    /// <summary>
    ///     Create 1000 entities with basic components.
    ///     Tests performance at larger scale.
    /// </summary>
    [Benchmark]
    public void CreateBatch1000Entities_Basic()
    {
        for (int i = 0; i < 1000; i++)
        {
            World.Create(
                new Position { X = i, Y = i, MapId = 1 },
                new Sprite("")
            );
        }
    }

    /// <summary>
    ///     Create 1000 entities with full component set.
    ///     Stress test for large-scale entity creation.
    /// </summary>
    [Benchmark]
    public void CreateBatch1000Entities_Full()
    {
        for (int i = 0; i < 1000; i++)
        {
            World.Create(
                new Position { X = i, Y = i, MapId = 1 },
                new GridMovement(4.0f),
                new Sprite(""),
                new Animation("idle"),
                new Collision { IsSolid = true }
            );
        }
    }

    /// <summary>
    ///     Iteration cleanup to prevent entity accumulation between iterations.
    /// </summary>
    public override void IterationCleanup()
    {
        World.Clear();
    }
}
