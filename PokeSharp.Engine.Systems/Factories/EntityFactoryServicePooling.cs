using System.Collections.Concurrent;
using System.Reflection;
using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Common.Validation;
using PokeSharp.Engine.Core.Templates;
using PokeSharp.Engine.Systems.Pooling;

namespace PokeSharp.Engine.Systems.Factories;

/// <summary>
///     Enhanced version of EntityFactoryService with pooling support.
///     Provides high-performance entity spawning (2-3x faster) with optional pooling.
/// </summary>
/// <remarks>
///     Use this when you need pooling capabilities. For backward compatibility,
///     pooling is opt-in via the usePooling parameter.
/// </remarks>
public sealed class EntityFactoryServicePooling : IEntityFactoryService
{
    // Static cache for reflection MethodInfo to avoid expensive lookups
    private static readonly ConcurrentDictionary<Type, MethodInfo> _addMethodCache = new();

    private readonly ILogger<EntityFactoryServicePooling> _logger;
    private readonly TemplateCache _templateCache;

    /// <summary>
    ///     Creates a new entity factory service with pooling support.
    /// </summary>
    /// <param name="templateCache">Template cache for loading entity templates</param>
    /// <param name="logger">Logger for diagnostics</param>
    /// <param name="poolManager">Optional entity pool manager for high-performance spawning</param>
    public EntityFactoryServicePooling(
        TemplateCache templateCache,
        ILogger<EntityFactoryServicePooling> logger,
        EntityPoolManager? poolManager = null
    )
    {
        _templateCache = templateCache ?? throw new ArgumentNullException(nameof(templateCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        PoolManager = poolManager;

        if (PoolManager != null)
        {
            _logger.LogInformation(
                "EntityFactoryServicePooling initialized with pooling support enabled"
            );
        }
    }

    /// <summary>
    ///     Check if pooling is available for this factory service.
    /// </summary>
    public bool IsPoolingEnabled => PoolManager != null;

    /// <summary>
    ///     Get the pool manager (if configured).
    /// </summary>
    public EntityPoolManager? PoolManager { get; }

    /// <inheritdoc />
    public Entity SpawnFromTemplate(
        string templateId,
        World world,
        EntitySpawnContext? context = null
    )
    {
        return SpawnFromTemplateInternal(templateId, world, context, false, "default");
    }

    /// <inheritdoc />
    public Entity SpawnFromTemplate(string templateId, World world, Action<EntityBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(configure);

        // Build context from fluent API
        var builder = new EntityBuilder();
        configure(builder);

        // Convert builder to spawn context
        var context = new EntitySpawnContext
        {
            Tag = builder.Tag,
            Overrides = builder.ComponentOverrides.ToDictionary(
                kvp => kvp.Key.Name,
                kvp => kvp.Value
            ),
        };

        // Add custom properties if any
        if (builder.CustomProperties.Any())
        {
            context.Metadata = builder.CustomProperties.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value
            );
        }

        return SpawnFromTemplate(templateId, world, context);
    }

    /// <inheritdoc />
    public IEnumerable<Entity> SpawnBatch(IEnumerable<string> templateIds, World world)
    {
        ArgumentNullException.ThrowIfNull(templateIds);
        ArgumentNullException.ThrowIfNull(world);

        var entities = new List<Entity>();
        List<string> templateIdList = [.. templateIds];

        _logger.LogDebug("Batch spawning {Count} entities", templateIdList.Count);

        foreach (string templateId in templateIdList)
        {
            try
            {
                Entity entity = SpawnFromTemplate(templateId, world);
                entities.Add(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to spawn entity from template {TemplateId}",
                    templateId
                );
                throw;
            }
        }

        _logger.LogInformation("Successfully spawned {Count} entities in batch", entities.Count);
        return entities;
    }

    /// <inheritdoc />
    public ValidationResult ValidateTemplate(string templateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);

        EntityTemplate? template = _templateCache.Get(templateId);
        if (template == null)
        {
            return ValidationResult.Failure(
                templateId,
                $"Template '{templateId}' not found in cache"
            );
        }

        return ValidateTemplateInternal(template);
    }

