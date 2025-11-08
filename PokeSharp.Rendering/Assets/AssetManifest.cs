using PokeSharp.Rendering.Assets.Entries;

namespace PokeSharp.Rendering.Assets;

/// <summary>
///     Defines the structure of the asset manifest JSON file.
/// </summary>
public class AssetManifest
{
    /// <summary>
    ///     Gets or sets the list of tileset assets.
    /// </summary>
    public List<TilesetAssetEntry>? Tilesets { get; set; }

    /// <summary>
    ///     Gets or sets the list of sprite assets.
    /// </summary>
    public List<SpriteAssetEntry>? Sprites { get; set; }

    /// <summary>
    ///     Gets or sets the list of map assets.
    /// </summary>
    public List<MapAssetEntry>? Maps { get; set; }
}
