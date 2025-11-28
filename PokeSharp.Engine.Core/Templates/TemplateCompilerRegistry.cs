using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Core.Templates;

/// <summary>
///     Registry for managing multiple template compilers for different entity types.
///     Provides centralized access to all registered compilers.
/// </summary>
public sealed class TemplateCompilerRegistry
{
    private readonly Dictionary<Type, object> _compilers = new();
    private readonly ILogger<TemplateCompilerRegistry> _logger;

    public TemplateCompilerRegistry(ILogger<TemplateCompilerRegistry> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Register a compiler for a specific entity type.
    /// </summary>
    /// <typeparam name="TEntity">Entity type</typeparam>
    /// <param name="compiler">Compiler instance</param>
    public void RegisterCompiler<TEntity>(ITemplateCompiler<TEntity> compiler)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(compiler);

        Type entityType = typeof(TEntity);
        _compilers[entityType] = compiler;

        _logger.LogInformation("Registered compiler for entity type {EntityType}", entityType.Name);
    }

    /// <summary>
    ///     Get a compiler for a specific entity type.
    /// </summary>
    /// <typeparam name="TEntity">Entity type</typeparam>
    /// <returns>Compiler instance or null if not found</returns>
    public ITemplateCompiler<TEntity>? GetCompiler<TEntity>()
        where TEntity : class
    {
        Type entityType = typeof(TEntity);
        return _compilers.TryGetValue(entityType, out object? compiler)
            ? compiler as ITemplateCompiler<TEntity>
            : null;
    }

    /// <summary>
    ///     Check if a compiler is registered for an entity type.
    /// </summary>
    /// <typeparam name="TEntity">Entity type</typeparam>
    /// <returns>True if compiler exists</returns>
    public bool HasCompiler<TEntity>()
        where TEntity : class
    {
        return _compilers.ContainsKey(typeof(TEntity));
    }

    /// <summary>
    ///     Get all registered entity types.
    /// </summary>
    /// <returns>Collection of entity types</returns>
    public IEnumerable<Type> GetRegisteredTypes()
    {
        return _compilers.Keys;
    }
}
