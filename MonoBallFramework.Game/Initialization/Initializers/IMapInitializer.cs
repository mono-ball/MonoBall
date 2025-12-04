using Arch.Core;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Systems;
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
    /// <param name="mapId">The map identifier (e.g., "test-map", "littleroot_town").</param>
    /// <returns>The MapInfo entity containing map metadata, or null if loading failed.</returns>
    Task<Entity?> LoadMap(MapIdentifier mapId);

    /// <summary>
    ///     Loads a map from file path (LEGACY: Backward compatibility).
    ///     Use LoadMap(mapId) for definition-based loading instead.
    /// </summary>
    /// <param name="mapPath">Path to the map file.</param>
    /// <returns>The MapInfo entity containing map metadata, or null if loading failed.</returns>
    Task<Entity?> LoadMapFromFile(string mapPath);
}
