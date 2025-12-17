using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameData.ValueConverters;

/// <summary>
///     EF Core value converter for GameMapSectionId to string.
///     Supports both new unified format and legacy format for backwards compatibility.
/// </summary>
public class GameMapSectionIdValueConverter : ValueConverter<GameMapSectionId, string>
{
    public GameMapSectionIdValueConverter()
        : base(
            v => v.Value,
            v => ConvertFromString(v)
        )
    {
    }

    private static GameMapSectionId ConvertFromString(string value)
    {
        return GameMapSectionId.TryCreate(value) ?? GameMapSectionId.Create(value);
    }
}

/// <summary>
///     EF Core value converter for nullable GameMapSectionId to nullable string.
/// </summary>
public class NullableGameMapSectionIdValueConverter : ValueConverter<GameMapSectionId?, string?>
{
    public NullableGameMapSectionIdValueConverter()
        : base(
            v => v != null ? v.Value : null,
            v => ConvertFromString(v)
        )
    {
    }

    private static GameMapSectionId? ConvertFromString(string? value)
    {
        return value != null ? GameMapSectionId.TryCreate(value) : null;
    }
}
