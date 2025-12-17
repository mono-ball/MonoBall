using System.Collections.Concurrent;
using System.Reflection;
using Arch.Core;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Content;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Modding;
using MonoBallFramework.Game.Scripting.Api;
using MonoBallFramework.Game.Scripting.Runtime;

namespace MonoBallFramework.Game.Scripting.Services;

/// <summary>
///     Service for compiling and executing Roslyn C# scripts (.csx files).
///     Provides hot-reload support and script caching.
///     Orchestrates script compilation, caching, instantiation, and initialization.
/// </summary>
public class ScriptService : IAsyncDisposable
{
    /// <summary>
    ///     Static cache for Initialize MethodInfo to avoid expensive reflection lookups.
    ///     Thread-safe ConcurrentDictionary keyed by script type.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, MethodInfo> _onInitializeMethodCache = new();

    private readonly IScriptingApiProvider _apis;
    private readonly ScriptCache _cache;
    private readonly ScriptCompiler _compiler;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ScriptService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _scriptsBasePath;
    private readonly IServiceProvider _serviceProvider;
    private readonly World _world;

    private IContentProvider? _contentProvider;
    private bool _contentProviderResolved;
    private ModLoader? _modLoader;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScriptService" /> class.
    /// </summary>
    /// <param name="scriptsBasePath">Base path for script files (fallback when ContentProvider not available).</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="loggerFactory">Logger factory for creating child loggers.</param>
    /// <param name="apis">Scripting API provider.</param>
    /// <param name="eventBus">Event bus for script event subscriptions.</param>
    /// <param name="world">ECS world for mod initialization.</param>
    /// <param name="serviceProvider">Service provider for lazy resolution of IContentProvider.</param>
    public ScriptService(
        string scriptsBasePath,
        ILogger<ScriptService> logger,
        ILoggerFactory loggerFactory,
        IScriptingApiProvider apis,
        IEventBus eventBus,
        World world,
        IServiceProvider serviceProvider
    )
    {
        _scriptsBasePath = scriptsBasePath ?? throw new ArgumentNullException(nameof(scriptsBasePath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _apis = apis ?? throw new ArgumentNullException(nameof(apis));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        // Create dependencies
        ILogger<ScriptCompiler> compilerLogger = loggerFactory.CreateLogger<ScriptCompiler>();
        _compiler = new ScriptCompiler(compilerLogger);
        _cache = new ScriptCache();
    }

    /// <summary>
    ///     Gets the content provider, resolving it lazily to avoid circular dependency.
    ///     The circular dependency is: ModLoader -> ScriptService -> IContentProvider -> IModLoader -> ModLoader
    ///     By resolving lazily, we break the cycle at construction time.
    /// </summary>
    private IContentProvider? ContentProvider
    {
        get
        {
            if (!_contentProviderResolved)
            {
                _contentProvider = _serviceProvider.GetService(typeof(IContentProvider)) as IContentProvider;
                _contentProviderResolved = true;
            }

            return _contentProvider;
        }
    }

    /// <summary>
    ///     Asynchronously disposes resources and clears script cache.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        var exceptions = new List<Exception>();

        // Dispose any scripts that implement IAsyncDisposable
        foreach (object instance in _cache.GetAllInstances())
        {
            try
            {
                // Call OnUnload for ScriptBase instances to cleanup event handlers
                if (instance is ScriptBase scriptBase)
                {
                    scriptBase.OnUnload();
                }

                if (instance is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else if (instance is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing script instance");
                exceptions.Add(ex);
            }
        }

        _cache.Clear();
        GC.SuppressFinalize(this);

        if (exceptions.Count > 0)
        {
            throw new AggregateException("Errors during script disposal", exceptions);
        }
    }

    /// <summary>
    ///     Load and compile a script from a .csx file.
    ///     Returns a script instance that can be executed.
    /// </summary>
    /// <param name="scriptPath">Relative path to the .csx file.</param>
    /// <returns>Compiled and instantiated script object.</returns>
    public async Task<object?> LoadScriptAsync(string scriptPath)
    {
        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            throw new ArgumentException("Script path cannot be null or empty", nameof(scriptPath));
        }

        // Check cache first
        if (_cache.TryGetInstance(scriptPath, out object? cachedInstance))
        {
            _logger.LogDebug("Returning cached script instance for {Path}", scriptPath);
            return cachedInstance;
        }

        // Handle both absolute paths (from mods) and relative paths
        string? fullPath;
        if (Path.IsPathRooted(scriptPath))
        {
            // Absolute path - use directly (already resolved by ModLoader)
            fullPath = scriptPath;
        }
        else
        {
            // Relative path - try ContentProvider first (if available), then fallback to base path
            // ContentProvider is resolved lazily to avoid circular dependency at construction time
            fullPath = ContentProvider?.ResolveContentPath("Scripts", scriptPath);

            // Fallback to base scripts path if ContentProvider didn't resolve it
            if (fullPath == null || !File.Exists(fullPath))
            {
                fullPath = Path.Combine(_scriptsBasePath, scriptPath);
            }
        }

        if (!File.Exists(fullPath))
        {
            _logger.LogError("Script file not found: {Path} (resolved: {ResolvedPath})", scriptPath, fullPath);
            return null;
        }

        try
        {
            // Read script code
            string scriptCode = await File.ReadAllTextAsync(fullPath);

            // Check if we have a cached compiled script
            Script<object>? script = null;
            Type? scriptType = null;

            if (_cache.TryGetCompiled(scriptPath, out Script<object> cachedScript, out scriptType))
            {
                script = cachedScript;
            }
            else
            {
                // Compile the script
                script = _compiler.Compile(scriptCode, scriptPath);
                if (script == null)
                {
                    return null;
                }

                // Cache the compiled script
                scriptType = null; // Will be set after execution
                _cache.CacheCompiled(scriptPath, script, null);
            }

            // Execute script to get the instance
            object? instance = await _compiler.ExecuteAsync(script, scriptPath);
            if (instance == null)
            {
                return null;
            }

            // Update cache with script type
            if (scriptType == null)
            {
                scriptType = instance.GetType();
                _cache.CacheCompiled(scriptPath, script, scriptType);
            }

            // Cache the instance
            _cache.CacheInstance(scriptPath, instance);

            _logger.LogWorkflowStatus(
                "Script loaded",
                ("path", scriptPath),
                ("type", instance.GetType().Name)
            );
            return instance;
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "Script file not found: {Path}", scriptPath);
            return null;
        }
        catch (IOException ex)
        {
            _logger.LogError(
                ex,
                "I/O error reading script file {Path}: {Message}",
                scriptPath,
                ex.Message
            );
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error loading script {Path}", scriptPath);
            return null;
        }
    }

    /// <summary>
    ///     Reload a script from disk and recompile it.
    ///     Updates the cached instance with the new version.
    /// </summary>
    /// <param name="scriptPath">Relative path to the .csx file.</param>
    /// <returns>New script instance after reload.</returns>
    public async Task<object?> ReloadScriptAsync(string scriptPath)
    {
        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            throw new ArgumentException("Script path cannot be null or empty", nameof(scriptPath));
        }

        _logger.LogInformation("Hot-reloading script: {Path}", scriptPath);

        try
        {
            // Get old instance and call OnUnload to cleanup event handlers FIRST
            if (_cache.TryRemoveInstance(scriptPath, out object? oldInstance))
            {
                try
                {
                    // Call OnUnload to cleanup event subscriptions
                    if (oldInstance is ScriptBase oldScriptBase)
                    {
                        oldScriptBase.OnUnload();
                    }

                    // Dispose the old instance
                    if (oldInstance is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync();
                    }
                    else if (oldInstance is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Error cleaning up old script instance during reload: {Path}",
                        scriptPath
                    );
                    // Continue with reload even if cleanup fails
                }
            }

            // Load and compile new instance
            // Note: InitializeScript must be called separately by the caller to register events
            object? newInstance = await LoadScriptAsync(scriptPath);
            return newInstance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hot-reload script: {Path}", scriptPath);
            return null;
        }
    }

