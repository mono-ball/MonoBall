using BenchmarkDotNet.Attributes;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Components.Rendering;
using PokeSharp.Core.Queries;

namespace PokeSharp.Benchmarks;

/// <summary>
///     Benchmarks for ECS query performance across various entity counts and query types.
///     Tests the centralized query cache system and query execution speed.
/// </summary>
public class QueryBenchmarks : BenchmarkBase
{
    /// <summary>
    ///     Number of entities to create for benchmarking.
    ///     Tests performance at different scales: 100, 1000, and 10000 entities.
    /// </summary>
    [Params(100, 1000, 10000)]
    public int EntityCount;

    /// <summary>
    ///     Setup: Populate world with entities for query testing.
    /// </summary>
    [GlobalSetup]
    public override void Setup()
    {
        base.Setup();

        // Populate world with test entities
        for (int i = 0; i < EntityCount; i++)
        {
            // Create entities with various component combinations
            if (i % 3 == 0)
            {
                // Position + GridMovement + Sprite + Animation (33% of entities)
                World.Create(
                    new Position { X = i, Y = i, MapId = 1 },
                    new GridMovement(4.0f) { IsMoving = false },
                    new Sprite(""),
                    new Animation("idle")
                );
            }
            else if (i % 3 == 1)
            {
                // Position + GridMovement + Sprite (33% of entities)
                World.Create(
                    new Position { X = i, Y = i, MapId = 1 },
                    new GridMovement(4.0f) { IsMoving = false },
                    new Sprite("")
                );
            }
            else
            {
                // Position + Collision (33% of entities)
                World.Create(
                    new Position { X = i, Y = i, MapId = 1 },
                    new Collision { IsSolid = true }
                );
            }
        }
    }

    /// <summary>
    ///     Baseline: Query single component (Position).
    ///     Tests simplest query pattern.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void QuerySingleComponent()
    {
        World.Query(in Queries.AllPositioned, (ref Position pos) =>
        {
            pos.X += 1;
        });
    }

    /// <summary>
    ///     Query two components (Position + GridMovement).
    ///     Tests common movement query pattern.
    /// </summary>
    [Benchmark]
    public void QueryTwoComponents()
    {
        World.Query(in Queries.Movement, (ref Position pos, ref GridMovement mov) =>
        {
            pos.X += (int)mov.MovementSpeed;
        });
    }

    /// <summary>
    ///     Query three components (Position + GridMovement + Animation).
    ///     Tests more complex query with animation updates.
    /// </summary>
    [Benchmark]
    public void QueryThreeComponents()
    {
        World.Query(in Queries.MovementWithAnimation,
            (ref Position pos, ref GridMovement mov, ref Animation anim) =>
            {
                pos.X += (int)mov.MovementSpeed;
                anim.CurrentFrame++;
            });
    }

    /// <summary>
    ///     Query with WithNone filter (Position + GridMovement - Animation).
    ///     Tests exclusion filter performance.
    /// </summary>
    [Benchmark]
    public void QueryWithExclusionFilter()
    {
        World.Query(in Queries.MovementWithoutAnimation,
            (ref Position pos, ref GridMovement mov) =>
            {
                pos.X += (int)mov.MovementSpeed;
            });
    }

    /// <summary>
    ///     Query for collision checking (Position + Collision).
    ///     Tests collision query performance.
    /// </summary>
    [Benchmark]
    public void QueryCollision()
    {
        World.Query(in Queries.Collidable, (ref Position pos, ref Collision col) =>
        {
            pos.Y += 1;
        });
    }

    /// <summary>
    ///     Query for rendering (Position + Sprite).
    ///     Tests rendering query performance.
    /// </summary>
    [Benchmark]
    public void QueryRenderable()
    {
        World.Query(in Queries.Renderable, (ref Position pos, ref Sprite sprite) =>
        {
            sprite.TextureId = pos.X > 100 ? "tile1" : "tile0";
        });
    }

    /// <summary>
    ///     Query for animated sprites (Position + Sprite + Animation).
    ///     Tests animated rendering query performance.
    /// </summary>
    [Benchmark]
    public void QueryAnimatedSprites()
    {
        World.Query(in Queries.AnimatedSprites,
            (ref Position pos, ref Sprite sprite, ref Animation anim) =>
            {
                anim.CurrentFrame++;
            });
    }

    /// <summary>
    ///     Query for static sprites (Position + Sprite - Animation).
    ///     Tests optimized static sprite query.
    /// </summary>
    [Benchmark]
    public void QueryStaticSprites()
    {
        World.Query(in Queries.StaticSprites, (ref Position pos, ref Sprite sprite) =>
        {
            sprite.TextureId = $"map{pos.MapId}";
        });
    }

    /// <summary>
    ///     Multiple sequential queries in one frame.
    ///     Tests realistic game loop scenario.
    /// </summary>
    [Benchmark]
    public void MultipleQueriesSequential()
    {
        // Movement query
        World.Query(in Queries.Movement, (ref Position pos, ref GridMovement mov) =>
        {
            pos.X += (int)mov.MovementSpeed;
        });

        // Collision query
        World.Query(in Queries.Collidable, (ref Position pos, ref Collision col) =>
        {
            if (col.IsSolid) pos.Y += 0;
        });

        // Animation query
        World.Query(in Queries.AnimatedSprites,
            (ref Position pos, ref Sprite sprite, ref Animation anim) =>
            {
                anim.CurrentFrame++;
            });
    }
}
