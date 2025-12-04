using Arch.Core;
using PokeSharp.Game.Data.MapLoading.Tiled.Services;
using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

namespace PokeSharp.Game.Data.MapLoading.Tiled.Processors;

/// <summary>
///     Interface for processing map layers and creating tile entities.
///     Enables dependency injection and unit testing.
/// </summary>
public interface ILayerProcessor
{
    /// <summary>
    ///     Processes all tile layers and creates tile entities.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="tmxDoc">The Tiled map document.</param>
    /// <param name="mapInfoEntity">The map entity for establishing relationships.</param>
    /// <param name="mapId">The map runtime ID.</param>
    /// <param name="tilesets">Loaded tilesets.</param>
    /// <returns>Total number of tiles created.</returns>
    int ProcessLayers(
        World world,
        TmxDocument tmxDoc,
        Entity mapInfoEntity,
        int mapId,
        IReadOnlyList<LoadedTileset> tilesets
    );

    /// <summary>
    ///     Parses map connection properties from Tiled custom properties.
    /// </summary>
    /// <param name="tmxDoc">The Tiled document to parse connections from.</param>
    /// <returns>A list of map connections parsed from the document.</returns>
    List<MapConnection> ParseMapConnections(TmxDocument tmxDoc);
}
