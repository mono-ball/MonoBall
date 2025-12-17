using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameData.ValueConverters;

/// <summary>
///     EF Core value converter for GameAudioId to string.
/// </summary>
public class GameAudioIdValueConverter : ValueConverter<GameAudioId, string>
{
    public GameAudioIdValueConverter()
        : base(
            v => v.Value,
            v => ConvertFromString(v)
        )
    {
    }

    private static GameAudioId ConvertFromString(string value)
    {
        return GameAudioId.TryCreate(value) ?? new GameAudioId("music", value);
    }
}

/// <summary>
///     EF Core value converter for nullable GameAudioId to nullable string.
/// </summary>
public class NullableGameAudioIdValueConverter : ValueConverter<GameAudioId?, string?>
{
    public NullableGameAudioIdValueConverter()
        : base(
            v => v != null ? v.Value : null,
            v => ConvertFromString(v)
        )
    {
    }

    private static GameAudioId? ConvertFromString(string? value)
    {
        return value != null ? GameAudioId.TryCreate(value) : null;
    }
}
