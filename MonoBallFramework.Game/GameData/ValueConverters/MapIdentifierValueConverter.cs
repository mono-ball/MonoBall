using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameData.ValueConverters;

/// <summary>
///     EF Core value converter for MapIdentifier to string.
/// </summary>
public class MapIdentifierValueConverter : ValueConverter<MapIdentifier, string>
{
    public MapIdentifierValueConverter()
        : base(v => v.Value, v => new MapIdentifier(v)) { }
}
