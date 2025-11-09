using System.Reflection;
using Arch.Core;
using PokeSharp.Core.Systems;

namespace PokeSharp.Core.DependencyInjection;

/// <summary>
///     Factory for creating system instances with automatic dependency injection.
///     Supports constructor injection by analyzing system constructors and resolving dependencies.
/// </summary>
public class SystemFactory
{
    private readonly ServiceContainer _container;

    /// <summary>
    ///     Initializes a new instance of the SystemFactory.
    /// </summary>
    /// <param name="container">The service container for dependency resolution.</param>
    /// <exception cref="ArgumentNullException">Thrown if container is null.</exception>
    public SystemFactory(ServiceContainer container)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
    }

    /// <summary>
    ///     Creates a system instance of the specified type with automatic dependency injection.
    /// </summary>
    /// <typeparam name="TSystem">The system type to create.</typeparam>
    /// <returns>A new system instance with all dependencies resolved.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if the system cannot be created or dependencies cannot be resolved.
    /// </exception>
    public TSystem CreateSystem<TSystem>()
        where TSystem : ISystem
    {
        return (TSystem)CreateSystem(typeof(TSystem));
    }

    /// <summary>
    ///     Creates a system instance of the specified type with automatic dependency injection.
    /// </summary>
    /// <param name="systemType">The system type to create.</param>
    /// <returns>A new system instance with all dependencies resolved.</returns>
    /// <exception cref="ArgumentNullException">Thrown if systemType is null.</exception>
    /// <exception cref="ArgumentException">Thrown if systemType does not implement ISystem.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if the system cannot be created or dependencies cannot be resolved.
    /// </exception>
    public ISystem CreateSystem(Type systemType)
    {
        ArgumentNullException.ThrowIfNull(systemType);

        if (!typeof(ISystem).IsAssignableFrom(systemType))
            throw new ArgumentException(
                $"Type '{systemType.Name}' does not implement ISystem interface.",
                nameof(systemType)
            );

        return CreateWithDependencies(systemType);
    }

    /// <summary>
    ///     Creates a system instance with automatic constructor injection.
    ///     Analyzes constructors to find the best match and resolves all parameters.
    /// </summary>
    private ISystem CreateWithDependencies(Type systemType)
    {
        // Get all public constructors
        var constructors = systemType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        if (constructors.Length == 0)
            throw new InvalidOperationException(
                $"System type '{systemType.Name}' has no public constructors. "
                    + "Ensure the system has at least one public constructor."
            );

        // Sort constructors by parameter count (prefer constructors with more parameters)
        var orderedConstructors = constructors.OrderByDescending(c => c.GetParameters().Length);

        List<string> errors = new();

        foreach (var constructor in orderedConstructors)
        {
            try
            {
                var parameters = constructor.GetParameters();
                var resolvedParams = new object[parameters.Length];

                // Attempt to resolve all constructor parameters
                for (int i = 0; i < parameters.Length; i++)
                {
                    var paramType = parameters[i].ParameterType;

                    // Special handling for World parameter (must be null - will be set during Initialize)
                    if (paramType == typeof(World))
                    {
                        resolvedParams[i] = null!; // World will be set in Initialize
                        continue;
                    }

                    // Try to resolve from container
                    if (!_container.IsRegistered(paramType))
                    {
                        // Check if parameter has default value or is nullable
                        if (parameters[i].HasDefaultValue)
                        {
                            resolvedParams[i] = parameters[i].DefaultValue!;
                            continue;
                        }

                        throw new InvalidOperationException(
                            $"Cannot resolve parameter '{parameters[i].Name}' of type '{paramType.Name}' "
                                + $"for system '{systemType.Name}'. Service is not registered in the container."
                        );
                    }

                    // Resolve using reflection since we have the Type
                    var resolveMethod = typeof(ServiceContainer)
                        .GetMethod(nameof(ServiceContainer.Resolve))!
                        .MakeGenericMethod(paramType);
                    resolvedParams[i] = resolveMethod.Invoke(_container, null)!;
                }

                // Create the system instance
                var instance = constructor.Invoke(resolvedParams);
                return (ISystem)instance;
            }
            catch (Exception ex)
            {
                errors.Add(
                    $"Constructor with {constructor.GetParameters().Length} parameters failed: {ex.Message}"
                );
                continue; // Try next constructor
            }
        }

        // If we get here, all constructors failed
        throw new InvalidOperationException(
            $"Failed to create system '{systemType.Name}'. All constructors failed:\n"
                + string.Join("\n", errors)
        );
    }

    /// <summary>
    ///     Validates that all dependencies for a system type can be resolved.
    /// </summary>
    /// <typeparam name="TSystem">The system type to validate.</typeparam>
    /// <returns>
    ///     A tuple containing: (canResolve, missingDependencies).
    ///     canResolve is true if all dependencies can be resolved.
    ///     missingDependencies contains the names of any missing dependencies.
    /// </returns>
    public (bool canResolve, List<string> missingDependencies) ValidateDependencies<TSystem>()
        where TSystem : ISystem
    {
        return ValidateDependencies(typeof(TSystem));
    }

    /// <summary>
    ///     Validates that all dependencies for a system type can be resolved.
    /// </summary>
    /// <param name="systemType">The system type to validate.</param>
    /// <returns>
    ///     A tuple containing: (canResolve, missingDependencies).
    ///     canResolve is true if all dependencies can be resolved.
    ///     missingDependencies contains the names of any missing dependencies.
    /// </returns>
    public (bool canResolve, List<string> missingDependencies) ValidateDependencies(Type systemType)
    {
        ArgumentNullException.ThrowIfNull(systemType);

        var missingDeps = new List<string>();
        var constructors = systemType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        if (constructors.Length == 0)
        {
            missingDeps.Add("No public constructors found");
            return (false, missingDeps);
        }

        // Check the constructor with the most parameters
        var primaryConstructor = constructors.OrderByDescending(c => c.GetParameters().Length).First();

        foreach (var param in primaryConstructor.GetParameters())
        {
            // Skip World parameter (injected during Initialize)
            if (param.ParameterType == typeof(World))
                continue;

            // Skip parameters with default values
            if (param.HasDefaultValue)
                continue;

            // Check if dependency is registered
            if (!_container.IsRegistered(param.ParameterType))
                missingDeps.Add($"{param.Name} ({param.ParameterType.Name})");
        }

        return (missingDeps.Count == 0, missingDeps);
    }
}
