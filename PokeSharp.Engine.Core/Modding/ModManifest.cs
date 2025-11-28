using System.Text.RegularExpressions;

namespace PokeSharp.Engine.Core.Modding;

/// <summary>
///     Defines a mod's metadata and configuration.
///     Mods can add new content or patch existing content using JSON Patch operations.
/// </summary>
public sealed class ModManifest
{
    /// <summary>
    ///     Unique identifier for this mod (e.g. "myusername.coolmod")
    /// </summary>
    public required string ModId { get; init; }

    /// <summary>
    ///     Display name for the mod
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Mod author
    /// </summary>
    public string Author { get; init; } = "Unknown";

    /// <summary>
    ///     Mod version (semantic versioning recommended)
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    ///     Mod description
    /// </summary>
    public string Description { get; init; } = "";

    /// <summary>
    ///     List of mod IDs this mod depends on (must be loaded before this mod)
    /// </summary>
    public List<string> Dependencies { get; init; } = new();

    /// <summary>
    ///     List of mod IDs that must be loaded before this mod (soft dependencies)
    /// </summary>
    public List<string> LoadBefore { get; init; } = new();

    /// <summary>
    ///     List of mod IDs that must be loaded after this mod
    /// </summary>
    public List<string> LoadAfter { get; init; } = new();

    /// <summary>
    ///     Explicit load order priority (lower = earlier, default = 100)
    /// </summary>
    public int LoadPriority { get; init; } = 100;

    /// <summary>
    ///     Relative paths to JSON Patch files that modify existing data
    /// </summary>
    public List<string> Patches { get; init; } = new();

    /// <summary>
    ///     Relative paths to folders containing new content (templates, definitions, etc.)
    /// </summary>
    public Dictionary<string, string> ContentFolders { get; init; } = new();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ModId))
        {
            throw new InvalidOperationException("ModId is required");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("Name is required");
        }

        // Validate semantic versioning format (basic check)
        if (!Regex.IsMatch(Version, @"^\d+\.\d+\.\d+"))
        {
            throw new InvalidOperationException(
                $"Version must follow semantic versioning (e.g. 1.0.0): {Version}"
            );
        }
    }

    public override string ToString()
    {
        return $"{ModId} v{Version} ({Name})";
    }
}
