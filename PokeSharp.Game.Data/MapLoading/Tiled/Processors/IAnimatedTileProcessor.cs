using Arch.Core;
using PokeSharp.Game.Data.MapLoading.Tiled.Services;
using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

namespace PokeSharp.Game.Data.MapLoading.Tiled.Processors;

/// <summary>
///     Interface for processing animated tile entities from tileset animations.
///     Enables dependency injection and unit testing.
/// </summary>
public interface IAnimatedTileProcessor
{
    /// <summary>
    ///     Creates animated tile entities for all tilesets in a map.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="tmxDoc">The TMX document.</param>
    /// <param name="mapInfoEntity">The map entity for establishing relationships.</param>
    /// <param name="tilesets">Loaded tilesets.</param>
    /// <param name="mapId">The map ID to filter tiles by (prevents cross-map corruption).</param>
    /// <returns>Number of animated tiles created.</returns>
    int CreateAnimatedTileEntities(
        World world,
        TmxDocument tmxDoc,
        Entity mapInfoEntity,
        IReadOnlyList<LoadedTileset> tilesets,
        int mapId
    );
}
