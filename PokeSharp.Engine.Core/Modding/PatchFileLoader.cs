using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Core.Modding;

/// <summary>
///     Loads and parses JSON Patch files from disk
/// </summary>
public sealed class PatchFileLoader
{
    private readonly ILogger<PatchFileLoader> _logger;

    public PatchFileLoader(ILogger<PatchFileLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Loads a patch file from disk
    /// </summary>
    public ModPatch? LoadPatchFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning(
                    "[steelblue1]WF[/] [orange3]⚠[/] Patch file not found: [cyan]{Path}[/]",
                    filePath
                );
                return null;
            }

            string json = File.ReadAllText(filePath);
            ModPatch? patch = JsonSerializer.Deserialize<ModPatch>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (patch == null)
            {
                _logger.LogWarning(
                    "[steelblue1]WF[/] [orange3]⚠[/] Failed to deserialize patch file: [cyan]{Path}[/]",
                    filePath
                );
                return null;
            }

            // Validate all operations
            foreach (PatchOperation operation in patch.Operations)
            {
                operation.Validate();
            }

            _logger.LogDebug(
                "[steelblue1]WF[/] Loaded patch file: [cyan]{Path}[/] -> {Target} ([yellow]{Count}[/] operations)",
                Path.GetFileName(filePath),
                patch.Target,
                patch.Operations.Count
            );

            return patch;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[steelblue1]WF[/] [red]✗[/] Error loading patch file: [cyan]{Path}[/]",
                filePath
            );
            return null;
        }
    }

    /// <summary>
    ///     Loads all patch files specified in a mod manifest
    /// </summary>
    public List<ModPatch> LoadModPatches(LoadedMod mod)
    {
        var patches = new List<ModPatch>();

        foreach (string patchPath in mod.Manifest.Patches)
        {
            string fullPath = mod.ResolvePath(patchPath);
            ModPatch? patch = LoadPatchFile(fullPath);

            if (patch != null)
            {
                patches.Add(patch);
            }
        }

        return patches;
    }
}
