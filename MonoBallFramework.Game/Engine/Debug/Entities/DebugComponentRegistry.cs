using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using EntityExtensions = Arch.Core.Extensions.EntityExtensions;

// For Has<T> and Get<T> extension methods

namespace MonoBallFramework.Game.Engine.Debug.Entities;

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
    private readonly List<ComponentDescriptor> _descriptors = [];
    private readonly Dictionary<string, ComponentDescriptor> _nameToDescriptor = new();
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
            HasComponent = entity => entity.Has<T>()
        };

        _descriptors.Add(descriptor);
        _typeToDescriptor[typeof(T)] = descriptor;
        _nameToDescriptor[displayName] = descriptor;
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
            }
        };

        _descriptors.Add(descriptor);
        _typeToDescriptor[typeof(T)] = descriptor;
        _nameToDescriptor[displayName] = descriptor;
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
            GetProperties = entity => GetComponentPropertiesReflection(entity, componentType)
        };

        _descriptors.Add(descriptor);
        _typeToDescriptor[componentType] = descriptor;
        _nameToDescriptor[displayName] = descriptor;
        return this;
    }

    /// <summary>
    ///     Gets component properties using reflection on entity.Get<T>().
    /// </summary>
    // CA1031: Reflection operations can throw many exception types; catching general Exception is intentional
