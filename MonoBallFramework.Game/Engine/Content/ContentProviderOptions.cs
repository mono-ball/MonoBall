namespace MonoBallFramework.Game.Engine.Content;

/// <summary>
/// Configuration options for the content provider system.
/// </summary>
public class ContentProviderOptions
{
    /// <summary>
    /// Maximum number of entries to store in the content path cache.
    /// Default is 10,000 entries.
    /// </summary>
    public int MaxCacheSize { get; set; } = 10_000;

    /// <summary>
    /// The root directory for base game assets.
    /// Default is "Assets".
    /// </summary>
    public string BaseGameRoot { get; set; } = "Assets";

    /// <summary>
    /// Whether to log cache misses for debugging and optimization.
    /// Default is false.
    /// </summary>
    public bool LogCacheMisses { get; set; } = false;

    /// <summary>
    /// Whether to throw an exception when path traversal attempts (e.g., "..") are detected.
    /// Default is true for security.
    /// </summary>
    public bool ThrowOnPathTraversal { get; set; } = true;

    /// <summary>
    /// Mapping of content types to their base folder names within the Assets directory.
    /// This defines where each type of content is stored in the base game.
    /// </summary>
    public Dictionary<string, string> BaseContentFolders { get; set; } = new()
    {
        ["Root"] = "",  // Root-level assets (logo.png, MonoBall.wav, etc.)
        ["Graphics"] = "Graphics",
        ["Audio"] = "Audio",
        ["Scripts"] = "Scripts",
        ["Fonts"] = "Fonts",
        ["Tiled"] = "Tiled",
        ["Tilesets"] = "Tilesets",

        // Definition types - each has its own content type for precise mod override control
        ["TileBehaviorDefinitions"] = "Definitions/TileBehaviors",
        ["BehaviorDefinitions"] = "Definitions/Behaviors",
        ["SpriteDefinitions"] = "Definitions/Sprites",
        ["FontDefinitions"] = "Definitions/Fonts",
        ["MapDefinitions"] = "Definitions/Maps/Regions",
        ["AudioDefinitions"] = "Definitions/Audio",
        ["PopupBackgroundDefinitions"] = "Definitions/Maps/Popups/Backgrounds",
        ["PopupOutlineDefinitions"] = "Definitions/Maps/Popups/Outlines",
        ["PopupThemeDefinitions"] = "Definitions/Maps/Popups/Themes",
        ["MapSectionDefinitions"] = "Definitions/Maps/Sections",
        ["TextWindowDefinitions"] = "Definitions/TextWindow"
    };

    /// <summary>
    /// Custom content types registered by mods.
    /// These are added dynamically when mods declare custom types in their manifests.
    /// Key is the content type name (e.g., "WeatherEffects"), value is the folder path.
    /// </summary>
    public Dictionary<string, string> CustomContentTypes { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentProviderOptions"/> class with default values.
    /// </summary>
    public ContentProviderOptions()
    {
    }

    /// <summary>
    /// Gets the base content folder path for a specific content type.
    /// </summary>
    /// <param name="contentType">The content type to look up.</param>
    /// <returns>The folder name for the content type, or the content type itself if not found in the mapping.</returns>
    public string GetContentFolder(string contentType)
    {
        // Check base content folders first
        if (BaseContentFolders.TryGetValue(contentType, out var folder))
            return folder;

        // Check custom content types
        if (CustomContentTypes.TryGetValue(contentType, out var customFolder))
            return customFolder;

        // Fall back to the content type itself
        return contentType;
    }

    /// <summary>
    /// Adds a custom content type (called by ModLoader during mod discovery).
    /// </summary>
    /// <param name="contentType">The content type key.</param>
    /// <param name="folderPath">The folder path within the mod.</param>
    public void AddCustomContentType(string contentType, string folderPath)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type cannot be null or whitespace.", nameof(contentType));

        if (string.IsNullOrWhiteSpace(folderPath))
            throw new ArgumentException("Folder path cannot be null or whitespace.", nameof(folderPath));

        // Reject path traversal attempts
        if (folderPath.Contains(".."))
            throw new ArgumentException("Folder path cannot contain path traversal sequences.", nameof(folderPath));

        CustomContentTypes[contentType] = folderPath;
    }

    /// <summary>
    /// Checks if a content type is known (either base or custom).
    /// </summary>
    public bool IsKnownContentType(string contentType)
    {
        return BaseContentFolders.ContainsKey(contentType) || CustomContentTypes.ContainsKey(contentType);
    }

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when configuration is invalid.</exception>
    public void Validate()
    {
        if (MaxCacheSize <= 0)
        {
            throw new ArgumentException("MaxCacheSize must be greater than zero.", nameof(MaxCacheSize));
        }

        if (string.IsNullOrWhiteSpace(BaseGameRoot))
        {
            throw new ArgumentException("BaseGameRoot cannot be null or empty.", nameof(BaseGameRoot));
        }

        if (BaseContentFolders == null || BaseContentFolders.Count == 0)
        {
            throw new ArgumentException("BaseContentFolders must contain at least one mapping.", nameof(BaseContentFolders));
        }

        foreach (var kvp in BaseContentFolders)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
            {
                throw new ArgumentException("BaseContentFolders cannot contain null or empty keys.", nameof(BaseContentFolders));
            }

            // Allow empty value for "Root" content type (root-level assets)
            if (kvp.Key != "Root" && string.IsNullOrWhiteSpace(kvp.Value))
            {
                throw new ArgumentException($"BaseContentFolders value for '{kvp.Key}' cannot be null or empty.", nameof(BaseContentFolders));
            }
        }

        // Validate CustomContentTypes entries
        foreach (var kvp in CustomContentTypes)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
            {
                throw new ArgumentException("CustomContentTypes key cannot be null or empty.", nameof(CustomContentTypes));
            }
            if (string.IsNullOrWhiteSpace(kvp.Value))
            {
                throw new ArgumentException($"CustomContentTypes value for '{kvp.Key}' cannot be null or empty.", nameof(CustomContentTypes));
            }
            if (kvp.Value.Contains(".."))
            {
                throw new ArgumentException($"CustomContentTypes path for '{kvp.Key}' cannot contain path traversal sequences.", nameof(CustomContentTypes));
            }
        }
    }
}
