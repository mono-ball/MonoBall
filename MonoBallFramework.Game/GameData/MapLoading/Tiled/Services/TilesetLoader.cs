using System.Text.Json;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Content;
using MonoBallFramework.Game.Engine.Rendering.Assets;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Tmx;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.TiledJson;

namespace MonoBallFramework.Game.GameData.MapLoading.Tiled.Services;

/// <summary>
///     Handles loading and management of tilesets from Tiled maps.
///     Responsible for loading tileset textures, external tileset files, and parsing animations.
/// </summary>
public class TilesetLoader
{
    private readonly IAssetProvider _assetManager;
    private readonly IContentProvider _contentProvider;
    private readonly ILogger<TilesetLoader>? _logger;

    public TilesetLoader(
        IAssetProvider assetManager,
        IContentProvider contentProvider,
        ILogger<TilesetLoader>? logger = null)
    {
        _assetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));
        _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
        _logger = logger;
    }

    /// <summary>
    ///     Asynchronously loads all tilesets from a TmxDocument.
    ///     First loads external tileset JSON files in parallel, then loads textures.
    /// </summary>
    public async Task<List<LoadedTileset>> LoadTilesetsAsync(TmxDocument tmxDoc, string mapPath, CancellationToken cancellationToken = default)
    {
        if (tmxDoc.Tilesets.Count == 0)
        {
            return new List<LoadedTileset>();
        }

        // First, load all external tileset files in parallel
        string mapDirectory = Path.GetDirectoryName(mapPath) ?? string.Empty;
        await LoadExternalTilesetsAsync(tmxDoc, mapDirectory, cancellationToken).ConfigureAwait(false);

        // Then handle texture loading
        var loadedTilesets = new List<LoadedTileset>(tmxDoc.Tilesets.Count);
        var textureLoadTasks = new List<Task>();

        foreach (TmxTileset tileset in tmxDoc.Tilesets)
        {
            string tilesetId = ExtractTilesetId(tileset, mapPath);
            tileset.Name = tilesetId;

            if (tileset.Image != null && !string.IsNullOrEmpty(tileset.Image.Source))
            {
                if (!_assetManager.HasTexture(tilesetId))
                {
                    string pathForLoader = GetTexturePathForLoader(tileset, mapPath);

                    // Start async texture preload if available
                    if (!_assetManager.IsTextureLoading(tilesetId))
                    {
                        var preloadTask = Task.Run(() =>
                        {
                            _assetManager.PreloadTextureAsync(tilesetId, pathForLoader);
                        }, cancellationToken);
                        textureLoadTasks.Add(preloadTask);
                    }
                }
            }

            loadedTilesets.Add(new LoadedTileset(tileset, tilesetId));
        }

        // Wait for all texture preloads to start
        if (textureLoadTasks.Count > 0)
        {
            await Task.WhenAll(textureLoadTasks).ConfigureAwait(false);
        }

        loadedTilesets.Sort((a, b) => a.Tileset.FirstGid.CompareTo(b.Tileset.FirstGid));
        return loadedTilesets;
    }

    /// <summary>
    ///     Loads all tilesets from a TmxDocument.
    /// </summary>
    public List<LoadedTileset> LoadTilesets(TmxDocument tmxDoc, string mapPath)
    {
        if (tmxDoc.Tilesets.Count == 0)
        {
            return new List<LoadedTileset>();
        }

        var loadedTilesets = new List<LoadedTileset>(tmxDoc.Tilesets.Count);
        foreach (TmxTileset tileset in tmxDoc.Tilesets)
        {
            string tilesetId = ExtractTilesetId(tileset, mapPath);
            tileset.Name = tilesetId;

            if (tileset.Image != null && !string.IsNullOrEmpty(tileset.Image.Source))
            {
                if (!_assetManager.HasTexture(tilesetId))
                {
                    LoadTilesetTexture(tileset, mapPath, tilesetId);
                }
            }

            loadedTilesets.Add(new LoadedTileset(tileset, tilesetId));
        }

        loadedTilesets.Sort((a, b) => a.Tileset.FirstGid.CompareTo(b.Tileset.FirstGid));
        return loadedTilesets;
    }

    /// <summary>
    ///     Asynchronously loads external tileset files referenced in the map JSON in parallel.
    ///     Tiled JSON format can reference external tileset files via "source" field.
    /// </summary>
    public async Task LoadExternalTilesetsAsync(TmxDocument tmxDoc, string mapBasePath, CancellationToken cancellationToken = default)
    {
        var externalTilesets = tmxDoc.Tilesets
            .Where(t => !string.IsNullOrEmpty(t.Source) && t.TileWidth == 0)
            .ToList();

        if (externalTilesets.Count == 0)
            return;

        var loadTasks = externalTilesets.Select(async tileset =>
        {
            if (string.IsNullOrEmpty(tileset.Source))
                throw new InvalidOperationException("External tileset has null or empty Source property");

            string tilesetPath = Path.Combine(mapBasePath, tileset.Source);
            if (!File.Exists(tilesetPath))
                throw new FileNotFoundException($"External tileset not found: {tilesetPath}");

            try
            {
                await using var stream = File.OpenRead(tilesetPath);
                var tilesetData = await JsonSerializer.DeserializeAsync(stream, TiledJsonContext.Default.TiledJsonTileset, cancellationToken).ConfigureAwait(false);

                if (tilesetData == null)
                    throw new InvalidOperationException($"Failed to deserialize tileset from {tilesetPath}");

                ApplyTilesetData(tilesetData, tileset, tilesetPath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load external tileset from {Path}", tilesetPath);
                throw;
            }
        });

        await Task.WhenAll(loadTasks).ConfigureAwait(false);
    }

    /// <summary>
    ///     Loads external tileset files referenced in the map JSON.
    ///     Tiled JSON format can reference external tileset files via "source" field.
    /// </summary>
    public void LoadExternalTilesets(TmxDocument tmxDoc, string mapBasePath)
    {
        foreach (TmxTileset tileset in tmxDoc.Tilesets)
        {
            if (string.IsNullOrEmpty(tileset.Source) || tileset.TileWidth != 0)
                continue;

            string tilesetPath = Path.Combine(mapBasePath, tileset.Source);
            if (!File.Exists(tilesetPath))
                throw new FileNotFoundException($"External tileset not found: {tilesetPath}");

            try
            {
                string tilesetJson = File.ReadAllText(tilesetPath);
                var tilesetData = JsonSerializer.Deserialize(tilesetJson, TiledJsonContext.Default.TiledJsonTileset);

                if (tilesetData == null)
                    throw new InvalidOperationException($"Failed to deserialize tileset from {tilesetPath}");

                ApplyTilesetData(tilesetData, tileset, tilesetPath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load external tileset from {Path}", tilesetPath);
                throw;
            }
        }
    }

    /// <summary>
    ///     Applies properties from a deserialized tileset data object to a TmxTileset.
    ///     Shared between async and sync loading paths to eliminate duplication.
    /// </summary>
    private void ApplyTilesetData(TiledJsonTileset tilesetData, TmxTileset tileset, string tilesetPath)
    {
        int originalFirstGid = tileset.FirstGid;
        tileset.Name = tilesetData.Name ?? "";
        tileset.TileWidth = tilesetData.TileWidth ?? 0;
        tileset.TileHeight = tilesetData.TileHeight ?? 0;
        tileset.TileCount = tilesetData.TileCount ?? 0;
        tileset.Margin = tilesetData.Margin ?? 0;
        tileset.Spacing = tilesetData.Spacing ?? 0;

        // Image data is at top level in tileset JSON
        if (!string.IsNullOrEmpty(tilesetData.Image))
        {
            string tilesetDir = Path.GetDirectoryName(tilesetPath) ?? string.Empty;
            string imageAbsolute = Path.GetFullPath(Path.Combine(tilesetDir, tilesetData.Image));

            tileset.Image = new TmxImage
            {
                Source = imageAbsolute,
                Width = tilesetData.ImageWidth ?? 0,
                Height = tilesetData.ImageHeight ?? 0,
            };
        }

        tileset.FirstGid = originalFirstGid; // Preserve from map reference

        // Parse tile animations from tiles array
        if (tilesetData.Tiles?.Count > 0)
        {
            ParseTilesetAnimationsFromData(tilesetData.Tiles, tileset);
        }

        _logger?.LogDebug(
            "Loaded external tileset: {Name} ({Width}x{Height}) with {AnimCount} animations from {Path}",
            tileset.Name,
            tileset.TileWidth,
            tileset.TileHeight,
            tileset.Animations.Count,
            tileset.Source
        );
    }

    /// <summary>
    ///     Parses tile animations and properties from deserialized TiledJsonTileDefinition objects.
    /// </summary>
    private void ParseTilesetAnimationsFromData(List<TiledJsonTileDefinition> tiles, TmxTileset tileset)
    {
        foreach (var tile in tiles)
        {
            int tileId = tile.Id;

            // Parse animation data
            if (tile.Animation != null && tile.Animation.Count > 0)
            {
                var frameTileIds = new List<int>();
                var frameDurations = new List<float>();

                foreach (var frame in tile.Animation)
                {
                    frameTileIds.Add(frame.TileId);
                    // Convert milliseconds to seconds
                    frameDurations.Add(frame.Duration / 1000f);
                }

                if (frameTileIds.Count > 0)
                {
                    tileset.Animations[tileId] = new TmxTileAnimation
                    {
                        FrameTileIds = frameTileIds.ToArray(),
                        FrameDurations = frameDurations.ToArray(),
                    };
                }
            }

            // Parse tile properties (collision, ledge, etc.)
            if (tile.Properties != null && tile.Properties.Count > 0)
            {
                var properties = new Dictionary<string, object>();

                foreach (var prop in tile.Properties)
                {
                    if (!string.IsNullOrEmpty(prop.Name) && prop.Value.ValueKind != JsonValueKind.Undefined)
                    {
                        // Store JsonElement directly - downstream code handles conversion
                        properties[prop.Name] = prop.Value;
                    }
                }

                if (properties.Count > 0)
                {
                    tileset.TileProperties[tileId] = properties;
                }
            }
        }
    }

    /// <summary>
    ///     Parses tile animations and properties from Tiled tileset JSON "tiles" array.
    ///     Tiled format: tiles: [{ "id": 0, "animation": [...], "properties": [...] }]
    /// </summary>
    private void ParseTilesetAnimations(JsonElement tilesArray, TmxTileset tileset)
    {
        foreach (JsonElement tileElement in tilesArray.EnumerateArray())
        {
            if (!tileElement.TryGetProperty("id", out JsonElement tileIdProp))
            {
                continue;
            }

            int tileId = tileIdProp.GetInt32();

            // Parse animation data
            if (tileElement.TryGetProperty("animation", out JsonElement animArray))
            {
                var frameTileIds = new List<int>();
                var frameDurations = new List<float>();

                foreach (JsonElement frameElement in animArray.EnumerateArray())
                {
                    if (
                        frameElement.TryGetProperty("tileid", out JsonElement frameTileId)
                        && frameElement.TryGetProperty("duration", out JsonElement frameDuration)
                    )
                    {
                        frameTileIds.Add(frameTileId.GetInt32());
                        // Convert milliseconds to seconds
                        frameDurations.Add(frameDuration.GetInt32() / 1000f);
                    }
                }

                if (frameTileIds.Count > 0)
                {
                    tileset.Animations[tileId] = new TmxTileAnimation
                    {
                        FrameTileIds = frameTileIds.ToArray(),
                        FrameDurations = frameDurations.ToArray(),
                    };
                }
            }

            // Parse tile properties (collision, ledge, etc.)
            if (tileElement.TryGetProperty("properties", out JsonElement propsArray))
            {
                var properties = new Dictionary<string, object>();

                foreach (JsonElement propElement in propsArray.EnumerateArray())
                {
                    if (
                        propElement.TryGetProperty("name", out JsonElement propName)
                        && propElement.TryGetProperty("value", out JsonElement propValue)
                    )
                    {
                        string? key = propName.GetString();
                        if (!string.IsNullOrEmpty(key))
                        {
                            // Get value based on type
                            object value = propValue.ValueKind switch
                            {
                                JsonValueKind.String => propValue.GetString() ?? "",
                                JsonValueKind.Number => propValue.TryGetInt32(out int i)
                                    ? i
                                    : propValue.GetDouble(),
                                JsonValueKind.True => true,
                                JsonValueKind.False => false,
                                _ => propValue.ToString(),
                            };
                            properties[key] = value;
                        }
                    }
                }

                if (properties.Count > 0)
                {
                    tileset.TileProperties[tileId] = properties;
                }
            }
        }
    }

    /// <summary>
    ///     Loads a tileset texture into the asset manager.
    /// </summary>
    private void LoadTilesetTexture(TmxTileset tileset, string mapPath, string tilesetId)
    {
        if (tileset.Image == null || string.IsNullOrEmpty(tileset.Image.Source))
        {
            throw new InvalidOperationException("Tileset has no image source");
        }

        string pathForLoader = GetTexturePathForLoader(tileset, mapPath);

        try
        {
            _assetManager.LoadTexture(tilesetId, pathForLoader);
            _logger?.LogInformation(
                "[TilesetDebug] Loaded tileset texture: TilesetId='{TilesetId}', Path='{PathForLoader}', "
                    + "ImageSource='{ImageSource}'",
                tilesetId,
                pathForLoader,
                tileset.Image?.Source ?? "null"
            );
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "Failed to load tileset texture: {TilesetId} from {PathForLoader}",
                tilesetId,
                pathForLoader
            );
            throw;
        }
    }

    /// <summary>
    ///     Gets the texture path for the asset loader.
    ///     Resolves tileset image paths relative to the tileset JSON file location.
    /// </summary>
    private string GetTexturePathForLoader(TmxTileset tileset, string mapPath)
    {
        if (tileset.Image == null || string.IsNullOrEmpty(tileset.Image.Source))
        {
            throw new InvalidOperationException("Tileset has no image source");
        }

        // The mapPath here is actually the tileset JSON path or map path
        // The image source in tileset is relative to the tileset JSON file
        string contextDirectory = Path.GetDirectoryName(mapPath) ?? string.Empty;

        // Resolve the image path relative to the context directory
        string tilesetImagePath = Path.IsPathRooted(tileset.Image.Source)
            ? tileset.Image.Source
            : Path.GetFullPath(Path.Combine(contextDirectory, tileset.Image.Source));

        // If the computed path exists, use it directly
        if (File.Exists(tilesetImagePath))
        {
            return tilesetImagePath;
        }

        // Fallback: try to resolve via ContentProvider Tilesets type
        string relativeTilesetPath = tileset.Image.Source;
        string? resolvedPath = _contentProvider.ResolveContentPath("Tilesets", relativeTilesetPath);
        if (resolvedPath != null)
        {
            return resolvedPath;
        }

        throw new FileNotFoundException($"Tileset image not found: {tilesetImagePath}");
    }

    /// <summary>
    ///     Extracts a unique tileset ID from a tileset.
    /// </summary>
    private static string ExtractTilesetId(TmxTileset tileset, string mapPath)
    {
        // If tileset has an image, use the image filename as ID
        if (tileset.Image != null && !string.IsNullOrEmpty(tileset.Image.Source))
        {
            string id = Path.GetFileNameWithoutExtension(tileset.Image.Source);
            if (!string.IsNullOrEmpty(id))
            {
                return id;
            }
        }

        // Fallback to tileset name (handle both null and empty string)
        if (!string.IsNullOrEmpty(tileset.Name))
        {
            return tileset.Name;
        }

        // Last resort: generate ID from map path
        string mapName = Path.GetFileNameWithoutExtension(mapPath);
        return !string.IsNullOrEmpty(mapName) ? mapName : "default-tileset";
    }

    /// <summary>
    ///     Asynchronously preloads tileset JSON files and textures for a map.
    ///     This is the async version for predictive loading during map transitions.
    /// </summary>
    /// <param name="mapPath">Full path to the map file.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the preload operation.</param>
    public async Task PreloadMapTilesetsAsync(string mapPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(mapPath))
        {
            _logger?.LogWarning("Cannot preload tilesets - map file not found: {MapPath}", mapPath);
            return;
        }

        try
        {
            // Parse the map to find tileset references
            string mapContent = await File.ReadAllTextAsync(mapPath, cancellationToken).ConfigureAwait(false);
            string mapDirectory = Path.GetDirectoryName(mapPath) ?? string.Empty;

            // Parse as JSON to get tileset info
            using var jsonDoc = JsonDocument.Parse(mapContent);
            var root = jsonDoc.RootElement;

            if (!root.TryGetProperty("tilesets", out var tilesetsArray))
            {
                return;
            }

            // Load all external tileset JSON files in parallel
            var tilesetLoadTasks = new List<Task>();

            foreach (var tilesetElement in tilesetsArray.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Check for external tileset reference
                if (tilesetElement.TryGetProperty("source", out var sourceElement))
                {
                    string? source = sourceElement.GetString();
                    if (!string.IsNullOrEmpty(source))
                    {
                        var loadTask = LoadAndPreloadExternalTilesetAsync(source, mapDirectory, cancellationToken);
                        tilesetLoadTasks.Add(loadTask);
                    }
                }
                else if (tilesetElement.TryGetProperty("image", out var imageElement))
                {
                    // Inline tileset - preload texture directly
                    string? imagePath = imageElement.GetString();
                    if (!string.IsNullOrEmpty(imagePath))
                    {
                        string tilesetId = Path.GetFileNameWithoutExtension(imagePath);
                        string fullImagePath = Path.GetFullPath(Path.Combine(mapDirectory, imagePath));

                        if (!_assetManager.HasTexture(tilesetId) && !_assetManager.IsTextureLoading(tilesetId))
                        {
                            string pathForLoader = GetPathForAssetManager(fullImagePath);
                            var preloadTask = Task.Run(() =>
                            {
                                _assetManager.PreloadTextureAsync(tilesetId, pathForLoader);
                            }, cancellationToken);
                            tilesetLoadTasks.Add(preloadTask);
                        }
                    }
                }
            }

            // Wait for all tileset loads to complete
            if (tilesetLoadTasks.Count > 0)
            {
                await Task.WhenAll(tilesetLoadTasks).ConfigureAwait(false);
                _logger?.LogDebug("Preloaded {Count} tilesets for map: {MapPath}", tilesetLoadTasks.Count, mapPath);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to preload map tilesets: {MapPath}", mapPath);
        }
    }

    /// <summary>
    ///     Loads an external tileset JSON file and preloads its texture asynchronously.
    /// </summary>
    private async Task LoadAndPreloadExternalTilesetAsync(string tilesetSource, string mapDirectory, CancellationToken cancellationToken)
    {
        string tilesetPath = Path.Combine(mapDirectory, tilesetSource);
        if (!File.Exists(tilesetPath))
        {
            _logger?.LogWarning("External tileset not found for preload: {Path}", tilesetPath);
            return;
        }

        try
        {
            // Use stream-based async deserialization with source-generated context
            await using var stream = File.OpenRead(tilesetPath);
            var tilesetData = await JsonSerializer.DeserializeAsync(stream, TiledJsonContext.Default.TiledJsonTileset, cancellationToken).ConfigureAwait(false);

            if (tilesetData != null && !string.IsNullOrEmpty(tilesetData.Image))
            {
                string imagePath = tilesetData.Image;
                string tilesetDir = Path.GetDirectoryName(tilesetPath) ?? string.Empty;
                string absoluteImagePath = Path.GetFullPath(Path.Combine(tilesetDir, imagePath));
                string tilesetId = Path.GetFileNameWithoutExtension(imagePath);

                // Preload texture if not already loaded
                if (!_assetManager.HasTexture(tilesetId) && !_assetManager.IsTextureLoading(tilesetId))
                {
                    string pathForLoader = GetPathForAssetManager(absoluteImagePath);
                    _assetManager.PreloadTextureAsync(tilesetId, pathForLoader);
                    _logger?.LogDebug("Started async preload for external tileset: {TilesetId}", tilesetId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to preload external tileset: {Path}", tilesetPath);
        }
    }

    /// <summary>
    ///     Converts an absolute path to a path suitable for the asset manager using IContentProvider.
    /// </summary>
    private string GetPathForAssetManager(string absolutePath)
    {
        // Use IContentProvider to resolve the path
        string? resolvedPath = _contentProvider.ResolveContentPath("Graphics", Path.GetFileName(absolutePath));
        if (resolvedPath == null)
        {
            throw new FileNotFoundException($"Asset not found: {absolutePath}");
        }
        return resolvedPath;
    }

    /// <summary>
    ///     Preloads tileset textures asynchronously for an adjacent map.
    ///     Call this before the player enters the map to reduce stutter.
    /// </summary>
    /// <param name="mapPath">Full path to the map file.</param>
    public void PreloadMapTexturesAsync(string mapPath)
    {
        if (!File.Exists(mapPath))
        {
            _logger?.LogWarning("Cannot preload textures - map file not found: {MapPath}", mapPath);
            return;
        }

        try
        {
            // Parse the map to find tileset references
            string mapContent = File.ReadAllText(mapPath);
            string mapDirectory = Path.GetDirectoryName(mapPath) ?? string.Empty;

            // Parse as JSON to get tileset info
            using var jsonDoc = System.Text.Json.JsonDocument.Parse(mapContent);
            var root = jsonDoc.RootElement;

            if (!root.TryGetProperty("tilesets", out var tilesetsArray))
            {
                return;
            }

            foreach (var tilesetElement in tilesetsArray.EnumerateArray())
            {
                string? imagePath = null;
                string? tilesetId = null;

                // Get image path - either directly or from external tileset
                if (tilesetElement.TryGetProperty("image", out var imageElement))
                {
                    imagePath = imageElement.GetString();
                    tilesetId = Path.GetFileNameWithoutExtension(imagePath);
                }
                else if (tilesetElement.TryGetProperty("source", out var sourceElement))
                {
                    // External tileset - need to read it
                    string? source = sourceElement.GetString();
                    if (!string.IsNullOrEmpty(source))
                    {
                        string tilesetPath = Path.Combine(mapDirectory, source);
                        if (File.Exists(tilesetPath))
                        {
                            try
                            {
                                // Use source-generated context for deserialization
                                string tilesetJson = File.ReadAllText(tilesetPath);
                                var tilesetData = JsonSerializer.Deserialize(tilesetJson, TiledJsonContext.Default.TiledJsonTileset);

                                if (tilesetData != null && !string.IsNullOrEmpty(tilesetData.Image))
                                {
                                    imagePath = tilesetData.Image;
                                    string tilesetDir = Path.GetDirectoryName(tilesetPath) ?? string.Empty;

                                    // Resolve image path through content provider
                                    string absoluteImagePath = Path.GetFullPath(Path.Combine(tilesetDir, imagePath));
                                    string? resolvedPath = _contentProvider.ResolveContentPath("Graphics", Path.GetFileName(absoluteImagePath));
                                    if (resolvedPath == null)
                                    {
                                        throw new FileNotFoundException($"Tileset image not found: {absoluteImagePath}");
                                    }
                                    imagePath = resolvedPath;
                                    tilesetId = Path.GetFileNameWithoutExtension(imagePath);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogWarning(ex, "Failed to read external tileset for preload: {Path}", tilesetPath);
                            }
                        }
                    }
                }

                // Start async preload if we have both ID and path
                if (!string.IsNullOrEmpty(tilesetId) && !string.IsNullOrEmpty(imagePath))
                {
                    if (!_assetManager.HasTexture(tilesetId) && !_assetManager.IsTextureLoading(tilesetId))
                    {
                        _assetManager.PreloadTextureAsync(tilesetId, imagePath);
                        _logger?.LogDebug("Started async preload for tileset: {TilesetId}", tilesetId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to preload map textures: {MapPath}", mapPath);
        }
    }
}

/// <summary>
///     Represents a loaded tileset with its ID.
///     Shared between TilesetLoader and MapLoader.
/// </summary>
public sealed class LoadedTileset
{
    public LoadedTileset(TmxTileset tileset, string tilesetId)
    {
        Tileset = tileset ?? throw new ArgumentNullException(nameof(tileset));
        TilesetId = tilesetId ?? throw new ArgumentNullException(nameof(tilesetId));
    }

    public TmxTileset Tileset { get; }
    public string TilesetId { get; }
}
