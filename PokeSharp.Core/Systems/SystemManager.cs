using System.Diagnostics;
using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Core.Logging;

namespace PokeSharp.Core.Systems;

/// <summary>
///     Manages registration and execution of all game systems.
///     Systems are executed in priority order each frame.
///     Tracks performance metrics for each system.
/// </summary>
public class SystemManager(ILogger<SystemManager>? logger = null)
{
    private const float TargetFrameTime = 16.67f; // 60 FPS target
    private const double SlowSystemThresholdPercent = 0.1; // Warn if system takes >10% of frame budget
    
    private readonly object _lock = new();
    private readonly ILogger<SystemManager>? _logger = logger;
    private readonly Dictionary<ISystem, SystemMetrics> _metrics = new();
    private readonly List<ISystem> _systems = new();
    private bool _initialized;
    private ulong _frameCounter;


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
    ///     Registers a system with the manager.
    ///     Systems are automatically sorted by priority.
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
    ///     Unregisters a system from the manager.
    /// </summary>
    /// <param name="system">The system to unregister.</param>
    public void UnregisterSystem(ISystem system)
    {
        ArgumentNullException.ThrowIfNull(system);

        lock (_lock)
        {
            _systems.Remove(system);
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
            {
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
            _logger?.LogWarning("Attempting to update systems before initialization");
            throw new InvalidOperationException(
                "SystemManager has not been initialized. Call Initialize() first."
            );
        }

        _frameCounter++;

        lock (_lock)
        {
            foreach (var system in _systems)
            {
                if (!system.Enabled)
                    continue;

                var sw = Stopwatch.StartNew();
                try
                {
                    system.Update(world, deltaTime);
                }
                catch (Exception ex)
                {
                    _logger?.LogExceptionWithContext(
                        ex,
                        "Error updating system: {SystemName}",
                        system.GetType().Name
                    );
                    throw;
                }
                finally
                {
                    sw.Stop();
                    UpdateMetrics(system, sw.Elapsed.TotalMilliseconds);
                }
            }
        }

        // Log performance stats periodically (every 5 seconds at 60fps)
        if (_frameCounter % 300 == 0)
        {
            LogPerformanceStats();
        }
    }

    private void UpdateMetrics(ISystem system, double elapsedMs)
    {
        if (!_metrics.TryGetValue(system, out var metrics))
            return;

        metrics.UpdateCount++;
        metrics.TotalTimeMs += elapsedMs;
        metrics.LastUpdateMs = elapsedMs;

        if (elapsedMs > metrics.MaxUpdateMs)
            metrics.MaxUpdateMs = elapsedMs;

        // Warn about slow systems (taking >10% of frame budget)
        if (elapsedMs > TargetFrameTime * SlowSystemThresholdPercent)
        {
            var percent = (elapsedMs / TargetFrameTime) * 100;
            _logger?.LogSlowSystem(system.GetType().Name, elapsedMs, percent);
        }
    }

    private void LogPerformanceStats()
    {
        foreach (var (system, metrics) in _metrics)
        {
            if (metrics.UpdateCount > 0)
            {
                _logger?.LogSystemPerformance(
                    system.GetType().Name,
                    metrics.AverageUpdateMs,
                    metrics.MaxUpdateMs,
                    metrics.UpdateCount
                );
            }
        }
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
}

/// <summary>
///     Performance metrics for a system.
/// </summary>
public class SystemMetrics
{
    public long UpdateCount;
    public double TotalTimeMs;
    public double LastUpdateMs;
    public double MaxUpdateMs;
    public double AverageUpdateMs => UpdateCount > 0 ? TotalTimeMs / UpdateCount : 0;
}
