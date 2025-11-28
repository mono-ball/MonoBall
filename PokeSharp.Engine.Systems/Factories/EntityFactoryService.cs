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
///     Implementation of <see cref="IEntityFactoryService" /> for spawning Arch ECS entities from templates.
///     Resolves templates from <see cref="TemplateCache" /> and instantiates entities with components.
///     Thread-safe and supports hot-reload via template cache invalidation.
/// </summary>
public sealed class EntityFactoryService(
    TemplateCache templateCache,
    ILogger<EntityFactoryService> logger,
    EntityPoolManager poolManager
) : IEntityFactoryService
{
    // Static cache for reflection MethodInfo to avoid expensive lookups
    private static readonly ConcurrentDictionary<Type, MethodInfo> _addMethodCache = new();

    private readonly ILogger<EntityFactoryService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly EntityPoolManager _poolManager =
        poolManager ?? throw new ArgumentNullException(nameof(poolManager));

    private readonly TemplateCache _templateCache =
        templateCache ?? throw new ArgumentNullException(nameof(templateCache));

    /// <inheritdoc />
    public Entity SpawnFromTemplate(
        string templateId,
        World world,
        EntitySpawnContext? context = null
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

        // Create empty entity first (use pool if available)
        Entity entity;
        try
        {
            string poolName = GetPoolNameFromTemplateId(templateId);
            entity = _poolManager.Acquire(poolName);
            _logger.LogDebug(
                "Acquired entity from pool '{PoolName}' for template '{TemplateId}'",
                poolName,
                templateId
            );
        }
        catch (KeyNotFoundException ex)
        {
            // Pool doesn't exist - this indicates a configuration error
            _logger.LogError(
                "Pool not found for template '{TemplateId}': {Error}. This may cause memory leaks. Register pool or update template configuration.",
                templateId,
                ex.Message
            );
            entity = world.Create();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("exhausted"))
        {
            // Pool exhausted - this indicates insufficient pool size
            _logger.LogError(
                "Pool exhausted for template '{TemplateId}': {Error}. Increase maxSize or release entities more aggressively.",
                templateId,
                ex.Message
            );
            throw; // Don't fall back - fail fast to reveal the problem
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
            "Spawned entity {EntityId} from template {TemplateId} with {ComponentCount} components",
            entity.Id,
            templateId,
            components.Count
        );

        return entity;
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

    /// <summary>
    ///     Spawn multiple entities from the same template efficiently.
    ///     More performant than calling SpawnFromTemplate in a loop because it:
    ///     - Resolves template hierarchy only once
    ///     - Validates template once
    ///     - Reuses reflection cache
    /// </summary>
    /// <param name="templateId">Template ID to spawn from</param>
    /// <param name="world">World to spawn entities in</param>
    /// <param name="count">Number of entities to spawn</param>
    /// <param name="configureEach">Optional per-entity configuration with index</param>
    /// <returns>Array of spawned entities</returns>
    /// <example>
    ///     <code>
    /// // Spawn 100 enemies with different positions
    /// var enemies = factory.SpawnBatchFromTemplate("enemy/goblin", world, 100,
    ///     (builder, i) => {
    ///         builder.OverrideComponent(new Position(i * 50, 100));
    ///     }
    /// );
    /// </code>
    /// </example>
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

            // Create entity (use pool if available)
            Entity entity;
            try
            {
                string poolName = GetPoolNameFromTemplateId(templateId);
                entity = _poolManager.Acquire(poolName);
            }
            catch (KeyNotFoundException ex)
            {
                // Pool doesn't exist - this indicates a configuration error
                _logger.LogError(
                    "Pool not found for template '{TemplateId}' in batch spawn: {Error}. This may cause memory leaks.",
                    templateId,
                    ex.Message
                );
                entity = world.Create();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("exhausted"))
            {
                // Pool exhausted during batch spawn - fail fast
                _logger.LogError(
                    "Pool exhausted during batch spawn for template '{TemplateId}': {Error}",
                    templateId,
                    ex.Message
                );
                throw;
            }

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

    /// <summary>
    ///     Release multiple entities back to pool or destroy them.
    ///     Uses entity pool manager if available, otherwise destroys entities directly.
    /// </summary>
    /// <param name="entities">Entities to release</param>
    /// <param name="world">World to destroy entities in (used if no pool manager)</param>
    /// <example>
    ///     <code>
    /// // Clean up spawned entities
    /// factory.ReleaseBatch(spawnedEnemies, world);
    /// </code>
    /// </example>
    public void ReleaseBatch(Entity[] entities, World world)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(world);

        _logger.LogDebug("Releasing batch of {Count} entities", entities.Length);

        foreach (Entity entity in entities)
        {
            _poolManager.Release(entity);
        }
    }

    // Private helper methods

    /// <summary>
    ///     Maps a template ID to a pool name by extracting the base entity type.
    ///     Examples: "npc/generic" → "npc", "player" → "player", "tile/water" → "tile"
    /// </summary>
    /// <param name="templateId">Full template ID (may contain '/' separator)</param>
    /// <returns>Pool name (first part before '/' if present)</returns>
    private static string GetPoolNameFromTemplateId(string templateId)
    {
        // Split on '/' and take the first part
        string[] parts = templateId.Split('/');
        return parts[0];
    }

    /// <summary>
    ///     Gets or creates cached MethodInfo for World.Add{T} to avoid expensive reflection every spawn.
    /// </summary>
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

                // Make the generic method concrete for this component type
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

    /// <summary>
    ///     Resolves template inheritance by merging base template components with child template.
    ///     Child components override base components of the same type.
    /// </summary>
    /// <param name="template">Template to resolve</param>
    /// <returns>Resolved template with all inherited components</returns>
    /// <exception cref="InvalidOperationException">Thrown on circular dependency</exception>
    private EntityTemplate ResolveTemplateInheritance(EntityTemplate template)
    {
        // If no base template, return as-is
        if (string.IsNullOrWhiteSpace(template.BaseTemplateId))
        {
            return template;
        }

        _logger.LogDebug(
            "Resolving inheritance for template '{TemplateId}' (base: '{BaseTemplateId}')",
            template.TemplateId,
            template.BaseTemplateId
        );

        // Track visited templates to detect circular dependencies
        var visited = new HashSet<string> { template.TemplateId };
        var inheritanceChain = new List<EntityTemplate>();

        // Walk up the inheritance chain
        string? currentTemplateId = template.BaseTemplateId;
        while (!string.IsNullOrWhiteSpace(currentTemplateId))
        {
            // Check for circular dependency
            if (visited.Contains(currentTemplateId))
            {
                throw new InvalidOperationException(
                    $"Circular template inheritance detected: {string.Join(" → ", visited)} → {currentTemplateId}"
                );
            }

            // Get base template
            EntityTemplate? baseTemplate = _templateCache.Get(currentTemplateId);
            if (baseTemplate == null)
            {
                throw new InvalidOperationException(
                    $"Base template '{currentTemplateId}' not found for template '{template.TemplateId}'"
                );
            }

            visited.Add(currentTemplateId);
            inheritanceChain.Add(baseTemplate);

            // Continue up the chain
            currentTemplateId = baseTemplate.BaseTemplateId;
        }

        // Reverse chain so we start from root and work down
        inheritanceChain.Reverse();

        _logger.LogDebug(
            "Inheritance chain for '{TemplateId}': {Chain}",
            template.TemplateId,
            string.Join(
                " → ",
                inheritanceChain.Select(t => t.TemplateId).Append(template.TemplateId)
            )
        );

        // Merge components from base to derived (derived overrides base)
        var mergedComponents = new Dictionary<Type, ComponentTemplate>();

        // Start with root base template
        foreach (EntityTemplate baseTemplate in inheritanceChain)
        foreach (ComponentTemplate component in baseTemplate.Components)
        // Base components are added or overridden
        {
            mergedComponents[component.ComponentType] = component;
        }

        // Apply child template components (final overrides)
        foreach (ComponentTemplate component in template.Components)
        {
            mergedComponents[component.ComponentType] = component;
        }

        // Create resolved template
        var resolvedTemplate = new EntityTemplate
        {
            TemplateId = template.TemplateId,
            Name = template.Name,
            Tag = template.Tag,
            Metadata = template.Metadata,
            BaseTemplateId = null, // Clear to avoid re-resolution
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
            // Use override if available, otherwise use template's initial data
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
