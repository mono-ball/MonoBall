using BenchmarkDotNet.Attributes;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Components.Rendering;
using PokeSharp.Core.Queries;

namespace PokeSharp.Benchmarks;

/// <summary>
///     Benchmarks focused on memory allocation patterns and garbage collection pressure.
///     Uses [MemoryDiagnoser] to track allocations and GC collections.
/// </summary>
[MemoryDiagnoser]
public class MemoryAllocationBenchmarks : BenchmarkBase
{
    [Params(100, 1000)]
    public int EntityCount;

    /// <summary>
    ///     Setup: Populate world with entities.
    /// </summary>
    [GlobalSetup]
    public override void Setup()
    {
        base.Setup();

        for (int i = 0; i < EntityCount; i++)
        {
            World.Create(
                new Position { X = i, Y = i, MapId = 1 },
                new GridMovement(4.0f),
                new Sprite(""),
                new Animation("idle")
            );
        }
    }

    /// <summary>
    ///     Baseline: Zero-allocation query using cached QueryDescription.
    ///     Should show minimal allocations.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void Query_CachedDescription_ZeroAlloc()
    {
        World.Query(in Queries.Movement, (ref Position pos, ref GridMovement mov) =>
        {
            pos.X += (int)mov.MovementSpeed;
        });
    }

    /// <summary>
    ///     Create entities - measures allocation for entity creation.
    /// </summary>
    [Benchmark]
    public void CreateEntities_100()
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
    ///     Add component to existing entities.
    ///     Tests component addition allocation.
    /// </summary>
    [Benchmark]
    public void AddComponent_ToExistingEntities()
    {
        var entities = new List<Arch.Core.Entity>();

        // Get entities
        World.Query(in Queries.AllPositioned, (Arch.Core.Entity entity, ref Position pos) =>
        {
            entities.Add(entity);
        });

        // Add collision component
        foreach (var entity in entities)
        {
            World.Add(entity, new Collision { IsSolid = true });
        }
    }

    /// <summary>
    ///     Query with lambda closure capturing local variables.
    ///     May cause allocations due to closure.
    /// </summary>
    [Benchmark]
    public void Query_WithClosureCapture()
    {
        float speedMultiplier = 2.0f;
        int frameIncrement = 1;

        World.Query(in Queries.MovementWithAnimation,
            (ref Position pos, ref GridMovement mov, ref Animation anim) =>
            {
                pos.X += (int)(mov.MovementSpeed * speedMultiplier);
                anim.CurrentFrame += frameIncrement;
            });
    }

    /// <summary>
    ///     Multiple queries in sequence - tests allocation accumulation.
    /// </summary>
    [Benchmark]
    public void MultipleQueries_Sequential()
    {
        // Query 1: Movement
        World.Query(in Queries.Movement, (ref Position pos, ref GridMovement mov) =>
        {
            pos.X += (int)mov.MovementSpeed;
        });

        // Query 2: Animation
        World.Query(in Queries.AnimatedSprites,
            (ref Position pos, ref Sprite sprite, ref Animation anim) =>
            {
                anim.CurrentFrame++;
            });

        // Query 3: Static sprites
        World.Query(in Queries.StaticSprites, (ref Position pos, ref Sprite sprite) =>
        {
            sprite.TextureId = $"map{pos.MapId}";
        });
    }

    /// <summary>
    ///     Entity destruction - tests cleanup allocations.
    /// </summary>
    [Benchmark]
    public void DestroyEntities_HalfOfWorld()
    {
        var toDestroy = new List<Arch.Core.Entity>();

        int count = 0;
        World.Query(in Queries.AllPositioned, (Arch.Core.Entity entity, ref Position pos) =>
        {
            if (count++ % 2 == 0)
            {
                toDestroy.Add(entity);
            }
        });

        foreach (var entity in toDestroy)
        {
            World.Destroy(entity);
        }
    }

    /// <summary>
    ///     Cleanup between iterations to prevent state accumulation.
    /// </summary>
    public override void IterationCleanup()
    {
        World.Clear();

        // Repopulate for next iteration
        for (int i = 0; i < EntityCount; i++)
        {
            World.Create(
                new Position { X = i, Y = i, MapId = 1 },
                new GridMovement(4.0f),
                new Sprite(""),
                new Animation("idle")
            );
        }
    }
}
