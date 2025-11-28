using System.Diagnostics;
using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Configuration;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Core.Systems;

namespace PokeSharp.Engine.Systems.Management;

/// <summary>
///     Manages registration and execution of all game systems.
///     Update systems are executed in priority order during the update phase.
///     Render systems are executed in render order during the draw phase.
///     Delegates performance tracking to SystemPerformanceTracker.
/// </summary>
/// <remarks>
///     <para>
///         Systems should implement either <see cref="IUpdateSystem" /> for game logic
///         or <see cref="IRenderSystem" /> for rendering logic. The separation ensures
///         clean architecture and enables optimizations like parallel execution.
///     </para>
/// </remarks>
public class SystemManager
{
    private readonly List<IRenderSystem> _cachedEnabledRenderSystems = new();

    // Cached enabled systems to avoid LINQ allocations on every frame (120x/sec)
    private readonly List<IUpdateSystem> _cachedEnabledUpdateSystems = new();
    private readonly object _lock = new();
    private readonly ILogger<SystemManager>? _logger;
    private readonly SystemPerformanceTracker _performanceTracker;
    private readonly List<IRenderSystem> _renderSystems = new();

    private readonly List<IUpdateSystem> _updateSystems = new();
    private bool _enabledCacheDirty = true;

    private bool _initialized;

    /// <summary>
    ///     Creates a new SystemManager with optional logger and performance configuration.
    /// </summary>
    /// <param name="logger">Optional logger for system diagnostics.</param>
    /// <param name="config">Optional performance configuration. Uses default if not specified.</param>
    public SystemManager(
        ILogger<SystemManager>? logger = null,
        PerformanceConfiguration? config = null
    )
    {
        _logger = logger;
        _performanceTracker = new SystemPerformanceTracker(logger, config);
    }

    /// <summary>
    ///     Gets all registered update systems.
    /// </summary>
    protected IReadOnlyList<IUpdateSystem> RegisteredUpdateSystems
    {
        get
        {
            lock (_lock)
            {
                return _updateSystems.AsReadOnly();
            }
        }
    }

    /// <summary>
    ///     Gets all registered render systems.
    /// </summary>
    protected IReadOnlyList<IRenderSystem> RegisteredRenderSystems
    {
        get
        {
            lock (_lock)
            {
                return _renderSystems.AsReadOnly();
            }
        }
    }

    /// <summary>
    ///     Gets the total count of registered systems (update + render).
    /// </summary>
    public int SystemCount
    {
        get
        {
            lock (_lock)
            {
                return _updateSystems.Count + _renderSystems.Count;
            }
        }
    }

    /// <summary>
    ///     Gets a specific system by type from registered systems.
    ///     Searches both update and render systems.
    /// </summary>
    /// <typeparam name="T">The type of system to retrieve.</typeparam>
    /// <returns>The system instance, or null if not found.</returns>
    public T? GetSystem<T>()
        where T : class
    {
        lock (_lock)
        {
            // Search update systems
            T? updateSystem = _updateSystems.OfType<T>().FirstOrDefault();
            if (updateSystem != null)
            {
                return updateSystem;
            }

            // Search render systems
            return _renderSystems.OfType<T>().FirstOrDefault();
        }
    }

    /// <summary>
    ///     Registers a pre-created update system instance.
    ///     Update systems execute during the Update phase of the game loop.
    /// </summary>
    /// <param name="system">The update system instance to register.</param>
    public virtual void RegisterUpdateSystem(IUpdateSystem system)
    {
        ArgumentNullException.ThrowIfNull(system);

        lock (_lock)
        {
            _updateSystems.Add(system);
            _updateSystems.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            _enabledCacheDirty = true; // Mark cache as dirty when system list changes

            _logger?.LogDebug(
                "Registered update system: {SystemName} (Priority: {Priority})",
                system.GetType().Name,
                system.Priority
            );
        }
    }

    /// <summary>
    ///     Registers a pre-created render system instance.
    ///     Render systems execute during the Draw phase of the game loop.
    /// </summary>
    /// <param name="system">The render system instance to register.</param>
    public virtual void RegisterRenderSystem(IRenderSystem system)
    {
        ArgumentNullException.ThrowIfNull(system);

        lock (_lock)
        {
            _renderSystems.Add(system);
            _renderSystems.Sort((a, b) => a.RenderOrder.CompareTo(b.RenderOrder));
            _enabledCacheDirty = true; // Mark cache as dirty when system list changes

            _logger?.LogDebug(
                "Registered render system: {SystemName} (Order: {RenderOrder})",
                system.GetType().Name,
                system.RenderOrder
            );
        }
    }

    /// <summary>
    ///     Initializes all registered systems with the given world.
    ///     Initializes both update and render systems.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    public void Initialize(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (_initialized)
        {
            throw new InvalidOperationException("SystemManager has already been initialized.");
        }

        int totalSystems = _updateSystems.Count + _renderSystems.Count;
        _logger?.LogSystemsInitializing(totalSystems);

