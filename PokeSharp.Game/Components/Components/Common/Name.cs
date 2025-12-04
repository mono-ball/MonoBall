namespace PokeSharp.Game.Components.Common;

/// <summary>
///     Component storing the primary display name for an entity.
///     Useful for players, NPCs, trainers, and any interactable characters.
/// </summary>
public struct Name
{
    /// <summary>
    ///     The display name shown to players (e.g., "ASH", "NURSE JOY").
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Name" /> struct.
    /// </summary>
    /// <param name="displayName">
    ///     The name to display in UI; defaults to an empty string when not provided.
    /// </param>
    public Name(string displayName)
    {
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName;
    }
}
