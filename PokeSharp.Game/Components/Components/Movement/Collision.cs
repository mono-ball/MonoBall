namespace PokeSharp.Game.Components.Movement;

/// <summary>
///     Simple collision component for any entity (tiles, NPCs, items).
///     Determines if an entity blocks movement or is passable.
/// </summary>
/// <remarks>
///     Examples:
///     - Solid: walls, trees, buildings, NPCs, trainers
///     - Non-solid: grass (triggers encounters), items on ground, trigger zones
/// </remarks>
public struct Collision
{
    /// <summary>
    ///     Gets or sets whether this entity blocks movement.
    /// </summary>
    /// <remarks>
    ///     True = blocks movement (walls, NPCs, solid objects)
    ///     False = passable but may trigger events (grass, items, zones)
    /// </remarks>
    public bool IsSolid { get; set; }

    /// <summary>
    ///     Initializes a new instance of the Collision struct.
    /// </summary>
    /// <param name="isSolid">Whether the entity blocks movement.</param>
    public Collision(bool isSolid)
    {
        IsSolid = isSolid;
    }
}
