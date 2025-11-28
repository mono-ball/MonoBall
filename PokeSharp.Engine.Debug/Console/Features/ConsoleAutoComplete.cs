using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Debug.Console.Configuration;

namespace PokeSharp.Engine.Debug.Console.Features;

/// <summary>
///     Provides reflection-based auto-completion for C# code.
///     Supports completion for runtime objects using reflection and ScriptState.
///     Thread Safety:
///     - This class is NOT thread-safe and should only be accessed from the main game thread.
///     - The GetCompletionsAsync method returns a Task but executes synchronously on the calling thread.
///     - Member cache is not protected by locks; concurrent access will result in undefined behavior.
///     - Script state and globals instance should only be updated from the main thread.
/// </summary>
public class ConsoleAutoComplete
{
    private const int MaxCacheSize = 100; // Limit cache to prevent unbounded growth

    // Compiled regex patterns for better performance
    private static readonly Regex MemberAccessRegex = new(@"(\w+)\.$", RegexOptions.Compiled);
    private static readonly Regex PartialMemberAccessRegex = new(
        @"(\w+)\.(\w*)$",
        RegexOptions.Compiled
    );
    private readonly ILogger? _logger;

    // Cache for reflection results to avoid repeated lookups
    private readonly Dictionary<Type, List<CompletionItem>> _memberCache = new();

    // Performance tracking
    private readonly Stopwatch _performanceTimer = new();
    private object? _globalsInstance;
    private List<string>? _importedNamespaces;
    private List<Assembly>? _referencedAssemblies;
    private ScriptState<object>? _scriptState;
    private long _totalCompletionRequests;
    private double _totalCompletionTimeMs;

    // Cache for type completions from assemblies
    private List<CompletionItem>? _typeCompletionsCache;

    public ConsoleAutoComplete(ILogger? logger = null)
    {
        _logger = logger;
        _logger?.LogDebug(
            "Auto-complete initialized with reflection-based completion and ScriptState tracking"
        );
    }

    /// <summary>
    ///     Set the globals instance to enable member completion
    /// </summary>
    public void SetGlobals(object globals)
    {
        _globalsInstance = globals;
        _logger?.LogAutoCompleteGlobalsSet(globals.GetType().Name);
    }

    /// <summary>
    ///     Update the script state after execution to track variables
    /// </summary>
    public void UpdateScriptState(ScriptState<object>? state)
    {
        _scriptState = state;
        if (state != null)
        {
            int varCount = state.Variables.Count();
            _logger?.LogAutoCompleteScriptStateUpdated(varCount);
        }
    }

    /// <summary>
    ///     Set the referenced assemblies and imported namespaces for type completion
    /// </summary>
    public void SetReferences(IEnumerable<Assembly> assemblies, IEnumerable<string> namespaces)
    {
        _referencedAssemblies = assemblies.ToList();
        _importedNamespaces = namespaces.ToList();
        _typeCompletionsCache = null; // Clear cache when references change
        _logger?.LogDebug(
            "AutoComplete references updated: {AssemblyCount} assemblies, {NamespaceCount} namespaces",
            _referencedAssemblies.Count,
            _importedNamespaces.Count
        );
    }

    /// <summary>
    ///     Clears the reflection member cache.
    ///     Useful for reducing memory usage or refreshing after dynamic assembly loads.
    /// </summary>
    public void ClearCache()
    {
        int count = _memberCache.Count;
        _memberCache.Clear();
        _logger?.LogInformation("Cleared autocomplete member cache ({Count} types removed)", count);
    }

    /// <summary>
    ///     Gets auto-completion suggestions for the given code at the cursor position using reflection.
    ///     Synchronous operation wrapped in Task for interface compatibility.
    /// </summary>
    public Task<List<CompletionItem>> GetCompletionsAsync(
        string code,
        int cursorPosition,
        string? globals = null
    )
    {
        _performanceTimer.Restart();
        List<CompletionItem> result = GetCompletions(code, cursorPosition, globals);
        _performanceTimer.Stop();

        // Track performance metrics
        _totalCompletionRequests++;
        _totalCompletionTimeMs += _performanceTimer.Elapsed.TotalMilliseconds;

        // Log performance occasionally (every 50 requests)
        if (_totalCompletionRequests % 50 == 0)
        {
            double avgTime = _totalCompletionTimeMs / _totalCompletionRequests;
            _logger?.LogDebug(
                "Autocomplete performance: {Requests} requests, avg {AvgMs:F2}ms, last {LastMs:F2}ms",
                _totalCompletionRequests,
                avgTime,
                _performanceTimer.Elapsed.TotalMilliseconds
            );
        }

        return Task.FromResult(result);
    }

