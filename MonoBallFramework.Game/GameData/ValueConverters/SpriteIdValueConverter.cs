using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameData.ValueConverters;

/// <summary>
///     EF Core value converter for SpriteId to string.
/// </summary>
public class SpriteIdValueConverter : ValueConverter<SpriteId?, string?>
{
    public SpriteIdValueConverter()
        : base(
            v => v.HasValue ? v.Value.Value : null!,
            v => v != null ? new SpriteId(v) : default(SpriteId?)
        ) { }
}
