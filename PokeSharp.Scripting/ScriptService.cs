using System.Collections.Concurrent;
using System.Reflection;
using Arch.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Scripting;

/// <summary>
///     Service for compiling and executing Roslyn C# scripts (.csx files).
///     Provides hot-reload support and script caching.
/// </summary>
public class ScriptService
{
    private readonly ScriptOptions _defaultOptions;
    private readonly ILogger<ScriptService> _logger;
    private readonly ConcurrentDictionary<
        string,
        (Script<object> compiled, Type? scriptType)
    > _scriptCache = new();
    private readonly ConcurrentDictionary<string, object> _scriptInstances = new();
    private readonly string _scriptsBasePath;

    /// <summary>
    ///     Initializes a new ScriptService.
    /// </summary>
    /// <param name="scriptsBasePath">Base path for script files.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    public ScriptService(string scriptsBasePath, ILogger<ScriptService> logger)
    {
        _scriptsBasePath =
            scriptsBasePath ?? throw new ArgumentNullException(nameof(scriptsBasePath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _defaultOptions = ScriptCompilationOptions.GetDefaultOptions();
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
            throw new ArgumentException("Script path cannot be null or empty", nameof(scriptPath));

        var fullPath = Path.Combine(_scriptsBasePath, scriptPath);
        if (!File.Exists(fullPath))
        {
            _logger.LogError("Script file not found: {Path}", fullPath);
            return null;
        }

        try
        {
            var scriptCode = await File.ReadAllTextAsync(fullPath);

            // Script should already end with "new ClassName()" to return an instance
            // If it doesn't, we'll log an error
            var script = CSharpScript.Create<object>(scriptCode, _defaultOptions);

            // Compile the script
            var diagnostics = script.Compile();
            if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                _logger.LogError("Script compilation failed for {Path}:", scriptPath);
                foreach (
                    var diagnostic in diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                )
                    _logger.LogError("  {Message}", diagnostic.GetMessage());
                return null;
            }

            // Execute script to get the instance
            var result = await script.RunAsync();
            var instance = result.ReturnValue;

            if (instance == null)
            {
                _logger.LogError("Script {Path} did not return an instance", scriptPath);
                return null;
            }

            if (instance is not TypeScriptBase)
            {
                _logger.LogError(
                    "Script {Path} returned {Type}, expected TypeScriptBase",
                    scriptPath,
                    instance.GetType().Name
                );
                return null;
            }

            // Cache the compiled script and instance
            _scriptCache[scriptPath] = (script, instance.GetType());
            _scriptInstances[scriptPath] = instance;

            _logger.LogInformation(
                "Successfully loaded script: {Path} ({Type})",
                scriptPath,
                instance.GetType().Name
            );
            return instance;
        }
        catch (CompilationErrorException ex)
        {
            _logger.LogError(
                ex,
                "Script compilation error in {Path}: {Message}",
                scriptPath,
                ex.Message
            );
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading script {Path}", scriptPath);
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
            throw new ArgumentException("Script path cannot be null or empty", nameof(scriptPath));

        _logger.LogInformation("Hot-reloading script: {Path}", scriptPath);

        // Remove from cache
        _scriptCache.TryRemove(scriptPath, out _);
        _scriptInstances.TryRemove(scriptPath, out _);

        // Load again
        return await LoadScriptAsync(scriptPath);
    }

    /// <summary>
    ///     Get a cached script instance.
    /// </summary>
    /// <param name="scriptPath">Relative path to the .csx file.</param>
    /// <returns>Cached script instance, or null if not loaded.</returns>
    public object? GetScriptInstance(string scriptPath)
    {
        if (string.IsNullOrWhiteSpace(scriptPath))
            return null;

        return _scriptInstances.TryGetValue(scriptPath, out var instance) ? instance : null;
    }

    /// <summary>
    ///     Initialize a script instance with world, entity context, and logger.
    /// </summary>
    /// <param name="scriptInstance">The script instance to initialize.</param>
    /// <param name="world">The ECS world.</param>
    /// <param name="entity">The entity (optional).</param>
    /// <param name="logger">Logger instance for the script (optional).</param>
    public void InitializeScript(
        object scriptInstance,
        World world,
        Entity? entity = null,
        ILogger? logger = null
    )
    {
        if (scriptInstance is TypeScriptBase scriptBase)
        {
            // Use reflection to call internal Initialize method
            var initMethod = typeof(TypeScriptBase).GetMethod(
                "Initialize",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            initMethod?.Invoke(scriptBase, new object?[] { world, entity, logger });
        }
    }

    /// <summary>
    ///     Check if a script is loaded and cached.
    /// </summary>
    /// <param name="scriptPath">Relative path to the .csx file.</param>
    public bool IsScriptLoaded(string scriptPath)
    {
        return _scriptInstances.ContainsKey(scriptPath);
    }

    /// <summary>
    ///     Clear all cached scripts.
    /// </summary>
    public void ClearCache()
    {
        _scriptCache.Clear();
        _scriptInstances.Clear();
        _logger.LogInformation("Cleared script cache");
    }
}
