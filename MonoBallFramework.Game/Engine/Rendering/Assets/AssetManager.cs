using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using FontStashSharp;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Rendering.Configuration;

namespace MonoBallFramework.Game.Engine.Rendering.Assets;

/// <summary>
///     Manages runtime asset loading for textures and resources.
///     Provides PNG loading without MonoGame Content Pipeline.
/// </summary>
public class AssetManager(
    GraphicsDevice graphicsDevice,
    string assetRoot = RenderingConstants.DefaultAssetRoot,
    ILogger<AssetManager>? logger = null
) : IAssetProvider, IDisposable
{
    private readonly GraphicsDevice _graphicsDevice =
        graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));

    private readonly ILogger<AssetManager>? _logger = logger;

    // LRU cache with 50MB budget for texture memory management
    private readonly LruCache<string, Texture2D> _textures = new(
        50_000_000, // 50MB budget
        texture => texture.Width * texture.Height * 4L, // RGBA = 4 bytes/pixel
        logger
    );

    // Font cache - FontStashSharp FontSystem instances
    private readonly Dictionary<string, FontSystem> _fontSystems = new();
    private readonly Dictionary<string, byte[]> _fontDataCache = new();

    // Async texture preloading - file bytes ready for GPU upload
    private readonly ConcurrentQueue<PendingTexture> _pendingTextures = new();
    private readonly ConcurrentDictionary<string, Task> _loadingTextures = new();

    // Maximum textures to upload to GPU per frame (prevents stutter)
    private const int MaxTextureUploadsPerFrame = 2;

    private bool _disposed;

    /// <summary>
    ///     Gets the root directory path where assets are stored.
    /// </summary>
    public string AssetRoot { get; } = assetRoot;

    /// <summary>
    ///     Gets the number of loaded textures.
    /// </summary>
    public int LoadedTextureCount => _textures.Count;

    /// <summary>
    ///     Gets the current texture cache memory usage in bytes.
    /// </summary>
    public long TextureCacheSizeBytes => _textures.CurrentSize;

    // REMOVED: LoadManifest() and LoadManifestInternal() - obsolete
    // manifest.json has been replaced by EF Core MapEntity and on-demand texture loading

    /// <summary>
    ///     Loads a texture from a PNG file and caches it.
    ///     If the texture was preloaded asynchronously, uses the preloaded data instead of reading from disk.
    /// </summary>
    /// <param name="id">Unique identifier for the texture.</param>
    /// <param name="relativePath">Path relative to asset root.</param>
    public void LoadTexture(string id, string relativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentException.ThrowIfNullOrEmpty(relativePath);

        // Already loaded - skip
        if (HasTexture(id))
        {
            return;
        }

        // Check if texture is being preloaded - wait for it to complete
        if (_loadingTextures.TryGetValue(id, out Task? loadTask))
        {
            _logger?.LogDebug("Waiting for preloading texture: {Id}", id);
            loadTask.Wait(TimeSpan.FromSeconds(5)); // Wait up to 5 seconds
        }

        // Check if preloaded data is available in the pending queue
        // Drain matching entries from the queue
        if (TryGetPreloadedTexture(id, out byte[]? preloadedData) && preloadedData != null)
        {
            var sw = Stopwatch.StartNew();

            using var memoryStream = new MemoryStream(preloadedData);
            var texture = Texture2D.FromStream(_graphicsDevice, memoryStream);

            sw.Stop();
            double elapsedMs = sw.Elapsed.TotalMilliseconds;

            _textures.AddOrUpdate(id, texture);
            _logger?.LogDebug("Used preloaded texture data: {Id} ({ElapsedMs:F1}ms GPU upload)", id, elapsedMs);
            return;
        }

        // Fallback to synchronous file load
        string normalizedRelative = relativePath.Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.Combine(AssetRoot, normalizedRelative);

        if (!File.Exists(fullPath))
        {
            string? fallbackPath = ResolveFallbackTexturePath(id, normalizedRelative);
            if (fallbackPath is not null && File.Exists(fallbackPath))
            {
                _logger?.LogWarning(
                    "Texture '{TextureId}' not found at '{OriginalPath}'. Using fallback '{FallbackPath}'.",
                    id,
                    fullPath,
                    fallbackPath
                );
                fullPath = fallbackPath;
            }
            else
            {
                throw new FileNotFoundException($"Texture file not found: {fullPath}");
            }
        }

        var swSync = Stopwatch.StartNew();

        using FileStream fileStream = File.OpenRead(fullPath);
        var textureSync = Texture2D.FromStream(_graphicsDevice, fileStream);

        swSync.Stop();
        double elapsedMsSync = swSync.Elapsed.TotalMilliseconds;

        _textures.AddOrUpdate(id, textureSync); // LRU cache auto-evicts if needed

        // Log texture loading with timing
        _logger?.LogTextureLoaded(id, elapsedMsSync, textureSync.Width, textureSync.Height);

        // Warn about slow texture loads (>100ms)
        if (elapsedMsSync > 100.0)
        {
            _logger?.LogSlowTextureLoad(id, elapsedMsSync);
        }
    }

    /// <summary>
    ///     Tries to find preloaded texture data in the pending queue.
    ///     Removes the entry from the queue if found.
    /// </summary>
    private bool TryGetPreloadedTexture(string id, out byte[]? data)
    {
        data = null;

        // We need to search through the queue - create a temporary list
        var tempList = new List<PendingTexture>();
        bool found = false;

        while (_pendingTextures.TryDequeue(out PendingTexture pending))
        {
            if (pending.Id == id && !found)
            {
                data = pending.Data;
                found = true;
                // Don't re-queue this one
            }
            else
            {
                tempList.Add(pending);
            }
        }

        // Re-queue items we didn't use
        foreach (var item in tempList)
        {
            _pendingTextures.Enqueue(item);
        }

        return found;
    }

    /// <summary>
    ///     Checks if a texture is loaded in cache.
    /// </summary>
    /// <param name="id">The texture identifier.</param>
    /// <returns>True if texture is loaded.</returns>
    public bool HasTexture(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        return _textures.TryGetValue(id, out _);
    }

    /// <summary>
    ///     Tries to get a texture from cache in a single lookup.
    ///     PERFORMANCE: Combines HasTexture + GetTexture into single dictionary lookup.
    /// </summary>
    /// <param name="id">The texture identifier.</param>
    /// <param name="texture">The texture if found, null otherwise.</param>
    /// <returns>True if texture was found.</returns>
    public bool TryGetTexture(string id, out Texture2D? texture)
    {
        if (string.IsNullOrEmpty(id))
        {
            texture = null;
            return false;
        }
        return _textures.TryGetValue(id, out texture);
    }

    /// <summary>
    ///     Loads a font from file and caches it.
    /// </summary>
    public void LoadFont(string id, string relativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentException.ThrowIfNullOrEmpty(relativePath);

        if (_fontSystems.ContainsKey(id))
        {
            _logger?.LogDebug("Font '{FontId}' already loaded", id);
            return;
        }

        string fullPath = Path.Combine(AssetRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Font file not found: {fullPath}");
        }

        byte[] fontData = File.ReadAllBytes(fullPath);
        _fontDataCache[id] = fontData;

        var fontSystem = new FontSystem();
        fontSystem.AddFont(fontData);
        _fontSystems[id] = fontSystem;

        _logger?.LogInformation("Font loaded and cached: {FontId} ({Size:N0} bytes)", id, fontData.Length);
    }

    /// <summary>
    ///     Gets a cached FontSystem by ID.
    /// </summary>
    public FontSystem? GetFontSystem(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        return _fontSystems.TryGetValue(id, out var fontSystem) ? fontSystem : null;
    }

    /// <summary>
    ///     Checks if a font is loaded.
    /// </summary>
    public bool HasFont(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        return _fontSystems.ContainsKey(id);
    }

    /// <summary>
    ///     Gets the number of textures waiting to be uploaded to GPU.
    /// </summary>
    public int PendingTextureCount => _pendingTextures.Count;

    /// <summary>
    ///     Checks if a texture is currently being preloaded asynchronously.
    /// </summary>
    public bool IsTextureLoading(string id)
    {
        return _loadingTextures.ContainsKey(id);
    }

    /// <summary>
    ///     Preloads a texture asynchronously - reads file bytes on background thread.
    ///     Call ProcessTextureQueue() from Update loop to upload to GPU incrementally.
    /// </summary>
    /// <param name="id">Unique identifier for the texture.</param>
    /// <param name="relativePath">Path relative to asset root.</param>
    public void PreloadTextureAsync(string id, string relativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentException.ThrowIfNullOrEmpty(relativePath);

        // Skip if already loaded, already loading, or already queued
        if (HasTexture(id) || _loadingTextures.ContainsKey(id))
        {
            return;
        }

        string normalizedRelative = relativePath.Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.Combine(AssetRoot, normalizedRelative);

        if (!File.Exists(fullPath))
        {
            string? fallbackPath = ResolveFallbackTexturePath(id, normalizedRelative);
            if (fallbackPath is not null && File.Exists(fallbackPath))
            {
                fullPath = fallbackPath;
            }
            else
            {
                _logger?.LogWarning("Texture file not found for async preload: {Path}", fullPath);
                return;
            }
        }

        // Start async file read on background thread
        string pathToLoad = fullPath;
        var loadTask = Task.Run(async () =>
        {
            try
            {
                byte[] data = await File.ReadAllBytesAsync(pathToLoad);
                _pendingTextures.Enqueue(new PendingTexture(id, data));
                _logger?.LogDebug("Texture bytes loaded for preload: {Id} ({Size:N0} bytes)", id, data.Length);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to preload texture bytes: {Id}", id);
            }
            finally
            {
                _loadingTextures.TryRemove(id, out _);
            }
        });

        _loadingTextures.TryAdd(id, loadTask);
    }

    /// <summary>
    ///     Processes pending textures by uploading them to GPU.
    ///     Call this from the Update loop to incrementally upload textures.
    ///     IMPORTANT: Must be called from the main thread (GPU operations).
    /// </summary>
    /// <returns>Number of textures uploaded this call.</returns>
    public int ProcessTextureQueue()
    {
        int uploaded = 0;

        while (uploaded < MaxTextureUploadsPerFrame && _pendingTextures.TryDequeue(out PendingTexture pending))
        {
            // Skip if texture was loaded synchronously while waiting
            if (HasTexture(pending.Id))
            {
                continue;
            }

            try
            {
                var sw = Stopwatch.StartNew();

                using var memoryStream = new MemoryStream(pending.Data);
                var texture = Texture2D.FromStream(_graphicsDevice, memoryStream);

                sw.Stop();
                double elapsedMs = sw.Elapsed.TotalMilliseconds;

                _textures.AddOrUpdate(pending.Id, texture);
                uploaded++;

                _logger?.LogTextureLoaded(pending.Id, elapsedMs, texture.Width, texture.Height);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to upload preloaded texture to GPU: {Id}", pending.Id);
            }
        }

        return uploaded;
    }

    /// <summary>
    ///     Disposes all loaded textures.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _textures.Clear(); // LruCache.Clear() disposes all textures

        // Dispose font systems
        foreach (var fontSystem in _fontSystems.Values)
        {
            fontSystem.Dispose();
        }
        _fontSystems.Clear();
        _fontDataCache.Clear();

        _disposed = true;

        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Gets all loaded texture IDs (for debugging).
    /// </summary>
    public IEnumerable<string> GetLoadedTextureIds()
    {
        return _textures.Keys;
    }

    private string? ResolveFallbackTexturePath(string id, string normalizedRelativePath)
    {
        string normalized = normalizedRelativePath.Replace('\\', '/');

        if (normalized.StartsWith("Tilesets/", StringComparison.OrdinalIgnoreCase))
        {
            string fileName = Path.GetFileName(normalizedRelativePath);
            string tilesetsRoot = Path.Combine(AssetRoot, "Tilesets");
            string tilesetDir = Path.Combine(tilesetsRoot, id);

            if (!string.IsNullOrEmpty(fileName))
            {
                string nestedPath = Path.Combine(tilesetDir, fileName);
                if (File.Exists(nestedPath))
                {
                    return nestedPath;
                }
            }

            // Try reading the tileset JSON for the actual image name
            string tilesetJson = Path.Combine(tilesetDir, $"{id}.json");
            if (File.Exists(tilesetJson))
            {
                string? imageName = TryGetTilesetImageName(tilesetJson);
                if (!string.IsNullOrEmpty(imageName))
                {
                    string jsonImagePath = Path.Combine(tilesetDir, imageName);
                    if (File.Exists(jsonImagePath))
                    {
                        return jsonImagePath;
                    }
                }
            }

            // Fallback: pick the first PNG found in the tileset directory
            if (Directory.Exists(tilesetDir))
            {
                string? pngMatch = Directory
                    .EnumerateFiles(tilesetDir, "*.png", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault();
                if (pngMatch is not null)
                {
                    return pngMatch;
                }
            }
        }

        return null;
    }

    private string? TryGetTilesetImageName(string tilesetJsonPath)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(tilesetJsonPath));
            if (doc.RootElement.TryGetProperty("image", out JsonElement imageProperty))
            {
                return imageProperty.GetString();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(
                ex,
                "Failed to read tileset JSON '{TilesetJson}' while resolving fallback texture path.",
                tilesetJsonPath
            );
        }

        return null;
    }

    /// <summary>
    ///     Gets a cached texture by its identifier.
    /// </summary>
    /// <param name="id">The texture identifier.</param>
    /// <returns>The cached texture.</returns>
    /// <exception cref="ArgumentException">Thrown if id is null or empty.</exception>
    /// <exception cref="KeyNotFoundException">Thrown if texture not found.</exception>
    public Texture2D GetTexture(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        if (_textures.TryGetValue(id, out Texture2D? texture) && texture != null)
        {
            return texture;
        }

        throw new KeyNotFoundException($"Texture '{id}' not loaded or was evicted from cache.");
    }

    // REMOVED: HotReloadTexture() - obsolete (depended on manifest.json)
    // For hot-reloading during development, use UnregisterTexture() + LoadTexture() manually

    /// <summary>
    ///     Registers a pre-loaded texture with the asset manager.
    ///     Useful for runtime-loaded textures (e.g., sprite sheets).
    /// </summary>
    /// <param name="id">Unique identifier for the texture.</param>
    /// <param name="texture">The pre-loaded texture to register.</param>
    public void RegisterTexture(string id, Texture2D texture)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(texture);

        // LruCache will handle old texture disposal automatically
        _textures.AddOrUpdate(id, texture);
        _logger?.LogSpriteTextureRegisteredDebug(id);
    }

    /// <summary>
    ///     Unregisters and disposes a texture.
    /// </summary>
    /// <param name="id">The texture identifier to remove.</param>
    /// <returns>True if texture was found and removed.</returns>
    public bool UnregisterTexture(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        bool removed = _textures.Remove(id); // LruCache handles disposal
        if (removed)
        {
            _logger?.LogSpriteTextureUnregistered(id);
        }

        return removed;
    }
}

/// <summary>
///     Represents a texture that has been loaded from disk and is waiting for GPU upload.
/// </summary>
/// <param name="Id">Texture identifier.</param>
/// <param name="Data">Raw image bytes (PNG/etc.).</param>
internal readonly record struct PendingTexture(string Id, byte[] Data);
