using System.Collections.Concurrent;
using System.Security;
using System.Text.Json;
using Arch.Core;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Modding.CustomTypes;
using MonoBallFramework.Game.Scripting.Api;
using MonoBallFramework.Game.Scripting.Runtime;
using MonoBallFramework.Game.Scripting.Services;

namespace MonoBallFramework.Game.Engine.Core.Modding;

/// <summary>
///     Unified mod loader that handles all mod types: scripts, patches, content overrides, and code mods.
///     Discovers, loads, and manages mods from the /Mods/ directory.
/// </summary>
public sealed class ModLoader : IModLoader
{
    private const string ModManifestFileName = "mod.json";
    private const string ModsDirectoryName = "Mods";

    private static readonly HashSet<string> BuiltInContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Root", "Definitions", "Graphics", "Audio", "Scripts", "Fonts", "Tiled", "Tilesets",
        "TileBehaviors", "Behaviors", "Sprites", "MapDefinitions", "AudioDefinitions",
        "PopupBackgrounds", "PopupOutlines", "PopupThemes", "MapSections"
    };

    private readonly IScriptingApiProvider _apis;
    private readonly ModDependencyResolver _dependencyResolver;
    private readonly IEventBus _eventBus;
    private readonly ConcurrentDictionary<string, ModManifest> _loadedMods = new();
    private readonly ILogger<ModLoader> _logger;
    private readonly string _modsBasePath;
    private readonly ConcurrentDictionary<string, List<object>> _modScriptInstances = new();
    private readonly ConcurrentDictionary<string, List<ModPatch>> _modPatches = new();
    private readonly PatchApplicator _patchApplicator;
    private readonly PatchFileLoader _patchFileLoader;
    private readonly ScriptService _scriptService;
    private readonly World _world;
    private readonly CustomTypesApiService? _customTypesService;
    private readonly CustomTypeSchemaValidator? _schemaValidator;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ModLoader" /> class.
    /// </summary>
    public ModLoader(
        ScriptService scriptService,
        ILogger<ModLoader> logger,
        World world,
        IEventBus eventBus,
        IScriptingApiProvider apis,
        PatchApplicator patchApplicator,
        PatchFileLoader patchFileLoader,
        string gameBasePath,
        CustomTypesApiService? customTypesService = null,
        CustomTypeSchemaValidator? schemaValidator = null
    )
    {
        _scriptService = scriptService ?? throw new ArgumentNullException(nameof(scriptService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _apis = apis ?? throw new ArgumentNullException(nameof(apis));
        _patchApplicator = patchApplicator ?? throw new ArgumentNullException(nameof(patchApplicator));
        _patchFileLoader = patchFileLoader ?? throw new ArgumentNullException(nameof(patchFileLoader));
        _customTypesService = customTypesService;
        _schemaValidator = schemaValidator;

        _modsBasePath = Path.Combine(gameBasePath, ModsDirectoryName);
        _dependencyResolver = new ModDependencyResolver();
    }

    /// <summary>
    ///     Gets a read-only collection of loaded mod manifests.
    /// </summary>
    public IReadOnlyDictionary<string, ModManifest> LoadedMods => _loadedMods;

    /// <summary>
    ///     Gets all patches for all loaded mods.
    /// </summary>
    public IReadOnlyDictionary<string, List<ModPatch>> AllPatches => _modPatches;

    /// <summary>
    ///     Discovers and loads all mods from the /Mods/ directory.
    ///     Must be called AFTER core scripts are loaded.
    /// </summary>
    public async Task LoadModsAsync()
    {
        // Two-phase loading for backward compatibility
        await DiscoverModsAsync();
        await LoadModScriptsAsync();
    }

    /// <summary>
    ///     Phase 1: Discovers mod manifests and registers content folders.
    ///     Call this BEFORE game data loading to enable content overrides.
    ///     Does not load scripts or patches.
    /// </summary>
    public async Task DiscoverModsAsync()
    {
        _logger.LogInformation("Discovering mods in: {Path}", _modsBasePath);

        if (!Directory.Exists(_modsBasePath))
        {
            _logger.LogWarning(
                "Mods directory not found: {Path}. Creating it...",
                _modsBasePath
            );
            Directory.CreateDirectory(_modsBasePath);
            return;
        }

        try
        {
            // Discover all mod manifests
            List<ModManifest> manifests = DiscoverMods();

            if (manifests.Count == 0)
            {
                _logger.LogInformation("No mods found in {Path}", _modsBasePath);
                return;
            }

            _logger.LogInformation("Found {Count} mod(s)", manifests.Count);

            // Resolve dependencies and determine load order
            List<ModManifest> orderedManifests;
            try
            {
                orderedManifests = _dependencyResolver.ResolveDependencies(manifests);
            }
            catch (ModDependencyException ex)
            {
                _logger.LogError(ex, "Failed to resolve mod dependencies: {Message}", ex.Message);
                throw;
            }

            // Register manifests (content folders become available to ContentProvider)
            foreach (ModManifest manifest in orderedManifests)
            {
                if (!_loadedMods.TryAdd(manifest.Id, manifest))
                {
                    _logger.LogWarning("Mod '{Id}' already registered. Skipping.", manifest.Id);
                    continue;
                }

                // Register custom type categories from this mod
                RegisterCustomTypes(manifest);

                _logger.LogInformation(
                    "Registered mod: {Name} v{Version} (priority {Priority}, {ContentCount} content folders)",
                    manifest.Name, manifest.Version, manifest.Priority, manifest.ContentFolders.Count);
            }

            _logger.LogInformation("Discovered and registered {Count} mod(s)", _loadedMods.Count);
        }
        catch (ModDependencyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during mod discovery");
            throw;
        }

        await Task.CompletedTask; // Async for interface compatibility
    }

    /// <summary>
    ///     Phase 2: Loads scripts and patches for previously discovered mods.
    ///     Call this AFTER API providers are set up.
    /// </summary>
    public async Task LoadModScriptsAsync()
    {
        _logger.LogInformation("Loading mod scripts and patches for {Count} mod(s)", _loadedMods.Count);

        foreach (var manifest in _loadedMods.Values.OrderByDescending(m => m.Priority))
        {
            await LoadModScriptsAndPatchesAsync(manifest);
        }

        _logger.LogInformation("Loaded scripts and patches for {Count} mod(s)", _loadedMods.Count);
    }

    /// <summary>
    ///     Loads scripts and patches for a single mod (Phase 2).
    /// </summary>
    private async Task LoadModScriptsAndPatchesAsync(ModManifest manifest)
    {
        var scriptInstances = new List<object>();
        var patches = new List<ModPatch>();

        try
        {
            var loadedMod = new LoadedMod { Manifest = manifest, RootPath = manifest.DirectoryPath };

            // Load patches
            if (manifest.Patches.Count > 0)
            {
                patches = _patchFileLoader.LoadModPatches(loadedMod);
                _modPatches.AddOrUpdate(manifest.Id, patches, (key, oldValue) => patches);
                _logger.LogInformation("Loaded {Count} patch(es) for mod '{Id}'", patches.Count, manifest.Id);
            }

            // Load scripts
            if (manifest.Scripts.Count > 0)
            {
                foreach (string scriptFile in manifest.Scripts)
                {
                    // Validate script path for security
                    if (!IsPathSafe(manifest.DirectoryPath, scriptFile, out string scriptPath))
                    {
                        _logger.LogWarning(
                            "Security: Rejected script path '{ScriptFile}' for mod '{Id}' (path traversal attempt)",
                            scriptFile, manifest.Id);
                        continue;
                    }

                    if (!File.Exists(scriptPath))
                    {
                        _logger.LogError("Script file not found for mod '{Id}': {Path}", manifest.Id, scriptPath);
                        continue;
                    }

                    // Pass full absolute path - ScriptService handles both absolute and relative paths
                    object? instance = await _scriptService.LoadScriptAsync(scriptPath);

                    if (instance == null)
                    {
                        _logger.LogError("Failed to load script '{Script}' for mod '{Id}'", scriptFile, manifest.Id);
                        continue;
                    }

                    if (instance is ScriptBase scriptBase)
                    {
                        _scriptService.InitializeScript(scriptBase, _world, null, _logger);
                        _logger.LogDebug("Loaded and initialized script: {Script} ({Type})", scriptFile, instance.GetType().Name);
                    }
                    else
                    {
                        _logger.LogWarning("Script '{Script}' is not a ScriptBase. Loaded but not initialized.", scriptFile);
                    }

                    scriptInstances.Add(instance);
                }
            }

            if (scriptInstances.Count > 0)
            {
                _modScriptInstances.AddOrUpdate(manifest.Id, scriptInstances, (key, oldValue) => scriptInstances);
            }

            _logger.LogDebug("Mod '{Id}' scripts loaded: {Scripts} script(s), {Patches} patch(es)",
                manifest.Id, scriptInstances.Count, patches.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load scripts for mod '{Id}'", manifest.Id);
        }
    }

    /// <summary>
    /// Validates that a resolved path stays within the allowed base directory.
    /// Prevents path traversal attacks using ".." or absolute paths.
    /// </summary>
    private static bool IsPathSafe(string basePath, string userPath, out string resolvedPath)
    {
        resolvedPath = string.Empty;

        // Reject null/empty paths
        if (string.IsNullOrWhiteSpace(userPath))
            return false;

        // Reject absolute paths in user input
        if (Path.IsPathRooted(userPath))
            return false;

        // Reject obvious traversal attempts
        if (userPath.Contains(".."))
            return false;

        try
        {
            // Resolve to full path
            resolvedPath = Path.GetFullPath(Path.Combine(basePath, userPath));
            string baseFullPath = Path.GetFullPath(basePath);

            // Ensure resolved path is within base directory
            return resolvedPath.StartsWith(baseFullPath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
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
                    "Failed to parse manifest at {Path}: {Message}",
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
            _logger.LogError("Failed to deserialize manifest: {Path}", manifestPath);
            return null;
        }

        manifest.DirectoryPath = modDirectory;

        try
        {
            manifest.Validate();
        }
        catch (Exception ex)
        {
            _logger.LogError("Invalid manifest at {Path}: {Error}", manifestPath, ex.Message);
            return null;
        }

        // Validate content folder keys against known content types
        ValidateContentFolderKeys(manifest, manifestPath);

        _logger.LogDebug("Parsed manifest: {Mod}", manifest);
        return manifest;
    }

    /// <summary>
    ///     Validates that content folder keys match known content types.
    ///     Warns about unknown keys that won't be recognized by ContentProvider.
    /// </summary>
    private void ValidateContentFolderKeys(ModManifest manifest, string manifestPath)
    {
        // Start with built-in content types
        var validContentTypes = new HashSet<string>(BuiltInContentTypes, StringComparer.OrdinalIgnoreCase);

        // Collect all custom types first (thread-safe)
        var customTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Add custom types from all loaded mods (allows mod B to use mod A's custom types)
        foreach (var loadedMod in _loadedMods.Values)
        {
            foreach (var customType in loadedMod.CustomTypes.Keys)
            {
                customTypes.Add(customType);
            }
        }

        // Also add this mod's own custom types
        foreach (var customType in manifest.CustomTypes.Keys)
        {
            customTypes.Add(customType);
        }

        // Add all collected custom types to valid types
        validContentTypes.UnionWith(customTypes);

        foreach (string key in manifest.ContentFolders.Keys)
        {
            if (!validContentTypes.Contains(key))
            {
                _logger.LogWarning(
                    "Unknown content folder key '{Key}' in mod '{Id}' ({Path}). " +
                    "Valid keys: {ValidKeys}. This content type will be ignored.",
                    key, manifest.Id, manifestPath,
                    string.Join(", ", validContentTypes.OrderBy(k => k)));
            }
        }
    }

    /// <summary>
    /// Registers custom type categories declared by a mod.
    /// Called during mod discovery to enable ContentProvider to resolve custom content.
    /// </summary>
    private void RegisterCustomTypes(ModManifest manifest)
    {
        if (manifest.CustomTypes.Count == 0)
            return;

        if (_customTypesService == null)
        {
            _logger.LogWarning(
                "Mod '{ModId}' declares custom types but CustomTypesApiService is not available. Custom types will be ignored.",
                manifest.Id);
            return;
        }

        foreach (var (typeName, schema) in manifest.CustomTypes)
        {
            // Register the category in the custom types service
            _customTypesService.RegisterCategory(typeName);

            _logger.LogInformation(
                "Registered custom type '{TypeName}' from mod '{ModId}' (folder: {Folder})",
                typeName, manifest.Id, schema.Folder);
        }
    }

    /// <summary>
    /// Loads custom type definitions from all mods.
    /// Should be called after mod discovery and before scripts need access to custom types.
    /// </summary>
    public Task LoadCustomTypeDefinitions()
    {
        if (_customTypesService == null)
        {
            _logger.LogDebug("CustomTypesApiService not available, skipping custom type definition loading");
            return Task.CompletedTask;
        }

        int totalLoaded = 0;

        // Process all loaded mods in priority order (highest first)
        foreach (var manifest in _loadedMods.Values.OrderByDescending(m => m.Priority))
        {
            // Load definitions for custom types declared by this mod
            foreach (var (typeName, schema) in manifest.CustomTypes)
            {
                int loaded = LoadCustomTypeDefinitionsForCategory(manifest, typeName, schema);
                totalLoaded += loaded;
            }

            // Also load definitions for custom types declared by OTHER mods (cross-mod content)
            // Find content folders that match custom type categories from other mods
            foreach (var (contentType, folderPath) in manifest.ContentFolders)
            {
                // Skip built-in content types
                if (IsBuiltInContentType(contentType))
                    continue;

                // Check if any mod has declared this as a custom type
                CustomTypes.CustomTypeSchema? schema = FindCustomTypeSchema(contentType);
                if (schema != null)
                {
                    // Validate content folder path for security
                    if (!IsPathSafe(manifest.DirectoryPath, folderPath, out string fullPath))
                    {
                        _logger.LogWarning(
                            "Security: Rejected content folder '{FolderPath}' for mod '{Id}' (path traversal attempt)",
                            folderPath, manifest.Id);
                        continue;
                    }

                    if (Directory.Exists(fullPath))
                    {
                        int loaded = LoadDefinitionsFromFolder(manifest.Id, contentType, fullPath, schema.Pattern);
                        totalLoaded += loaded;
                    }
                }
            }
        }

        _logger.LogInformation("Loaded {Count} custom type definition(s)", totalLoaded);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Loads custom type definitions for a specific category from a mod.
    /// </summary>
    private int LoadCustomTypeDefinitionsForCategory(ModManifest manifest, string typeName, CustomTypes.CustomTypeSchema schema)
    {
        // Validate folder path for security
        if (!IsPathSafe(manifest.DirectoryPath, schema.Folder, out string folderPath))
        {
            _logger.LogWarning(
                "Security: Rejected custom type folder '{Folder}' for mod '{Id}' (path traversal attempt)",
                schema.Folder, manifest.Id);
            return 0;
        }

        if (!Directory.Exists(folderPath))
        {
            _logger.LogDebug("Custom type folder does not exist: {Path}", folderPath);
            return 0;
        }

        // Resolve schema path to absolute path if provided
        string? schemaPath = null;
        if (!string.IsNullOrEmpty(schema.SchemaPath))
        {
            // Reject absolute paths in schema path (security)
            if (Path.IsPathRooted(schema.SchemaPath))
            {
                _logger.LogWarning(
                    "Security: Rejected absolute schema path '{SchemaPath}' for mod '{Id}'",
                    schema.SchemaPath, manifest.Id);
                return 0;
            }

            // Validate schema path for security
            if (!IsPathSafe(manifest.DirectoryPath, schema.SchemaPath, out schemaPath))
            {
                _logger.LogWarning(
                    "Security: Rejected schema path '{SchemaPath}' for mod '{Id}' (path traversal attempt)",
                    schema.SchemaPath, manifest.Id);
                return 0;
            }
        }

        return LoadDefinitionsFromFolder(manifest.Id, typeName, folderPath, schema.Pattern, schemaPath);
    }

    /// <summary>
    /// Loads definitions from a folder and registers them with CustomTypesApiService.
    /// </summary>
    private int LoadDefinitionsFromFolder(string modId, string category, string folderPath, string pattern, string? schemaPath = null)
    {
        int count = 0;
        string[] files = Directory.GetFiles(folderPath, pattern, SearchOption.AllDirectories);

        foreach (string file in files)
        {
            // Skip schema files by matching against the defined schema path
            if (!string.IsNullOrEmpty(schemaPath) &&
                Path.GetFullPath(file).Equals(Path.GetFullPath(schemaPath), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Fallback: also skip files named "schema.json" for backward compatibility
            string fileName = Path.GetFileName(file);
            if (fileName.Equals("schema.json", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                // Validate against schema if provided
                if (!string.IsNullOrEmpty(schemaPath) && _schemaValidator != null)
                {
                    var validationResult = _schemaValidator.ValidateFile(schemaPath, file);
                    if (!validationResult.IsValid)
                    {
                        _logger.LogWarning(
                            "Skipping {File} - schema validation failed:\n{Errors}",
                            file,
                            validationResult.FormatErrors());
                        continue;
                    }
                }

                string json = File.ReadAllText(file);
                using JsonDocument doc = JsonDocument.Parse(json);

                // Extract ID from JSON or derive from filename
                string localId = Path.GetFileNameWithoutExtension(file);
                if (doc.RootElement.TryGetProperty("id", out JsonElement idElement))
                {
                    localId = idElement.GetString() ?? localId;
                }

                // Extract optional properties
                string displayName = localId;
                string? description = null;
                string version = "1.0.0";

                if (doc.RootElement.TryGetProperty("name", out JsonElement nameElement))
                    displayName = nameElement.GetString() ?? displayName;
                if (doc.RootElement.TryGetProperty("displayName", out JsonElement dnElement))
                    displayName = dnElement.GetString() ?? displayName;
                if (doc.RootElement.TryGetProperty("description", out JsonElement descElement))
                    description = descElement.GetString();
                if (doc.RootElement.TryGetProperty("version", out JsonElement verElement))
                    version = verElement.GetString() ?? version;

                // Create full ID: "mod:category:localid"
                string fullId = $"{modId}:{category}:{localId}";

                var definition = new CustomTypes.CustomTypeDefinition
                {
                    DefinitionId = fullId,
                    Category = category,
                    Name = displayName,
                    Description = description,
                    SourceMod = modId,
                    Version = version,
                    RawData = doc.RootElement.Clone()
                };

                _customTypesService!.RegisterDefinition(definition);
                count++;

                _logger.LogDebug("Loaded custom type definition: {Id} from {File}", fullId, file);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load custom type definition from {File}", file);
            }
        }

        if (count > 0)
        {
            _logger.LogInformation(
                "Loaded {Count} {Category} definition(s) from mod '{ModId}'",
                count, category, modId);
        }

        return count;
    }

    /// <summary>
    /// Checks if a content type is a built-in type.
    /// </summary>
    private static bool IsBuiltInContentType(string contentType)
    {
        return BuiltInContentTypes.Contains(contentType);
    }

    /// <summary>
    /// Finds a custom type schema by name from any loaded mod.
    /// </summary>
    private CustomTypes.CustomTypeSchema? FindCustomTypeSchema(string typeName)
    {
        foreach (var manifest in _loadedMods.Values)
        {
            if (manifest.CustomTypes.TryGetValue(typeName, out var schema))
                return schema;
        }
        return null;
    }

    /// <summary>
    ///     Loads a single mod: patches, content folders, scripts, and code mods.
    /// </summary>
    private async Task LoadModAsync(ModManifest manifest)
    {
        _logger.LogInformation("Loading mod: {Mod}", manifest);

        // Check for conflicts
        if (_loadedMods.ContainsKey(manifest.Id))
        {
            _logger.LogWarning(
                "Mod '{Id}' is already loaded. Skipping duplicate.",
                manifest.Id
            );
            return;
        }

        var scriptInstances = new List<object>();
        var patches = new List<ModPatch>();

        try
        {
            var loadedMod = new LoadedMod { Manifest = manifest, RootPath = manifest.DirectoryPath };

            // Step 1: Load patches (data modification)
            if (manifest.Patches.Count > 0)
            {
                patches = _patchFileLoader.LoadModPatches(loadedMod);
                _modPatches.AddOrUpdate(manifest.Id, patches, (key, oldValue) => patches);

                _logger.LogInformation(
                    "Loaded {Count} patch(es) for mod '{Id}'",
                    patches.Count,
                    manifest.Id
                );
            }

            // Step 2: Content folders are handled by the data loader during game data loading
            // They're registered via the manifest and resolved when loading content
            if (manifest.ContentFolders.Count > 0)
            {
                _logger.LogInformation(
                    "Registered {Count} content folder(s) for mod '{Id}'",
                    manifest.ContentFolders.Count,
                    manifest.Id
                );
            }

            // Step 3: Load scripts (behavior/logic)
            if (manifest.Scripts.Count > 0)
            {
                foreach (string scriptFile in manifest.Scripts)
                {
                    // Validate script path for security
                    if (!IsPathSafe(manifest.DirectoryPath, scriptFile, out string scriptPath))
                    {
                        _logger.LogWarning(
                            "Security: Rejected script path '{ScriptFile}' for mod '{Id}' (path traversal attempt)",
                            scriptFile, manifest.Id);
                        continue;
                    }

                    if (!File.Exists(scriptPath))
                    {
                        _logger.LogError(
                            "Script file not found for mod '{Id}': {Path}",
                            manifest.Id,
                            scriptPath
                        );
                        continue;
                    }

                    // Pass full absolute path - ScriptService handles both absolute and relative paths
                    object? instance = await _scriptService.LoadScriptAsync(scriptPath);

                    if (instance == null)
                    {
                        _logger.LogError(
                            "Failed to load script '{Script}' for mod '{Id}'",
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
                            "Loaded and initialized script: {Script} ({Type})",
                            scriptFile,
                            instance.GetType().Name
                        );
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Script '{Script}' is not a ScriptBase. Loaded but not initialized.",
                            scriptFile
                        );
                    }

                    scriptInstances.Add(instance);
                }
            }

            // Step 4: Load code mods (compiled DLLs) - if supported
            // This would load .dll files from the mod directory
            // For now, this is a placeholder for future compiled mod support

            // Mark mod as loaded
            _loadedMods.AddOrUpdate(manifest.Id, manifest, (key, oldValue) => manifest);
            if (scriptInstances.Count > 0)
            {
                _modScriptInstances.AddOrUpdate(manifest.Id, scriptInstances, (key, oldValue) => scriptInstances);
            }

            _logger.LogWorkflowStatus(
                "Mod loaded successfully",
                ("id", manifest.Id),
                ("name", manifest.Name),
                ("scripts", scriptInstances.Count.ToString()),
                ("patches", patches.Count.ToString()),
                ("contentFolders", manifest.ContentFolders.Count.ToString())
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load mod '{Id}': {Message}", manifest.Id, ex.Message);

            // Cleanup partially loaded mod
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
            _logger.LogWarning("Mod '{Id}' is not loaded", modId);
            return;
        }

        _logger.LogInformation("Unloading mod: {Id}", modId);

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
                        "Error disposing script instance for mod '{Id}'",
                        modId
                    );
                }
            }

            _modScriptInstances.TryRemove(modId, out _);
        }

        _modPatches.TryRemove(modId, out _);
        _loadedMods.TryRemove(modId, out _);
        _logger.LogInformation("Mod '{Id}' unloaded", modId);
    }

    /// <summary>
    ///     Reloads a mod (unload then load).
    /// </summary>
    public async Task ReloadModAsync(string modId)
    {
        if (!_loadedMods.TryGetValue(modId, out ModManifest? manifest))
        {
            _logger.LogWarning("Cannot reload mod '{Id}': not loaded", modId);
            return;
        }

        _logger.LogInformation("Reloading mod: {Id}", modId);

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

    /// <summary>
    ///     Gets all patches for a loaded mod.
    /// </summary>
    public IReadOnlyList<ModPatch> GetModPatches(string modId)
    {
        return _modPatches.TryGetValue(modId, out List<ModPatch>? patches) ? patches : new List<ModPatch>();
    }

    /// <summary>
    ///     Gets the content folder path for a specific content type in a mod.
    /// </summary>
    public string? GetContentFolderPath(string modId, string contentType)
    {
        if (!_loadedMods.TryGetValue(modId, out ModManifest? manifest))
        {
            return null;
        }

        if (!manifest.ContentFolders.TryGetValue(contentType, out string? relativePath))
        {
            return null;
        }

        // Validate content folder path for security
        if (!IsPathSafe(manifest.DirectoryPath, relativePath, out string fullPath))
        {
            _logger.LogWarning(
                "Security: Rejected content folder '{RelativePath}' for mod '{Id}' (path traversal attempt)",
                relativePath, modId);
            return null;
        }

        return fullPath;
    }

    /// <summary>
    ///     Gets all content folder paths for a mod.
    /// </summary>
    public IReadOnlyDictionary<string, string> GetContentFolders(string modId)
    {
        if (!_loadedMods.TryGetValue(modId, out ModManifest? manifest))
        {
            return new Dictionary<string, string>();
        }

        var result = new Dictionary<string, string>();
        foreach (var kvp in manifest.ContentFolders)
        {
            // Validate content folder path for security
            if (!IsPathSafe(manifest.DirectoryPath, kvp.Value, out string fullPath))
            {
                _logger.LogWarning(
                    "Security: Rejected content folder '{RelativePath}' for mod '{Id}' (path traversal attempt)",
                    kvp.Value, modId);
                continue;
            }

            result[kvp.Key] = fullPath;
        }

        return result;
    }

    /// <summary>
    /// Loads the base game content as a special mod.
    /// Should be called before LoadModsAsync().
    /// </summary>
    public async Task LoadBaseGameAsync(string baseGameRoot)
    {
        string manifestPath = Path.Combine(baseGameRoot, "mod.json");

        if (!File.Exists(manifestPath))
        {
            _logger.LogWarning(
                "Base game mod.json not found at {Path}. Creating default manifest.",
                manifestPath);
            await CreateDefaultBaseManifestAsync(manifestPath, baseGameRoot);
        }

        ModManifest? manifest = ParseManifest(manifestPath, baseGameRoot);
        if (manifest != null)
        {
            // Base game should have high priority but allow mods to override
            _loadedMods.AddOrUpdate(manifest.Id, manifest, (key, oldValue) => manifest);
            _logger.LogInformation(
                "Loaded base game: {Name} v{Version} (priority {Priority})",
                manifest.Name, manifest.Version, manifest.Priority);
        }
    }

    private async Task CreateDefaultBaseManifestAsync(string path, string baseGameRoot)
    {
        var manifest = new
        {
            id = "base:pokesharp-core",
            name = "PokeSharp Core Content",
            author = "PokeSharp Team",
            version = "1.0.0",
            description = "Base game content",
            priority = 1000,
            contentFolders = new Dictionary<string, string>
            {
                // Broad content categories
                ["Root"] = "",
                ["Definitions"] = "Definitions",
                ["Graphics"] = "Graphics",
                ["Audio"] = "Audio",
                ["Scripts"] = "Scripts",
                ["Fonts"] = "Fonts",
                ["Tiled"] = "Tiled",
                ["Tilesets"] = "Tilesets",
                // Fine-grained definition types for mod overrides
                ["TileBehaviors"] = "Definitions/TileBehaviors",
                ["Behaviors"] = "Definitions/Behaviors",
                ["Sprites"] = "Definitions/Sprites",
                ["MapDefinitions"] = "Definitions/Maps/Regions",
                ["AudioDefinitions"] = "Definitions/Audio",
                ["PopupBackgrounds"] = "Definitions/Maps/Popups/Backgrounds",
                ["PopupOutlines"] = "Definitions/Maps/Popups/Outlines",
                ["PopupThemes"] = "Definitions/Maps/Popups/Themes",
                ["MapSections"] = "Definitions/Maps/Sections"
            },
            scripts = Array.Empty<string>(),
            patches = Array.Empty<string>(),
            dependencies = Array.Empty<string>()
        };

        string json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
        _logger.LogInformation("Created default base game manifest at {Path}", path);
    }
}

/// <summary>
///     Represents a loaded mod with its manifest and file system location.
/// </summary>
public sealed class LoadedMod
{
    public required ModManifest Manifest { get; init; }
    public required string RootPath { get; init; }

    /// <summary>
    ///     Resolves a relative path within this mod's directory.
    ///     Validates the path to prevent path traversal attacks.
    /// </summary>
    /// <exception cref="SecurityException">Thrown when path traversal is detected.</exception>
    public string ResolvePath(string relativePath)
    {
        // Reject null/empty paths
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new SecurityException("Path cannot be null or empty");

        // Reject absolute paths in user input
        if (Path.IsPathRooted(relativePath))
            throw new SecurityException($"Absolute paths are not allowed: {relativePath}");

        // Reject obvious traversal attempts
        if (relativePath.Contains(".."))
            throw new SecurityException($"Path traversal detected: {relativePath}");

        try
        {
            // Resolve to full path
            string resolvedPath = Path.GetFullPath(Path.Combine(RootPath, relativePath));
            string baseFullPath = Path.GetFullPath(RootPath);

            // Ensure resolved path is within base directory
            if (!resolvedPath.StartsWith(baseFullPath, StringComparison.OrdinalIgnoreCase))
                throw new SecurityException($"Path escapes mod directory: {relativePath}");

            return resolvedPath;
        }
        catch (SecurityException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SecurityException($"Invalid path: {relativePath}", ex);
        }
    }

    public override string ToString()
    {
        return Manifest.ToString();
    }
}
