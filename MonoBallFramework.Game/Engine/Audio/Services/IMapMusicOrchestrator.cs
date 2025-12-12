namespace MonoBallFramework.Game.Engine.Audio.Services;

/// <summary>
///     Interface for the map music orchestrator service.
///     Manages background music playback based on map transitions and events.
/// </summary>
/// <remarks>
///     This interface has no public methods because the orchestrator works entirely
///     via event subscriptions (MapTransitionEvent, MapRenderReadyEvent).
///     The interface exists for:
///     - Dependency injection registration and lifecycle management
///     - Testability (can be mocked for unit tests)
///     - Service discovery and documentation
/// </remarks>
public interface IMapMusicOrchestrator : IDisposable
{
    // No public methods - orchestrator operates autonomously via event subscriptions
}
