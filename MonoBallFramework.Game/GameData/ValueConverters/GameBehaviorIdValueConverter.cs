using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameData.ValueConverters;

/// <summary>
///     EF Core value converter for GameBehaviorId to string.
/// </summary>
public class GameBehaviorIdValueConverter : ValueConverter<GameBehaviorId, string>
{
    public GameBehaviorIdValueConverter()
        : base(
            v => v.Value,
            v => ConvertFromString(v)
        )
    {
    }

    private static GameBehaviorId ConvertFromString(string value)
    {
        return GameBehaviorId.TryCreate(value) ?? new GameBehaviorId("npc", value);
    }
}

/// <summary>
///     EF Core value converter for nullable GameBehaviorId to nullable string.
/// </summary>
public class NullableGameBehaviorIdValueConverter : ValueConverter<GameBehaviorId?, string?>
{
    public NullableGameBehaviorIdValueConverter()
        : base(
            v => v != null ? v.Value : null,
            v => ConvertFromString(v)
        )
    {
    }

    private static GameBehaviorId? ConvertFromString(string? value)
    {
        return value != null ? GameBehaviorId.TryCreate(value) : null;
    }
}
