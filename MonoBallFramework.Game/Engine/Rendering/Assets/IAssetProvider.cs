namespace MonoBallFramework.Game.Engine.Rendering.Assets;

/// <summary>
///     Provides texture loading and management capabilities for map loading and rendering.
///     Abstraction to enable testing without GraphicsDevice dependency.
/// </summary>
public interface IAssetProvider
{
    /// <summary>
    ///     Loads a texture synchronously from the specified path.
    ///     Uses ContentProvider to resolve the path, ensuring mod overrides are respected.
    /// </summary>
    /// <param name="id">Unique identifier for the texture.</param>
    /// <param name="relativePath">Path to the texture file relative to content root.</param>
    void LoadTexture(string id, string relativePath);

    /// <summary>
    ///     Loads a texture from an already-resolved absolute path.
    ///     Use this when the caller has already resolved the path via ContentProvider.
    /// </summary>
    /// <param name="id">Unique identifier for the texture.</param>
    /// <param name="absolutePath">The absolute path to the texture file (already resolved via ContentProvider).</param>
    void LoadTextureFromResolvedPath(string id, string absolutePath);

    /// <summary>
    ///     Checks if a texture with the specified identifier has been loaded.
    /// </summary>
    /// <param name="id">Texture identifier to check.</param>
    /// <returns>True if the texture is loaded, false otherwise.</returns>
    bool HasTexture(string id);

    /// <summary>
    ///     Preloads a texture asynchronously - reads file bytes on background thread.
    ///     Uses ContentProvider to resolve the path, ensuring mod overrides are respected.
    ///     Call ProcessTextureQueue() from Update loop to upload to GPU incrementally.
    /// </summary>
    /// <param name="id">Unique identifier for the texture.</param>
    /// <param name="relativePath">Path to the texture file relative to content root.</param>
    void PreloadTextureAsync(string id, string relativePath);

    /// <summary>
    ///     Preloads a texture asynchronously from an already-resolved absolute path.
    ///     Use this when the caller has already resolved the path via ContentProvider.
    ///     Call ProcessTextureQueue() from Update loop to upload to GPU incrementally.
    /// </summary>
    /// <param name="id">Unique identifier for the texture.</param>
    /// <param name="absolutePath">The absolute path to the texture file (already resolved via ContentProvider).</param>
    void PreloadTextureFromResolvedPathAsync(string id, string absolutePath);

    /// <summary>
    ///     Checks if a texture is currently being preloaded asynchronously.
    /// </summary>
    /// <param name="id">Texture identifier to check.</param>
    /// <returns>True if texture is currently loading.</returns>
    bool IsTextureLoading(string id);

    /// <summary>
    ///     Gets the number of textures waiting to be uploaded to GPU.
    /// </summary>
    int PendingTextureCount { get; }

    /// <summary>
    ///     Processes pending textures by uploading them to GPU.
    ///     Call this from the Update loop to incrementally upload textures.
    /// </summary>
    /// <returns>Number of textures uploaded this call.</returns>
    int ProcessTextureQueue();
}
