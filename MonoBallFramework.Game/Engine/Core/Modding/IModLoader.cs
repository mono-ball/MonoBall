namespace MonoBallFramework.Game.Engine.Core.Modding;

/// <summary>
///     Interface for accessing loaded mod information.
///     Used by content systems to resolve mod-provided content paths.
/// </summary>
public interface IModLoader
{
    /// <summary>
    ///     Gets a read-only collection of loaded mod manifests, keyed by mod ID.
    /// </summary>
    IReadOnlyDictionary<string, ModManifest> LoadedMods { get; }

    /// <summary>
    ///     Gets all patches for all loaded mods.
    /// </summary>
    IReadOnlyDictionary<string, List<ModPatch>> AllPatches { get; }

    /// <summary>
    ///     Discovers and registers mod manifests without loading scripts.
    ///     Call this early (before game data loading) to enable content overrides.
    /// </summary>
    Task DiscoverModsAsync();

    /// <summary>
    ///     Loads mod scripts and patches for previously discovered mods.
    ///     Call this after API providers are set up.
    /// </summary>
    Task LoadModScriptsAsync();

    /// <summary>
    ///     Loads custom type definitions from all discovered mods.
    ///     Call this after mod discovery and before scripts need access to custom types.
    /// </summary>
    Task LoadCustomTypeDefinitions();

    /// <summary>
    ///     Loads the base game content as a special mod.
    ///     Should be called before LoadModsAsync().
    /// </summary>
    Task LoadBaseGameAsync(string baseGameRoot);

    /// <summary>
    ///     Unloads a mod and disposes its script instances.
    /// </summary>
    Task UnloadModAsync(string modId);

    /// <summary>
    ///     Reloads a mod (unload then load).
    /// </summary>
    Task ReloadModAsync(string modId);

    /// <summary>
    ///     Gets the manifest for a loaded mod.
    /// </summary>
    ModManifest? GetModManifest(string modId);

    /// <summary>
    ///     Checks if a mod is currently loaded.
    /// </summary>
    bool IsModLoaded(string modId);

    /// <summary>
    ///     Gets all patches for a loaded mod.
    /// </summary>
    IReadOnlyList<ModPatch> GetModPatches(string modId);

    /// <summary>
    ///     Gets the content folder path for a specific content type in a mod.
    /// </summary>
    string? GetContentFolderPath(string modId, string contentType);

    /// <summary>
    ///     Gets all content folder paths for a mod.
    /// </summary>
    IReadOnlyDictionary<string, string> GetContentFolders(string modId);
}