        lock (_lock)
        {
            // Initialize update systems
            foreach (IUpdateSystem system in _updateSystems)
            {
                if (system is ISystem legacySystem)
                {
                    try
                    {
                        _logger?.LogSystemInitializing(system.GetType().Name);
                        legacySystem.Initialize(world);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogExceptionWithContext(
                            ex,
                            "Failed to initialize update system: {SystemName}",
                            system.GetType().Name
                        );
                        throw;
                    }
                }
            }

            // Initialize render systems
            foreach (IRenderSystem system in _renderSystems)
            {
                if (system is ISystem legacySystem)
                {
                    try
                    {
                        _logger?.LogSystemInitializing(system.GetType().Name);
                        legacySystem.Initialize(world);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogExceptionWithContext(
                            ex,
                            "Failed to initialize render system: {SystemName}",
                            system.GetType().Name
                        );
                        throw;
                    }
                }
            }
        }

        _initialized = true;
        _logger?.LogSystemsInitialized();
    }

    /// <summary>
    ///     Rebuilds the cached enabled systems lists if marked dirty.
    ///     This avoids LINQ allocations on every frame (120x/sec) by caching enabled systems
    ///     and only rebuilding when systems are added/removed or enabled state changes.
    /// </summary>
    private void RebuildEnabledCache()
    {
        if (!_enabledCacheDirty)
        {
            return;
        }

        _cachedEnabledUpdateSystems.Clear();
        foreach (IUpdateSystem system in _updateSystems)
        {
            if (system.Enabled)
            {
                _cachedEnabledUpdateSystems.Add(system);
            }
        }

        _cachedEnabledRenderSystems.Clear();
        foreach (IRenderSystem system in _renderSystems)
        {
            if (system.Enabled)
            {
                _cachedEnabledRenderSystems.Add(system);
            }
        }

        _enabledCacheDirty = false;
    }

    /// <summary>
    ///     Invalidates the enabled systems cache.
    ///     Call this when a system's Enabled property changes to ensure the cache is rebuilt.
    /// </summary>
    public void InvalidateEnabledCache()
    {
        lock (_lock)
        {
            _enabledCacheDirty = true;
        }
    }

    /// <summary>
    ///     Updates all registered update systems in priority order.
    ///     This should be called from the Update() method of the game loop.
    /// </summary>
    /// <param name="world">The ECS world containing all entities.</param>
    /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
    public void Update(World world, float deltaTime)
    {
        ArgumentNullException.ThrowIfNull(world);

        lock (_lock)
        {
            RebuildEnabledCache();
        }

        _performanceTracker.IncrementFrame();

        foreach (IUpdateSystem system in _cachedEnabledUpdateSystems)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                system.Update(world, deltaTime);
                sw.Stop();

                TrackSystemPerformance(system.GetType().Name, sw.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger?.LogError(
                    ex,
                    "Update system {SystemName} failed during execution",
                    system.GetType().Name
                );
            }
        }

        // Log performance stats periodically (every 5 seconds at 60fps)
        if (_performanceTracker.FrameCount % 300 == 0)
        {
            _performanceTracker.LogPerformanceStats();
        }
    }

    /// <summary>
    ///     Renders all registered render systems in render order.
    ///     This should be called from the Draw() method of the game loop.
    /// </summary>
    /// <param name="world">The ECS world containing all entities to render.</param>
    public void Render(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        lock (_lock)
        {
            RebuildEnabledCache();
        }

        foreach (IRenderSystem system in _cachedEnabledRenderSystems)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                system.Render(world);
                sw.Stop();

                TrackSystemPerformance(system.GetType().Name, sw.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger?.LogError(
                    ex,
                    "Render system {SystemName} failed during execution",
                    system.GetType().Name
                );
            }
        }
    }

    /// <summary>
    ///     Helper method to track system performance metrics.
    ///     Used internally and by derived classes (e.g., ParallelSystemManager).
    /// </summary>
    /// <param name="systemName">Name of the system.</param>
    /// <param name="elapsedMs">Execution time in milliseconds.</param>
    protected void TrackSystemPerformance(string systemName, double elapsedMs)
    {
        _performanceTracker.TrackSystemPerformance(systemName, elapsedMs);
    }

    /// <summary>
    ///     Gets performance metrics for all systems.
    /// </summary>
    public IReadOnlyDictionary<string, SystemMetrics> GetMetrics()
    {
        return _performanceTracker.GetAllMetrics();
    }

    /// <summary>
    ///     Resets all performance metrics.
    /// </summary>
    public void ResetMetrics()
    {
        _performanceTracker.ResetMetrics();
    }
}

/// <summary>
///     Performance metrics for a system.
/// </summary>
public class SystemMetrics
{
    public double LastUpdateMs;
    public double MaxUpdateMs;
    public double TotalTimeMs;
    public long UpdateCount;
    public double AverageUpdateMs => UpdateCount > 0 ? TotalTimeMs / UpdateCount : 0;
}
