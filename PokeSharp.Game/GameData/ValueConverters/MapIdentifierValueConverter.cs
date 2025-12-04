using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PokeSharp.Game.Engine.Core.Types;

namespace PokeSharp.Game.Data.ValueConverters;

/// <summary>
///     EF Core value converter for MapIdentifier to string.
/// </summary>
public class MapIdentifierValueConverter : ValueConverter<MapIdentifier, string>
{
    public MapIdentifierValueConverter()
        : base(v => v.Value, v => new MapIdentifier(v)) { }
}
