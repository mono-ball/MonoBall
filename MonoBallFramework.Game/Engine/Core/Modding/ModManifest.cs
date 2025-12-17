using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MonoBallFramework.Game.Engine.Core.Modding.CustomTypes;

namespace MonoBallFramework.Game.Engine.Core.Modding;

/// <summary>
///     Unified mod manifest that supports both script-based and content-based modding.
///     Defines a mod's metadata, dependencies, scripts, patches, and content folders.
/// </summary>
public sealed partial class ModManifest
{
    [GeneratedRegex(@"^\d+\.\d+\.\d+")]
    private static partial Regex SemVerRegex();
    /// <summary>
    ///     Unique identifier for this mod (e.g. "myusername.coolmod")
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    ///     Display name for the mod
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    ///     Mod author
    /// </summary>
    [JsonPropertyName("author")]
    public string Author { get; init; } = "Unknown";

    /// <summary>
    ///     Mod version (semantic versioning recommended, e.g. "1.0.0")
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    ///     Mod description
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = "";

    /// <summary>
    ///     List of mod dependencies with version constraints.
    ///     Format: "mod-id >= version" or "mod-id == version"
    ///     Example: ["pokesharp-core >= 1.0.0", "another-mod == 2.1.0"]
    /// </summary>
    [JsonPropertyName("dependencies")]
    public List<string> Dependencies { get; init; } = [];

    /// <summary>
    ///     List of mod IDs that must be loaded before this mod (soft dependencies)
    /// </summary>
    [JsonPropertyName("loadBefore")]
    public List<string> LoadBefore { get; init; } = [];

    /// <summary>
    ///     List of mod IDs that must be loaded after this mod
    /// </summary>
    [JsonPropertyName("loadAfter")]
    public List<string> LoadAfter { get; init; } = [];

    /// <summary>
    ///     Explicit load order priority (higher = loads first, default = 0).
    ///     Used when no dependencies exist to determine load order.
    /// </summary>
    [JsonPropertyName("priority")]
    public int Priority { get; init; }

    /// <summary>
    ///     List of .csx script files to load (relative to mod directory).
    ///     Example: ["ledge_crumble.csx", "jump_boost_item.csx"]
    /// </summary>
    [JsonPropertyName("scripts")]
    public List<string> Scripts { get; init; } = [];

    /// <summary>
    ///     List of required permissions for this mod.
    ///     Example: ["events:subscribe", "world:modify", "effects:play"]
    /// </summary>
    [JsonPropertyName("permissions")]
    public List<string> Permissions { get; init; } = [];

    /// <summary>
    ///     Relative paths to JSON Patch files that modify existing data.
    ///     Uses RFC 6902 JSON Patch format.
    /// </summary>
    [JsonPropertyName("patches")]
    public List<string> Patches { get; init; } = [];

    /// <summary>
    ///     Relative paths to folders containing new content (templates, definitions, etc.).
    ///     Key is the content type, value is the relative folder path.
    ///     Example: { "Templates": "content/templates", "Sprites": "content/sprites" }
    /// </summary>
    [JsonPropertyName("contentFolders")]
    public Dictionary<string, string> ContentFolders { get; init; } = new();

    /// <summary>
    ///     Custom content type declarations for this mod.
    ///     Key is the content type name (e.g., "WeatherEffects"), value is the schema.
    ///     Other mods can then provide content for these custom types.
    /// </summary>
    [JsonPropertyName("customTypes")]
    public Dictionary<string, CustomTypeSchema> CustomTypes { get; init; } = new();

    /// <summary>
    ///     Directory path where the mod is located (set by ModLoader).
    /// </summary>
    [JsonIgnore]
    public string DirectoryPath { get; set; } = string.Empty;

    /// <summary>
    ///     Validates the manifest has required fields.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new InvalidOperationException("Mod manifest missing required 'id' field");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("Mod manifest missing required 'name' field");
        }

        // Validate semantic versioning format (basic check)
        if (!SemVerRegex().IsMatch(Version))
        {
            throw new InvalidOperationException(
                $"Version must follow semantic versioning (e.g. 1.0.0): {Version}"
            );
        }

        // Mod should have at least scripts, patches, or content folders (but allow empty mods for now)
        // This validation can be removed if you want to allow metadata-only mods
    }

    public override string ToString()
    {
        return $"{Name} ({Id}) v{Version} by {Author}";
    }
}