#pragma warning disable CA1031
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
#pragma warning restore CA1031

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
        catch (Exception)
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
        catch (Exception)
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
        return FormatValue(value, 0);
    }

    /// <summary>
    ///     Formats a value for display with support for arrays, collections, and complex types.
    ///     Supports nested indentation for multi-level collections.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <param name="indentLevel">The current indentation level (number of 2-space indents).</param>
    private static string FormatValue(object? value, int indentLevel)
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

                string dictIndent = new(' ', indentLevel * 2);
                string dictItemIndent = new(' ', (indentLevel + 1) * 2);
                var lines = new List<string> { $"{dictIndent}{{{dict.Count} entries}}" };

                foreach (DictionaryEntry entry in dict)
                {
                    string key = FormatValue(entry.Key, indentLevel + 1);
                    string val = FormatValue(entry.Value, indentLevel + 1);

                    // Split key and value into lines
                    string[] keyLines = key.Split('\n');
                    string[] valLines = val.Split('\n');

                    // If key is multiline, put all key lines first, then value on new line
                    if (keyLines.Length > 1)
                    {
                        // Add all key lines
                        foreach (string keyLine in keyLines)
                        {
                            lines.Add($"{dictItemIndent}{keyLine}");
                        }

                        // Add value on new line with proper indentation
                        lines.Add($"{dictItemIndent}→ {valLines[0]}");
                        for (int i = 1; i < valLines.Length; i++)
                        {
                            lines.Add($"{dictItemIndent}  {valLines[i]}");
                        }
                    }
                    else
                    {
                        // Simple case: key is single line, value can be multiline
                        lines.Add($"{dictItemIndent}{key}: {valLines[0]}");
                        for (int i = 1; i < valLines.Length; i++)
                        {
                            lines.Add($"{dictItemIndent}  {valLines[i]}");
                        }
                    }
                }

                return string.Join("\n", lines);

            // Handle arrays
            case Array arr:
                if (arr.Length == 0)
                {
                    return "[]";
                }

                return FormatArray(arr, indentLevel);

            // Handle common collections (non-generic)
            case ICollection collection:
                return collection.Count == 0 ? "[]" : FormatCollection(collection, indentLevel);

            // Handle IEnumerable (last resort for collections like HashSet<T>, List<T>, etc.)
            case IEnumerable enumerable when !(value is string):
                return FormatEnumerable(enumerable, indentLevel);

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
                    tupleItems.Add(FormatValue(tuple[i], indentLevel));
                }

                return $"({string.Join(", ", tupleItems)})";

            // Default: use ToString() or format as record if applicable
            default:
                // Check if this is a record type (especially *Id types) that should be formatted nicely
                Type valueType = value.GetType();
                if (ShouldFormatAsRecord(valueType, value))
                {
                    return FormatRecordType(value, valueType, indentLevel);
                }

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
    private static string FormatArray(Array arr, int indentLevel)
    {
        if (arr.Length == 0)
        {
            return "[]";
        }

        string arrayIndent = new(' ', indentLevel * 2);
        // Array items get 2 more levels of indentation (4 spaces) for proper nesting
        string itemIndent = new(' ', (indentLevel + 2) * 2);
        var lines = new List<string> { $"{arrayIndent}[{arr.Length} items]" };

        for (int i = 0; i < arr.Length; i++)
        {
            string itemValue = FormatValue(arr.GetValue(i), indentLevel + 2);
            // If the item is multiline, indent each line
            string[] itemLines = itemValue.Split('\n');
            lines.Add($"{itemIndent}[{i}]");
            // If item has content, add it on next line with extra indent
            if (itemLines.Length > 0 && !string.IsNullOrWhiteSpace(itemLines[0]))
            {
                lines.Add($"{itemIndent}  {itemLines[0]}");
                for (int j = 1; j < itemLines.Length; j++)
                {
                    // Skip trailing empty lines from the formatted value
                    if (j == itemLines.Length - 1 && string.IsNullOrWhiteSpace(itemLines[j]))
                    {
                        continue;
                    }

                    lines.Add($"{itemIndent}  {itemLines[j]}");
                }
            }

            // Add blank line after each item (except the last) for better readability
            if (i < arr.Length - 1)
            {
                lines.Add("");
            }
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    ///     Formats a collection for display - always multiline, shows all items.
    /// </summary>
    private static string FormatCollection(ICollection collection, int indentLevel)
    {
        if (collection.Count == 0)
        {
            return "[]";
        }

        string collectionIndent = new(' ', indentLevel * 2);
        // Collection items get 2 more levels of indentation (4 spaces) for proper nesting
        string itemIndent = new(' ', (indentLevel + 2) * 2);
        var lines = new List<string> { $"{collectionIndent}[{collection.Count} items]" };

        int idx = 0;
        foreach (object? item in collection)
        {
            string itemValue = FormatValue(item, indentLevel + 2);
            // If the item is multiline, indent each line
            string[] itemLines = itemValue.Split('\n');
            lines.Add($"{itemIndent}[{idx}]");
            // If item has content, add it on next line with extra indent
            if (itemLines.Length > 0 && !string.IsNullOrWhiteSpace(itemLines[0]))
            {
                lines.Add($"{itemIndent}  {itemLines[0]}");
                for (int j = 1; j < itemLines.Length; j++)
                {
                    // Skip trailing empty lines from the formatted value
                    if (j == itemLines.Length - 1 && string.IsNullOrWhiteSpace(itemLines[j]))
                    {
                        continue;
                    }

                    lines.Add($"{itemIndent}  {itemLines[j]}");
                }
            }

            // Add blank line after each item (except the last) for better readability
            if (idx < collection.Count - 1)
            {
                lines.Add("");
            }

            idx++;
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    ///     Formats an IEnumerable for display - always multiline, shows all items.
    /// </summary>
    private static string FormatEnumerable(IEnumerable enumerable, int indentLevel)
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

        string enumerableIndent = new(' ', indentLevel * 2);
        // Enumerable items get 2 more levels of indentation (4 spaces) for proper nesting
        string itemIndent = new(' ', (indentLevel + 2) * 2);
        var lines = new List<string> { $"{enumerableIndent}[{items.Count} items]" };
        for (int i = 0; i < items.Count; i++)
        {
            string itemValue = FormatValue(items[i], indentLevel + 2);
            // If the item is multiline, indent each line
            string[] itemLines = itemValue.Split('\n');
            lines.Add($"{itemIndent}[{i}]");
            // If item has content, add it on next line with extra indent
            if (itemLines.Length > 0 && !string.IsNullOrWhiteSpace(itemLines[0]))
            {
                lines.Add($"{itemIndent}  {itemLines[0]}");
                for (int j = 1; j < itemLines.Length; j++)
                {
                    // Skip trailing empty lines from the formatted value
                    if (j == itemLines.Length - 1 && string.IsNullOrWhiteSpace(itemLines[j]))
                    {
                        continue;
                    }

                    lines.Add($"{itemIndent}  {itemLines[j]}");
                }
            }

            // Add blank line after each item (except the last) for better readability
            if (i < items.Count - 1)
            {
                lines.Add("");
            }
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    ///     Determines if a type should be formatted as a record (with properties on separate lines).
    ///     This is especially useful for *Id types like GameNpcId, SpriteId, etc.
    /// </summary>
    private static bool ShouldFormatAsRecord(Type type, object value)
    {
        // Check if type name ends with "Id" (like GameNpcId, SpriteId, etc.)
        if (type.Name.EndsWith("Id", StringComparison.Ordinal))
        {
            // Check if it has public properties (records typically do)
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            if (properties.Length > 0)
            {
                return true;
            }
        }

        // Also check for record types by looking for compiler-generated Equals/GetHashCode
        // Records have these methods, but this is a heuristic
        return false;
    }

    /// <summary>
    ///     Formats a record type with each property on its own line for better readability.
    ///     Especially useful for *Id types that have multiple properties.
    /// </summary>
    private static string FormatRecordType(object value, Type type, int indentLevel)
    {
        var lines = new List<string>();
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        if (properties.Length == 0)
        {
            // Fallback to ToString() if no properties
            return value.ToString() ?? "<unknown>";
        }

        // For simple records with just a Value property, show it inline
        if (properties.Length == 1 && properties[0].Name == "Value")
        {
            object? propValue = properties[0].GetValue(value);
            return FormatValue(propValue, indentLevel);
        }

        // For records with multiple properties, show each on its own line with clear delimiters
        string recordIndent = new(' ', indentLevel * 2);
        string propIndent = new(' ', (indentLevel + 1) * 2);

        // Add opening brace for record
        lines.Add($"{recordIndent}{{");

        foreach (PropertyInfo prop in properties)
        {
            try
            {
                object? propValue = prop.GetValue(value);
                string formattedValue = FormatValue(propValue, indentLevel + 1);
                // If the value is multiline, indent each line
                string[] valueLines = formattedValue.Split('\n');
                lines.Add($"{propIndent}{prop.Name}: {valueLines[0]}");
                for (int i = 1; i < valueLines.Length; i++)
                {
                    lines.Add($"{propIndent}{valueLines[i]}");
                }
            }
            catch
            {
                // Skip properties that can't be read
            }
        }

        if (lines.Count == 1)
        {
            // Only opening brace was added, no properties - fallback
            return value.ToString() ?? "<unknown>";
        }

        // Add closing brace
        lines.Add($"{recordIndent}}}");
        return string.Join("\n", lines);
    }

    /// <summary>
    ///     Detects all registered components on an entity.
    ///     Uses Arch's native GetComponentTypes() for O(1) performance instead of O(M) reflection.
    /// </summary>
    public List<string> DetectComponents(Entity entity)
    {
        try
        {
            // Use Arch's native GetComponentTypes() - O(1) instead of O(M) reflection calls
            ComponentType[] componentTypes = entity.GetComponentTypes();

            var detectedDescriptors = new List<ComponentDescriptor>(componentTypes.Length);
            var unregisteredNames = new List<string>();

            foreach (ComponentType ct in componentTypes)
            {
                if (_typeToDescriptor.TryGetValue(ct.Type, out ComponentDescriptor? descriptor))
                {
                    detectedDescriptors.Add(descriptor);
                }
                else
                {
                    // Component type not in registry - use type name directly
                    unregisteredNames.Add(ct.Type.Name);
                }
            }

            // Sort registered components by priority, then name
            var result = detectedDescriptors
                .OrderByDescending(d => d.Priority)
                .ThenBy(d => d.DisplayName)
                .Select(d => d.DisplayName)
                .ToList();

            // Append unregistered components at end (sorted alphabetically)
            result.AddRange(unregisteredNames.OrderBy(n => n));

            return result;
        }
        catch
        {
            // Entity may be dead/invalid - fall back to empty list
            return new List<string>();
        }
    }

    /// <summary>
    ///     Gets properties for all components on an entity.
    /// </summary>
    public Dictionary<string, string> GetEntityProperties(Entity entity)
    {
        var properties = new Dictionary<string, string>();

        try
        {
            foreach (
                ComponentDescriptor descriptor in _descriptors.Where(d =>
                    d.HasComponent(entity) && d.GetProperties != null
                )
            )
            {
                try
                {
                    Dictionary<string, string> componentProps = descriptor.GetProperties!(entity);
                    foreach ((string key, string value) in componentProps)
                    {
                        // Prefix with component name to avoid collisions
                        properties[$"{descriptor.DisplayName}.{key}"] = value;
                    }
                }
                catch
                {
                    // Skip components that fail to read
                }
            }
        }
        catch
        {
            // Entity may be dead/invalid - return partial results
        }

        return properties;
    }

    /// <summary>
    ///     Gets a simple properties dictionary (no component prefix) for display.
    /// </summary>
    public Dictionary<string, string> GetSimpleProperties(Entity entity)
    {
        var properties = new Dictionary<string, string>();

        try
        {
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
        }
        catch
        {
            // Entity may be dead/invalid - return partial results
        }

        return properties;
    }

    /// <summary>
    ///     Gets a simple properties dictionary using pre-detected component names (avoids duplicate detection).
    /// </summary>
    public Dictionary<string, string> GetSimpleProperties(Entity entity, IReadOnlyList<string> detectedComponentNames)
    {
        var properties = new Dictionary<string, string>();

        try
        {
            foreach (string componentName in detectedComponentNames)
            {
                // O(1) lookup by display name instead of O(N) iteration
                if (_nameToDescriptor.TryGetValue(componentName, out ComponentDescriptor? descriptor))
                {
                    if (descriptor.GetProperties != null)
                    {
                        try
                        {
                            Dictionary<string, string> componentProps = descriptor.GetProperties(entity);
                            foreach ((string key, string value) in componentProps)
                            {
                                // Use simple key, last one wins in case of collision
                                properties[key] = value;
                            }
                        }
                        catch
                        {
                            // Skip components that fail to read
                        }
                    }
                }
            }
        }
        catch
        {
            // Entity may be dead/invalid - return partial results
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
            componentData["_GetComponentData_Error"] = new Dictionary<string, string> { ["error"] = ex.Message };
        }

        return componentData;
    }

    /// <summary>
    ///     Gets component data using pre-detected component names (avoids duplicate detection).
    ///     This is a performance optimization that bypasses the expensive HasComponent check
    ///     when the caller already knows which components are present.
    /// </summary>
    /// <param name="entity">The entity to get component data from.</param>
    /// <param name="detectedComponentNames">Pre-detected list of component names (from cache).</param>
    /// <returns>Dictionary mapping component names to their field dictionaries.</returns>
    public Dictionary<string, Dictionary<string, string>> GetComponentData(
        Entity entity,
        IReadOnlyList<string> detectedComponentNames
    )
    {
        var componentData = new Dictionary<string, Dictionary<string, string>>();

        try
        {
            // Use pre-detected components instead of re-checking all descriptors
            foreach (string componentName in detectedComponentNames)
            {
                var fields = new Dictionary<string, string>();

                // O(1) lookup by display name instead of O(N) iteration
                if (_nameToDescriptor.TryGetValue(componentName, out ComponentDescriptor? descriptor))
                {
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
                }

                // Unregistered component - add empty entry
                // This can happen if component detection found a component
                // that wasn't explicitly registered with a GetProperties function
                // Always add to componentData (even if empty) so we know it's there
                componentData[componentName] = fields;
            }
        }
        catch (Exception ex)
        {
            // Add error to help debug
            componentData["_GetComponentData_Error"] = new Dictionary<string, string> { ["error"] = ex.Message };
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
