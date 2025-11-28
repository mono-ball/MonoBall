namespace PokeSharp.Engine.Core.Services;

/// <summary>
///     Provides time control operations for debugging and development.
///     Allows pausing, stepping through frames, and time scaling.
/// </summary>
/// <remarks>
///     <para>
///         This interface is defined in the engine layer so it can be used by debug tools
///         without creating circular dependencies with game-specific code.
///     </para>
///     <para>
///         Game implementations should extend this interface (e.g., IGameTimeService : ITimeControl)
///         to add game-specific timing properties like DeltaTime and TotalSeconds.
///     </para>
/// </remarks>
public interface ITimeControl
{
    /// <summary>
    ///     Gets or sets the time scale multiplier.
    ///     1.0 = normal speed, 0.5 = half speed, 2.0 = double speed, 0 = paused.
    ///     Valid range: 0.0 to 10.0
    /// </summary>
    float TimeScale { get; set; }

    /// <summary>
    ///     Gets whether time is currently paused (TimeScale == 0).
    /// </summary>
    bool IsPaused { get; }

    /// <summary>
    ///     Pauses time by setting TimeScale to 0.
    ///     The previous time scale is remembered for Resume().
    /// </summary>
    void Pause();

    /// <summary>
    ///     Resumes time at the previous time scale (or 1.0 if none).
    /// </summary>
    void Resume();

    /// <summary>
    ///     Steps forward by the specified number of frames when paused.
    ///     Each call replaces any pending step count (does not accumulate).
    ///     Has no effect if not paused.
    /// </summary>
    /// <param name="frames">Number of frames to advance (minimum: 1).</param>
    void Step(int frames = 1);

    /// <summary>
    ///     Raised when the time scale changes (including pause/resume).
    ///     The parameter is the new time scale value.
    /// </summary>
    event Action<float>? OnTimeScaleChanged;
}
