using System.Diagnostics;
using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Core.DependencyInjection;
using PokeSharp.Core.Logging;

namespace PokeSharp.Core.Systems;

/// <summary>
///     Manages registration and execution of all game systems.
///     Systems are executed in priority order each frame.
///     Tracks performance metrics for each system.
///     Supports dependency injection for automatic system creation and service resolution.
/// </summary>
public class SystemManager(ILogger<SystemManager>? logger = null)
{
    private const float TargetFrameTime = 16.67f; // 60 FPS target
    private const double SlowSystemThresholdPercent = 0.1; // Warn if system takes >10% of frame budget
    private const ulong SlowSystemWarningCooldownFrames = 300; // Only warn once per 5 seconds (at 60fps)
    private readonly Dictionary<ISystem, ulong> _lastSlowWarningFrame = new();

    private readonly object _lock = new();
    private readonly ILogger<SystemManager>? _logger = logger;
    private readonly Dictionary<ISystem, SystemMetrics> _metrics = new();
    private readonly ServiceContainer _serviceContainer = new();
    private readonly List<ISystem> _systems = new();
    private ulong _frameCounter;
    private bool _initialized;
    private SystemFactory? _systemFactory;

    /// <summary>
    ///     Gets all registered systems.
    /// </summary>
    public IReadOnlyList<ISystem> Systems
    {
        get
        {
            lock (_lock)
            {
                return _systems.AsReadOnly();
            }
        }
    }

    /// <summary>
    ///     Gets the count of registered systems.
    /// </summary>
    public int SystemCount
    {
        get
        {
            lock (_lock)
            {
                return _systems.Count;
            }
        }
    }

    /// <summary>
    ///     Registers a system instance with the manager.
    ///     Systems are automatically sorted by priority.
    ///     Use this method for manual system instantiation (backward compatible).
    /// </summary>
    /// <param name="system">The system to register.</param>
    /// <exception cref="ArgumentNullException">Thrown if system is null.</exception>
    public void RegisterSystem(ISystem system)
    {
        ArgumentNullException.ThrowIfNull(system);

        lock (_lock)
        {
            if (_systems.Contains(system))
                throw new InvalidOperationException(
                    $"System {system.GetType().Name} is already registered."
                );

            _systems.Add(system);
            _systems.Sort((a, b) => a.Priority.CompareTo(b.Priority));

            // Initialize metrics for this system
            _metrics[system] = new SystemMetrics();

            _logger?.LogSystemRegistered(system.GetType().Name, system.Priority);
        }
    }

    /// <summary>
    ///     Registers a system type with automatic dependency injection.
    ///     The system will be created using the SystemFactory with all dependencies resolved.
    /// </summary>
    /// <typeparam name="TSystem">The system type to register.</typeparam>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if the system cannot be created or dependencies cannot be resolved.
    /// </exception>
    public void RegisterSystem<TSystem>()
        where TSystem : ISystem
    {
        lock (_lock)
        {
            // Initialize factory on first use
            if (_systemFactory == null)
                _systemFactory = new SystemFactory(_serviceContainer);

            // Create system with dependency injection
            var system = _systemFactory.CreateSystem<TSystem>();

            // Register the created instance
            RegisterSystem(system);

            _logger?.LogDebug(
                "System {SystemName} registered with dependency injection",
                typeof(TSystem).Name
            );
        }
    }

    /// <summary>
    ///     Registers a system type with automatic dependency injection and a specific priority override.
    ///     Note: The priority property of the system type itself will be used; this is for future extensibility.
    /// </summary>
    /// <typeparam name="TSystem">The system type to register.</typeparam>
    /// <param name="priority">Priority hint (currently not used, for future extensibility).</param>
    public void RegisterSystem<TSystem>(int priority)
        where TSystem : ISystem
    {
        // For now, just delegate to the parameterless version
        // In the future, this could support priority overrides
        RegisterSystem<TSystem>();
    }

    /// <summary>
    ///     Unregisters a system from the manager.
    /// </summary>
    /// <param name="system">The system to unregister.</param>
    public void UnregisterSystem(ISystem system)
    {
        ArgumentNullException.ThrowIfNull(system);

        lock (_lock)
        {
            _systems.Remove(system);
            _metrics.Remove(system);
            _lastSlowWarningFrame.Remove(system);
        }
    }

    /// <summary>
    ///     Initializes all registered systems with the given world.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    public void Initialize(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (_initialized)
            throw new InvalidOperationException("SystemManager has already been initialized.");

        _logger?.LogSystemsInitializing(_systems.Count);

        lock (_lock)
        {
            foreach (var system in _systems)
                try
                {
                    _logger?.LogSystemInitializing(system.GetType().Name);
                    system.Initialize(world);
                }
                catch (Exception ex)
                {
                    _logger?.LogExceptionWithContext(
                        ex,
                        "Failed to initialize system: {SystemName}",
                        system.GetType().Name
                    );
                    throw;
                }
        }

