using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameData.ValueConverters;

/// <summary>
///     EF Core value converter for GameTrainerId to string.
///     Supports both new unified format and legacy format for backwards compatibility.
/// </summary>
public class GameTrainerIdValueConverter : ValueConverter<GameTrainerId, string>
{
    public GameTrainerIdValueConverter()
        : base(
            v => v.Value,
            v => ConvertFromString(v)
        )
    {
    }

    private static GameTrainerId ConvertFromString(string value)
    {
        return GameTrainerId.TryCreate(value) ?? GameTrainerId.Create(value);
    }
}

/// <summary>
///     EF Core value converter for nullable GameTrainerId to nullable string.
/// </summary>
public class NullableGameTrainerIdValueConverter : ValueConverter<GameTrainerId?, string?>
{
    public NullableGameTrainerIdValueConverter()
        : base(
            v => v != null ? v.Value : null,
            v => ConvertFromString(v)
        )
    {
    }

    private static GameTrainerId? ConvertFromString(string? value)
    {
        return value != null ? GameTrainerId.TryCreate(value) : null;
    }
}
