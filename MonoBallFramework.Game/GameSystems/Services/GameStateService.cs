using Microsoft.Extensions.Logging;

namespace MonoBallFramework.Game.GameSystems.Services;

/// <summary>
///     Engine-level service for managing global game state.
/// </summary>
public class GameStateService : IGameStateService
{
    private readonly ILogger<GameStateService>? _logger;
    private bool _collisionServiceEnabled = true;

    public GameStateService(ILogger<GameStateService>? logger = null)
    {
        _logger = logger;
        _logger?.LogInformation("GameStateService created");
    }

    /// <inheritdoc />
    public bool CollisionServiceEnabled
    {
        get => _collisionServiceEnabled;
        set => _collisionServiceEnabled = value;
    }
}
