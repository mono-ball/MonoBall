namespace PokeSharp.Engine.UI.Debug.Animation;

/// <summary>
///     Advanced animator with easing functions and configurable duration.
///     Supports timed animations with various easing curves.
/// </summary>
public class Tween
{
    private float _currentTime;

    /// <summary>
    ///     Creates a new tween.
    /// </summary>
    public Tween(
        float startValue,
        float endValue,
        float duration,
        EasingType easing = EasingType.Linear
    )
    {
        StartValue = startValue;
        EndValue = endValue;
        Duration = Math.Max(0, duration);
        Easing = easing;
        CurrentValue = startValue;
        _currentTime = 0;
        IsPlaying = false;
        IsComplete = false;
    }

    /// <summary>
    ///     Gets the current animated value.
    /// </summary>
    public float CurrentValue { get; private set; }

    /// <summary>
    ///     Gets the start value of the animation.
    /// </summary>
    public float StartValue { get; private set; }

    /// <summary>
    ///     Gets the end/target value of the animation.
    /// </summary>
    public float EndValue { get; private set; }

    /// <summary>
    ///     Gets the animation duration in seconds.
    /// </summary>
    public float Duration { get; private set; }

    /// <summary>
    ///     Gets the easing function type.
    /// </summary>
    public EasingType Easing { get; private set; }

    /// <summary>
    ///     Gets whether the tween is currently playing.
    /// </summary>
    public bool IsPlaying { get; private set; }

    /// <summary>
    ///     Gets whether the tween has completed.
    /// </summary>
    public bool IsComplete { get; private set; }

    /// <summary>
    ///     Gets the normalized time (0-1) of the animation.
    /// </summary>
    public float NormalizedTime => Duration > 0 ? Math.Clamp(_currentTime / Duration, 0f, 1f) : 1f;

    /// <summary>
    ///     Event fired when the tween completes.
    /// </summary>
    public event Action? OnComplete;

    /// <summary>
    ///     Starts or restarts the tween.
    /// </summary>
    public void Play()
    {
        _currentTime = 0;
        IsPlaying = true;
        IsComplete = false;
        CurrentValue = StartValue;
    }

    /// <summary>
    ///     Pauses the tween.
    /// </summary>
    public void Pause()
    {
        IsPlaying = false;
    }

    /// <summary>
    ///     Resumes the tween from its current position.
    /// </summary>
    public void Resume()
    {
        if (!IsComplete)
        {
            IsPlaying = true;
        }
    }

    /// <summary>
    ///     Stops the tween and resets to start value.
    /// </summary>
    public void Stop()
    {
        IsPlaying = false;
        IsComplete = false;
        _currentTime = 0;
        CurrentValue = StartValue;
    }

    /// <summary>
    ///     Immediately completes the tween, jumping to the end value.
    /// </summary>
    public void Complete()
    {
        _currentTime = Duration;
        CurrentValue = EndValue;
        IsPlaying = false;
        IsComplete = true;

        try
        {
            OnComplete?.Invoke();
        }
        catch
        {
            // Swallow exceptions from user callbacks to prevent crashing the animation system
        }
    }

    /// <summary>
    ///     Updates the tween animation.
    /// </summary>
    public void Update(float deltaTime)
    {
        if (!IsPlaying || IsComplete)
        {
            return;
        }

        _currentTime += deltaTime;

        if (_currentTime >= Duration)
        {
            // Animation complete
            _currentTime = Duration;
            CurrentValue = EndValue;
            IsPlaying = false;
            IsComplete = true;

            try
            {
                OnComplete?.Invoke();
            }
            catch
            {
                // Swallow exceptions from user callbacks to prevent crashing the animation system
            }
        }
        else
        {
            // Interpolate using easing function
            float t = NormalizedTime;
            CurrentValue = EasingFunctions.Lerp(StartValue, EndValue, t, Easing);
        }
    }

    /// <summary>
    ///     Changes the target value, creating a new animation from current value to new target.
    /// </summary>
    public void SetTarget(float newEndValue, float? newDuration = null)
    {
        StartValue = CurrentValue;
        EndValue = newEndValue;

        if (newDuration.HasValue)
        {
            Duration = Math.Max(0, newDuration.Value);
        }

        _currentTime = 0;
        IsPlaying = true;
        IsComplete = false;
    }

    /// <summary>
    ///     Changes the easing function.
    /// </summary>
    public void SetEasing(EasingType easing)
    {
        Easing = easing;
    }

    /// <summary>
    ///     Factory method to create and immediately start a tween.
    /// </summary>
    public static Tween To(
        float from,
        float to,
        float duration,
        EasingType easing = EasingType.Linear
    )
    {
        var tween = new Tween(from, to, duration, easing);
        tween.Play();
        return tween;
    }
}
