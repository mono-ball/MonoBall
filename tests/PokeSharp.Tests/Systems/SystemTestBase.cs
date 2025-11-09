using Arch.Core;
using PokeSharp.Core.Systems;
using Xunit;

namespace PokeSharp.Tests.ECS.Systems;

/// <summary>
///     Base class for system-specific tests.
///     Provides utilities for testing system behavior, initialization, and updates.
/// </summary>
/// <typeparam name="TSystem">The system type being tested.</typeparam>
public abstract class SystemTestBase<TSystem> : IDisposable where TSystem : ISystem
{
    private bool _disposed;

    /// <summary>
    ///     Gets the system under test.
    /// </summary>
    protected TSystem System { get; private set; }

    /// <summary>
    ///     Gets the test world instance.
    /// </summary>
    protected World World { get; private set; }

    /// <summary>
    ///     Gets the system manager for coordinated system testing.
    /// </summary>
    protected SystemManager SystemManager { get; private set; }

    /// <summary>
    ///     Gets whether the system has been initialized.
    /// </summary>
    protected bool IsInitialized { get; private set; }

    /// <summary>
    ///     Initializes a new instance of the system test base.
    /// </summary>
    protected SystemTestBase()
    {
        World = World.Create();
        SystemManager = new SystemManager();
        System = CreateSystem();
        IsInitialized = false;
    }

    /// <summary>
    ///     Creates an instance of the system under test.
    ///     Override this to provide custom system instantiation.
    /// </summary>
    /// <returns>A new instance of TSystem.</returns>
    protected abstract TSystem CreateSystem();

    /// <summary>
    ///     Initializes the system for testing.
    ///     Call this in test setup or at the beginning of tests.
    /// </summary>
    protected void InitializeSystem()
    {
        if (!IsInitialized)
        {
            System.Initialize(World);
            IsInitialized = true;
        }
    }

    /// <summary>
    ///     Initializes the system through the SystemManager.
    ///     Useful for testing systems that depend on other systems.
    /// </summary>
    protected void InitializeSystemViaManager()
    {
        if (!IsInitialized)
        {
            SystemManager.RegisterSystem(System);
            ConfigureAdditionalSystems(SystemManager);
            SystemManager.Initialize(World);
            IsInitialized = true;
        }
    }

    /// <summary>
    ///     Override this to register additional systems required for testing.
    /// </summary>
    /// <param name="manager">The system manager.</param>
    protected virtual void ConfigureAdditionalSystems(SystemManager manager)
    {
        // Override in derived classes to add dependent systems
    }

    /// <summary>
    ///     Updates the system once with a specified delta time.
    /// </summary>
    /// <param name="deltaTime">Time delta (default: 0.016f for 60 FPS).</param>
    protected void UpdateSystem(float deltaTime = 0.016f)
    {
        if (!IsInitialized)
        {
            InitializeSystem();
        }

        System.Update(World, deltaTime);
    }

    /// <summary>
    ///     Updates the system multiple times.
    /// </summary>
    /// <param name="frameCount">Number of updates to perform.</param>
    /// <param name="deltaTime">Time delta per update.</param>
    protected void UpdateSystemForFrames(int frameCount, float deltaTime = 0.016f)
    {
        if (!IsInitialized)
        {
            InitializeSystem();
        }

        for (int i = 0; i < frameCount; i++)
        {
            System.Update(World, deltaTime);
        }
    }

    /// <summary>
    ///     Updates all systems through the SystemManager.
    /// </summary>
    /// <param name="deltaTime">Time delta.</param>
    protected void UpdateAllSystems(float deltaTime = 0.016f)
    {
        if (!IsInitialized)
        {
            InitializeSystemViaManager();
        }

        SystemManager.Update(World, deltaTime);
    }

    /// <summary>
    ///     Updates all systems multiple times through the SystemManager.
    /// </summary>
    /// <param name="frameCount">Number of updates.</param>
    /// <param name="deltaTime">Time delta per update.</param>
    protected void UpdateAllSystemsForFrames(int frameCount, float deltaTime = 0.016f)
    {
        if (!IsInitialized)
        {
            InitializeSystemViaManager();
        }

        for (int i = 0; i < frameCount; i++)
        {
            SystemManager.Update(World, deltaTime);
        }
    }

    /// <summary>
    ///     Asserts that the system is enabled.
    /// </summary>
    protected void AssertSystemEnabled()
    {
        Assert.True(System.Enabled, "System should be enabled");
    }

    /// <summary>
    ///     Asserts that the system is disabled.
    /// </summary>
    protected void AssertSystemDisabled()
    {
        Assert.False(System.Enabled, "System should be disabled");
    }

    /// <summary>
    ///     Disables the system.
    /// </summary>
    protected void DisableSystem()
    {
        System.Enabled = false;
    }

    /// <summary>
    ///     Enables the system.
    /// </summary>
    protected void EnableSystem()
    {
        System.Enabled = true;
    }

    /// <summary>
    ///     Gets system metrics if using SystemManager.
    /// </summary>
    /// <returns>SystemMetrics for the system under test.</returns>
    protected SystemMetrics? GetSystemMetrics()
    {
        var metrics = SystemManager.GetMetrics();
        return metrics.TryGetValue(System, out var systemMetrics) ? systemMetrics : null;
    }

    /// <summary>
    ///     Asserts that the system has processed entities.
    /// </summary>
    protected void AssertSystemHasProcessedEntities()
    {
        var metrics = GetSystemMetrics();
        Assert.NotNull(metrics);
        Assert.True(metrics.UpdateCount > 0, "System should have been updated at least once");
    }

    /// <summary>
    ///     Asserts that the system update time is within acceptable limits.
    /// </summary>
    /// <param name="maxMilliseconds">Maximum acceptable update time in milliseconds.</param>
    protected void AssertSystemPerformance(double maxMilliseconds)
    {
        var metrics = GetSystemMetrics();
        Assert.NotNull(metrics);
        Assert.True(
            metrics.AverageUpdateMs <= maxMilliseconds,
            $"System average update time ({metrics.AverageUpdateMs:F2}ms) exceeds maximum ({maxMilliseconds}ms)"
        );
    }

    /// <summary>
    ///     Creates a test entity for system testing.
    /// </summary>
    /// <returns>The created entity.</returns>
    protected Entity CreateTestEntity()
    {
        return World.Create();
    }

    /// <summary>
    ///     Gets the number of entities in the world.
    /// </summary>
    /// <returns>Entity count.</returns>
    protected int GetEntityCount()
    {
        return World.CountEntities();
    }

    /// <summary>
    ///     Performs cleanup after each test.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Disposes resources used by the test.
    /// </summary>
    /// <param name="disposing">True if called from Dispose.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            World?.Dispose();
        }

        _disposed = true;
    }
}
