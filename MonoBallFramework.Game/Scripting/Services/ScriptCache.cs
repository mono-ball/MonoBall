using System.Collections.Concurrent;
using Microsoft.CodeAnalysis.Scripting;

namespace MonoBallFramework.Game.Scripting.Services;

/// <summary>
///     Thread-safe cache for compiled scripts and script instances.
///     Manages both the compiled Script objects and the instantiated script objects.
/// </summary>
public class ScriptCache
{
    /// <summary>
    ///     Cache of compiled scripts keyed by script path.
    ///     Stores both the compiled Script and the script type.
    /// </summary>
    private readonly ConcurrentDictionary<
        string,
        (Script<object> compiled, Type? scriptType)
    > _compiledScripts = new();

    /// <summary>
    ///     Cache of instantiated script objects keyed by script path.
    /// </summary>
    private readonly ConcurrentDictionary<string, object> _scriptInstances = new();

    /// <summary>
    ///     Gets a cached compiled script.
    /// </summary>
    /// <param name="scriptPath">The script path key.</param>
    /// <param name="compiled">The compiled script if found.</param>
    /// <param name="scriptType">The script type if found.</param>
    /// <returns>True if the script is cached, false otherwise.</returns>
    public bool TryGetCompiled(string scriptPath, out Script<object> compiled, out Type? scriptType)
    {
        if (
            _compiledScripts.TryGetValue(
                scriptPath,
                out (Script<object> compiled, Type? scriptType) cached
            )
        )
        {
            compiled = cached.compiled;
            scriptType = cached.scriptType;
            return true;
        }

        compiled = null!;
        scriptType = null;
        return false;
    }

    /// <summary>
    ///     Caches a compiled script.
    /// </summary>
    /// <param name="scriptPath">The script path key.</param>
    /// <param name="compiled">The compiled script.</param>
    /// <param name="scriptType">The script type.</param>
    public void CacheCompiled(string scriptPath, Script<object> compiled, Type? scriptType)
    {
        _compiledScripts[scriptPath] = (compiled, scriptType);
    }

    /// <summary>
    ///     Gets a cached script instance.
    /// </summary>
    /// <param name="scriptPath">The script path key.</param>
    /// <param name="instance">The script instance if found.</param>
    /// <returns>True if the instance is cached, false otherwise.</returns>
    public bool TryGetInstance(string scriptPath, out object? instance)
    {
        return _scriptInstances.TryGetValue(scriptPath, out instance);
    }

    /// <summary>
    ///     Caches a script instance.
    /// </summary>
    /// <param name="scriptPath">The script path key.</param>
    /// <param name="instance">The script instance.</param>
    public void CacheInstance(string scriptPath, object instance)
    {
        _scriptInstances[scriptPath] = instance;
    }

    /// <summary>
    ///     Removes a script instance from the cache.
    /// </summary>
    /// <param name="scriptPath">The script path key.</param>
    /// <returns>True if the instance was removed, false if not found.</returns>
    public bool TryRemoveInstance(string scriptPath, out object? instance)
    {
        return _scriptInstances.TryRemove(scriptPath, out instance);
    }

    /// <summary>
    ///     Checks if a script instance is cached.
    /// </summary>
    /// <param name="scriptPath">The script path key.</param>
    /// <returns>True if the script is cached, false otherwise.</returns>
    public bool IsInstanceCached(string scriptPath)
    {
        return _scriptInstances.ContainsKey(scriptPath);
    }

    /// <summary>
    ///     Clears all cached scripts and instances.
    /// </summary>
    public void Clear()
    {
        _compiledScripts.Clear();
        _scriptInstances.Clear();
    }

    /// <summary>
    ///     Gets all cached script instances for disposal.
    /// </summary>
    /// <returns>Collection of all cached script instances.</returns>
    public IEnumerable<object> GetAllInstances()
    {
        return _scriptInstances.Values;
    }
}