        _initialized = true;
        _logger?.LogSystemsInitialized();
    }

    /// <summary>
    ///     Updates all enabled systems in priority order.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
    public void Update(World world, float deltaTime)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (!_initialized)
        {
            _logger?.LogOperationSkipped("SystemManager.Update", "SystemManager not initialized");
            throw new InvalidOperationException(
                "SystemManager has not been initialized. Call Initialize() first."
            );
        }

        // Copy systems list once under lock to minimize lock contention
        ISystem[] systemsToUpdate;
        lock (_lock)
        {
            systemsToUpdate = _systems.ToArray();
        }

        _frameCounter++;

        // Update systems without holding lock
        foreach (var system in systemsToUpdate)
        {
            if (!system.Enabled)
                continue;

            var stopwatch = Stopwatch.StartNew();

            try
            {
                system.Update(world, deltaTime);
            }
            catch (Exception ex)
            {
                _logger?.LogExceptionWithContext(
                    ex,
                    "System {System} threw exception during update",
                    system.GetType().Name
                );
                continue; // Continue with next system instead of crashing
            }

            stopwatch.Stop();

            // Update metrics under lock
            lock (_lock)
            {
                if (!_metrics.ContainsKey(system))
                    _metrics[system] = new SystemMetrics();

                var metrics = _metrics[system];
                metrics.UpdateCount++;
                metrics.TotalTimeMs += stopwatch.Elapsed.TotalMilliseconds;
                metrics.LastUpdateMs = stopwatch.Elapsed.TotalMilliseconds;

                if (stopwatch.Elapsed.TotalMilliseconds > metrics.MaxUpdateMs)
                    metrics.MaxUpdateMs = stopwatch.Elapsed.TotalMilliseconds;
            }

            // Log slow systems (throttled to avoid spam)
            var elapsed = stopwatch.Elapsed.TotalMilliseconds;
            if (elapsed > TargetFrameTime * SlowSystemThresholdPercent)
                lock (_lock)
                {
                    // Only warn if cooldown period has passed since last warning for this system
                    if (
                        !_lastSlowWarningFrame.TryGetValue(system, out var lastWarning)
                        || _frameCounter - lastWarning >= SlowSystemWarningCooldownFrames
                    )
                    {
                        _lastSlowWarningFrame[system] = _frameCounter;
                        var percentOfFrame = elapsed / TargetFrameTime * 100;
                        _logger?.LogSlowSystem(system.GetType().Name, elapsed, percentOfFrame);
                    }
                }
        }

        // Log performance stats periodically (every 5 seconds at 60fps)
        if (_frameCounter % 300 == 0)
            LogPerformanceStats();
    }

    private void LogPerformanceStats()
    {
        foreach (var (system, metrics) in _metrics)
            if (metrics.UpdateCount > 0)
                _logger?.LogSystemPerformance(
                    system.GetType().Name,
                    metrics.AverageUpdateMs,
                    metrics.MaxUpdateMs,
                    metrics.UpdateCount
                );
    }

    /// <summary>
    ///     Gets performance metrics for all systems.
    /// </summary>
    public IReadOnlyDictionary<ISystem, SystemMetrics> GetMetrics()
    {
        lock (_lock)
        {
            return new Dictionary<ISystem, SystemMetrics>(_metrics);
        }
    }

    /// <summary>
    ///     Resets all performance metrics.
    /// </summary>
    public void ResetMetrics()
    {
        lock (_lock)
        {
            foreach (var metrics in _metrics.Values)
            {
                metrics.UpdateCount = 0;
                metrics.TotalTimeMs = 0;
                metrics.LastUpdateMs = 0;
                metrics.MaxUpdateMs = 0;
            }
        }
    }

    /// <summary>
    ///     Registers a singleton service in the dependency injection container.
    ///     The same instance will be injected into all systems that depend on this service.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <param name="instance">The service instance.</param>
    /// <returns>This SystemManager for method chaining.</returns>
    public SystemManager RegisterService<TService>(TService instance)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(instance);

        lock (_lock)
        {
            _serviceContainer.RegisterSingleton(instance);
            _logger?.LogDebug("Service {ServiceName} registered as singleton", typeof(TService).Name);
        }

        return this;
    }

    /// <summary>
    ///     Registers a singleton service using a factory function.
    ///     The factory is called once when first needed, then the instance is cached.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <param name="factory">Factory function that creates the service.</param>
    /// <returns>This SystemManager for method chaining.</returns>
    public SystemManager RegisterService<TService>(Func<ServiceContainer, TService> factory)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(factory);

        lock (_lock)
        {
            _serviceContainer.RegisterSingleton(factory);
            _logger?.LogDebug(
                "Service {ServiceName} registered as singleton with factory",
                typeof(TService).Name
            );
        }

        return this;
    }

    /// <summary>
    ///     Registers a transient service using a factory function.
    ///     A new instance is created each time the service is requested.
    /// </summary>
    /// <typeparam name="TService">The service type.</typeparam>
    /// <param name="factory">Factory function that creates the service.</param>
    /// <returns>This SystemManager for method chaining.</returns>
    public SystemManager RegisterTransientService<TService>(Func<ServiceContainer, TService> factory)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(factory);

        lock (_lock)
        {
            _serviceContainer.RegisterTransient(factory);
            _logger?.LogDebug(
                "Service {ServiceName} registered as transient with factory",
                typeof(TService).Name
            );
        }

        return this;
    }

    /// <summary>
    ///     Gets the service container for advanced dependency injection scenarios.
    /// </summary>
    public ServiceContainer ServiceContainer => _serviceContainer;

    /// <summary>
    ///     Validates that all dependencies for a system type can be resolved.
    ///     Useful for debugging dependency issues before attempting to register a system.
    /// </summary>
    /// <typeparam name="TSystem">The system type to validate.</typeparam>
    /// <returns>
    ///     A tuple containing: (canResolve, missingDependencies).
    ///     canResolve is true if all dependencies can be resolved.
    ///     missingDependencies contains the names of any missing dependencies.
    /// </returns>
    public (bool canResolve, List<string> missingDependencies) ValidateSystemDependencies<TSystem>()
        where TSystem : ISystem
    {
        lock (_lock)
        {
            if (_systemFactory == null)
                _systemFactory = new SystemFactory(_serviceContainer);

            return _systemFactory.ValidateDependencies<TSystem>();
        }
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
