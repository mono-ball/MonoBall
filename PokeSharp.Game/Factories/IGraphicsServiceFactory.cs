using Arch.Core;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Core.Systems;
using PokeSharp.Rendering.Assets;
using PokeSharp.Rendering.Loaders;

namespace PokeSharp.Game.Factories;

/// <summary>
///     Abstract Factory for creating services that depend on GraphicsDevice.
///     This pattern solves the DI split where some services are created in Program.cs
///     and others in PokeSharpGame.Initialize() due to GraphicsDevice availability.
/// </summary>
public interface IGraphicsServiceFactory
{
    /// <summary>
    ///     Creates an AssetManager instance configured with the GraphicsDevice.
    /// </summary>
    /// <param name="graphicsDevice">The MonoGame GraphicsDevice for texture loading.</param>
    /// <returns>A configured AssetManager instance with logging support.</returns>
    AssetManager CreateAssetManager(GraphicsDevice graphicsDevice);

    /// <summary>
    ///     Creates a MapLoader instance that uses the provided AssetManager.
    /// </summary>
    /// <param name="assetManager">The AssetManager for texture access during map loading.</param>
    /// <returns>A configured MapLoader instance with entity factory and logging support.</returns>
    MapLoader CreateMapLoader(AssetManager assetManager);

    /// <summary>
    ///     Creates a SpatialHashSystem instance for the given ECS world.
    /// </summary>
    /// <param name="world">The Arch ECS World for entity queries.</param>
    /// <returns>A configured SpatialHashSystem instance with logging support.</returns>
    SpatialHashSystem CreateSpatialHashSystem(World world);
}
