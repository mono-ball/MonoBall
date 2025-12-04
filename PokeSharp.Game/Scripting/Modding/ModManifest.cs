using System.Text.Json.Serialization;

namespace PokeSharp.Game.Scripting.Modding;

/// <summary>
///     Represents a mod manifest (mod.json) containing metadata and configuration for a mod.
///     Supports JSON deserialization for automatic loading from mod directories.
/// </summary>
public class ModManifest
{
    /// <summary>
    ///     Unique identifier for the mod (kebab-case recommended).
    ///     Example: "enhanced-ledges"
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    ///     Human-readable name of the mod.
    ///     Example: "Enhanced Ledges Mod"
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Semantic version string (e.g., "1.0.0", "2.1.3-beta").
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    ///     Author or organization name.
    /// </summary>
    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    /// <summary>
    ///     Description of what the mod does.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///     List of .csx script files to load (relative to mod directory).
    ///     Example: ["ledge_crumble.csx", "jump_boost_item.csx"]
    /// </summary>
    [JsonPropertyName("scripts")]
    public string[] Scripts { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     List of mod dependencies with version constraints.
    ///     Format: "mod-id >= version" or "mod-id == version"
    ///     Example: ["pokesharp-core >= 1.0.0", "another-mod == 2.1.0"]
    /// </summary>
    [JsonPropertyName("dependencies")]
    public string[] Dependencies { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     List of required permissions for this mod.
    ///     Example: ["events:subscribe", "world:modify", "effects:play"]
    /// </summary>
    [JsonPropertyName("permissions")]
    public string[] Permissions { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     Optional priority for load order resolution (higher = loads first).
    ///     Defaults to 0 if not specified. Useful for mods that must load before/after others.
    /// </summary>
    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 0;

    /// <summary>
    ///     Directory path where the mod is located (set by ModLoader).
    /// </summary>
    [JsonIgnore]
    public string DirectoryPath { get; set; } = string.Empty;

    /// <summary>
    ///     Validates the manifest has required fields.
    /// </summary>
    public bool IsValid(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            error = "Mod manifest missing required 'id' field";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            error = "Mod manifest missing required 'name' field";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Version))
        {
            error = "Mod manifest missing required 'version' field";
            return false;
        }

        if (Scripts == null || Scripts.Length == 0)
        {
            error = "Mod manifest must specify at least one script";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    ///     Gets a display-friendly string representation of the mod.
    /// </summary>
    public override string ToString()
    {
        return $"{Name} ({Id}) v{Version} by {Author}";
    }
}
