using System.Reflection;
using Arch.Core;
using Arch.Core.Extensions; // For Has<T> and Get<T> extension methods

namespace PokeSharp.Engine.Debug.Entities;

/// <summary>
///     Registry for ECS component types used in the entity browser.
///     Supports both static (compile-time) and dynamic (runtime) component registration.
///     Uses Arch's GetComponentTypes() for efficient component detection.
/// </summary>
public class DebugComponentRegistry
{
    private readonly List<ComponentDescriptor> _descriptors = new();
    private readonly Dictionary<Type, ComponentDescriptor> _typeToDescriptor = new();

    // Cache for Has<T> method lookups
    private static readonly Dictionary<Type, MethodInfo> _hasMethodCache = new();
    private static readonly Dictionary<Type, MethodInfo> _getMethodCache = new();
    private static readonly Type _entityExtensionsType = typeof(Arch.Core.Extensions.EntityExtensions);

    /// <summary>
    ///     Registers a component type with the given display name.
    /// </summary>
    public DebugComponentRegistry Register<T>(
        string displayName,
        string? category = null,
        int priority = 0
    )
        where T : struct
    {
        var descriptor = new ComponentDescriptor
        {
            ComponentType = typeof(T),
            DisplayName = displayName,
            Category = category,
            Priority = priority,
            HasComponent = entity => entity.Has<T>(),
        };

        _descriptors.Add(descriptor);
        _typeToDescriptor[typeof(T)] = descriptor;
        return this;
    }

    /// <summary>
    ///     Registers a component type with a property reader.
    /// </summary>
    public DebugComponentRegistry Register<T>(
        string displayName,
        Func<T, Dictionary<string, string>> propertyReader,
        string? category = null,
        int priority = 0
    )
        where T : struct
    {
        var descriptor = new ComponentDescriptor
        {
            ComponentType = typeof(T),
            DisplayName = displayName,
            Category = category,
            Priority = priority,
            HasComponent = entity => entity.Has<T>(),
            GetProperties = entity =>
            {
                if (!entity.Has<T>())
                {
                    return new Dictionary<string, string>();
                }

                ref T component = ref entity.Get<T>();
                return propertyReader(component);
            },
        };

        _descriptors.Add(descriptor);
        _typeToDescriptor[typeof(T)] = descriptor;
        return this;
    }

    /// <summary>
    ///     Registers a component type dynamically using reflection.
    ///     Used for auto-discovery of component types at runtime.
    /// </summary>
    public DebugComponentRegistry RegisterDynamic(
        Type componentType,
        string displayName,
        string? category = null,
        int priority = 0
    )
    {
        if (_typeToDescriptor.ContainsKey(componentType))
        {
            return this; // Already registered
        }

        var descriptor = new ComponentDescriptor
        {
            ComponentType = componentType,
            DisplayName = displayName,
            Category = category,
            Priority = priority,
            HasComponent = entity => HasComponentDynamic(entity, componentType),
            GetProperties = entity => GetPropertiesDynamic(entity, componentType),
        };

        _descriptors.Add(descriptor);
        _typeToDescriptor[componentType] = descriptor;
        return this;
    }

