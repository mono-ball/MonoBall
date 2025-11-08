using System.Reflection;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Rendering.Assets;

namespace PokeSharp.Tests.Loaders;

/// <summary>
///     Fake AssetManager for testing that doesn't require GraphicsDevice or file I/O.
///     Uses reflection to bypass GraphicsDevice requirement.
/// </summary>
internal class FakeAssetManager : AssetManager
{
    private readonly HashSet<string> _textureIds = new();

    public FakeAssetManager()
        : base(null!, "Assets", null) // Pass null, we'll handle it with reflection
    {
        // Use reflection to set the private _graphicsDevice field to avoid null reference
        // This is a testing workaround since we can't create a real GraphicsDevice in headless tests
        var graphicsDeviceField = typeof(AssetManager).GetField("_graphicsDevice",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Create a fake but non-null reference (we won't actually use it)
        // Since we override all methods that would use it, this is safe
        graphicsDeviceField?.SetValue(this, CreatePlaceholderGraphicsDevice());
    }

    /// <summary>
    ///     Creates a placeholder object to satisfy null checks.
    ///     We never actually call methods on this since we override all AssetManager methods.
    /// </summary>
    private static GraphicsDevice? CreatePlaceholderGraphicsDevice()
    {
        // Return null - our overridden methods won't use it anyway
        return null;
    }

    /// <summary>
    ///     Overrides texture loading to avoid file I/O.
    ///     Just tracks that the texture was requested.
    /// </summary>
    public new void LoadTexture(string id, string relativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        _textureIds.Add(id);
    }

    /// <summary>
    ///     Overrides texture check to use our tracked IDs.
    /// </summary>
    public new bool HasTexture(string id)
    {
        return _textureIds.Contains(id);
    }

    /// <summary>
    ///     Gets the count of loaded textures for test verification.
    /// </summary>
    public new int LoadedTextureCount => _textureIds.Count;
}
