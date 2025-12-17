using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameData.ValueConverters;

/// <summary>
///     EF Core value converter for GameTileBehaviorId to string.
/// </summary>
public class GameTileBehaviorIdValueConverter : ValueConverter<GameTileBehaviorId, string>
{
    public GameTileBehaviorIdValueConverter()
        : base(
            v => v.Value,
            v => ConvertFromString(v)
        )
    {
    }

    private static GameTileBehaviorId ConvertFromString(string value)
    {
        return GameTileBehaviorId.TryCreate(value) ?? GameTileBehaviorId.CreateMovement(value);
    }
}

/// <summary>
///     EF Core value converter for nullable GameTileBehaviorId to nullable string.
/// </summary>
public class NullableGameTileBehaviorIdValueConverter : ValueConverter<GameTileBehaviorId?, string?>
{
    public NullableGameTileBehaviorIdValueConverter()
        : base(
            v => v != null ? v.Value : null,
            v => ConvertFromString(v)
        )
    {
    }

    private static GameTileBehaviorId? ConvertFromString(string? value)
    {
        return value != null ? GameTileBehaviorId.TryCreate(value) : null;
    }
}
