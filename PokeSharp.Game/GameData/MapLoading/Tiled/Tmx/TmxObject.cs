namespace PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

/// <summary>
///     Represents an object in an object group.
/// </summary>
public class TmxObject
{
    /// <summary>
    ///     Gets or sets the object ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the object X position in pixels.
    /// </summary>
    public float X { get; set; }

    /// <summary>
    ///     Gets or sets the object Y position in pixels.
    /// </summary>
    public float Y { get; set; }

    /// <summary>
    ///     Gets or sets the object width in pixels.
    /// </summary>
    public float Width { get; set; }

    /// <summary>
    ///     Gets or sets the object height in pixels.
    /// </summary>
    public float Height { get; set; }

    /// <summary>
    ///     Gets or sets the object type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    ///     Gets or sets the object name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     Gets or sets custom properties for this object.
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new();
}
