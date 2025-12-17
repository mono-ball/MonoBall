using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameData.ValueConverters;

/// <summary>
///     EF Core value converter for GameThemeId to string.
///     Supports both new unified format and legacy format for backwards compatibility.
/// </summary>
public class GameThemeIdValueConverter : ValueConverter<GameThemeId, string>
{
    public GameThemeIdValueConverter()
        : base(
            v => v.Value,
            v => ConvertFromString(v)
        )
    {
    }

    private static GameThemeId ConvertFromString(string value)
    {
        return GameThemeId.TryCreate(value) ?? GameThemeId.Create(value);
    }
}

/// <summary>
///     EF Core value converter for nullable GameThemeId to nullable string.
/// </summary>
public class NullableGameThemeIdValueConverter : ValueConverter<GameThemeId?, string?>
{
    public NullableGameThemeIdValueConverter()
        : base(
            v => v != null ? v.Value : null,
            v => ConvertFromString(v)
        )
    {
    }

    private static GameThemeId? ConvertFromString(string? value)
    {
        return value != null ? GameThemeId.TryCreate(value) : null;
    }
}
