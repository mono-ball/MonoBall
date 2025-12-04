using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using EntityExtensions = Arch.Core.Extensions.EntityExtensions;

// For Has<T> and Get<T> extension methods

namespace PokeSharp.Game.Engine.Debug.Entities;

/// <summary>
///     Registry for ECS component types used in the entity browser.
///     Supports both static (compile-time) and dynamic (runtime) component registration.
///     Uses Arch's GetComponentTypes() for efficient component detection.
/// </summary>
public class DebugComponentRegistry
{
    // Cache for Has<T> method lookups
    private static readonly Dictionary<Type, MethodInfo> _hasMethodCache = new();
    private static readonly Dictionary<Type, MethodInfo> _getMethodCache = new();
    private static readonly Type _entityExtensionsType = typeof(EntityExtensions);
    private readonly List<ComponentDescriptor> _descriptors = new();
    private readonly Dictionary<Type, ComponentDescriptor> _typeToDescriptor = new();

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
            // Create a GetProperties function that uses entity.Get<T> with reflection
            GetProperties = entity => GetComponentPropertiesReflection(entity, componentType),
        };

        _descriptors.Add(descriptor);
        _typeToDescriptor[componentType] = descriptor;
        return this;
    }

    /// <summary>
    ///     Gets component properties using reflection on entity.Get<T>().
    /// </summary>
    private static Dictionary<string, string> GetComponentPropertiesReflection(
        Entity entity,
        Type componentType
    )
    {
        var properties = new Dictionary<string, string>();

        try
        {
            if (!HasComponentDynamic(entity, componentType))
            {
                return properties;
            }

            // Get the component using entity.Get<T>() via reflection
            // Note: Arch's Get method might have "in Entity" or "ref Entity" parameter
            if (!_getMethodCache.TryGetValue(componentType, out MethodInfo? getMethod))
            {
                MethodInfo? genericGet = _entityExtensionsType
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m =>
                    {
                        if (m.Name != "Get" || !m.IsGenericMethod)
                        {
                            return false;
                        }

                        ParameterInfo[] parameters = m.GetParameters();
                        // Check for single Entity parameter (might be by-ref or in)
                        return parameters.Length == 1;
                    });

                if (genericGet == null)
                {
                    properties["_error"] = "Get<T> method not found via reflection";
                    return properties;
                }

                getMethod = genericGet.MakeGenericMethod(componentType);
                _getMethodCache[componentType] = getMethod;
            }

            // Invoke Get<T> - even though it returns ref T, Invoke will box it
            object? component = getMethod.Invoke(null, new object[] { entity });

            if (component == null)
            {
                properties["_error"] = "Get<T> returned null";
                return properties;
            }

            // Extract fields and properties
            properties = ExtractComponentFields(component, componentType);

            // Empty structs (flags) won't have any fields - this is expected
            // Don't add debug message for empty components
        }
        catch (Exception ex)
        {
            // Add detailed error info for debugging
            properties["_error"] = $"{ex.GetType().Name}: {ex.Message}";
            if (ex.InnerException != null)
            {
                properties["_inner_error"] = ex.InnerException.Message;
            }
        }

        return properties;
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
                    .First(m =>
                        m.Name == "Has" && m.GetParameters().Length == 1 && m.IsGenericMethod
                    );

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
    private static Dictionary<string, string> GetPropertiesDynamic(
        Entity entity,
        Type componentType
    )
    {
        var properties = new Dictionary<string, string>();

        try
        {
            if (!HasComponentDynamic(entity, componentType))
            {
                return properties;
            }

            // Note: This method doesn't have access to World, so it can't extract values
            // Use GetPropertiesDynamicWithWorld instead when World is available
            return properties;
        }
        catch
        {
            // Return empty on any error
        }

        return properties;
    }

    /// <summary>
    ///     Gets component properties using reflection with World access.
    ///     Extracts public properties and fields from the component.
    /// </summary>
    private static Dictionary<string, string> GetPropertiesDynamicWithWorld(
        Entity entity,
        Type componentType,
        World world
    )
    {
        var properties = new Dictionary<string, string>();

        try
        {
            // Use World.GetComponent<T>(Entity) which returns by value
            MethodInfo? getComponentMethod = typeof(World)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m =>
                    m.Name == "GetComponent" && m.IsGenericMethod && m.GetParameters().Length == 1
                );

            if (getComponentMethod == null)
            {
                return properties;
            }

            MethodInfo genericGet = getComponentMethod.MakeGenericMethod(componentType);
            object? component = genericGet.Invoke(world, new object[] { entity });

            if (component == null)
            {
                return properties;
            }

            // Extract public properties
            foreach (
                PropertyInfo prop in componentType.GetProperties(
                    BindingFlags.Public | BindingFlags.Instance
                )
            )
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
            foreach (
                FieldInfo field in componentType.GetFields(
                    BindingFlags.Public | BindingFlags.Instance
                )
            )
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
    ///     Formats a value for display with support for arrays, collections, and complex types.
    /// </summary>
    private static string FormatValue(object? value)
    {
        if (value == null)
        {
            return "null";
        }

        // Handle common types nicely
        switch (value)
        {
            case float f:
                return f.ToString("F2");
            case double d:
                return d.ToString("F2");
            case bool b:
                return b.ToString().ToLower();
            case Enum e:
                return e.ToString();

            // Handle dictionaries BEFORE ICollection (since IDictionary inherits from ICollection)
            case IDictionary dict:
                if (dict.Count == 0)
                {
                    return "{}";
                }

                var lines = new List<string> { $"{{{dict.Count} entries}}" };

                foreach (DictionaryEntry entry in dict)
                {
                    string key = FormatValue(entry.Key);
                    string val = FormatValue(entry.Value);
                    lines.Add($"  {key}: {val}");
                }

                return string.Join("\n", lines);

            // Handle arrays
            case Array arr:
                if (arr.Length == 0)
                {
                    return "[]";
                }

                return FormatArray(arr);

            // Handle common collections (non-generic)
            case ICollection collection:
                if (collection.Count == 0)
                {
                    return "[]";
                }

                return FormatCollection(collection);

            // Handle IEnumerable (last resort for collections like HashSet<T>, List<T>, etc.)
            case IEnumerable enumerable when !(value is string):
                return FormatEnumerable(enumerable);

            // Handle XNA/MonoGame types
            case Vector2 v2:
                return $"({v2.X:F1}, {v2.Y:F1})";
            case Vector3 v3:
                return $"({v3.X:F1}, {v3.Y:F1}, {v3.Z:F1})";
            case Rectangle rect:
                return $"({rect.X}, {rect.Y}, {rect.Width}x{rect.Height})";
            case Point pt:
                return $"({pt.X}, {pt.Y})";
            case Color color:
                return $"#{color.R:X2}{color.G:X2}{color.B:X2}{(color.A < 255 ? color.A.ToString("X2") : "")}";

            // Handle ValueTuple types
            case ITuple tuple:
                var tupleItems = new List<string>();
                for (int i = 0; i < tuple.Length; i++)
                {
                    tupleItems.Add(FormatValue(tuple[i]));
                }

                return $"({string.Join(", ", tupleItems)})";

            // Default: use ToString()
            default:
                string str = value.ToString() ?? "?";
                // If ToString() just returns the type name, it's not helpful
                if (str == value.GetType().FullName || str == value.GetType().Name)
                {
                    return $"<{value.GetType().Name}>";
                }

                return str;
        }
    }

    /// <summary>
    ///     Formats an array for display - always multiline, shows all items.
    /// </summary>
    private static string FormatArray(Array arr)
    {
        if (arr.Length == 0)
        {
            return "[]";
        }

        var lines = new List<string> { $"[{arr.Length} items]" };

        for (int i = 0; i < arr.Length; i++)
        {
            lines.Add($"  [{i}] {FormatValue(arr.GetValue(i))}");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    ///     Formats a collection for display - always multiline, shows all items.
    /// </summary>
    private static string FormatCollection(ICollection collection)
    {
        if (collection.Count == 0)
        {
            return "[]";
        }

        var lines = new List<string> { $"[{collection.Count} items]" };

        int idx = 0;
        foreach (object? item in collection)
        {
            lines.Add($"  [{idx}] {FormatValue(item)}");
            idx++;
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    ///     Formats an IEnumerable for display - always multiline, shows all items.
    /// </summary>
    private static string FormatEnumerable(IEnumerable enumerable)
    {
        var items = new List<object?>();
        foreach (object? item in enumerable)
        {
            items.Add(item);
        }

        if (items.Count == 0)
        {
            return "[]";
        }

        var lines = new List<string> { $"[{items.Count} items]" };
        for (int i = 0; i < items.Count; i++)
        {
            lines.Add($"  [{i}] {FormatValue(items[i])}");
        }

        return string.Join("\n", lines);
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
    ///     Gets component data grouped by component name for structured display.
    ///     Uses registered GetProperties functions to extract component field values.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> GetComponentData(Entity entity)
    {
        var componentData = new Dictionary<string, Dictionary<string, string>>();

        try
        {
            var componentsOnEntity = _descriptors.Where(d => d.HasComponent(entity)).ToList();

            // Iterate through registered descriptors and extract component data
            foreach (ComponentDescriptor descriptor in componentsOnEntity)
            {
                var fields = new Dictionary<string, string>();

                // If descriptor has GetProperties, use it (already has the component data)
                if (descriptor.GetProperties != null)
                {
                    try
                    {
                        Dictionary<string, string> props = descriptor.GetProperties(entity);
                        fields = props;
                    }
                    catch (Exception ex)
                    {
                        // Add error for debugging
                        fields["_error"] = $"Failed: {ex.Message}";
                    }
                }

                // Always add to componentData (even if empty) so we know it's there
                componentData[descriptor.DisplayName] = fields;
            }
        }
        catch (Exception ex)
        {
            // Add error to help debug
            componentData["_GetComponentData_Error"] = new Dictionary<string, string>
            {
                ["error"] = ex.Message,
            };
        }

        return componentData;
    }

    /// <summary>
    ///     Extracts public fields and properties from a component object.
    /// </summary>
    private static Dictionary<string, string> ExtractComponentFields(
        object component,
        Type componentType
    )
    {
        var fields = new Dictionary<string, string>();

        // Extract public properties
        foreach (
            PropertyInfo prop in componentType.GetProperties(
                BindingFlags.Public | BindingFlags.Instance
            )
        )
        {
            try
            {
                object? value = prop.GetValue(component);
                fields[prop.Name] = FormatValue(value);
            }
            catch
            {
                // Skip properties that can't be read
            }
        }

        // Extract public fields
        foreach (
            FieldInfo field in componentType.GetFields(BindingFlags.Public | BindingFlags.Instance)
        )
        {
            try
            {
                object? value = field.GetValue(component);
                fields[field.Name] = FormatValue(value);
            }
            catch
            {
                // Skip fields that can't be read
            }
        }

        return fields;
    }

    /// <summary>
    ///     Gets all registered component names.
    /// </summary>
    public IEnumerable<string> GetAllComponentNames()
    {
        return _descriptors.Select(d => d.DisplayName).Distinct();
    }

    /// <summary>
    ///     Gets count of registered descriptors with GetProperties function.
    /// </summary>
    public (int Total, int WithGetProperties) GetRegistrationStats()
    {
        int total = _descriptors.Count;
        int withProps = _descriptors.Count(d => d.GetProperties != null);
        return (total, withProps);
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
