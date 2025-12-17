using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Core.Modding.CustomTypes;
using MonoBallFramework.Game.Scripting.Api;

namespace MonoBallFramework.Game.Scripting.Services;

/// <summary>
///     Service implementation for custom types API.
///     Provides O(1) lookup for definitions by ID.
/// </summary>
public sealed class CustomTypesApiService : ICustomTypesApi
{
    // Category -> (LocalId -> Definition)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ICustomTypeDefinition>> _definitions =
        new();

    // Full ID -> Definition (for O(1) lookup by full ID)
    private readonly ConcurrentDictionary<string, ICustomTypeDefinition> _definitionsByFullId = new();
    private readonly ILogger<CustomTypesApiService> _logger;

    public CustomTypesApiService(ILogger<CustomTypesApiService> logger)
    {
        _logger = logger;
    }

    public ICustomTypeDefinition? GetDefinition(string id)
    {
        _definitionsByFullId.TryGetValue(id, out ICustomTypeDefinition? definition);
        return definition;
    }

    public ICustomTypeDefinition? GetDefinition(string category, string localId)
    {
        if (_definitions.TryGetValue(category, out ConcurrentDictionary<string, ICustomTypeDefinition>? categoryDict))
        {
            categoryDict.TryGetValue(localId, out ICustomTypeDefinition? definition);
            return definition;
        }

        return null;
    }

    public IEnumerable<ICustomTypeDefinition> GetAllDefinitions(string category)
    {
        return _definitions.TryGetValue(category, out ConcurrentDictionary<string, ICustomTypeDefinition>? categoryDict)
            ? categoryDict.Values
            : [];
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
        return _definitions.TryGetValue(category, out ConcurrentDictionary<string, ICustomTypeDefinition>? categoryDict)
            ? categoryDict.Count
            : 0;
    }

    public IEnumerable<ICustomTypeDefinition> Where(string category, Func<ICustomTypeDefinition, bool> predicate)
    {
        return GetAllDefinitions(category).Where(predicate);
    }

    /// <summary>
    ///     Registers a custom type definition.
    ///     Called by ModLoader when loading custom content.
    /// </summary>
    public void RegisterDefinition(ICustomTypeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        // Get or create category dictionary
        ConcurrentDictionary<string, ICustomTypeDefinition> categoryDict = _definitions.GetOrAdd(definition.Category,
            _ => new ConcurrentDictionary<string, ICustomTypeDefinition>());

        // Extract local ID from full ID (e.g., "weather:effect:rain" -> "rain")
        string localId = ExtractLocalId(definition.DefinitionId);

        // Register in both dictionaries
        categoryDict[localId] = definition;
        _definitionsByFullId[definition.DefinitionId] = definition;

        _logger.LogDebug("Registered custom type: {DefinitionId} in category {Category}", definition.DefinitionId,
            definition.Category);
    }

    /// <summary>
    ///     Registers a custom type category (for empty categories).
    /// </summary>
    public void RegisterCategory(string category)
    {
        _definitions.GetOrAdd(category, _ => new ConcurrentDictionary<string, ICustomTypeDefinition>());
        _logger.LogDebug("Registered custom type category: {Category}", category);
    }

    /// <summary>
    ///     Clears all registered definitions (for testing/hot reload).
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
        return lastColon >= 0 ? fullId[(lastColon + 1)..] : fullId;
    }
}
