namespace PokeSharp.Game.Systems.Services;

/// <summary>
///     Default implementation of IGameTimeService.
///     Tracks game time for timestamps and time-based game logic.
///     Supports time scaling for debug purposes (slow-mo, pause, step).
/// </summary>
/// <remarks>
///     Thread safety: TimeScale uses volatile for safe cross-thread access.
///     The Update method should only be called from the main game thread.
/// </remarks>
public class GameTimeService : IGameTimeService
{
    // Use volatile for thread-safe reads/writes from console commands
    private volatile float _timeScale = 1.0f;
    private volatile float _previousTimeScale = 1.0f;

    /// <inheritdoc />
    public event Action<float>? OnTimeScaleChanged;

    /// <inheritdoc />
    public float TotalSeconds { get; private set; }

    /// <inheritdoc />
    public double TotalMilliseconds => TotalSeconds * 1000.0;

    /// <inheritdoc />
    public float DeltaTime { get; private set; }

    /// <inheritdoc />
    public float UnscaledDeltaTime { get; private set; }

    /// <inheritdoc />
    public float TimeScale
    {
        get => _timeScale;
        set
        {
            var oldValue = _timeScale;

            // Clamp to reasonable range (0 to 10x)
            var newValue = Math.Clamp(value, 0f, 10f);
            _timeScale = newValue;

            // If setting to non-zero, remember it for resume
            if (newValue > 0)
                _previousTimeScale = newValue;

            // Fire event if value actually changed
            if (Math.Abs(oldValue - newValue) > 0.0001f)
                OnTimeScaleChanged?.Invoke(newValue);
        }
    }

    /// <inheritdoc />
    public bool IsPaused => _timeScale == 0f;

    /// <inheritdoc />
    public int StepFrames { get; set; }

    /// <inheritdoc />
    /// <remarks>
    ///     Each call replaces any pending step count (does not accumulate).
    ///     For example, calling Step(5) then Step(3) results in 3 pending frames, not 8.
    /// </remarks>
    public void Step(int frames = 1)
    {
        StepFrames = Math.Max(1, frames);
    }

    /// <inheritdoc />
    public void Pause()
    {
        var oldValue = _timeScale;
        if (oldValue > 0)
            _previousTimeScale = oldValue;

        _timeScale = 0f;

        if (oldValue > 0)
            OnTimeScaleChanged?.Invoke(0f);
    }

    /// <inheritdoc />
    public void Resume()
    {
        var oldValue = _timeScale;
        var newValue = _previousTimeScale > 0 ? _previousTimeScale : 1.0f;

        _timeScale = newValue;
        StepFrames = 0;

        if (Math.Abs(oldValue - newValue) > 0.0001f)
            OnTimeScaleChanged?.Invoke(newValue);
    }

    /// <inheritdoc />
    public void Update(float totalSeconds, float deltaTime)
    {
        TotalSeconds = totalSeconds;
        UnscaledDeltaTime = deltaTime;

        // Handle step frames when paused
        if (IsPaused && StepFrames > 0)
        {
            // Allow one frame to pass at normal speed
            DeltaTime = deltaTime;
            StepFrames--;
        }
        else
        {
            // Apply time scale
            DeltaTime = deltaTime * _timeScale;
        }
    }
}
