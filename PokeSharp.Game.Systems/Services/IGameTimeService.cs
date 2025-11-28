using PokeSharp.Engine.Core.Services;

namespace PokeSharp.Game.Systems.Services;

/// <summary>
///     Provides access to game time information.
///     Tracks elapsed time since game start for timestamps and time-based logic.
///     Supports time scaling for debug purposes (slow-mo, pause, step).
/// </summary>
/// <remarks>
///     Extends <see cref="ITimeControl"/> to provide the standard time control interface
///     that can be used by engine-level debug tools without layer violations.
/// </remarks>
public interface IGameTimeService : ITimeControl
{
    /// <summary>
    ///     Gets the total elapsed game time in seconds since game start.
    /// </summary>
    float TotalSeconds { get; }

    /// <summary>
    ///     Gets the total elapsed game time in milliseconds since game start.
    /// </summary>
    double TotalMilliseconds { get; }

    /// <summary>
    ///     Gets the time since the last frame in seconds (delta time).
    ///     This value is scaled by <see cref="ITimeControl.TimeScale"/>.
    /// </summary>
    float DeltaTime { get; }

    /// <summary>
    ///     Gets the unscaled time since the last frame in seconds.
    ///     Not affected by <see cref="ITimeControl.TimeScale"/> - useful for UI animations.
    /// </summary>
    float UnscaledDeltaTime { get; }

    /// <summary>
    ///     Gets or sets the number of frames to step through when paused.
    ///     When > 0 and paused, the game will advance this many frames then re-pause.
    /// </summary>
    int StepFrames { get; set; }

    /// <summary>
    ///     Updates the game time. Should be called once per frame.
    /// </summary>
    /// <param name="totalSeconds">Total elapsed time since game start in seconds.</param>
    /// <param name="deltaTime">Time since last frame in seconds.</param>
    void Update(float totalSeconds, float deltaTime);

    // Inherited from ITimeControl:
    // - float TimeScale { get; set; }
    // - bool IsPaused { get; }
    // - void Step(int frames = 1);
    // - void Pause();
    // - void Resume();
    // - event Action<float>? OnTimeScaleChanged;
}