    /// <summary>
    ///     Gets auto-completion suggestions synchronously.
    /// </summary>
    private List<CompletionItem> GetCompletions(
        string code,
        int cursorPosition,
        string? globals = null
    )
    {
        try
        {
            _logger?.LogAutoCompleteTriggered(code, cursorPosition);

            // Validate and clamp cursor position
            if (cursorPosition < 0)
            {
                _logger?.LogWarning(
                    "Cursor position {Pos} is negative, clamping to 0",
                    cursorPosition
                );
                cursorPosition = 0;
            }

            if (cursorPosition > code.Length)
            {
                _logger?.LogWarning(
                    "Cursor position {Pos} exceeds code length {Len}, clamping to length",
                    cursorPosition,
                    code.Length
                );
                cursorPosition = code.Length;
            }

            // Get text up to cursor for pattern matching
            string textUpToCursor = code.Substring(0, cursorPosition);

            // Check for member access using compiled regex (e.g., "player." or "World.")
            Match memberAccessMatch = MemberAccessRegex.Match(textUpToCursor);
            if (memberAccessMatch.Success)
            {
                string memberName = memberAccessMatch.Groups[1].Value;
                _logger?.LogAutoCompleteMemberAccess(memberName);

                List<CompletionItem> members = GetMembersForObject(memberName);
                _logger?.LogAutoCompleteMembersFound(members.Count, memberName);
                return members;
            }

            // Check for partial member access using compiled regex (e.g., "player.Na" - user typing after the dot)
            Match partialMemberMatch = PartialMemberAccessRegex.Match(textUpToCursor);
            if (partialMemberMatch.Success)
            {
                string objectName = partialMemberMatch.Groups[1].Value;
                string partial = partialMemberMatch.Groups[2].Value.ToLower();
                _logger?.LogDebug(
                    "Partial member access detected: '{ObjectName}.{Partial}'",
                    objectName,
                    partial
                );

                List<CompletionItem> members = GetMembersForObject(objectName);
                var filtered = members
                    .Where(m => m.DisplayText.ToLower().StartsWith(partial))
                    .ToList();
                _logger?.LogAutoCompleteMembersFound(filtered.Count, objectName);
                return filtered;
            }

            // Provide global variable completions
            List<CompletionItem> globalCompletions = GetGlobalCompletions();
            _logger?.LogAutoCompleteGlobalsProvided(globalCompletions.Count);
            return globalCompletions;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting auto-complete suggestions");

            // Provide fallback suggestions on error (C# keywords + basic help)
            return GetFallbackCompletions(ex.Message);
        }
    }

    /// <summary>
    ///     Get fallback completions when an error occurs
    ///     Provides basic C# keywords and a helpful error message
    /// </summary>
    private List<CompletionItem> GetFallbackCompletions(string errorMessage)
    {
        var fallbacks = new List<CompletionItem>();

        // Add a helpful error indicator
        fallbacks.Add(
            CompletionItem.Create(
                "⚠️ Error",
                inlineDescription: $"Autocomplete error: {TruncateMessage(errorMessage, 50)}"
            )
        );

        // Add basic C# keywords as fallback
        string[] keywords = ConsoleConstants.AutoComplete.Keywords;
        foreach (string keyword in keywords.Take(5)) // Just show a few to keep list short
        {
            fallbacks.Add(CompletionItem.Create(keyword, inlineDescription: "keyword"));
        }

        return fallbacks;
    }

    /// <summary>
    ///     Truncate an error message to a maximum length
    /// </summary>
    private static string TruncateMessage(string message, int maxLength)
    {
        if (string.IsNullOrEmpty(message) || message.Length <= maxLength)
        {
            return message;
        }

        return message.Substring(0, maxLength - 3) + "...";
    }

