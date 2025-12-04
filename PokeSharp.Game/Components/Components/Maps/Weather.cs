namespace PokeSharp.Game.Components.Maps;

/// <summary>
///     Default weather for this map (e.g., "clear", "rain", "sandstorm", "snow").
/// </summary>
public struct Weather
{
    public string Value { get; set; }

    public Weather(string value)
    {
        Value = value;
    }
}
