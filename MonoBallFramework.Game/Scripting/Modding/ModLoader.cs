using System.Text.Json;
using Arch.Core;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Scripting.Api;
using MonoBallFramework.Game.Scripting.Runtime;
using MonoBallFramework.Game.Scripting.Services;

namespace MonoBallFramework.Game.Scripting.Modding;

/// <summary>
///     Discovers, loads, and manages mods from the /Mods/ directory.
///     Handles manifest parsing, dependency resolution, and script loading.
/// </summary>
public class ModLoader
{
    private const string ModManifestFileName = "mod.json";
    private const string ModsDirectoryName = "Mods";
    private readonly IScriptingApiProvider _apis;
    private readonly ModDependencyResolver _dependencyResolver;
    private readonly IEventBus _eventBus;

    private readonly Dictionary<string, ModManifest> _loadedMods = new();

    private readonly ILogger<ModLoader> _logger;
    private readonly string _modsBasePath;
    private readonly Dictionary<string, List<object>> _modScriptInstances = new();
    private readonly ScriptService _scriptService;
    private readonly World _world;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ModLoader" /> class.
    /// </summary>
    public ModLoader(
        ScriptService scriptService,
        ILogger<ModLoader> logger,
        World world,
        IEventBus eventBus,
        IScriptingApiProvider apis,
        string gameBasePath
    )
    {
        _scriptService = scriptService ?? throw new ArgumentNullException(nameof(scriptService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _apis = apis ?? throw new ArgumentNullException(nameof(apis));

        _modsBasePath = Path.Combine(gameBasePath, ModsDirectoryName);
        _dependencyResolver = new ModDependencyResolver();
    }

    /// <summary>
    ///     Gets a read-only collection of loaded mod manifests.
    /// </summary>
    public IReadOnlyDictionary<string, ModManifest> LoadedMods => _loadedMods;

    /// <summary>
    ///     Discovers and loads all mods from the /Mods/ directory.
    ///     Must be called AFTER core scripts are loaded.
    /// </summary>
    public async Task LoadModsAsync()
    {
        _logger.LogInformation("🔍 Scanning for mods in: {Path}", _modsBasePath);

        if (!Directory.Exists(_modsBasePath))
        {
            _logger.LogWarning(
                "⚠️  Mods directory not found: {Path}. Creating it...",
                _modsBasePath
            );
            Directory.CreateDirectory(_modsBasePath);
            return;
        }

        try
        {
            // Step 1: Discover all mod manifests
            List<ModManifest> manifests = DiscoverMods();

            if (manifests.Count == 0)
            {
                _logger.LogInformation("ℹ️  No mods found in {Path}", _modsBasePath);
                return;
            }

            _logger.LogInformation("📦 Found {Count} mod(s)", manifests.Count);

            // Step 2: Resolve dependencies and determine load order
            List<ModManifest> orderedManifests;
            try
            {
                orderedManifests = _dependencyResolver.ResolveDependencies(manifests);
            }
            catch (ModDependencyException ex)
            {
                _logger.LogError(ex, "❌ Failed to resolve mod dependencies: {Message}", ex.Message);
                throw;
            }

            // Step 3: Load each mod in dependency order
            foreach (ModManifest manifest in orderedManifests)
            {
                await LoadModAsync(manifest);
            }

            _logger.LogInformation("✅ Successfully loaded {Count} mod(s)", _loadedMods.Count);
        }
        catch (ModDependencyException)
        {
            throw; // Re-throw dependency errors as-is
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Unexpected error during mod loading");
            throw;
        }
    }

    /// <summary>
    ///     Discovers all mod manifests by scanning subdirectories for mod.json files.
    /// </summary>
    private List<ModManifest> DiscoverMods()
    {
        var manifests = new List<ModManifest>();

        foreach (string modDirectory in Directory.GetDirectories(_modsBasePath))
        {
            string manifestPath = Path.Combine(modDirectory, ModManifestFileName);

            if (!File.Exists(manifestPath))
            {
                _logger.LogDebug(
                    "Skipping {Path} (no {FileName})",
                    modDirectory,
                    ModManifestFileName
                );
                continue;
            }

            try
            {
                ModManifest? manifest = ParseManifest(manifestPath, modDirectory);
                if (manifest != null)
                {
                    manifests.Add(manifest);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "❌ Failed to parse manifest at {Path}: {Message}",
                    manifestPath,
                    ex.Message
                );
                // Continue with other mods
            }
        }

        return manifests;
    }

    /// <summary>
    ///     Parses a mod.json manifest file.
    /// </summary>
    private ModManifest? ParseManifest(string manifestPath, string modDirectory)
    {
        string json = File.ReadAllText(manifestPath);

        ModManifest? manifest = JsonSerializer.Deserialize<ModManifest>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        if (manifest == null)
        {
            _logger.LogError("❌ Failed to deserialize manifest: {Path}", manifestPath);
            return null;
        }

        manifest.DirectoryPath = modDirectory;

        if (!manifest.IsValid(out string? error))
        {
            _logger.LogError("❌ Invalid manifest at {Path}: {Error}", manifestPath, error);
            return null;
        }

        _logger.LogDebug("✅ Parsed manifest: {Mod}", manifest);
        return manifest;
    }

    /// <summary>
    ///     Loads a single mod by loading all its scripts and initializing them.
    /// </summary>
    private async Task LoadModAsync(ModManifest manifest)
    {
        _logger.LogInformation("⚙️  Loading mod: {Mod}", manifest);

        // Check for conflicts
        if (_loadedMods.ContainsKey(manifest.Id))
        {
            _logger.LogWarning(
                "⚠️  Mod '{Id}' is already loaded. Skipping duplicate.",
                manifest.Id
            );
            return;
        }

        var scriptInstances = new List<object>();

        try
        {
            // Load each script in the mod
            foreach (string scriptFile in manifest.Scripts)
            {
                string scriptPath = Path.Combine(manifest.DirectoryPath, scriptFile);

                if (!File.Exists(scriptPath))
                {
                    _logger.LogError(
                        "❌ Script file not found for mod '{Id}': {Path}",
                        manifest.Id,
                        scriptPath
                    );
                    continue;
                }

                // Load script using ScriptService (relative to mod directory)
                string relativeScriptPath = Path.GetRelativePath(_modsBasePath, scriptPath);

                object? instance = await _scriptService.LoadScriptAsync(relativeScriptPath);

                if (instance == null)
                {
                    _logger.LogError(
                        "❌ Failed to load script '{Script}' for mod '{Id}'",
                        scriptFile,
                        manifest.Id
                    );
                    continue;
                }

                // Initialize the script
                if (instance is ScriptBase scriptBase)
                {
                    _scriptService.InitializeScript(scriptBase, _world, null, _logger);

                    _logger.LogDebug(
                        "✅ Loaded and initialized script: {Script} ({Type})",
                        scriptFile,
                        instance.GetType().Name
                    );
                }
                else
                {
                    _logger.LogWarning(
                        "⚠️  Script '{Script}' is not a ScriptBase. Loaded but not initialized.",
                        scriptFile
                    );
                }

                scriptInstances.Add(instance);
            }

            // Mark mod as loaded
            _loadedMods[manifest.Id] = manifest;
            _modScriptInstances[manifest.Id] = scriptInstances;

            _logger.LogWorkflowStatus(
                "Mod loaded successfully",
                ("id", manifest.Id),
                ("name", manifest.Name),
                ("scripts", scriptInstances.Count.ToString())
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to load mod '{Id}': {Message}", manifest.Id, ex.Message);

            // Cleanup partially loaded scripts
            await UnloadModAsync(manifest.Id);
            throw;
        }
    }

    /// <summary>
    ///     Unloads a mod and disposes its script instances.
    /// </summary>
    public async Task UnloadModAsync(string modId)
    {
        if (!_loadedMods.ContainsKey(modId))
        {
            _logger.LogWarning("⚠️  Mod '{Id}' is not loaded", modId);
            return;
        }

        _logger.LogInformation("🔄 Unloading mod: {Id}", modId);

        // Dispose all script instances
        if (_modScriptInstances.TryGetValue(modId, out List<object>? instances))
        {
            foreach (object instance in instances)
            {
                try
                {
                    // Call OnUnload for ScriptBase instances
                    if (instance is ScriptBase scriptBase)
                    {
                        scriptBase.OnUnload();
                    }

                    // Dispose if applicable
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
                    _logger.LogWarning(
                        ex,
                        "⚠️  Error disposing script instance for mod '{Id}'",
                        modId
                    );
                }
            }

            _modScriptInstances.Remove(modId);
        }

        _loadedMods.Remove(modId);
        _logger.LogInformation("✅ Mod '{Id}' unloaded", modId);
    }

    /// <summary>
    ///     Reloads a mod (unload then load).
    /// </summary>
    public async Task ReloadModAsync(string modId)
    {
        if (!_loadedMods.TryGetValue(modId, out ModManifest? manifest))
        {
            _logger.LogWarning("⚠️  Cannot reload mod '{Id}': not loaded", modId);
            return;
        }

        _logger.LogInformation("🔄 Reloading mod: {Id}", modId);

        await UnloadModAsync(modId);
        await LoadModAsync(manifest);
    }

    /// <summary>
    ///     Gets the manifest for a loaded mod.
    /// </summary>
    public ModManifest? GetModManifest(string modId)
    {
        return _loadedMods.TryGetValue(modId, out ModManifest? manifest) ? manifest : null;
    }

    /// <summary>
    ///     Checks if a mod is currently loaded.
    /// </summary>
    public bool IsModLoaded(string modId)
    {
        return _loadedMods.ContainsKey(modId);
    }
}
