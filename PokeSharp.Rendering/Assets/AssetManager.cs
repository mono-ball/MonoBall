using Microsoft.Xna.Framework.Graphics;
using System.Text.Json;

namespace PokeSharp.Rendering.Assets;

/// <summary>
/// Manages runtime asset loading for textures and resources.
/// Provides PNG loading without MonoGame Content Pipeline.
/// </summary>
public class AssetManager : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly string _assetRoot;
    private readonly Dictionary<string, Texture2D> _textures;
    private AssetManifest? _manifest;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the AssetManager class.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device for creating textures.</param>
    /// <param name="assetRoot">Root directory for assets (default: "Assets").</param>
    public AssetManager(GraphicsDevice graphicsDevice, string assetRoot = "Assets")
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _assetRoot = assetRoot;
        _textures = new Dictionary<string, Texture2D>();
    }

    /// <summary>
    /// Loads all assets defined in a manifest file.
    /// </summary>
    /// <param name="manifestPath">Path to the manifest JSON file.</param>
    public void LoadManifest(string manifestPath = "Assets/manifest.json")
    {
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException($"Asset manifest not found: {manifestPath}");
        }

        var json = File.ReadAllText(manifestPath);
        Console.WriteLine($"📄 Manifest JSON content:\n{json}");

        // Use case-insensitive deserialization to match lowercase JSON keys with PascalCase C# properties
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        _manifest = JsonSerializer.Deserialize<AssetManifest>(json, options)
            ?? throw new InvalidOperationException("Failed to deserialize asset manifest");

        Console.WriteLine($"🔍 Deserialized manifest:");
        Console.WriteLine($"   Tilesets: {_manifest.Tilesets?.Count ?? 0}");
        Console.WriteLine($"   Sprites: {_manifest.Sprites?.Count ?? 0}");
        Console.WriteLine($"   Maps: {_manifest.Maps?.Count ?? 0}");

        // Load all tilesets
        if (_manifest.Tilesets != null)
        {
            Console.WriteLine($"📦 Loading {_manifest.Tilesets.Count} tileset(s)...");
            foreach (var tileset in _manifest.Tilesets)
            {
                try
                {
                    Console.WriteLine($"   → Loading tileset '{tileset.Id}' from '{tileset.Path}'...");
                    LoadTexture(tileset.Id, tileset.Path);
                    Console.WriteLine($"   ✅ Tileset '{tileset.Id}' loaded successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ❌ Failed to load tileset '{tileset.Id}': {ex.Message}");
                }
            }
        }

        // Load all sprites
        if (_manifest.Sprites != null)
        {
            Console.WriteLine($"🎨 Loading {_manifest.Sprites.Count} sprite(s)...");
            foreach (var sprite in _manifest.Sprites)
            {
                try
                {
                    Console.WriteLine($"   → Loading sprite '{sprite.Id}' from '{sprite.Path}'...");
                    LoadTexture(sprite.Id, sprite.Path);
                    Console.WriteLine($"   ✅ Sprite '{sprite.Id}' loaded successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ❌ Failed to load sprite '{sprite.Id}': {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Loads a texture from a PNG file and caches it.
    /// </summary>
    /// <param name="id">Unique identifier for the texture.</param>
    /// <param name="relativePath">Path relative to asset root.</param>
    public void LoadTexture(string id, string relativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentException.ThrowIfNullOrEmpty(relativePath);

        var fullPath = Path.Combine(_assetRoot, relativePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Texture file not found: {fullPath}");
        }

        using var fileStream = File.OpenRead(fullPath);
        var texture = Texture2D.FromStream(_graphicsDevice, fileStream);

        _textures[id] = texture;
    }

    /// <summary>
    /// Gets a cached texture by its identifier.
    /// </summary>
    /// <param name="id">The texture identifier.</param>
    /// <returns>The cached texture.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if texture not found.</exception>
    public Texture2D GetTexture(string id)
    {
        if (_textures.TryGetValue(id, out var texture))
        {
            return texture;
        }

        throw new KeyNotFoundException($"Texture '{id}' not loaded. Available textures: {string.Join(", ", _textures.Keys)}");
    }

    /// <summary>
    /// Checks if a texture is loaded.
    /// </summary>
    /// <param name="id">The texture identifier.</param>
    /// <returns>True if texture is loaded.</returns>
    public bool HasTexture(string id)
    {
        return _textures.ContainsKey(id);
    }

    /// <summary>
    /// Hot-reloads a texture from disk (useful for development).
    /// </summary>
    /// <param name="id">The texture identifier to reload.</param>
    public void HotReloadTexture(string id)
    {
        if (!_textures.ContainsKey(id))
        {
            throw new KeyNotFoundException($"Cannot hot-reload texture '{id}' - not originally loaded");
        }

        if (_manifest == null)
        {
            throw new InvalidOperationException("Cannot hot-reload without manifest");
        }

        // Find the texture path in manifest
        var tilesetEntry = _manifest.Tilesets?.FirstOrDefault(t => t.Id == id);
        var spriteEntry = _manifest.Sprites?.FirstOrDefault(s => s.Id == id);

        var path = tilesetEntry?.Path ?? spriteEntry?.Path
            ?? throw new KeyNotFoundException($"Texture '{id}' not found in manifest");

        // Dispose old texture
        _textures[id].Dispose();
        _textures.Remove(id);

        // Reload
        LoadTexture(id, path);
    }

    /// <summary>
    /// Gets the number of loaded textures.
    /// </summary>
    public int LoadedTextureCount => _textures.Count;

    /// <summary>
    /// Disposes all loaded textures.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var texture in _textures.Values)
        {
            texture.Dispose();
        }

        _textures.Clear();
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}
