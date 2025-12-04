using Microsoft.Xna.Framework.Graphics;
using MonoBallFramework.Game.Engine.Rendering.Assets;
using MonoBallFramework.Game.Engine.Systems.Factories;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Core;

namespace MonoBallFramework.Game.GameData.Factories;

/// <summary>
///     Abstract Factory for creating graphics-related services that depend on GraphicsDevice.
///     Encapsulates the creation of AssetManager and MapLoader which cannot be constructed
///     until the GraphicsDevice is available at runtime.
/// </summary>
public interface IGraphicsServiceFactory
{
    /// <summary>
    ///     Creates an AssetManager instance with the provided GraphicsDevice.
    /// </summary>
    /// <param name="graphicsDevice">The MonoGame GraphicsDevice required for texture loading.</param>
    /// <param name="assetRoot">Root directory for assets (default: "Assets").</param>
    /// <returns>A configured AssetManager instance.</returns>
    AssetManager CreateAssetManager(GraphicsDevice graphicsDevice, string assetRoot = "Assets");

    /// <summary>
    ///     Creates a MapLoader instance with the provided AssetManager and optional EntityFactory.
    /// </summary>
    /// <param name="assetManager">The AssetManager for loading textures.</param>
    /// <param name="entityFactory">Optional EntityFactory for template-based entity creation.</param>
    /// <returns>A configured MapLoader instance.</returns>
    MapLoader CreateMapLoader(
        AssetManager assetManager,
        IEntityFactoryService? entityFactory = null
    );
}
