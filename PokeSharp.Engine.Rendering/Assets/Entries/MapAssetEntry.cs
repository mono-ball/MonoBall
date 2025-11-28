namespace PokeSharp.Engine.Rendering.Assets.Entries;

/// <summary>
///     Represents a map entry in the asset manifest.
/// </summary>
public class MapAssetEntry
{
    /// <summary>
    ///     Gets or sets the unique identifier for the map.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    ///     Gets or sets the relative path to the TMX map file.
    /// </summary>
    public required string Path { get; set; }
}
