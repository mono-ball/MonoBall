namespace PokeSharp.Game.Components.Maps;

/// <summary>
///     Map type for categorization (e.g., "town", "route", "cave", "building").
/// </summary>
public struct MapType
{
    public string Value { get; set; }

    public MapType(string value)
    {
        Value = value;
    }
}
