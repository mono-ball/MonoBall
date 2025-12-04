namespace PokeSharp.Game.Components.Maps;

/// <summary>
///     Marks a tile that can trigger wild Pokemon encounters.
///     Typically attached to grass, water, and cave tiles.
/// </summary>
public struct EncounterZone
{
    /// <summary>
    ///     Gets or sets the encounter table identifier.
    ///     References a data file defining which Pokemon can appear.
    /// </summary>
    public string EncounterTableId { get; set; }

    /// <summary>
    ///     Gets or sets the encounter rate (0-255, Pokemon standard).
    /// </summary>
    /// <remarks>
    ///     In Pokemon games: 0 = no encounters, 255 = very frequent encounters.
    ///     Typical values: grass = 20-30, water = 10-15, cave = 10-20.
    /// </remarks>
    public int EncounterRate { get; set; }

    /// <summary>
    ///     Initializes a new instance of the EncounterZone struct.
    /// </summary>
    /// <param name="encounterTableId">Encounter table identifier.</param>
    /// <param name="encounterRate">Encounter rate (0-255).</param>
    public EncounterZone(string encounterTableId, int encounterRate)
    {
        EncounterTableId = encounterTableId;
        EncounterRate = encounterRate;
    }
}
