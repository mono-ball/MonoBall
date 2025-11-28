namespace PokeSharp.Engine.UI.Debug.Animation;

/// <summary>
///     Reusable animator for smooth transitions between values.
///     Can be used for position, size, opacity, or any numeric property.
/// </summary>
public class Animator
{
    private float _speed;

    /// <summary>
    ///     Initializes a new animator.
    /// </summary>
    /// <param name="initialValue">Starting value.</param>
    /// <param name="speed">Animation speed (units per second).</param>
    public Animator(float initialValue = 0f, float speed = 5f)
    {
        CurrentValue = initialValue;
        TargetValue = initialValue;
        _speed = speed;
        IsAnimating = false;
    }

    /// <summary>
    ///     Gets the current animated value.
    /// </summary>
    public float CurrentValue { get; private set; }

    /// <summary>
    ///     Gets the target value being animated towards.
    /// </summary>
    public float TargetValue { get; private set; }

    /// <summary>
    ///     Gets whether the animator is currently animating.
    /// </summary>
    public bool IsAnimating { get; private set; }

    /// <summary>
    ///     Gets the animation speed (units per second).
    /// </summary>
    public float Speed
    {
        get => _speed;
        set => _speed = Math.Max(0, value);
    }

    /// <summary>
    ///     Sets a new target value and starts animating towards it.
    /// </summary>
    /// <param name="targetValue">The value to animate to.</param>
    public void AnimateTo(float targetValue)
    {
        TargetValue = targetValue;
        IsAnimating = Math.Abs(CurrentValue - TargetValue) > 0.01f;
    }

    /// <summary>
    ///     Immediately sets the value without animation.
    /// </summary>
    /// <param name="value">The value to set.</param>
    public void SetImmediate(float value)
    {
        CurrentValue = value;
        TargetValue = value;
        IsAnimating = false;
    }

    /// <summary>
    ///     Updates the animator, smoothly interpolating towards the target.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last update (in seconds).</param>
    public void Update(float deltaTime)
    {
        if (!IsAnimating)
        {
            return;
        }

        // Smooth interpolation using exponential easing
        float difference = TargetValue - CurrentValue;
        float step = difference * _speed * deltaTime;

        // Stop animating if we're very close to the target
        if (Math.Abs(difference) < 0.01f)
        {
            CurrentValue = TargetValue;
            IsAnimating = false;
        }
        else
        {
            CurrentValue += step;
        }
    }

    /// <summary>
    ///     Resets the animator to a specific value and stops animation.
    /// </summary>
    /// <param name="value">The value to reset to.</param>
    public void Reset(float value = 0f)
    {
        SetImmediate(value);
    }
}
