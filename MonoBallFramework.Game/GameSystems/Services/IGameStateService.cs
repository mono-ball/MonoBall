namespace MonoBallFramework.Game.GameSystems.Services;

/// <summary>
///     Engine-level service for managing global game state.
///     Provides centralized access to service enable/disable flags and runtime state.
/// </summary>
/// <remarks>
///     This is the engine-level service. For script access, use IGameStateApi
///     which delegates to this service.
/// </remarks>
public interface IGameStateService
{
    /// <summary>
    ///     Gets or sets whether the collision service is enabled.
    ///     When false, all collision checks return walkable.
    /// </summary>
    /// <remarks>
    ///     Default is true. Set to false for debug/cheat mode (walk through walls).
    /// </remarks>
    bool CollisionServiceEnabled { get; set; }
}