    /// <summary>
    ///     Get members (properties, fields, methods) for an object by name
    ///     Uses caching to avoid repeated reflection lookups
    /// </summary>
    private List<CompletionItem> GetMembersForObject(string objectName)
    {
        try
        {
            object? targetObject = null;
            Type? targetType = null;

            // First, try to find in script state variables (case-insensitive for user convenience)
            if (_scriptState != null)
            {
                ScriptVariable? variable = _scriptState.Variables.FirstOrDefault(v =>
                    string.Equals(v.Name, objectName, StringComparison.OrdinalIgnoreCase)
                );
                if (variable != null)
                {
                    targetObject = variable.Value;
                    targetType = variable.Type;
                    _logger?.LogInformation(
                        "Auto-complete object found: '{ObjectName}' of type {TypeName} in {Source}",
                        objectName,
                        targetType.Name,
                        "ScriptState"
                    );
                }
                else
                {
                    _logger?.LogInformation(
                        "Object '{ObjectName}' not found in ScriptState ({VarCount} variables available)",
                        objectName,
                        _scriptState.Variables.Count()
                    );
                }
            }
            else
            {
                _logger?.LogInformation("ScriptState is null, cannot search variables");
            }

            // If not found in script state, try globals (case-insensitive)
            if (targetType == null && _globalsInstance != null)
            {
                _logger?.LogInformation("Searching for '{ObjectName}' in globals...", objectName);
                Type globalsType = _globalsInstance.GetType();
                PropertyInfo? property = globalsType.GetProperty(
                    objectName,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase
                );
                FieldInfo? field = globalsType.GetField(
                    objectName,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase
                );

                if (property != null)
                {
                    targetObject = property.GetValue(_globalsInstance);
                    targetType = property.PropertyType;

                    // Null check: if the property value is null, we can still show its type members
                    if (targetObject == null)
                    {
                        _logger?.LogInformation(
                            "Auto-complete property '{ObjectName}' is null, showing type {TypeName} members",
                            objectName,
                            targetType.Name
                        );
                    }
                    else
                    {
                        _logger?.LogInformation(
                            "Auto-complete object found: '{ObjectName}' of type {TypeName} in {Source}",
                            objectName,
                            targetType.Name,
                            "globals property"
                        );
                    }
                }
                else if (field != null)
                {
                    targetObject = field.GetValue(_globalsInstance);
                    targetType = field.FieldType;

                    // Null check: if the field value is null, we can still show its type members
                    if (targetObject == null)
                    {
                        _logger?.LogInformation(
                            "Auto-complete field '{ObjectName}' is null, showing type {TypeName} members",
                            objectName,
                            targetType.Name
                        );
                    }
                    else
                    {
                        _logger?.LogInformation(
                            "Auto-complete object found: '{ObjectName}' of type {TypeName} in {Source}",
                            objectName,
                            targetType.Name,
                            "globals field"
                        );
                    }
                }
            }

            // If not found as a variable, try to find as a type name
            if (targetType == null)
            {
                targetType = FindTypeByName(objectName);
                if (targetType != null)
                {
                    _logger?.LogInformation(
                        "Found '{ObjectName}' as type {TypeName}",
                        objectName,
                        targetType.FullName
                    );

                    // For enums, show enum values
                    if (targetType.IsEnum)
                    {
                        return GetEnumValues(targetType);
                    }

                    // For types, show static members
                    return GetStaticMembersForType(targetType);
                }
            }

            if (targetType == null)
            {
                _logger?.LogWarning(
                    "Auto-complete object not found: '{ObjectName}' (searched ScriptState: {HasState}, Globals: {HasGlobals}, Types: {HasTypes})",
                    objectName,
                    _scriptState != null,
                    _globalsInstance != null,
                    _referencedAssemblies != null
                );
                return new List<CompletionItem>();
            }

            // Check cache first to avoid repeated reflection
            if (_memberCache.TryGetValue(targetType, out List<CompletionItem>? cachedMembers))
            {
                _logger?.LogInformation(
                    "Using cached members for type {TypeName} ({Count} items) | TypeHash: {Hash} | CacheEntry.Count: {CacheCount}",
                    targetType.Name,
                    cachedMembers.Count,
                    targetType.GetHashCode(),
                    _memberCache[targetType].Count
                );
                _logger?.LogInformation(
                    "First 5 cached members: {Members}",
                    string.Join(", ", cachedMembers.Take(5).Select(m => m.DisplayText))
                );
                _logger?.LogInformation(
                    "CachedMembers same reference as dictionary entry: {IsSame}",
                    ReferenceEquals(cachedMembers, _memberCache[targetType])
                );
                // Return a NEW list to prevent external modifications from affecting the cache
                return new List<CompletionItem>(cachedMembers);
            }

            // Get all public members via reflection
            _logger?.LogInformation(
                "Cache miss for type {TypeName}, performing reflection...",
                targetType.Name
            );
            List<CompletionItem> completions = GetMembersForType(targetType);

            // Cache the results for this type (with size limit)
            _logger?.LogInformation(
                "Reflection found {Count} members for type {TypeName}",
                completions.Count,
                targetType.Name
            );
            _logger?.LogInformation(
                "First 5 reflected members: {Members}",
                string.Join(", ", completions.Take(5).Select(m => m.DisplayText))
            );

            if (_memberCache.Count < MaxCacheSize)
            {
                _memberCache[targetType] = completions;
                _logger?.LogInformation(
                    "Cached members for type {TypeName} ({Count} items, cache size: {CacheSize})",
                    targetType.Name,
                    completions.Count,
                    _memberCache.Count
                );
            }
            else
            {
                _logger?.LogWarning(
                    "Member cache limit reached ({MaxSize}), not caching type {TypeName}",
                    MaxCacheSize,
                    targetType.Name
                );
            }

            return completions;
        }
        catch (TargetInvocationException ex)
        {
            _logger?.LogError(
                ex,
                "Error invoking reflection target for object '{ObjectName}'",
                objectName
            );
            return new List<CompletionItem>();
        }
        catch (ArgumentException ex)
        {
            _logger?.LogError(
                ex,
                "Invalid argument when reflecting on object '{ObjectName}'",
                objectName
            );
            return new List<CompletionItem>();
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "Unexpected error getting members for object '{ObjectName}'",
                objectName
            );
            return new List<CompletionItem>();
        }
    }

    /// <summary>
    ///     Gets completion items for all members of a type using reflection
    ///     Returns items sorted by category (properties, fields, then methods) and alphabetically within each category
    /// </summary>
    private List<CompletionItem> GetMembersForType(Type targetType)
    {
        var properties = new List<CompletionItem>();
        var fields = new List<CompletionItem>();
        var methods = new List<CompletionItem>();

        // Properties - highest priority (most commonly accessed)
        IOrderedEnumerable<PropertyInfo> propMembers = targetType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => !p.GetIndexParameters().Any()) // Exclude indexers
            .OrderBy(p => p.Name);
        foreach (PropertyInfo prop in propMembers)
        {
            properties.Add(
                CompletionItem.Create(
                    prop.Name,
                    inlineDescription: $"property: {GetFriendlyTypeName(prop.PropertyType)}"
                )
            );
        }

        // Fields - medium priority
        IOrderedEnumerable<FieldInfo> fieldMembers = targetType
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(f => f.Name);
        foreach (FieldInfo fld in fieldMembers)
        {
            fields.Add(
                CompletionItem.Create(
                    fld.Name,
                    inlineDescription: $"field: {GetFriendlyTypeName(fld.FieldType)}"
                )
            );
        }

        // Methods - lower priority (come after data members)
        IOrderedEnumerable<IGrouping<string, MethodInfo>> methodGroups = targetType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName) // Exclude property getters/setters
            .GroupBy(m => m.Name)
            .OrderBy(g => g.Key);

        foreach (IGrouping<string, MethodInfo> group in methodGroups)
        {
            MethodInfo method = group.First(); // Take first overload for display
            int overloadCount = group.Count();
            int paramCount = method.GetParameters().Length;

            // Create compact description
            string description;
            if (paramCount == 0)
            {
                description = overloadCount > 1 ? $"method() +{overloadCount - 1}" : "method()";
            }
            else
            {
                description =
                    overloadCount > 1 ? $"method(...) +{overloadCount - 1}" : "method(...)";
            }

            methods.Add(CompletionItem.Create(method.Name, inlineDescription: description));
        }

        // Combine in priority order: properties, fields, methods
        var completions = new List<CompletionItem>();
        completions.AddRange(properties);
        completions.AddRange(fields);
        completions.AddRange(methods);

        return completions;
    }

    /// <summary>
    ///     Get completions for global variables, script variables, types, and common keywords
    /// </summary>
    private List<CompletionItem> GetGlobalCompletions()
    {
        var completions = new List<CompletionItem>();

        // Add script state variables
        if (_scriptState != null)
        {
            foreach (ScriptVariable? variable in _scriptState.Variables)
            {
                completions.Add(
                    CompletionItem.Create(
                        variable.Name,
                        inlineDescription: $"var: {GetFriendlyTypeName(variable.Type)}"
                    )
                );
            }
        }

        // Add globals
        if (_globalsInstance != null)
        {
            Type globalsType = _globalsInstance.GetType();

            // Add all public properties from globals
            foreach (
                PropertyInfo prop in globalsType.GetProperties(
                    BindingFlags.Public | BindingFlags.Instance
                )
            )
            {
                completions.Add(
                    CompletionItem.Create(
                        prop.Name,
                        inlineDescription: $"{GetFriendlyTypeName(prop.PropertyType)}"
                    )
                );
            }

            // Add all public fields from globals
            foreach (
                FieldInfo field in globalsType.GetFields(
                    BindingFlags.Public | BindingFlags.Instance
                )
            )
            {
                completions.Add(
                    CompletionItem.Create(
                        field.Name,
                        inlineDescription: $"{GetFriendlyTypeName(field.FieldType)}"
                    )
                );
            }
        }

        // Add types from referenced assemblies (cached)
        completions.AddRange(GetTypeCompletions());

        // Add common C# keywords
        string[] keywords = new[]
        {
            "var",
            "int",
            "string",
            "bool",
            "float",
            "double",
            "if",
            "else",
            "for",
            "foreach",
            "while",
            "return",
            "true",
            "false",
            "null",
            "new",
        };
        foreach (string keyword in keywords)
        {
            completions.Add(CompletionItem.Create(keyword, inlineDescription: "keyword"));
        }

        return completions.OrderBy(c => c.DisplayText).ToList();
    }

    /// <summary>
    ///     Get type completions from referenced assemblies (cached for performance)
    /// </summary>
    private List<CompletionItem> GetTypeCompletions()
    {
        // Return cached types if available
        if (_typeCompletionsCache != null)
        {
            return _typeCompletionsCache;
        }

        var typeCompletions = new List<CompletionItem>();

        if (_referencedAssemblies == null || _importedNamespaces == null)
        {
            _logger?.LogDebug(
                "No referenced assemblies or namespaces set, skipping type completions"
            );
            return typeCompletions;
        }

        try
        {
            // Get all public types from imported namespaces
            var typesToInclude = new HashSet<Type>();

            foreach (Assembly assembly in _referencedAssemblies)
            {
                try
                {
                    IEnumerable<Type> assemblyTypes = assembly
                        .GetExportedTypes()
                        .Where(t => t.IsPublic && !t.IsNested); // Only top-level public types

                    foreach (Type type in assemblyTypes)
                    {
                        // Check if type is in an imported namespace (exact match only)
                        // In C#, importing "System" does NOT import "System.Globalization"
                        if (_importedNamespaces.Any(ns => type.Namespace == ns))
                        {
                            typesToInclude.Add(type);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(
                        ex,
                        "Error loading types from assembly {Assembly}",
                        assembly.FullName
                    );
                }
            }

            // Create completion items for types
            foreach (Type type in typesToInclude.OrderBy(t => t.Name))
            {
                string kind =
                    type.IsEnum ? "enum"
                    : type.IsInterface ? "interface"
                    : type.IsValueType ? "struct"
                    : type.IsAbstract && type.IsSealed ? "static class"
                    : "class";

                typeCompletions.Add(CompletionItem.Create(type.Name, inlineDescription: $"{kind}"));
            }

            _logger?.LogInformation(
                "Loaded {Count} type completions from {AssemblyCount} assemblies",
                typeCompletions.Count,
                _referencedAssemblies.Count
            );

            // Cache the results
            _typeCompletionsCache = typeCompletions;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error building type completions");
        }

        return typeCompletions;
    }

    /// <summary>
    ///     Find a type by name from referenced assemblies
    /// </summary>
    private Type? FindTypeByName(string typeName)
    {
        if (_referencedAssemblies == null || _importedNamespaces == null)
        {
            return null;
        }

        try
        {
            // Search through all referenced assemblies for a matching type name
            foreach (Assembly assembly in _referencedAssemblies)
            {
                try
                {
                    Type[] types = assembly.GetExportedTypes();

                    // Try exact match first
                    Type? exactMatch = types.FirstOrDefault(t =>
                        t.Name.Equals(typeName, StringComparison.Ordinal)
                        && t.IsPublic
                        && _importedNamespaces.Any(ns =>
                            t.Namespace != null
                            && (t.Namespace == ns || t.Namespace.StartsWith(ns + "."))
                        )
                    );

                    if (exactMatch != null)
                    {
                        return exactMatch;
                    }

                    // Try case-insensitive match
                    Type? caseInsensitiveMatch = types.FirstOrDefault(t =>
                        t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase)
                        && t.IsPublic
                        && _importedNamespaces.Any(ns =>
                            t.Namespace != null
                            && (t.Namespace == ns || t.Namespace.StartsWith(ns + "."))
                        )
                    );

                    if (caseInsensitiveMatch != null)
                    {
                        return caseInsensitiveMatch;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(
                        ex,
                        "Error searching for type '{TypeName}' in assembly {Assembly}",
                        typeName,
                        assembly.FullName
                    );
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error finding type '{TypeName}'", typeName);
        }

        return null;
    }

    /// <summary>
    ///     Get enum values for an enum type
    /// </summary>
    private List<CompletionItem> GetEnumValues(Type enumType)
    {
        var completions = new List<CompletionItem>();

        try
        {
            string[] names = Enum.GetNames(enumType);
            foreach (string name in names)
            {
                object value = Enum.Parse(enumType, name);
                completions.Add(
                    CompletionItem.Create(name, inlineDescription: $"{value} (enum value)")
                );
            }

            _logger?.LogInformation(
                "Found {Count} enum values for {EnumType}",
                completions.Count,
                enumType.Name
            );
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting enum values for {EnumType}", enumType.Name);
        }

        return completions;
    }

    /// <summary>
    ///     Get static members for a type (for types like Console, Math, etc.)
    /// </summary>
    private List<CompletionItem> GetStaticMembersForType(Type type)
    {
        var properties = new List<CompletionItem>();
        var fields = new List<CompletionItem>();
        var methods = new List<CompletionItem>();

        try
        {
            // Static properties
            IOrderedEnumerable<PropertyInfo> staticProps = type.GetProperties(
                    BindingFlags.Public | BindingFlags.Static
                )
                .Where(p => !p.GetIndexParameters().Any())
                .OrderBy(p => p.Name);

            foreach (PropertyInfo prop in staticProps)
            {
                properties.Add(
                    CompletionItem.Create(
                        prop.Name,
                        inlineDescription: $"static property: {GetFriendlyTypeName(prop.PropertyType)}"
                    )
                );
            }

            // Static fields (including const)
            IOrderedEnumerable<FieldInfo> staticFields = type.GetFields(
                    BindingFlags.Public | BindingFlags.Static
                )
                .OrderBy(f => f.Name);

            foreach (FieldInfo field in staticFields)
            {
                string kind = field.IsLiteral ? "const" : "static field";
                fields.Add(
                    CompletionItem.Create(
                        field.Name,
                        inlineDescription: $"{kind}: {GetFriendlyTypeName(field.FieldType)}"
                    )
                );
            }

            // Static methods
            IOrderedEnumerable<IGrouping<string, MethodInfo>> staticMethods = type.GetMethods(
                    BindingFlags.Public | BindingFlags.Static
                )
                .Where(m => !m.IsSpecialName)
                .GroupBy(m => m.Name)
                .OrderBy(g => g.Key);

            foreach (IGrouping<string, MethodInfo> group in staticMethods)
            {
                MethodInfo method = group.First();
                int overloadCount = group.Count();
                int paramCount = method.GetParameters().Length;

                // Create compact description
                string description;
                if (paramCount == 0)
                {
                    description =
                        overloadCount > 1
                            ? $"static method() +{overloadCount - 1}"
                            : "static method()";
                }
                else
                {
                    description =
                        overloadCount > 1
                            ? $"static method(...) +{overloadCount - 1}"
                            : "static method(...)";
                }

                methods.Add(CompletionItem.Create(method.Name, inlineDescription: description));
            }

            _logger?.LogInformation(
                "Found {PropCount} static properties, {FieldCount} static fields, {MethodCount} static methods for {TypeName}",
                properties.Count,
                fields.Count,
                methods.Count,
                type.Name
            );
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting static members for type {TypeName}", type.Name);
        }

        // Combine in priority order
        var completions = new List<CompletionItem>();
        completions.AddRange(properties);
        completions.AddRange(fields);
        completions.AddRange(methods);

        return completions;
    }

    /// <summary>
    ///     Converts a .NET Type to a friendly C# display name.
    ///     Handles nullables, arrays, pointers, generics, tuples, and primitives.
    ///     Includes protection against excessive nesting depth.
    /// </summary>
    /// <param name="type">The type to convert.</param>
    /// <returns>A C#-style type name string (e.g., "int?", "List&lt;string&gt;").</returns>
    private string GetFriendlyTypeName(Type type)
    {
        return GetFriendlyTypeName(type, 0);
    }

    private string GetFriendlyTypeName(Type type, int depth)
    {
        // Safety: prevent stack overflow on pathologically deep generic nesting
        if (depth > 10)
        {
            return type.Name;
        }

        // Handle ByRef types (ref parameters, e.g., ref int)
        if (type.IsByRef)
        {
            Type? elementType = type.GetElementType();
            return $"ref {GetFriendlyTypeName(elementType!, depth + 1)}";
        }

        // Handle nullable types (e.g., int?)
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            Type? underlyingType = Nullable.GetUnderlyingType(type);
            return underlyingType != null
                ? $"{GetFriendlyTypeName(underlyingType, depth + 1)}?"
                : type.Name;
        }

        // Handle arrays (e.g., int[], string[][])
        if (type.IsArray)
        {
            Type? elementType = type.GetElementType();
            string rankSpecifier = new(',', type.GetArrayRank() - 1);
            return $"{GetFriendlyTypeName(elementType!, depth + 1)}[{rankSpecifier}]";
        }

        // Handle pointers (e.g., int*)
        if (type.IsPointer)
        {
            Type? elementType = type.GetElementType();
            return $"{GetFriendlyTypeName(elementType!, depth + 1)}*";
        }

        // Handle generic types (e.g., List<int>, Dictionary<string, int>)
        // Supports nested generics like Dictionary<string, List<int>>
        if (type.IsGenericType)
        {
            Type genericType = type.GetGenericTypeDefinition();
            Type[] genericArgs = type.GetGenericArguments();
            string genericTypeName = genericType.Name.Substring(0, genericType.Name.IndexOf('`'));
            string genericArgsNames = string.Join(
                ", ",
                genericArgs.Select(arg => GetFriendlyTypeName(arg, depth + 1))
            );

            // Handle ValueTuple specially (show as (int, string) instead of ValueTuple<int, string>)
            if (genericType.FullName?.StartsWith("System.ValueTuple") == true)
            {
                return $"({genericArgsNames})";
            }

            return $"{genericTypeName}<{genericArgsNames}>";
        }

        // Handle primitive types and common aliases
        return type.Name switch
        {
            "Int16" => "short",
            "Int32" => "int",
            "Int64" => "long",
            "UInt16" => "ushort",
            "UInt32" => "uint",
            "UInt64" => "ulong",
            "Byte" => "byte",
            "SByte" => "sbyte",
            "Single" => "float",
            "Double" => "double",
            "Decimal" => "decimal",
            "Boolean" => "bool",
            "String" => "string",
            "Char" => "char",
            "Object" => "object",
            "Void" => "void",
            _ => type.Name,
        };
    }
}
