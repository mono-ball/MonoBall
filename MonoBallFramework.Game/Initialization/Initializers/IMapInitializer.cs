using Arch.Core;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Systems.Rendering;

namespace MonoBallFramework.Game.Initialization.Initializers;

/// <summary>
///     Interface for handling map loading and initialization.
/// </summary>
public interface IMapInitializer
{
    /// <summary>
    ///     Sets the sprite texture loader after construction (for delayed initialization).
    /// </summary>
    /// <param name="loader">The sprite texture loader instance.</param>
    void SetSpriteTextureLoader(SpriteTextureLoader loader);

    /// <summary>
    ///     Loads a map from EF Core definition (definition-based loading).
    ///     Creates individual entities for each tile with appropriate components.
    /// </summary>
    /// <param name="mapId">The unified map identifier.</param>
    /// <returns>The MapInfo entity containing map metadata, or null if loading failed.</returns>
    Task<Entity?> LoadMap(GameMapId mapId);
}
