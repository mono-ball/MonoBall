using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameData.ValueConverters;

/// <summary>
///     EF Core value converter for GamePopupBackgroundId to string.
/// </summary>
public class GamePopupBackgroundIdValueConverter : ValueConverter<GamePopupBackgroundId, string>
{
    public GamePopupBackgroundIdValueConverter()
        : base(
            v => v.Value,
            v => ConvertFromString(v)
        )
    {
    }

    private static GamePopupBackgroundId ConvertFromString(string value)
    {
        return GamePopupBackgroundId.TryCreate(value) ?? GamePopupBackgroundId.Create(value);
    }
}

/// <summary>
///     EF Core value converter for nullable GamePopupBackgroundId to nullable string.
/// </summary>
public class NullableGamePopupBackgroundIdValueConverter : ValueConverter<GamePopupBackgroundId?, string?>
{
    public NullableGamePopupBackgroundIdValueConverter()
        : base(
            v => v != null ? v.Value : null,
            v => ConvertFromString(v)
        )
    {
    }

    private static GamePopupBackgroundId? ConvertFromString(string? value)
    {
        return value != null ? GamePopupBackgroundId.TryCreate(value) : null;
    }
}
