using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.Rendering.Assets;
using PokeSharp.Game.Services;

namespace PokeSharp.Game.Systems;

/// <summary>
///     Service responsible for loading sprite sheet textures into the AssetManager.
///     Scans available sprite manifests and loads their sprite sheets.
/// </summary>
public class SpriteTextureLoader
{
    private readonly SpriteLoader _spriteLoader;
    private readonly AssetManager _assetManager;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly ILogger<SpriteTextureLoader>? _logger;
    private readonly string _spritesBasePath;

    public SpriteTextureLoader(
        SpriteLoader spriteLoader,
        AssetManager assetManager,
        GraphicsDevice graphicsDevice,
        string spritesBasePath = "Assets/Sprites/NPCs",
        ILogger<SpriteTextureLoader>? logger = null)
    {
        _spriteLoader = spriteLoader ?? throw new ArgumentNullException(nameof(spriteLoader));
        _assetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _spritesBasePath = spritesBasePath;
        _logger = logger;
    }

    /// <summary>
    ///     Loads all sprite sheet textures found in the sprites directory.
    /// </summary>
    /// <returns>Number of sprite sheets loaded.</returns>
    public async Task<int> LoadAllSpriteTexturesAsync()
    {
        _logger?.LogInformation("Loading sprite sheet textures...");

        var manifests = await _spriteLoader.LoadAllSpritesAsync();
        _logger?.LogInformation("Found {Count} sprite manifests to load", manifests.Count);

        var loadedCount = 0;
        var failedCount = 0;

        foreach (var manifest in manifests)
        {
            try
            {
                var textureKey = GetTextureKey(manifest.Category, manifest.Name);
                var spritesheetPath = _spriteLoader.GetSpriteSheetPath(manifest);

                _logger?.LogInformation(
                    "Loading sprite {Index}/{Total}: {Category}/{Name}",
                    loadedCount + 1,
                    manifests.Count,
                    manifest.Category,
                    manifest.Name);

                if (string.IsNullOrEmpty(spritesheetPath) || !File.Exists(spritesheetPath))
                {
                    _logger?.LogWarning(
                        "Sprite sheet not found for {Category}/{Name}",
                        manifest.Category,
                        manifest.Name);
                    failedCount++;
                    continue;
                }

                _logger?.LogDebug("Opening file stream: {Path}", spritesheetPath);

                // Load texture using MonoGame's Texture2D.FromStream
                using var fileStream = File.OpenRead(spritesheetPath);

                _logger?.LogDebug("Decoding texture from stream...");
                var texture = Texture2D.FromStream(_graphicsDevice, fileStream);

                _logger?.LogInformation("Loaded texture: {Category}/{Name} Format={Format}, Size={Width}x{Height}",
                    manifest.Category, manifest.Name, texture.Format, texture.Width, texture.Height);

                // Note: Transparency should be baked into the PNG by the extractor
                // Runtime mask color application doesn't persist correctly

                _logger?.LogDebug("Registering texture: {Key}", textureKey);
                // Register with AssetManager (using a direct registration method)
                RegisterTexture(textureKey, texture);

                _logger?.LogDebug("Successfully loaded: {Category}/{Name}", manifest.Category, manifest.Name);
                loadedCount++;
            }
            catch (Exception ex)
            {
                _logger?.LogError(
                    ex,
                    "Failed to load sprite sheet for {Category}/{Name}",
                    manifest.Category,
                    manifest.Name);
                failedCount++;
            }
        }

        _logger?.LogInformation(
            "Sprite sheet loading complete: {Loaded} loaded, {Failed} failed",
            loadedCount,
            failedCount);

        return loadedCount;
    }

    /// <summary>
    ///     Loads a specific sprite sheet texture.
    /// </summary>
    public void LoadSpriteTexture(string category, string spriteName)
    {
        var textureKey = GetTextureKey(category, spriteName);

        // Check if already loaded
        if (_assetManager.HasTexture(textureKey))
            return;

        // Use NPCSpriteLoader to resolve the actual path
        var spritesheetPath = _spriteLoader.GetSpriteSheetPath(category, spriteName);

        if (string.IsNullOrEmpty(spritesheetPath) || !File.Exists(spritesheetPath))
        {
            _logger?.LogWarning(
                "Sprite sheet not found for {Category}/{SpriteName}",
                category,
                spriteName);
            return;
        }

        // Load texture (transparency is already baked into the PNG)
        using var fileStream = File.OpenRead(spritesheetPath);
        var texture = Texture2D.FromStream(_graphicsDevice, fileStream);

        _logger?.LogInformation("Lazy-loaded texture: {TextureKey}, Format={Format}", textureKey, texture.Format);

        // Note: Transparency should be baked into the PNG by the extractor
        // Runtime mask color application doesn't persist correctly

        // Register with AssetManager
        RegisterTexture(textureKey, texture);

        _logger?.LogDebug(
            "Loaded sprite sheet on-demand: {TextureKey}",
            textureKey);
    }

    /// <summary>
    ///     Gets the texture key used by the rendering system.
    /// </summary>
    private static string GetTextureKey(string category, string spriteName)
    {
        return $"sprites/{category}/{spriteName}";
    }

    /// <summary>
    ///     Registers a texture directly with the AssetManager.
    /// </summary>
    private void RegisterTexture(string textureKey, Texture2D texture)
    {
        _assetManager.RegisterTexture(textureKey, texture);
    }

}

