using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameData.ValueConverters;

/// <summary>
///     EF Core value converter for GameFontId to string.
/// </summary>
public class GameFontIdValueConverter : ValueConverter<GameFontId, string>
{
    public GameFontIdValueConverter()
        : base(
            v => v.Value,
            v => ConvertFromString(v)
        )
    {
    }

    private static GameFontId ConvertFromString(string value)
    {
        return GameFontId.TryCreate(value) ?? new GameFontId("game", value);
    }
}

/// <summary>
///     EF Core value converter for nullable GameFontId to nullable string.
/// </summary>
public class NullableGameFontIdValueConverter : ValueConverter<GameFontId?, string?>
{
    public NullableGameFontIdValueConverter()
        : base(
            v => v != null ? v.Value : null,
            v => ConvertFromString(v)
        )
    {
    }

    private static GameFontId? ConvertFromString(string? value)
    {
        return value != null ? GameFontId.TryCreate(value) : null;
    }
}
