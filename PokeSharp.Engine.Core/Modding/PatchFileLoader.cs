using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Core.Modding;

/// <summary>
/// Loads and parses JSON Patch files from disk
/// </summary>
public sealed class PatchFileLoader
{
    private readonly ILogger<PatchFileLoader> _logger;

    public PatchFileLoader(ILogger<PatchFileLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Loads a patch file from disk
    /// </summary>
    public ModPatch? LoadPatchFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Patch file not found: {Path}", filePath);
                return null;
            }

            var json = File.ReadAllText(filePath);
            var patch = JsonSerializer.Deserialize<ModPatch>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (patch == null)
            {
                _logger.LogWarning("Failed to deserialize patch file: {Path}", filePath);
                return null;
            }

            // Validate all operations
            foreach (var operation in patch.Operations)
            {
                operation.Validate();
            }

            _logger.LogDebug("Loaded patch file: {Path} -> {Target} ({Count} operations)",
                Path.GetFileName(filePath),
                patch.Target,
                patch.Operations.Count);

            return patch;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading patch file: {Path}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Loads all patch files specified in a mod manifest
    /// </summary>
    public List<ModPatch> LoadModPatches(LoadedMod mod)
    {
        var patches = new List<ModPatch>();

        foreach (var patchPath in mod.Manifest.Patches)
        {
            var fullPath = mod.ResolvePath(patchPath);
            var patch = LoadPatchFile(fullPath);

            if (patch != null)
            {
                patches.Add(patch);
            }
        }

        return patches;
    }
}

