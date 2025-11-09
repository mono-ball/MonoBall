using Arch.Core;
using BenchmarkDotNet.Attributes;
using PokeSharp.Core.Systems;

namespace PokeSharp.Benchmarks;

/// <summary>
///     Base class for all benchmarks providing common setup/cleanup and world management.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public abstract class BenchmarkBase
{
    /// <summary>
    ///     The Arch ECS World instance used by benchmarks.
    /// </summary>
    protected World World { get; private set; } = null!;

    /// <summary>
    ///     System manager for coordinating multiple systems.
    /// </summary>
    protected SystemManager? SystemManager { get; set; }

    /// <summary>
    ///     Global setup run once before all benchmark iterations.
    /// </summary>
    [GlobalSetup]
    public virtual void Setup()
    {
        World = World.Create();
        SystemManager = null; // Override in derived classes if needed
    }

    /// <summary>
    ///     Global cleanup run once after all benchmark iterations.
    /// </summary>
    [GlobalCleanup]
    public virtual void Cleanup()
    {
        World?.Dispose();
    }

    /// <summary>
    ///     Iteration setup run before each benchmark iteration.
    ///     Override this to reset state between iterations.
    /// </summary>
    [IterationSetup]
    public virtual void IterationSetup()
    {
        // Override in derived classes if needed
    }

    /// <summary>
    ///     Iteration cleanup run after each benchmark iteration.
    ///     Override this to clean up per-iteration resources.
    /// </summary>
    [IterationCleanup]
    public virtual void IterationCleanup()
    {
        // Override in derived classes if needed
    }
}
