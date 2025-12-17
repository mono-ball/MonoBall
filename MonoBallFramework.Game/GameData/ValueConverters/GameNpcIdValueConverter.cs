using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameData.ValueConverters;

/// <summary>
///     EF Core value converter for GameNpcId to string.
///     Supports both new unified format and legacy format for backwards compatibility.
/// </summary>
public class GameNpcIdValueConverter : ValueConverter<GameNpcId, string>
{
    public GameNpcIdValueConverter()
        : base(
            v => v.Value,
            v => ConvertFromString(v)
        )
    {
    }

    private static GameNpcId ConvertFromString(string value)
    {
        return GameNpcId.TryCreate(value) ?? GameNpcId.Create(value);
    }
}

/// <summary>
///     EF Core value converter for nullable GameNpcId to nullable string.
/// </summary>
public class NullableGameNpcIdValueConverter : ValueConverter<GameNpcId?, string?>
{
    public NullableGameNpcIdValueConverter()
        : base(
            v => v != null ? v.Value : null,
            v => ConvertFromString(v)
        )
    {
    }

    private static GameNpcId? ConvertFromString(string? value)
    {
        return value != null ? GameNpcId.TryCreate(value) : null;
    }
}
