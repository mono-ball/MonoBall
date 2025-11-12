using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.Common.Logging;

namespace PokeSharp.Engine.Rendering.Assets;

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
    private readonly string _assetRoot = assetRoot;

    private readonly GraphicsDevice _graphicsDevice =
        graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));

    private readonly ILogger<AssetManager>? _logger = logger;
    private readonly Dictionary<string, Texture2D> _textures = new();
    private bool _disposed;
    private AssetManifest? _manifest;

    /// <summary>
    ///     Gets the root directory path where assets are stored.
    /// </summary>
    public string AssetRoot => _assetRoot;

    /// <summary>
    ///     Gets the number of loaded textures.
    /// </summary>
    public int LoadedTextureCount => _textures.Count;

    /// <summary>
    ///     Disposes all loaded textures.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (var texture in _textures.Values)
            texture.Dispose();

        _textures.Clear();
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Loads all assets defined in a manifest file.
    /// </summary>
    /// <param name="manifestPath">Path to the manifest JSON file.</param>
    public void LoadManifest(string manifestPath = "Assets/manifest.json")
    {
        LoadManifestInternal(manifestPath);
    }

    private void LoadManifestInternal(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"Asset manifest not found: {manifestPath}");

        var json = File.ReadAllText(manifestPath);
        _logger?.LogDebug("Manifest JSON content:\n{Json}", json);

        // Use case-insensitive deserialization to match lowercase JSON keys with PascalCase C# properties
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        _manifest =
            JsonSerializer.Deserialize<AssetManifest>(json, options)
            ?? throw new InvalidOperationException("Failed to deserialize asset manifest");

        _logger?.LogAssetStatus("Manifest deserialized");
        _logger?.LogAssetStatus("Tilesets discovered", ("count", _manifest.Tilesets?.Count ?? 0));
        _logger?.LogAssetStatus("Sprites discovered", ("count", _manifest.Sprites?.Count ?? 0));
        _logger?.LogAssetStatus("Maps discovered", ("count", _manifest.Maps?.Count ?? 0));

        // Load all tilesets
        if (_manifest.Tilesets != null)
        {
            _logger?.LogAssetLoadingStarted("tileset(s)", _manifest.Tilesets.Count);
            var successful = 0;
            var failed = 0;

            foreach (var tileset in _manifest.Tilesets)
                try
                {
                    LoadTexture(tileset.Id, tileset.Path);
                    successful++;
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger?.LogExceptionWithContext(
                        ex,
                        "Failed to load tileset '{TilesetId}'",
                        tileset.Id
                    );
                }

            if (successful > 0)
            {
                if (failed > 0)
                    _logger?.LogAssetStatus(
                        "Tilesets loaded",
                        ("count", successful),
                        ("failed", failed)
                    );
                else
                    _logger?.LogAssetStatus("Tilesets loaded", ("count", successful));
            }
        }

        // Load all sprites
        if (_manifest.Sprites != null)
        {
            _logger?.LogAssetLoadingStarted("sprite(s)", _manifest.Sprites.Count);
            var successful = 0;
            var failed = 0;

            foreach (var sprite in _manifest.Sprites)
                try
                {
                    LoadTexture(sprite.Id, sprite.Path);
                    successful++;
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger?.LogExceptionWithContext(
                        ex,
                        "Failed to load sprite '{SpriteId}'",
                        sprite.Id
                    );
                }

            if (successful > 0)
            {
                if (failed > 0)
                    _logger?.LogAssetStatus(
                        "Sprites loaded",
                        ("count", successful),
                        ("failed", failed)
                    );
                else
                    _logger?.LogAssetStatus("Sprites loaded", ("count", successful));
            }
        }
    }

    /// <summary>
    ///     Loads a texture from a PNG file and caches it.
    /// </summary>
    /// <param name="id">Unique identifier for the texture.</param>
    /// <param name="relativePath">Path relative to asset root.</param>
    public void LoadTexture(string id, string relativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentException.ThrowIfNullOrEmpty(relativePath);

        var fullPath = Path.Combine(_assetRoot, relativePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Texture file not found: {fullPath}");

        var sw = Stopwatch.StartNew();

        using var fileStream = File.OpenRead(fullPath);
        var texture = Texture2D.FromStream(_graphicsDevice, fileStream);

        sw.Stop();
        var elapsedMs = sw.Elapsed.TotalMilliseconds;

        _textures[id] = texture;

        // Log texture loading with timing
        _logger?.LogTextureLoaded(id, elapsedMs, texture.Width, texture.Height);

        // Warn about slow texture loads (>100ms)
        if (elapsedMs > 100.0)
            _logger?.LogSlowTextureLoad(id, elapsedMs);
    }

    /// <summary>
    ///     Gets a cached texture by its identifier.
    /// </summary>
    /// <param name="id">The texture identifier.</param>
    /// <returns>The cached texture.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if texture not found.</exception>
    public Texture2D GetTexture(string id)
    {
        if (_textures.TryGetValue(id, out var texture))
            return texture;

        throw new KeyNotFoundException(
            $"Texture '{id}' not loaded. Available textures: {string.Join(", ", _textures.Keys)}"
        );
    }

    /// <summary>
    ///     Checks if a texture is loaded.
    /// </summary>
    /// <param name="id">The texture identifier.</param>
    /// <returns>True if texture is loaded.</returns>
    public bool HasTexture(string id)
    {
        return _textures.ContainsKey(id);
    }

    /// <summary>
    ///     Hot-reloads a texture from disk (useful for development).
    /// </summary>
    /// <param name="id">The texture identifier to reload.</param>
    public void HotReloadTexture(string id)
    {
        if (!_textures.ContainsKey(id))
            throw new KeyNotFoundException(
                $"Cannot hot-reload texture '{id}' - not originally loaded"
            );

        if (_manifest == null)
            throw new InvalidOperationException("Cannot hot-reload without manifest");

        // Find the texture path in manifest
        var tilesetEntry = _manifest.Tilesets?.FirstOrDefault(t => t.Id == id);
        var spriteEntry = _manifest.Sprites?.FirstOrDefault(s => s.Id == id);

        var path =
            tilesetEntry?.Path
            ?? spriteEntry?.Path
            ?? throw new KeyNotFoundException($"Texture '{id}' not found in manifest");

        // Dispose old texture
        _textures[id].Dispose();
        _textures.Remove(id);

        // Reload
        LoadTexture(id, path);
    }

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

        // If texture already exists, dispose the old one
        if (_textures.TryGetValue(id, out var oldTexture))
        {
            _logger?.LogDebug("Replacing existing texture: {TextureId}", id);
            oldTexture.Dispose();
        }

        _textures[id] = texture;
        _logger?.LogDebug(
            "Registered texture: {TextureId} ({Width}x{Height})",
            id,
            texture.Width,
            texture.Height);
    }

    /// <summary>
    ///     Unregisters and disposes a texture.
    /// </summary>
    /// <param name="id">The texture identifier to remove.</param>
    /// <returns>True if texture was found and removed.</returns>
    public bool UnregisterTexture(string id)
    {
        if (_textures.TryGetValue(id, out var texture))
        {
            texture.Dispose();
            _textures.Remove(id);
            _logger?.LogDebug("Unregistered texture: {TextureId}", id);
            return true;
        }

        return false;
    }
}
