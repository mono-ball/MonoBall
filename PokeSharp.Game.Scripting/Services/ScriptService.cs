using System.Collections.Concurrent;
using System.Reflection;
using Arch.Core;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Game.Scripting.Api;
using PokeSharp.Game.Scripting.Runtime;

namespace PokeSharp.Game.Scripting.Services;

/// <summary>
///     Service for compiling and executing Roslyn C# scripts (.csx files).
///     Provides hot-reload support and script caching.
///     Orchestrates script compilation, caching, instantiation, and initialization.
/// </summary>
public class ScriptService : IAsyncDisposable
{
    /// <summary>
    ///     Static cache for OnInitialize MethodInfo to avoid expensive reflection lookups.
    ///     Thread-safe ConcurrentDictionary keyed by script type.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, MethodInfo> _onInitializeMethodCache = new();

    private readonly IScriptingApiProvider _apis;
    private readonly ScriptCache _cache;
    private readonly ScriptCompiler _compiler;
    private readonly ILogger<ScriptService> _logger;
    private readonly string _scriptsBasePath;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScriptService" /> class.
    /// </summary>
    /// <param name="scriptsBasePath">Base path for script files.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="loggerFactory">Logger factory for creating child loggers.</param>
    /// <param name="apis">Scripting API provider.</param>
    public ScriptService(
        string scriptsBasePath,
        ILogger<ScriptService> logger,
        ILoggerFactory loggerFactory,
        IScriptingApiProvider apis
    )
    {
        _scriptsBasePath =
            scriptsBasePath ?? throw new ArgumentNullException(nameof(scriptsBasePath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _apis = apis ?? throw new ArgumentNullException(nameof(apis));

        // Create dependencies
        ILogger<ScriptCompiler> compilerLogger = loggerFactory.CreateLogger<ScriptCompiler>();
        _compiler = new ScriptCompiler(compilerLogger);
        _cache = new ScriptCache();
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

        string fullPath = Path.Combine(_scriptsBasePath, scriptPath);
        if (!File.Exists(fullPath))
        {
            _logger.LogError("Script file not found: {Path}", fullPath);
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
            // Load new instance FIRST
            object? newInstance = await LoadScriptAsync(scriptPath);

            // Then dispose and replace old instance atomically
            if (_cache.TryRemoveInstance(scriptPath, out object? oldInstance))
            {
                if (oldInstance is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else if (oldInstance is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

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
    /// <exception cref="ArgumentException">Thrown when scriptInstance is not a TypeScriptBase.</exception>
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
        if (scriptInstance is not TypeScriptBase scriptBase)
        {
            throw new ArgumentException(
                $"Script instance must be of type TypeScriptBase, but was {scriptInstance.GetType().FullName}",
                nameof(scriptInstance)
            );
        }

        try
        {
            // Get or cache the OnInitialize method using reflection
            Type scriptType = scriptBase.GetType();
            MethodInfo initMethod = _onInitializeMethodCache.GetOrAdd(
                scriptType,
                type =>
                {
                    MethodInfo? method = type.GetMethod(
                        "OnInitialize",
                        BindingFlags.NonPublic
                            | BindingFlags.Public
                            | BindingFlags.Instance
                            | BindingFlags.FlattenHierarchy
                    );

                    if (method == null)
                    {
                        throw new InvalidOperationException(
                            $"OnInitialize method not found on {type.FullName}"
                        );
                    }

                    return method;
                }
            );

            // Create ScriptContext for initialization (use NullLogger if no logger provided)
            ILogger effectiveLogger = logger ?? NullLogger.Instance;
            var context = new ScriptContext(world, entity, effectiveLogger, _apis);
            initMethod.Invoke(scriptBase, new object[] { context });

            _logger.LogDebug(
                "Successfully initialized script instance of type {Type}",
                scriptBase.GetType().FullName
            );
        }
        catch (TargetInvocationException ex)
        {
            _logger.LogError(
                ex.InnerException ?? ex,
                "Error invoking OnInitialize method on script instance of type {Type}",
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
}
