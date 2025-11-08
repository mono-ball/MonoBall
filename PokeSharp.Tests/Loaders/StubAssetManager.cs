using PokeSharp.Rendering.Assets;

namespace PokeSharp.Tests.Loaders;

/// <summary>
///     Stub implementation that matches AssetManager's public interface for testing.
///     Tracks texture loads without requiring GraphicsDevice or file I/O.
/// </summary>
internal class StubAssetManager : IAssetProvider
{
    private readonly HashSet<string> _textureIds = new();

    public void LoadTexture(string id, string relativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        _textureIds.Add(id);
    }

    public bool HasTexture(string id)
    {
        return _textureIds.Contains(id);
    }

    public int LoadedTextureCount => _textureIds.Count;
}
