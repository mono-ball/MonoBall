using System.Collections.Concurrent;

namespace PokeSharp.Core.DependencyInjection;

/// <summary>
///     Thread-safe dependency injection container for managing service registrations and resolution.
///     Supports both singleton and transient lifetimes, factory functions, and instance registration.
/// </summary>
public class ServiceContainer
{
    private readonly ConcurrentDictionary<Type, Func<ServiceContainer, object>> _factories = new();
    private readonly ConcurrentDictionary<Type, ServiceLifetime> _lifetimes = new();
    private readonly ConcurrentDictionary<Type, object> _singletons = new();

    /// <summary>
    ///     Registers a singleton service instance.
    ///     The same instance will be returned for all resolution requests.
    /// </summary>
    /// <typeparam name="TService">The service type to register.</typeparam>
    /// <param name="instance">The service instance.</param>
    /// <returns>This container for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if instance is null.</exception>
    public ServiceContainer RegisterSingleton<TService>(TService instance)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(instance);

        var type = typeof(TService);
        _singletons[type] = instance;
        _lifetimes[type] = ServiceLifetime.Singleton;

        return this;
    }

    /// <summary>
    ///     Registers a singleton service using a factory function.
    ///     The factory is called once on first resolution, then the instance is cached.
    /// </summary>
    /// <typeparam name="TService">The service type to register.</typeparam>
    /// <param name="factory">Factory function that creates the service instance.</param>
    /// <returns>This container for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if factory is null.</exception>
    public ServiceContainer RegisterSingleton<TService>(Func<ServiceContainer, TService> factory)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(factory);

        var type = typeof(TService);
        _factories[type] = container => factory(container);
        _lifetimes[type] = ServiceLifetime.Singleton;

        return this;
    }

    /// <summary>
    ///     Registers a transient service using a factory function.
    ///     A new instance is created each time the service is resolved.
    /// </summary>
    /// <typeparam name="TService">The service type to register.</typeparam>
    /// <param name="factory">Factory function that creates new service instances.</param>
    /// <returns>This container for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if factory is null.</exception>
    public ServiceContainer RegisterTransient<TService>(Func<ServiceContainer, TService> factory)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(factory);

        var type = typeof(TService);
        _factories[type] = container => factory(container);
        _lifetimes[type] = ServiceLifetime.Transient;

        return this;
    }

    /// <summary>
    ///     Resolves a service of the specified type.
    /// </summary>
    /// <typeparam name="TService">The service type to resolve.</typeparam>
    /// <returns>The resolved service instance.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if the service is not registered or cannot be resolved.
    /// </exception>
    public TService Resolve<TService>()
        where TService : class
    {
        var type = typeof(TService);

        // Check if singleton instance already exists
        if (_singletons.TryGetValue(type, out var singleton))
            return (TService)singleton;

        // Try to resolve using factory
        if (_factories.TryGetValue(type, out var factory))
        {
            var instance = factory(this);

            // Cache singleton instances
            if (_lifetimes.TryGetValue(type, out var lifetime) && lifetime == ServiceLifetime.Singleton)
                _singletons[type] = instance;

            return (TService)instance;
        }

        throw new InvalidOperationException(
            $"Service of type '{typeof(TService).Name}' is not registered. "
                + "Register it using RegisterSingleton or RegisterTransient before resolving."
        );
    }

    /// <summary>
    ///     Attempts to resolve a service of the specified type.
    ///     Returns false if the service is not registered.
    /// </summary>
    /// <typeparam name="TService">The service type to resolve.</typeparam>
    /// <param name="service">The resolved service instance, or null if not found.</param>
    /// <returns>True if the service was resolved successfully; otherwise, false.</returns>
    public bool TryResolve<TService>(out TService? service)
        where TService : class
    {
        try
        {
            service = Resolve<TService>();
            return true;
        }
        catch (InvalidOperationException)
        {
            service = null;
            return false;
        }
    }

    /// <summary>
    ///     Checks if a service of the specified type is registered.
    /// </summary>
    /// <typeparam name="TService">The service type to check.</typeparam>
    /// <returns>True if the service is registered; otherwise, false.</returns>
    public bool IsRegistered<TService>()
        where TService : class
    {
        var type = typeof(TService);
        return _singletons.ContainsKey(type) || _factories.ContainsKey(type);
    }

    /// <summary>
    ///     Checks if a service of the specified type is registered.
    /// </summary>
    /// <param name="serviceType">The service type to check.</param>
    /// <returns>True if the service is registered; otherwise, false.</returns>
    public bool IsRegistered(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return _singletons.ContainsKey(serviceType) || _factories.ContainsKey(serviceType);
    }

    /// <summary>
    ///     Gets the number of registered services.
    /// </summary>
    public int RegisteredServiceCount =>
        _singletons.Keys.Union(_factories.Keys).Distinct().Count();

    /// <summary>
    ///     Clears all service registrations.
    ///     Use with caution - this will remove all registered services.
    /// </summary>
    public void Clear()
    {
        _singletons.Clear();
        _factories.Clear();
        _lifetimes.Clear();
    }
}
