namespace PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

/// <summary>
///     Represents an object group (collision, triggers, etc.).
/// </summary>
public class TmxObjectGroup
{
    /// <summary>
    ///     Gets or sets the object group ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the object group name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the objects in this group.
    /// </summary>
    public List<TmxObject> Objects { get; set; } = new();
}
