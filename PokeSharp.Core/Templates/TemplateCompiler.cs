using Microsoft.Extensions.Logging;

namespace PokeSharp.Core.Templates;

/// <summary>
/// Default implementation of <see cref="ITemplateCompiler{TEntity}"/>.
/// Converts data layer entities into runtime EntityTemplates for ECS spawning.
/// Supports template validation, base template inheritance, and component mapping.
/// </summary>
/// <typeparam name="TEntity">Data layer entity type</typeparam>
public class TemplateCompiler<TEntity> : ITemplateCompiler<TEntity> where TEntity : class
{
    private readonly Dictionary<Type, Func<TEntity, EntityTemplate>> _compilers = new();
    private readonly ILogger<TemplateCompiler<TEntity>> _logger;

    public TemplateCompiler(ILogger<TemplateCompiler<TEntity>> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<EntityTemplate> CompileAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity, nameof(entity));

        var entityType = typeof(TEntity);

        // Check if we have a registered compiler for this type
        if (!_compilers.TryGetValue(entityType, out var compiler))
        {
            _logger.LogError("No compiler registered for type {EntityType}", entityType.Name);
            throw new InvalidOperationException(
                $"No compiler registered for entity type '{entityType.Name}'. " +
                $"Use RegisterCompiler<{entityType.Name}>() to register a compilation function.");
        }

        // Compile entity to template
        var template = compiler(entity);

        // Validate compiled template
        if (!template.Validate(out var errors))
        {
            _logger.LogError("Template compilation validation failed: {Errors}",
                string.Join(", ", errors));
            throw new InvalidOperationException(
                $"Compiled template is invalid: {string.Join(", ", errors)}");
        }

        _logger.LogDebug("Compiled template {TemplateId} from entity {EntityType}",
            template.TemplateId, entityType.Name);

        return await Task.FromResult(template);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<EntityTemplate>> CompileBatchAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities, nameof(entities));

        var templates = new List<EntityTemplate>();
        var entityList = entities.ToList();

        _logger.LogDebug("Batch compiling {Count} entities of type {EntityType}",
            entityList.Count, typeof(TEntity).Name);

        foreach (var entity in entityList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var template = await CompileAsync(entity, cancellationToken);
                templates.Add(template);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compile entity of type {EntityType}", typeof(TEntity).Name);
                throw;
            }
        }

        _logger.LogInformation("Successfully compiled {Count} templates from {EntityType}",
            templates.Count, typeof(TEntity).Name);

        return templates;
    }

    /// <inheritdoc/>
    public bool Validate(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity, nameof(entity));

        try
        {
            var template = CompileAsync(entity).GetAwaiter().GetResult();
            return template.Validate(out _);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Validation failed for entity {EntityType}", typeof(TEntity).Name);
            return false;
        }
    }

    /// <inheritdoc/>
    public bool SupportsType<T>() where T : class
    {
        return typeof(T) == typeof(TEntity);
    }

    /// <inheritdoc/>
    public void RegisterCompiler(Func<TEntity, EntityTemplate> compilationFunc)
    {
        ArgumentNullException.ThrowIfNull(compilationFunc, nameof(compilationFunc));

        var entityType = typeof(TEntity);
        _compilers[entityType] = compilationFunc;

        _logger.LogInformation("Registered compiler for entity type {EntityType}", entityType.Name);
    }
}

/// <summary>
/// Registry for managing multiple template compilers for different entity types.
/// Provides centralized access to all registered compilers.
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
    /// Register a compiler for a specific entity type.
    /// </summary>
    /// <typeparam name="TEntity">Entity type</typeparam>
    /// <param name="compiler">Compiler instance</param>
    public void RegisterCompiler<TEntity>(ITemplateCompiler<TEntity> compiler) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(compiler, nameof(compiler));

        var entityType = typeof(TEntity);
        _compilers[entityType] = compiler;

        _logger.LogInformation("Registered compiler for entity type {EntityType}", entityType.Name);
    }

    /// <summary>
    /// Get a compiler for a specific entity type.
    /// </summary>
    /// <typeparam name="TEntity">Entity type</typeparam>
    /// <returns>Compiler instance or null if not found</returns>
    public ITemplateCompiler<TEntity>? GetCompiler<TEntity>() where TEntity : class
    {
        var entityType = typeof(TEntity);
        return _compilers.TryGetValue(entityType, out var compiler)
            ? compiler as ITemplateCompiler<TEntity>
            : null;
    }

    /// <summary>
    /// Check if a compiler is registered for an entity type.
    /// </summary>
    /// <typeparam name="TEntity">Entity type</typeparam>
    /// <returns>True if compiler exists</returns>
    public bool HasCompiler<TEntity>() where TEntity : class
    {
        return _compilers.ContainsKey(typeof(TEntity));
    }

    /// <summary>
    /// Get all registered entity types.
    /// </summary>
    /// <returns>Collection of entity types</returns>
    public IEnumerable<Type> GetRegisteredTypes()
    {
        return _compilers.Keys;
    }
}
