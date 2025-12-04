namespace PokeSharp.Game.Components.Maps;

/// <summary>
///     Display name shown in-game (e.g., "Littleroot Town", "Route 101").
/// </summary>
public struct DisplayName
{
    public string Value { get; set; }

    public DisplayName(string value)
    {
        Value = value;
    }
}