    /// <summary>
    ///     Checks if an entity has a component using reflection.
    /// </summary>
    private static bool HasComponentDynamic(Entity entity, Type componentType)
    {
        try
        {
            if (!_hasMethodCache.TryGetValue(componentType, out MethodInfo? hasMethod))
            {
                // Get the generic Has<T> method
                MethodInfo genericHas = _entityExtensionsType
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .First(m => m.Name == "Has" && m.GetParameters().Length == 1 && m.IsGenericMethod);

                hasMethod = genericHas.MakeGenericMethod(componentType);
                _hasMethodCache[componentType] = hasMethod;
            }

            return (bool)hasMethod.Invoke(null, new object[] { entity })!;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Gets component properties using reflection.
    ///     Extracts public properties and fields from the component.
    /// </summary>
    private static Dictionary<string, string> GetPropertiesDynamic(Entity entity, Type componentType)
    {
        var properties = new Dictionary<string, string>();

        try
        {
            if (!HasComponentDynamic(entity, componentType))
            {
                return properties;
            }

            // Get the component value using reflection
            if (!_getMethodCache.TryGetValue(componentType, out MethodInfo? getMethod))
            {
                MethodInfo genericGet = _entityExtensionsType
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .First(m => m.Name == "Get" && m.GetParameters().Length == 1 && m.IsGenericMethod && !m.ReturnType.IsByRef);

                getMethod = genericGet.MakeGenericMethod(componentType);
                _getMethodCache[componentType] = getMethod;
            }

            object? component = getMethod.Invoke(null, new object[] { entity });
            if (component == null)
            {
                return properties;
            }

            // Extract public properties
            foreach (PropertyInfo prop in componentType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                try
                {
                    object? value = prop.GetValue(component);
                    properties[prop.Name] = FormatValue(value);
                }
                catch
                {
                    // Skip properties that can't be read
                }
            }

            // Extract public fields
            foreach (FieldInfo field in componentType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                try
                {
                    object? value = field.GetValue(component);
                    properties[field.Name] = FormatValue(value);
                }
                catch
                {
                    // Skip fields that can't be read
                }
            }
        }
        catch
        {
            // Return empty on any error
        }

        return properties;
    }

    /// <summary>
    ///     Formats a value for display.
    /// </summary>
    private static string FormatValue(object? value)
    {
        if (value == null)
        {
            return "null";
        }

        // Handle common types nicely
        return value switch
        {
            float f => f.ToString("F2"),
            double d => d.ToString("F2"),
            bool b => b.ToString().ToLower(),
            Enum e => e.ToString(),
            _ => value.ToString() ?? "?"
        };
    }

    /// <summary>
    ///     Detects all registered components on an entity.
    /// </summary>
    public List<string> DetectComponents(Entity entity)
    {
        return _descriptors
            .Where(d => d.HasComponent(entity))
            .OrderByDescending(d => d.Priority)
            .ThenBy(d => d.DisplayName)
            .Select(d => d.DisplayName)
            .ToList();
    }

    /// <summary>
    ///     Gets properties for all components on an entity.
    /// </summary>
    public Dictionary<string, string> GetEntityProperties(Entity entity)
    {
        var properties = new Dictionary<string, string>();

        foreach (
            ComponentDescriptor descriptor in _descriptors.Where(d =>
                d.HasComponent(entity) && d.GetProperties != null
            )
        )
        {
            Dictionary<string, string> componentProps = descriptor.GetProperties!(entity);
            foreach ((string key, string value) in componentProps)
            {
                // Prefix with component name to avoid collisions
                properties[$"{descriptor.DisplayName}.{key}"] = value;
            }
        }

        return properties;
    }

    /// <summary>
    ///     Gets a simple properties dictionary (no component prefix) for display.
    /// </summary>
    public Dictionary<string, string> GetSimpleProperties(Entity entity)
    {
        var properties = new Dictionary<string, string>();

        foreach (
            ComponentDescriptor descriptor in _descriptors.Where(d =>
                d.HasComponent(entity) && d.GetProperties != null
            )
        )
        {
            Dictionary<string, string> componentProps = descriptor.GetProperties!(entity);
            foreach ((string key, string value) in componentProps)
            {
                // Use simple key, last one wins in case of collision
                properties[key] = value;
            }
        }

        return properties;
    }

    /// <summary>
    ///     Gets all registered component names.
    /// </summary>
    public IEnumerable<string> GetAllComponentNames()
    {
        return _descriptors.Select(d => d.DisplayName).Distinct();
    }

    /// <summary>
    ///     Gets all categories.
    /// </summary>
    public IEnumerable<string> GetCategories()
    {
        return _descriptors.Where(d => d.Category != null).Select(d => d.Category!).Distinct();
    }

    /// <summary>
    ///     Determines entity name based on registered components (highest priority first).
    /// </summary>
    public string DetermineEntityName(Entity entity, List<string> components)
    {
        // Priority-based naming
        if (components.Contains("Player"))
        {
            return "Player";
        }

        if (components.Contains("Npc"))
        {
            return $"NPC_{entity.Id}";
        }

        if (components.Contains("MapInfo"))
        {
            return $"Map_{entity.Id}";
        }

        if (components.Contains("WarpPoint"))
        {
            return $"Warp_{entity.Id}";
        }

        if (components.Contains("TileSprite"))
        {
            return $"Tile_{entity.Id}";
        }

        if (components.Contains("AnimatedTile"))
        {
            return $"AnimTile_{entity.Id}";
        }

        if (components.Contains("Sprite") && components.Contains("Position"))
        {
            return $"Sprite_{entity.Id}";
        }

        return $"Entity_{entity.Id}";
    }

    /// <summary>
    ///     Determines entity tag based on components.
    /// </summary>
    public string? DetermineEntityTag(List<string> components)
    {
        if (components.Contains("Player"))
        {
            return "Player";
        }

        if (components.Contains("Npc"))
        {
            return "NPC";
        }

        if (components.Contains("MapInfo"))
        {
            return "Map";
        }

        if (components.Contains("WarpPoint"))
        {
            return "Warp";
        }

        if (components.Contains("TileSprite"))
        {
            return "Tile";
        }

        if (components.Contains("AnimatedTile"))
        {
            return "AnimatedTile";
        }

        if (components.Contains("Collision"))
        {
            return "Collision";
        }

        if (components.Contains("Sprite"))
        {
            return "Sprite";
        }

        if (components.Contains("Behavior"))
        {
            return "Behavior";
        }

        return components.Count > 0 ? components[0] : null;
    }

    /// <summary>
    ///     Describes a registered component type.
    /// </summary>
    public class ComponentDescriptor
    {
        public Type ComponentType { get; init; } = null!;
        public string DisplayName { get; init; } = "";
        public string? Category { get; init; }
        public Func<Entity, bool> HasComponent { get; init; } = _ => false;
        public Func<Entity, Dictionary<string, string>>? GetProperties { get; init; }
        public int Priority { get; init; }
    }
}