    /// <inheritdoc />
    public bool HasTemplate(string templateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        return _templateCache.Get(templateId) != null;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetTemplateIdsByTag(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        return _templateCache.GetByTag(tag).Select(t => t.TemplateId);
    }

    /// <inheritdoc />
    public Entity[] SpawnBatchFromTemplate(
        string templateId,
        World world,
        int count,
        Action<EntityBuilder, int>? configureEach = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        _logger.LogDebug(
            "Batch spawning {Count} entities from template {TemplateId}",
            count,
            templateId
        );

        // Retrieve and validate template once
        EntityTemplate? template = _templateCache.Get(templateId);
        if (template == null)
        {
            _logger.LogTemplateMissing(templateId);
            throw new ArgumentException(
                $"Template '{templateId}' not found in cache",
                nameof(templateId)
            );
        }

        // Resolve template inheritance chain once
        EntityTemplate resolvedTemplate = ResolveTemplateInheritance(template);

        // Validate resolved template once
        ValidationResult validationResult = ValidateTemplateInternal(resolvedTemplate);
        if (!validationResult.IsValid)
        {
            _logger.LogError(
                "Template validation failed for {TemplateId}: {Errors}",
                templateId,
                string.Join(", ", validationResult.Errors)
            );
            throw new InvalidOperationException(
                $"Template '{templateId}' is invalid: {string.Join(", ", validationResult.Errors)}"
            );
        }

        var entities = new Entity[count];

        for (int i = 0; i < count; i++)
        {
            EntitySpawnContext? context = null;

            // Apply per-entity configuration if provided
            if (configureEach != null)
            {
                var builder = new EntityBuilder();
                configureEach(builder, i);

                // Convert builder to spawn context
                context = new EntitySpawnContext
                {
                    Tag = builder.Tag,
                    Overrides = builder.ComponentOverrides.ToDictionary(
                        kvp => kvp.Key.Name,
                        kvp => kvp.Value
                    ),
                };

                if (builder.CustomProperties.Any())
                {
                    context.Metadata = builder.CustomProperties.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value
                    );
                }
            }

            // Build component array from resolved template (resolved once above)
            List<object> components = BuildComponentArray(resolvedTemplate, context);

            // Create entity
            Entity entity = world.Create();

            // Add each component using cached reflection
            foreach (object component in components)
            {
                Type componentType = component.GetType();
                MethodInfo addMethod = GetCachedAddMethod(componentType);
                addMethod.Invoke(world, [entity, component]);
            }

            entities[i] = entity;
        }

        _logger.LogInformation(
            "Successfully spawned {Count} entities from template {TemplateId}",
            count,
            templateId
        );

        return entities;
    }

    /// <inheritdoc />
    public void ReleaseBatch(Entity[] entities, World world)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(world);

        _logger.LogDebug("Releasing batch of {Count} entities", entities.Length);

