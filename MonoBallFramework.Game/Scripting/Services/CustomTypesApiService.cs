using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Core.Modding.CustomTypes;
using MonoBallFramework.Game.Scripting.Api;

namespace MonoBallFramework.Game.Scripting.Services;

/// <summary>
/// Service implementation for custom types API.
/// Provides O(1) lookup for definitions by ID.
/// </summary>
public sealed class CustomTypesApiService : ICustomTypesApi
{
    private readonly ILogger<CustomTypesApiService> _logger;

    // Category -> (LocalId -> Definition)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ICustomTypeDefinition>> _definitions = new();

    // Full ID -> Definition (for O(1) lookup by full ID)
    private readonly ConcurrentDictionary<string, ICustomTypeDefinition> _definitionsByFullId = new();

    public CustomTypesApiService(ILogger<CustomTypesApiService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Registers a custom type definition.
    /// Called by ModLoader when loading custom content.
    /// </summary>
    public void RegisterDefinition(ICustomTypeDefinition definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));

        // Get or create category dictionary
        var categoryDict = _definitions.GetOrAdd(definition.Category, _ => new ConcurrentDictionary<string, ICustomTypeDefinition>());

        // Extract local ID from full ID (e.g., "weather:effect:rain" -> "rain")
        string localId = ExtractLocalId(definition.Id);

        // Register in both dictionaries
        categoryDict[localId] = definition;
        _definitionsByFullId[definition.Id] = definition;

        _logger.LogDebug("Registered custom type: {Id} in category {Category}", definition.Id, definition.Category);
    }

    /// <summary>
    /// Registers a custom type category (for empty categories).
    /// </summary>
    public void RegisterCategory(string category)
    {
        _definitions.GetOrAdd(category, _ => new ConcurrentDictionary<string, ICustomTypeDefinition>());
        _logger.LogDebug("Registered custom type category: {Category}", category);
    }

    public ICustomTypeDefinition? GetDefinition(string id)
    {
        _definitionsByFullId.TryGetValue(id, out var definition);
        return definition;
    }

    public ICustomTypeDefinition? GetDefinition(string category, string localId)
    {
        if (_definitions.TryGetValue(category, out var categoryDict))
        {
            categoryDict.TryGetValue(localId, out var definition);
            return definition;
        }
        return null;
    }

    public IEnumerable<ICustomTypeDefinition> GetAllDefinitions(string category)
    {
        if (_definitions.TryGetValue(category, out var categoryDict))
        {
            return categoryDict.Values;
        }
        return Enumerable.Empty<ICustomTypeDefinition>();
    }

    public IReadOnlyCollection<string> GetCategories()
    {
        return _definitions.Keys.ToList();
    }

    public bool HasCategory(string category)
    {
        return _definitions.ContainsKey(category);
    }

    public int GetDefinitionCount(string category)
    {
        if (_definitions.TryGetValue(category, out var categoryDict))
        {
            return categoryDict.Count;
        }
        return 0;
    }

    public IEnumerable<ICustomTypeDefinition> Where(string category, Func<ICustomTypeDefinition, bool> predicate)
    {
        return GetAllDefinitions(category).Where(predicate);
    }

    /// <summary>
    /// Clears all registered definitions (for testing/hot reload).
    /// </summary>
    public void Clear()
    {
        _definitions.Clear();
        _definitionsByFullId.Clear();
    }

    private static string ExtractLocalId(string fullId)
    {
        // Full ID format: "mod:type:localid" or just "localid"
        int lastColon = fullId.LastIndexOf(':');
        return lastColon >= 0 ? fullId.Substring(lastColon + 1) : fullId;
    }
}
