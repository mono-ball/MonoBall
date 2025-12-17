using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameData.ValueConverters;

/// <summary>
///     EF Core value converter for GameMapId to string.
///     Supports both new unified format and legacy format for backwards compatibility.
/// </summary>
public class GameMapIdValueConverter : ValueConverter<GameMapId, string>
{
    public GameMapIdValueConverter()
        : base(
            v => v.Value,
            v => ConvertFromString(v)
        )
    {
    }

    private static GameMapId ConvertFromString(string value)
    {
        return GameMapId.TryCreate(value) ?? new GameMapId("hoenn", value);
    }
}

/// <summary>
///     EF Core value converter for nullable GameMapId to nullable string.
/// </summary>
public class NullableGameMapIdValueConverter : ValueConverter<GameMapId?, string?>
{
    public NullableGameMapIdValueConverter()
        : base(
            v => v != null ? v.Value : null,
            v => ConvertFromString(v)
        )
    {
    }

    private static GameMapId? ConvertFromString(string? value)
    {
        return value != null ? GameMapId.TryCreate(value) : null;
    }
}