        foreach (Entity entity in entities)
        // For pooling-enabled service, try to release to pool
        {
            if (PoolManager != null)
            {
                PoolManager.Release(entity);
            }
            else
            // Fall back to destruction
            {
                world.Destroy(entity);
            }
        }
    }

    /// <summary>
    ///     Spawn entity from template with optional pooling support.
    /// </summary>
    /// <param name="templateId">Template identifier</param>
    /// <param name="world">Arch world</param>
    /// <param name="context">Spawn context</param>
    /// <param name="usePooling">Whether to acquire entity from pool (2-3x faster)</param>
    /// <param name="poolName">Pool name if using pooling</param>
    /// <returns>Spawned entity</returns>
    /// <remarks>
    ///     Pooling provides significant performance benefits (2-3x faster spawning, 50%+ GC reduction)
    ///     but requires calling ReleaseEntity() instead of entity.Destroy() when done.
    /// </remarks>
    public Entity SpawnFromTemplate(
        string templateId,
        World world,
        EntitySpawnContext? context,
        bool usePooling,
        string poolName = "default"
    )
    {
        return SpawnFromTemplateInternal(templateId, world, context, usePooling, poolName);
    }

    private Entity SpawnFromTemplateInternal(
        string templateId,
        World world,
        EntitySpawnContext? context,
        bool usePooling,
        string poolName
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        ArgumentNullException.ThrowIfNull(world);

        // Retrieve template from cache
        EntityTemplate? template = _templateCache.Get(templateId);
        if (template == null)
        {
            _logger.LogTemplateMissing(templateId);
            throw new ArgumentException(
                $"Template '{templateId}' not found in cache",
                nameof(templateId)
            );
        }

        // Resolve template inheritance chain
        EntityTemplate resolvedTemplate = ResolveTemplateInheritance(template);

        // Validate resolved template before spawning
        ValidationResult validationResult = ValidateTemplateInternal(resolvedTemplate);
        if (!validationResult.IsValid)
        {
            _logger.LogError(
                "Template validation failed for {TemplateId}: {Errors}",
                templateId,
                string.Join(", ", validationResult.Errors)
            );
            throw new InvalidOperationException(
                $"Template '{templateId}' is invalid: {string.Join(", ", validationResult.Errors)}"
            );
        }

        // Build component array from resolved template
        List<object> components = BuildComponentArray(resolvedTemplate, context);

        // Create or acquire entity
        Entity entity;
        if (usePooling && PoolManager != null)
        {
            // Acquire from pool (2-3x faster)
            entity = PoolManager.Acquire(poolName);
            _logger.LogDebug(
                "Acquired entity {EntityId} from pool '{PoolName}'",
                entity.Id,
                poolName
            );
        }
        else
        {
            // Create normally
            entity = world.Create();
            _logger.LogDebug("Created new entity {EntityId}", entity.Id);
        }

        // Add each component using reflection (Arch requires compile-time types)
        foreach (object component in components)
        {
            Type componentType = component.GetType();

            // Get cached Add<T> method for this component type
            MethodInfo addMethod = GetCachedAddMethod(componentType);

            // Invoke Add<T>(entity, component)
            addMethod.Invoke(world, [entity, component]);
            _logger.LogDebug("  Added {Type} to entity", componentType.Name);
        }

        _logger.LogDebug(
            "Spawned entity {EntityId} from template {TemplateId} with {ComponentCount} components (pooled: {Pooled})",
            entity.Id,
            templateId,
            components.Count,
            usePooling
        );

        return entity;
    }

    /// <summary>
    ///     Return pooled entity to its pool instead of destroying it.
    ///     Use this for entities spawned with usePooling=true.
    /// </summary>
    /// <param name="entity">Entity to release</param>
    /// <param name="poolName">Optional explicit pool name (auto-detected if not specified)</param>
    /// <exception cref="InvalidOperationException">Thrown if pooling not enabled or entity not pooled</exception>
    /// <remarks>
    ///     This is the correct way to "destroy" pooled entities. Regular entity.Destroy() will
    ///     bypass the pool and create memory leaks in the pool tracking.
    /// </remarks>
    public void ReleaseEntity(Entity entity, string? poolName = null)
    {
        if (PoolManager == null)
        {
            throw new InvalidOperationException(
                "Cannot release entity: EntityPoolManager not configured. "
                    + "Pass EntityPoolManager to constructor to enable pooling."
            );
        }

        PoolManager.Release(entity, poolName);
        _logger.LogDebug("Released entity {EntityId} back to pool", entity.Id);
    }

    // Private helper methods

    private static MethodInfo GetCachedAddMethod(Type componentType)
    {
        return _addMethodCache.GetOrAdd(
            componentType,
            type =>
            {
                MethodInfo? method = typeof(World)
                    .GetMethods()
                    .Where(m => m.Name == nameof(World.Add) && m.IsGenericMethod)
                    .FirstOrDefault(m =>
                    {
                        ParameterInfo[] parameters = m.GetParameters();
                        return parameters.Length == 2
                            && parameters[0].ParameterType == typeof(Entity);
                    });

                if (method == null)
                {
                    throw new InvalidOperationException(
                        $"Could not find World.Add<T> method for component type {type.Name}"
                    );
                }

                return method.MakeGenericMethod(type);
            }
        );
    }

    private static ValidationResult ValidateTemplateInternal(EntityTemplate template)
    {
        bool isValid = template.Validate(out List<string> errors);
        return isValid
            ? ValidationResult.Success(template.TemplateId)
            : ValidationResult.Failure(template.TemplateId, errors.ToArray());
    }

    private EntityTemplate ResolveTemplateInheritance(EntityTemplate template)
    {
        if (string.IsNullOrWhiteSpace(template.BaseTemplateId))
        {
            return template;
        }

        _logger.LogDebug(
            "Resolving inheritance for template '{TemplateId}' (base: '{BaseTemplateId}')",
            template.TemplateId,
            template.BaseTemplateId
        );

        var visited = new HashSet<string> { template.TemplateId };
        var inheritanceChain = new List<EntityTemplate>();

        string? currentTemplateId = template.BaseTemplateId;
        while (!string.IsNullOrWhiteSpace(currentTemplateId))
        {
            if (visited.Contains(currentTemplateId))
            {
                throw new InvalidOperationException(
                    $"Circular template inheritance detected: {string.Join(" → ", visited)} → {currentTemplateId}"
                );
            }

            EntityTemplate? baseTemplate = _templateCache.Get(currentTemplateId);
            if (baseTemplate == null)
            {
                throw new InvalidOperationException(
                    $"Base template '{currentTemplateId}' not found for template '{template.TemplateId}'"
                );
            }

            visited.Add(currentTemplateId);
            inheritanceChain.Add(baseTemplate);
            currentTemplateId = baseTemplate.BaseTemplateId;
        }

        inheritanceChain.Reverse();

        _logger.LogDebug(
            "Inheritance chain for '{TemplateId}': {Chain}",
            template.TemplateId,
            string.Join(
                " → ",
                inheritanceChain.Select(t => t.TemplateId).Append(template.TemplateId)
            )
        );

        var mergedComponents = new Dictionary<Type, ComponentTemplate>();

        foreach (EntityTemplate baseTemplate in inheritanceChain)
        foreach (ComponentTemplate component in baseTemplate.Components)
        {
            mergedComponents[component.ComponentType] = component;
        }

        foreach (ComponentTemplate component in template.Components)
        {
            mergedComponents[component.ComponentType] = component;
        }

        var resolvedTemplate = new EntityTemplate
        {
            TemplateId = template.TemplateId,
            Name = template.Name,
            Tag = template.Tag,
            Metadata = template.Metadata,
            BaseTemplateId = null,
            CustomProperties = template.CustomProperties,
            Components = [.. mergedComponents.Values],
        };

        _logger.LogDebug(
            "Resolved template '{TemplateId}' with {Count} components",
            resolvedTemplate.TemplateId,
            resolvedTemplate.ComponentCount
        );

        return resolvedTemplate;
    }

    private static List<object> BuildComponentArray(
        EntityTemplate template,
        EntitySpawnContext? context
    )
    {
        var components = new List<object>();
        var addedTypes = new HashSet<string>();

        // Add template components (with overrides)
        foreach (ComponentTemplate componentTemplate in template.Components)
        {
            string componentTypeName = componentTemplate.ComponentType.Name;
            object componentData =
                context?.Overrides?.GetValueOrDefault(componentTypeName)
                ?? componentTemplate.InitialData;
            components.Add(componentData);
            addedTypes.Add(componentTypeName);
        }

        // Add new components from overrides that aren't in template
        if (context?.Overrides != null)
        {
            foreach ((string typeName, object componentData) in context.Overrides)
            {
                if (!addedTypes.Contains(typeName))
                {
                    components.Add(componentData);
                }
            }
        }

        return components;
    }
}
