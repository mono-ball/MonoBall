using Arch.Core;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Tmx;
using MonoBallFramework.Game.Scripting.Api;

namespace MonoBallFramework.Game.GameData.MapLoading.Tiled.Spawners;

/// <summary>
///     Context object passed to entity spawners containing all information
///     needed to spawn an entity from a Tiled object.
///
///     This eliminates parameter bloat and makes spawner signatures consistent.
/// </summary>
public sealed class EntitySpawnContext
{
    /// <summary>
    ///     The ECS world to create entities in.
    /// </summary>
    public required World World { get; init; }

    /// <summary>
    ///     The Tiled object being spawned.
    /// </summary>
    public required TmxObject TiledObject { get; init; }

    /// <summary>
    ///     The map info entity for establishing parent relationships.
    /// </summary>
    public required Entity MapInfoEntity { get; init; }

    /// <summary>
    ///     Game map ID of the current map.
    /// </summary>
    public required GameMapId MapId { get; init; }

    /// <summary>
    ///     Tile width for converting pixel X to tile coordinates.
    /// </summary>
    public required int TileWidth { get; init; }

    /// <summary>
    ///     Tile height for converting pixel Y to tile coordinates.
    /// </summary>
    public required int TileHeight { get; init; }

    /// <summary>
    ///     Collection to track sprite IDs that need loading.
    ///     Spawners should add any sprite IDs they reference to this set.
    /// </summary>
    public HashSet<GameSpriteId>? RequiredSpriteIds { get; init; }

    /// <summary>
    ///     Game state API for checking flag values during spawn.
    ///     Used for flag-based visibility control (e.g., FLAG_HIDE_* patterns).
    /// </summary>
    public IGameStateApi? GameStateApi { get; init; }

    /// <summary>
    ///     Gets the object's tile coordinates (converted from pixel position).
    /// </summary>
    public (int X, int Y) GetTilePosition()
    {
        int tileX = (int)(TiledObject.X / TileWidth);
        int tileY = (int)(TiledObject.Y / TileHeight);
        return (tileX, tileY);
    }

    /// <summary>
    ///     Creates a context string for error messages.
    /// </summary>
    public string CreateErrorContext()
    {
        var (x, y) = GetTilePosition();
        string name = TiledObject.Name ?? "(unnamed)";
        string type = TiledObject.Type ?? "(no type)";
        return $"{type} '{name}' at tile ({x}, {y}) in map {MapId}";
    }

    /// <summary>
    ///     Registers a sprite ID for lazy loading.
    /// </summary>
    public void RegisterSpriteId(GameSpriteId spriteId)
    {
        RequiredSpriteIds?.Add(spriteId);
    }
}
