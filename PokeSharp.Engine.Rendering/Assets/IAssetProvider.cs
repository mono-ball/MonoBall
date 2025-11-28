namespace PokeSharp.Engine.Rendering.Assets;

/// <summary>
///     Provides texture loading and management capabilities for map loading and rendering.
///     Abstraction to enable testing without GraphicsDevice dependency.
/// </summary>
public interface IAssetProvider
{
    /// <summary>
    ///     Loads a texture from the specified path with the given identifier.
    /// </summary>
    /// <param name="id">Unique identifier for the texture.</param>
    /// <param name="relativePath">Path to the texture file relative to content root.</param>
    void LoadTexture(string id, string relativePath);

    /// <summary>
    ///     Checks if a texture with the specified identifier has been loaded.
    /// </summary>
    /// <param name="id">Texture identifier to check.</param>
    /// <returns>True if the texture is loaded, false otherwise.</returns>
    bool HasTexture(string id);
}
