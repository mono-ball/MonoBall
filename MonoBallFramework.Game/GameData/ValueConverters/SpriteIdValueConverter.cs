using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameData.ValueConverters;

/// <summary>
///     EF Core value converter for GameSpriteId to string.
/// </summary>
public class GameSpriteIdValueConverter : ValueConverter<GameSpriteId?, string?>
{
    public GameSpriteIdValueConverter()
        : base(
            v => v != null ? v.Value : null,
            v => v != null ? GameSpriteId.TryCreate(v) : null
        )
    {
    }
}
