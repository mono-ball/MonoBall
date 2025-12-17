using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameData.ValueConverters;

/// <summary>
///     EF Core value converter for non-nullable GameSpriteId to string.
///     Used for SpriteEntity primary key.
/// </summary>
public class GameSpriteIdNonNullableValueConverter : ValueConverter<GameSpriteId, string>
{
    public GameSpriteIdNonNullableValueConverter()
        : base(
            v => v.Value,
            v => GameSpriteId.TryCreate(v) ?? new GameSpriteId(v)
        )
    {
    }
}
