using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;

namespace PokeSharp.Engine.Core.Templates;

/// <summary>
///     Default implementation of <see cref="ITemplateCompiler{TEntity}" />.
///     Converts data layer entities into runtime EntityTemplates for ECS spawning.
///     Supports template validation, base template inheritance, and component mapping.
/// </summary>
/// <typeparam name="TEntity">Data layer entity type</typeparam>
public class TemplateCompiler<TEntity> : ITemplateCompiler<TEntity>
    where TEntity : class
{
    private readonly Dictionary<Type, Func<TEntity, EntityTemplate>> _compilers = new();
    private readonly ILogger<TemplateCompiler<TEntity>> _logger;

    public TemplateCompiler(ILogger<TemplateCompiler<TEntity>> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<EntityTemplate> CompileAsync(
        TEntity entity,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(entity);

        Type entityType = typeof(TEntity);

        // Check if we have a registered compiler for this type
        if (!_compilers.TryGetValue(entityType, out Func<TEntity, EntityTemplate>? compiler))
        {
            _logger.LogTemplateCompilerMissing(entityType.Name);
            throw new InvalidOperationException(
                $"No compiler registered for entity type '{entityType.Name}'. "
                    + $"Use RegisterCompiler<{entityType.Name}>() to register a compilation function."
            );
        }

        // Compile entity to template
        EntityTemplate template = compiler(entity);

        // Validate compiled template
        if (!template.Validate(out List<string> errors))
        {
            _logger.LogError(
                "[steelblue1]WF[/] [red]✗[/] Template compilation validation failed: {Errors}",
                string.Join(", ", errors)
            );
            throw new InvalidOperationException(
                $"Compiled template is invalid: {string.Join(", ", errors)}"
            );
        }

        _logger.LogDebug(
            "Compiled template {TemplateId} from entity {EntityType}",
            template.TemplateId,
            entityType.Name
        );

        return await Task.FromResult(template);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<EntityTemplate>> CompileBatchAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(entities);

        var templates = new List<EntityTemplate>();
        var entityList = entities.ToList();

        _logger.LogDebug(
            "Batch compiling {Count} entities of type {EntityType}",
            entityList.Count,
            typeof(TEntity).Name
        );

        foreach (TEntity entity in entityList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                EntityTemplate template = await CompileAsync(entity, cancellationToken);
                templates.Add(template);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[steelblue1]WF[/] [red]✗[/] Failed to compile entity of type {EntityType}",
                    typeof(TEntity).Name
                );
                throw;
            }
        }

        _logger.LogInformation(
            "[steelblue1]WF[/] [green]✓[/] Successfully compiled [yellow]{Count}[/] templates from {EntityType}",
            templates.Count,
            typeof(TEntity).Name
        );

        return templates;
    }

    /// <inheritdoc />
    public bool Validate(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        try
        {
            EntityTemplate template = CompileAsync(entity).GetAwaiter().GetResult();
            return template.Validate(out _);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Validation failed for entity {EntityType}", typeof(TEntity).Name);
            return false;
        }
    }

    /// <inheritdoc />
    public bool SupportsType<T>()
        where T : class
    {
        return typeof(T) == typeof(TEntity);
    }

    /// <inheritdoc />
    public void RegisterCompiler(Func<TEntity, EntityTemplate> compilationFunc)
    {
        ArgumentNullException.ThrowIfNull(compilationFunc);

        Type entityType = typeof(TEntity);
        _compilers[entityType] = compilationFunc;

        _logger.LogInformation("Registered compiler for entity type {EntityType}", entityType.Name);
    }
}
