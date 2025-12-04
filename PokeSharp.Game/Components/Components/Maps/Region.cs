namespace PokeSharp.Game.Components.Maps;

/// <summary>
///     Region this map belongs to (e.g., "hoenn", "kanto", "johto").
/// </summary>
public struct Region
{
    public string Value { get; set; }

    public Region(string value)
    {
        Value = value;
    }
}
