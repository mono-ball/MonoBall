using Arch.Core;

namespace PokeSharp.Core.Systems;

/// <summary>
/// Manages registration and execution of all game systems.
/// Systems are executed in priority order each frame.
/// </summary>
public class SystemManager
{
    private readonly List<ISystem> _systems = new();
    private readonly object _lock = new();
    private bool _initialized;

    /// <summary>
    /// Registers a system with the manager.
    /// Systems are automatically sorted by priority.
    /// </summary>
    /// <param name="system">The system to register.</param>
    /// <exception cref="ArgumentNullException">Thrown if system is null.</exception>
    public void RegisterSystem(ISystem system)
    {
        ArgumentNullException.ThrowIfNull(system);

        lock (_lock)
        {
            if (_systems.Contains(system))
            {
                throw new InvalidOperationException($"System {system.GetType().Name} is already registered.");
            }

            _systems.Add(system);
            _systems.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }
    }

    /// <summary>
    /// Unregisters a system from the manager.
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
    /// Initializes all registered systems with the given world.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    public void Initialize(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (_initialized)
        {
            throw new InvalidOperationException("SystemManager has already been initialized.");
        }

        lock (_lock)
        {
            foreach (var system in _systems)
            {
                system.Initialize(world);
            }
        }

        _initialized = true;
    }

    /// <summary>
    /// Updates all enabled systems in priority order.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
    public void Update(World world, float deltaTime)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (!_initialized)
        {
            throw new InvalidOperationException("SystemManager has not been initialized. Call Initialize() first.");
        }

        lock (_lock)
        {
            foreach (var system in _systems)
            {
                if (system.Enabled)
                {
                    system.Update(world, deltaTime);
                }
            }
        }
    }

    /// <summary>
    /// Gets all registered systems.
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
    /// Gets the count of registered systems.
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
}
