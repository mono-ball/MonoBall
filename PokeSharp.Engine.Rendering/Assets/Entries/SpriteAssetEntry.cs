namespace PokeSharp.Engine.Rendering.Assets.Entries;

/// <summary>
///     Represents a sprite entry in the asset manifest.
/// </summary>
public class SpriteAssetEntry
{
    /// <summary>
    ///     Gets or sets the unique identifier for the sprite.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    ///     Gets or sets the relative path to the sprite PNG file.
    /// </summary>
    public required string Path { get; set; }
}