    /// <summary>
    ///     Get a cached script instance.
    /// </summary>
    /// <param name="scriptPath">Relative path to the .csx file.</param>
    /// <returns>Cached script instance, or null if not loaded.</returns>
    public object? GetScriptInstance(string scriptPath)
    {
        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            return null;
        }

        return _cache.TryGetInstance(scriptPath, out object? instance) ? instance : null;
    }

    /// <summary>
    ///     Initialize a script instance with world, entity context, and logger.
    ///     Validates all required parameters and ensures type safety.
    /// </summary>
    /// <param name="scriptInstance">The script instance to initialize.</param>
    /// <param name="world">The ECS world.</param>
    /// <param name="entity">The entity (optional).</param>
    /// <param name="logger">Logger instance for the script (optional).</param>
    /// <exception cref="ArgumentNullException">Thrown when scriptInstance or world is null.</exception>
    /// <exception cref="ArgumentException">Thrown when scriptInstance is not a ScriptBase.</exception>
    /// <exception cref="InvalidOperationException">Thrown when Initialize method cannot be found.</exception>
    public void InitializeScript(
        object scriptInstance,
        World world,
        Entity? entity = null,
        ILogger? logger = null
    )
    {
        // Validate required parameters
        if (scriptInstance == null)
        {
            throw new ArgumentNullException(
                nameof(scriptInstance),
                "Script instance cannot be null"
            );
        }

        if (world == null)
        {
            throw new ArgumentNullException(nameof(world), "World cannot be null");
        }

        // Validate script instance type
        if (scriptInstance is not ScriptBase scriptBase)
        {
            throw new ArgumentException(
                $"Script instance must be of type ScriptBase, but was {scriptInstance.GetType().FullName}",
                nameof(scriptInstance)
            );
        }

        try
        {
            // Create ScriptContext for initialization (use NullLogger if no logger provided)
            ILogger effectiveLogger = logger ?? NullLogger.Instance;
            var context = new ScriptContext(world, entity, effectiveLogger, _apis, _eventBus);

            // Call Initialize (sets up initial state)
            Type scriptType = scriptBase.GetType();
            MethodInfo initMethod = _onInitializeMethodCache.GetOrAdd(
                scriptType,
                type =>
                {
                    MethodInfo? method = type.GetMethod(
                        "Initialize",
                        BindingFlags.NonPublic
                        | BindingFlags.Public
                        | BindingFlags.Instance
                        | BindingFlags.FlattenHierarchy
                    );

                    if (method == null)
                    {
                        throw new InvalidOperationException(
                            $"Initialize method not found on {type.FullName}"
                        );
                    }

                    return method;
                }
            );
            initMethod.Invoke(scriptBase, new object[] { context });

            // Call RegisterEventHandlers (subscribes to events)
            scriptBase.RegisterEventHandlers(context);

            _logger.LogDebug(
                "Successfully initialized script instance of type {Type}",
                scriptBase.GetType().FullName
            );
        }
        catch (TargetInvocationException ex)
        {
            _logger.LogError(
                ex.InnerException ?? ex,
                "Error invoking Initialize method on script instance of type {Type}",
                scriptBase.GetType().FullName
            );
            throw new InvalidOperationException(
                $"Failed to initialize script instance: {ex.InnerException?.Message ?? ex.Message}",
                ex.InnerException ?? ex
            );
        }
    }

    /// <summary>
    ///     Check if a script is loaded and cached.
    /// </summary>
    /// <param name="scriptPath">Relative path to the .csx file.</param>
    public bool IsScriptLoaded(string scriptPath)
    {
        return _cache.IsInstanceCached(scriptPath);
    }

    /// <summary>
    ///     Clear all cached scripts.
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
        _logger.LogInformation("Cleared script cache");
    }

    /// <summary>
    ///     Load all mods from the /Mods/ directory.
    ///     Must be called AFTER core scripts are loaded.
    ///     Note: ModLoader should be registered via DI and injected, not created here.
    /// </summary>
    /// <param name="modLoader">The mod loader instance (should be injected via DI).</param>
    public async Task LoadModsAsync(ModLoader modLoader)
    {
        _modLoader = modLoader ?? throw new ArgumentNullException(nameof(modLoader));
        await _modLoader.LoadModsAsync();
    }

    /// <summary>
    ///     Gets the ModLoader instance for advanced mod management.
    /// </summary>
    public ModLoader? GetModLoader()
    {
        return _modLoader;
    }
}
