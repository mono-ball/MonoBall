using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Services;

namespace MonoBallFramework.Game.GameData.MapLoading.Tiled.Deferred;

/// <summary>
///     Contains all pre-computed data needed to create map entities.
///     Built on a background thread, applied on the main thread.
/// </summary>
public class PreparedMapData
{
    public required GameMapId MapId { get; init; }
    public required string MapName { get; init; }
    public required Vector2 WorldOffset { get; init; }
    public required int MapWidth { get; init; }
    public required int MapHeight { get; init; }
    public required int TileWidth { get; init; }
    public required int TileHeight { get; init; }

    // Map file path for texture resolution
    public required string MapPath { get; init; }

    // Pre-computed tile data for bulk entity creation
    public required List<PreparedTile> Tiles { get; init; }

    // Tileset references (already loaded)
    public required IReadOnlyList<LoadedTileset> Tilesets { get; init; }

    // Map properties
    public required Dictionary<string, object> Properties { get; init; }

    // Optional components
    public string? Name { get; init; }
    public string? RegionSection { get; init; }
    public string? MusicTrack { get; init; }

    // Map flags from MapEntity definition (critical for popup display!)
    public bool ShowMapName { get; init; } = true;

    // Border data for Pokemon Emerald-style map borders
    public PreparedBorderData? BorderData { get; init; }

    // Animation data for animated tiles (indices into Tilesets)
    public List<PreparedAnimatedTile>? AnimatedTiles { get; init; }

    // Connected maps info
    public List<MapConnection>? Connections { get; init; }

    // Image layers
    public List<PreparedImageLayer>? ImageLayers { get; init; }

    // Object groups (warps, NPCs, etc.)
    public List<PreparedObjectGroup>? ObjectGroups { get; init; }
}

/// <summary>
///     Pre-computed tile data ready for entity creation.
///     Contains all component data needed without further computation.
/// </summary>
public readonly struct PreparedTile
{
    public required int X { get; init; }
    public required int Y { get; init; }
    public required GameMapId MapId { get; init; }
    public required string TilesetId { get; init; }
    public required int TileGid { get; init; }
    public required Rectangle SourceRect { get; init; }
    public required bool FlipH { get; init; }
    public required bool FlipV { get; init; }
    public required bool FlipD { get; init; }
    public required byte Elevation { get; init; }

    // Optional layer offset for parallax
    public float LayerOffsetX { get; init; }
    public float LayerOffsetY { get; init; }

    // Optional tile properties (terrain, script, etc.)
    public Dictionary<string, object>? Properties { get; init; }
}

/// <summary>
///     Pre-computed image layer data.
/// </summary>
public readonly struct PreparedImageLayer
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string ImagePath { get; init; }
    public required float X { get; init; }
    public required float Y { get; init; }
    public required float OffsetX { get; init; }
    public required float OffsetY { get; init; }
    public required float Opacity { get; init; }
    public required bool Visible { get; init; }
}

/// <summary>
///     Pre-computed object group with objects.
/// </summary>
public class PreparedObjectGroup
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required List<PreparedObject> Objects { get; init; }
}

/// <summary>
///     Pre-computed object data (warps, NPCs, triggers, etc.)
/// </summary>
public readonly struct PreparedObject
{
    public required int Id { get; init; }
    public required string? Name { get; init; }
    public required string? Type { get; init; }
    public required float X { get; init; }
    public required float Y { get; init; }
    public required float Width { get; init; }
    public required float Height { get; init; }
    public required Dictionary<string, object> Properties { get; init; }
}

/// <summary>
///     Represents a connection between two maps.
/// </summary>
public readonly struct MapConnection
{
    public required GameMapId TargetMapId { get; init; }
    public required string Direction { get; init; }
    public required int Offset { get; init; }
}

/// <summary>
///     Pre-computed border data for map edges.
///     Borders are rendered at map boundaries to create seamless transitions.
/// </summary>
public class PreparedBorderData
{
    /// <summary>
    ///     Bottom layer tile GIDs (2x2 grid, flattened: [top-left, top-right, bottom-left, bottom-right]).
    /// </summary>
    public required int[] BottomLayerGids { get; init; }

    /// <summary>
    ///     Top layer tile GIDs (2x2 grid, flattened: [top-left, top-right, bottom-left, bottom-right]).
    /// </summary>
    public required int[] TopLayerGids { get; init; }

    /// <summary>
    ///     Tileset ID for border rendering.
    /// </summary>
    public required string TilesetId { get; init; }
}

/// <summary>
///     Pre-computed animated tile data.
///     Stored per tile GID with frame data for animation.
/// </summary>
public readonly struct PreparedAnimatedTile
{
    /// <summary>
    ///     The base GID of the animated tile.
    /// </summary>
    public required int TileGid { get; init; }

    /// <summary>
    ///     Tileset ID containing this animated tile.
    /// </summary>
    public required string TilesetId { get; init; }

    /// <summary>
    ///     Frame source rectangles for animation (pre-calculated).
    /// </summary>
    public required Rectangle[] FrameSourceRects { get; init; }

    /// <summary>
    ///     Duration of each frame in milliseconds.
    /// </summary>
    public required int[] FrameDurations { get; init; }

    /// <summary>
    ///     Total animation duration in milliseconds.
    /// </summary>
    public required int TotalDuration { get; init; }
}
