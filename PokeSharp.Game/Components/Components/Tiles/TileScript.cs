namespace PokeSharp.Game.Components.Tiles;

/// <summary>
///     Scripted tile behavior component.
///     References a Roslyn C# script file that executes when the player interacts with the tile.
/// </summary>
/// <remarks>
///     Used for:
///     - Healing tiles (Pokemon Center)
///     - PC access tiles
///     - Warp tiles
///     - Puzzle tiles
///     - Custom scripted interactions
/// </remarks>
public struct TileScript
{
    /// <summary>
    ///     Gets or sets the path to the C# script file (.csx).
    /// </summary>
    /// <remarks>
    ///     Path is relative to the scripts directory.
    ///     Example: "triggers/heal_tile.csx", "warps/pallet_to_viridian.csx"
    /// </remarks>
    public string ScriptPath { get; set; }

    /// <summary>
    ///     Initializes a new instance of the TileScript struct.
    /// </summary>
    /// <param name="scriptPath">Path to the script file.</param>
    public TileScript(string scriptPath)
    {
        ScriptPath = scriptPath;
    }
}
