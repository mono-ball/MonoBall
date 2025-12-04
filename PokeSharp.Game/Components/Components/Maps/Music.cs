namespace PokeSharp.Game.Components.Maps;

/// <summary>
///     Background music track ID for this map.
/// </summary>
public struct Music
{
    public string Value { get; set; }

    public Music(string value)
    {
        Value = value;
    }
}
