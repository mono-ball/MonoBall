using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Content;

namespace MonoBallFramework.Game.Engine.Core.Types;

/// <summary>
///     Thread-safe registry for type definitions with hot-reload support.
///     Manages loading, caching, and hot-reloading of moddable type definitions.
/// </summary>
/// <typeparam name="T">Type definition class that implements ITypeDefinition.</typeparam>
/// <remarks>
///     TypeRegistry provides O(1) lookup performance using ConcurrentDictionary.
///     Supports async JSON loading and Roslyn script compilation for IScriptedType implementations.
///     Thread-safe for use in multi-threaded game engines.
///     Uses IContentProvider for mod-aware loading when available, with fallback to direct path.
///     SCRIPT PATTERN:
///     Scripts are stored as object instances (ScriptBase) and cached as singletons.
///     Cast to ScriptBase in the consuming system (e.g., NpcBehaviorSystem).
/// </remarks>
public class TypeRegistry<T> : IAsyncDisposable
    where T : ITypeDefinition
{
    private readonly string _contentType;
    private readonly string _dataPath;
    private readonly ConcurrentDictionary<string, T> _definitions = new();
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, object> _scripts = new();
    private readonly IServiceProvider? _serviceProvider;

    // Lazy resolution to avoid circular dependency:
    // ModLoader -> TypeRegistry -> IContentProvider -> IModLoader -> ModLoader
    private IContentProvider? _contentProvider;
    private bool _contentProviderResolved;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TypeRegistry{T}" /> class.
    /// </summary>
    /// <param name="dataPath">Fallback path for definitions when ContentProvider is not available.</param>
    /// <param name="contentType">Content type for ContentProvider resolution (e.g., "BehaviorDefinitions").</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="serviceProvider">Service provider for lazy IContentProvider resolution.</param>
    public TypeRegistry(
        string dataPath,
        string contentType,
        ILogger logger,
        IServiceProvider? serviceProvider = null)
    {
        _dataPath = dataPath ?? throw new ArgumentNullException(nameof(dataPath));
        _contentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    ///     Legacy constructor for backward compatibility.
    /// </summary>
    /// <param name="dataPath">Path for definitions.</param>
    /// <param name="logger">Logger instance.</param>
    [Obsolete("Use the constructor with contentType and serviceProvider parameters for mod support.")]
    public TypeRegistry(string dataPath, ILogger logger)
        : this(dataPath, string.Empty, logger)
    {
    }

    /// <summary>
    ///     Gets the content provider, resolving it lazily to avoid circular dependency.
    /// </summary>
    private IContentProvider? ContentProvider
    {
        get
        {
            if (!_contentProviderResolved && _serviceProvider != null)
            {
                _contentProvider = _serviceProvider.GetService(typeof(IContentProvider)) as IContentProvider;
                _contentProviderResolved = true;

                if (_contentProvider != null)
                {
                    _logger.LogDebug(
                        "TypeRegistry<{TypeName}> resolved IContentProvider for content type '{ContentType}'",
                        typeof(T).Name,
                        _contentType);
                }
            }

            return _contentProvider;
        }
    }

    /// <summary>
    ///     Get count of registered types.
    /// </summary>
    public int Count => _definitions.Count;

    /// <summary>
    ///     Asynchronously disposes resources and clears script instances.
    ///     Errors during disposal are logged but do not prevent other resources from being disposed.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // Dispose any script instances that implement IAsyncDisposable
        foreach (object script in _scripts.Values)
        {
            try
            {
                if (script is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else if (script is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception ex)
            {
                // Log disposal errors but continue disposing other resources
                _logger.LogError(
                    ex,
                    "Error disposing script instance of type {Type}",
                    script.GetType().Name
                );
            }
        }

        Clear();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Register a compiled script instance for a type.
    ///     Called externally after script compilation.
    /// </summary>
    /// <param name="typeId">The type identifier.</param>
    /// <param name="scriptInstance">The compiled script instance (should be ScriptBase).</param>
    public void RegisterScript(string typeId, object scriptInstance)
    {
        if (string.IsNullOrWhiteSpace(typeId))
        {
            throw new ArgumentException("TypeId cannot be null or empty", nameof(typeId));
        }

        if (scriptInstance == null)
        {
            throw new ArgumentNullException(nameof(scriptInstance));
        }

        _scripts[typeId] = scriptInstance;
        _logger.LogDebug("Registered script for {TypeId}", typeId);
    }

    /// <summary>
    ///     Load all type definitions from JSON files.
    ///     Uses IContentProvider for mod-aware loading when available (searches mods by priority, then base game).
    ///     Falls back to direct path loading when ContentProvider is not available.
    /// </summary>
    /// <returns>Number of types successfully loaded.</returns>
    public async Task<int> LoadAllAsync()
    {
        IEnumerable<string> jsonFiles;

        // Try to use ContentProvider for mod-aware loading
        if (ContentProvider != null && !string.IsNullOrEmpty(_contentType))
        {
            jsonFiles = ContentProvider.GetAllContentPaths(_contentType);
            _logger.LogDebug(
                "TypeRegistry<{TypeName}> using ContentProvider with content type '{ContentType}'",
                typeof(T).Name,
                _contentType);
        }
        else
        {
            // Fallback to direct path loading
            if (!Directory.Exists(_dataPath))
            {
                _logger.LogResourceNotFound("Data path", _dataPath);
                return 0;
            }

            jsonFiles = Directory.GetFiles(_dataPath, "*.json", SearchOption.AllDirectories);
            _logger.LogDebug(
                "TypeRegistry<{TypeName}> using fallback path '{DataPath}'",
                typeof(T).Name,
                _dataPath);
        }

        int successCount = 0;

        foreach (string jsonPath in jsonFiles)
        {
            try
            {
                await RegisterFromJsonAsync(jsonPath);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading type from {JsonPath}", jsonPath);
            }
        }

        string sourcePath = ContentProvider != null && !string.IsNullOrEmpty(_contentType)
            ? $"ContentProvider:{_contentType}"
            : _dataPath;

        _logger.LogWorkflowStatus(
            "Type definitions loaded",
            ("count", successCount),
            ("source", sourcePath)
        );
        return successCount;
    }

    /// <summary>
    ///     Register a type definition from a JSON file.
    ///     If the type implements IScriptedType and has a behaviorScript property,
    ///     the script will be loaded and compiled.
    /// </summary>
    /// <param name="jsonPath">Path to JSON file containing type definition.</param>
    public async Task RegisterFromJsonAsync(string jsonPath)
    {
        string json = await File.ReadAllTextAsync(jsonPath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        T? definition = JsonSerializer.Deserialize<T>(json, options);
        if (definition == null)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize type definition from {jsonPath}"
            );
        }

        // Validate required properties
        if (string.IsNullOrWhiteSpace(definition.DefinitionId))
        {
            throw new InvalidOperationException(
                $"Type definition in {jsonPath} has null or empty DefinitionId"
            );
        }

        // Register the data definition
        _definitions[definition.DefinitionId] = definition;
        _logger.LogDebug("Registered type: {DefinitionId} from {Path}", definition.DefinitionId, jsonPath);

        // Note: Scripts are loaded separately after TypeRegistry initialization
        // See RegisterScript() method
    }

    /// <summary>
    ///     Register a type definition directly (useful for built-in types or testing).
    /// </summary>
    /// <param name="definition">The type definition to register.</param>
    public void Register(T definition)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        if (string.IsNullOrWhiteSpace(definition.DefinitionId))
        {
            throw new ArgumentException("DefinitionId cannot be null or empty", nameof(definition));
        }

        _definitions[definition.DefinitionId] = definition;
        _logger.LogDebug("Registered type: {DefinitionId}", definition.DefinitionId);
    }

    /// <summary>
    ///     Get a type definition by ID. Returns null if not found.
    ///     O(1) lookup performance.
    /// </summary>
    /// <param name="typeId">The type identifier to look up.</param>
    /// <returns>The type definition, or null if not found.</returns>
    public T? Get(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId))
        {
            return default;
        }

        return _definitions.TryGetValue(typeId, out T? def) ? def : default;
    }

    /// <summary>
    ///     Get the script instance for a type. Returns null if no script.
    ///     Cast to ScriptBase in the consuming system.
    /// </summary>
    /// <param name="typeId">The type identifier.</param>
    /// <returns>The cached script instance (should be ScriptBase), or null.</returns>
    public object? GetScript(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId))
        {
            return null;
        }

        return _scripts.TryGetValue(typeId, out object? script) ? script : null;
    }

    /// <summary>
    ///     Update a script instance (for hot-reload).
    ///     Called externally after script recompilation.
    /// </summary>
    /// <param name="typeId">The type identifier whose script to update.</param>
    /// <param name="scriptInstance">The new compiled script instance.</param>
    public void UpdateScript(string typeId, object scriptInstance)
    {
        if (string.IsNullOrWhiteSpace(typeId))
        {
            throw new ArgumentException("TypeId cannot be null or empty", nameof(typeId));
        }

        if (scriptInstance == null)
        {
            throw new ArgumentNullException(nameof(scriptInstance));
        }

        _scripts[typeId] = scriptInstance;
        _logger.LogInformation("Updated script for {TypeId}", typeId);
    }

    /// <summary>
    ///     Get all registered type IDs.
    /// </summary>
    /// <returns>Collection of all type identifiers in the registry.</returns>
    public IEnumerable<string> GetAllTypeIds()
    {
        return _definitions.Keys;
    }

    /// <summary>
    ///     Get all type definitions.
    /// </summary>
    /// <returns>Collection of all type definitions in the registry.</returns>
    public IEnumerable<T> GetAll()
    {
        return _definitions.Values;
    }

    /// <summary>
    ///     Check if a type is registered.
    /// </summary>
    /// <param name="typeId">The type identifier to check.</param>
    /// <returns>True if the type is registered, false otherwise.</returns>
    public bool Contains(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId))
        {
            return false;
        }

        return _definitions.ContainsKey(typeId);
    }

    /// <summary>
    ///     Remove a type definition from the registry.
    /// </summary>
    /// <param name="typeId">The type identifier to remove.</param>
    /// <returns>True if the type was removed, false if it didn't exist.</returns>
    public bool Remove(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId))
        {
            return false;
        }

        bool removed = _definitions.TryRemove(typeId, out _);
        if (removed)
        {
            _scripts.TryRemove(typeId, out _);
            _logger.LogDebug("Removed type: {TypeId}", typeId);
        }

        return removed;
    }

    /// <summary>
    ///     Clear all type definitions from the registry.
    /// </summary>
    public void Clear()
    {
        _definitions.Clear();
        _scripts.Clear();
        _logger.LogDebug("Cleared all type definitions");
    }

    /// <summary>
    ///     Check if a script is registered for a type.
    /// </summary>
    /// <param name="typeId">The type identifier to check.</param>
    /// <returns>True if a script is registered, false otherwise.</returns>
    public bool HasScript(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId))
        {
            return false;
        }

        return _scripts.ContainsKey(typeId);
    }
}
