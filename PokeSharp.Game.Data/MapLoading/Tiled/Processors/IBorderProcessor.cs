using Arch.Core;
using PokeSharp.Game.Components.Maps;
using PokeSharp.Game.Data.MapLoading.Tiled.Services;
using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

namespace PokeSharp.Game.Data.MapLoading.Tiled.Processors;

/// <summary>
///     Interface for processing border data from Tiled map properties.
///     Enables dependency injection and unit testing.
/// </summary>
public interface IBorderProcessor
{
    /// <summary>
    ///     Parses border data from map properties and creates a MapBorder component.
    /// </summary>
    /// <param name="tmxDoc">The Tiled map document.</param>
    /// <param name="tilesets">Loaded tilesets for source rectangle calculation.</param>
    /// <returns>A MapBorder if border data exists; otherwise, null.</returns>
    MapBorder? ParseBorder(TmxDocument tmxDoc, IReadOnlyList<LoadedTileset> tilesets);

    /// <summary>
    ///     Adds a MapBorder component to the map info entity if border data exists.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="mapInfoEntity">The entity to attach the MapBorder to.</param>
    /// <param name="tmxDoc">The Tiled map document.</param>
    /// <param name="tilesets">Loaded tilesets for source rectangle calculation.</param>
    /// <returns>True if border was added; otherwise, false.</returns>
    bool AddBorderToEntity(
        World world,
        Entity mapInfoEntity,
        TmxDocument tmxDoc,
        IReadOnlyList<LoadedTileset> tilesets
    );
}
