namespace PokeSharp.Game.Components.Tiles;

/// <summary>
///     Visual and audio terrain properties for tiles.
///     Used for footstep sounds, particle effects, and terrain-specific logic.
/// </summary>
public struct TerrainType
{
    /// <summary>
    ///     Gets or sets the terrain type identifier.
    /// </summary>
    /// <remarks>
    ///     Common values: "grass", "water", "sand", "cave", "snow", "ice"
    /// </remarks>
    public string TypeId { get; set; }

    /// <summary>
    ///     Gets or sets the footstep sound effect identifier.
    /// </summary>
    /// <remarks>
    ///     References audio assets for character footstep sounds.
    ///     Example: "footstep_grass", "footstep_sand", "footstep_cave"
    /// </remarks>
    public string FootstepSound { get; set; }

    /// <summary>
    ///     Initializes a new instance of the TerrainType struct.
    /// </summary>
    /// <param name="typeId">Terrain type identifier.</param>
    /// <param name="footstepSound">Footstep sound identifier.</param>
    public TerrainType(string typeId, string footstepSound)
    {
        TypeId = typeId;
        FootstepSound = footstepSound;
    }
}
