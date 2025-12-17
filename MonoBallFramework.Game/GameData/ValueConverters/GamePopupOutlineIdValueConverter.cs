using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameData.ValueConverters;

/// <summary>
///     EF Core value converter for GamePopupOutlineId to string.
/// </summary>
public class GamePopupOutlineIdValueConverter : ValueConverter<GamePopupOutlineId, string>
{
    public GamePopupOutlineIdValueConverter()
        : base(
            v => v.Value,
            v => ConvertFromString(v)
        )
    {
    }

    private static GamePopupOutlineId ConvertFromString(string value)
    {
        return GamePopupOutlineId.TryCreate(value) ?? GamePopupOutlineId.Create(value);
    }
}

/// <summary>
///     EF Core value converter for nullable GamePopupOutlineId to nullable string.
/// </summary>
public class NullableGamePopupOutlineIdValueConverter : ValueConverter<GamePopupOutlineId?, string?>
{
    public NullableGamePopupOutlineIdValueConverter()
        : base(
            v => v != null ? v.Value : null,
            v => ConvertFromString(v)
        )
    {
    }

    private static GamePopupOutlineId? ConvertFromString(string? value)
    {
        return value != null ? GamePopupOutlineId.TryCreate(value) : null;
    }
}
