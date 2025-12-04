namespace PokeSharp.Game.Components.Maps;

/// <summary>
///     Region map section for Town Map highlighting (e.g., "MAPSEC_LITTLEROOT_TOWN").
/// </summary>
public struct RegionSection
{
    public string Value { get; set; }

    public RegionSection(string value)
    {
        Value = value;
    }
}
