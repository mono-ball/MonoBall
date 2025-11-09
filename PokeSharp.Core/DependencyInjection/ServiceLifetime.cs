namespace PokeSharp.Core.DependencyInjection;

/// <summary>
///     Defines the lifetime of a service registration in the dependency injection container.
/// </summary>
public enum ServiceLifetime
{
    /// <summary>
    ///     A single instance is created and shared across all requests.
    ///     The instance is created on first resolution and reused thereafter.
    /// </summary>
    Singleton,

    /// <summary>
    ///     A new instance is created each time the service is requested.
    ///     Factory functions are called on every resolution.
    /// </summary>
    Transient
}
