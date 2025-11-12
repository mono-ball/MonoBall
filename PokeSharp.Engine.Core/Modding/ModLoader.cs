using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Core.Modding;

/// <summary>
/// Discovers and loads mods from the Mods directory.
/// Handles dependency resolution and load order.
/// </summary>
public sealed class ModLoader
{
    private readonly ILogger<ModLoader> _logger;
    private readonly string _modsDirectory;
    private readonly List<LoadedMod> _loadedMods = new();

    public IReadOnlyList<LoadedMod> LoadedMods => _loadedMods;

    public ModLoader(ILogger<ModLoader> logger, string modsDirectory)
    {
        _logger = logger;
        _modsDirectory = modsDirectory;
    }

    /// <summary>
    /// Discovers all mods in the Mods directory
    /// </summary>
    public List<LoadedMod> DiscoverMods()
    {
        _loadedMods.Clear();

        if (!Directory.Exists(_modsDirectory))
        {
            _logger.LogInformation("Mods directory not found | creating: {Path}", _modsDirectory);
            Directory.CreateDirectory(_modsDirectory);
            return _loadedMods;
        }

        var modDirectories = Directory.GetDirectories(_modsDirectory);
        _logger.LogInformation("Scanning for mods | path: {Path}, directories: {Count}", _modsDirectory, modDirectories.Length);

        foreach (var modDir in modDirectories)
        {
            try
            {
                var manifestPath = Path.Combine(modDir, "mod.json");
                if (!File.Exists(manifestPath))
                {
                    _logger.LogWarning("Skipping directory | reason: no mod.json, path: {Dir}", Path.GetFileName(modDir));
                    continue;
                }

                var manifestJson = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<ModManifest>(
                    manifestJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                if (manifest == null)
                {
                    _logger.LogWarning("Failed to deserialize | path: {Path}", manifestPath);
                    continue;
                }

                manifest.Validate();

                var loadedMod = new LoadedMod
                {
                    Manifest = manifest,
                    RootPath = modDir
                };

                _loadedMods.Add(loadedMod);
                _logger.LogInformation("Discovered mod | id: {ModId}, version: {Version}, name: {Name}",
                    manifest.ModId, manifest.Version, manifest.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mod load error | directory: {Dir}", modDir);
            }
        }

        return _loadedMods;
    }

    /// <summary>
    /// Sorts mods by dependencies and load order
    /// </summary>
    public List<LoadedMod> SortByLoadOrder(List<LoadedMod> mods)
    {
        var sorted = new List<LoadedMod>();
        var processing = new HashSet<string>();
        var processed = new HashSet<string>();

        void Visit(LoadedMod mod)
        {
            if (processed.Contains(mod.Manifest.ModId))
                return;

            if (processing.Contains(mod.Manifest.ModId))
            {
                throw new InvalidOperationException(
                    $"Circular dependency detected involving mod: {mod.Manifest.ModId}");
            }

            processing.Add(mod.Manifest.ModId);

            // Process hard dependencies first
            foreach (var depId in mod.Manifest.Dependencies)
            {
                var dep = mods.FirstOrDefault(m => m.Manifest.ModId == depId);
                if (dep == null)
                {
                    throw new InvalidOperationException(
                        $"Mod '{mod.Manifest.ModId}' depends on '{depId}' which is not loaded");
                }
                Visit(dep);
            }

            // Process LoadAfter dependencies
            foreach (var afterId in mod.Manifest.LoadAfter)
            {
                var after = mods.FirstOrDefault(m => m.Manifest.ModId == afterId);
                if (after != null) // Soft dependency - don't fail if missing
                {
                    Visit(after);
                }
            }

            processing.Remove(mod.Manifest.ModId);
            processed.Add(mod.Manifest.ModId);
            sorted.Add(mod);
        }

        // Sort by priority first (lower = earlier)
        var prioritySorted = mods.OrderBy(m => m.Manifest.LoadPriority).ToList();

        foreach (var mod in prioritySorted)
        {
            Visit(mod);
        }

        _logger.LogInformation(
            "Mod load order | sequence: {Order}",
            string.Join(" → ", sorted.Select(m => m.Manifest.ModId))
        );

        return sorted;
    }
}

/// <summary>
/// Represents a loaded mod with its manifest and file system location
/// </summary>
public sealed class LoadedMod
{
    public required ModManifest Manifest { get; init; }
    public required string RootPath { get; init; }

    /// <summary>
    /// Resolves a relative path within this mod's directory
    /// </summary>
    public string ResolvePath(string relativePath)
    {
        return Path.Combine(RootPath, relativePath);
    }

    public override string ToString() => Manifest.ToString();
}

