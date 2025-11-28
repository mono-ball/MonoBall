using System;

namespace PokeSharp.Engine.UI.Debug.Animation;

/// <summary>
/// Reusable animator for smooth transitions between values.
/// Can be used for position, size, opacity, or any numeric property.
/// </summary>
public class Animator
{
    private float _currentValue;
    private float _targetValue;
    private float _speed;
    private bool _isAnimating;

    /// <summary>
    /// Gets the current animated value.
    /// </summary>
    public float CurrentValue => _currentValue;

    /// <summary>
    /// Gets the target value being animated towards.
    /// </summary>
    public float TargetValue => _targetValue;

    /// <summary>
    /// Gets whether the animator is currently animating.
    /// </summary>
    public bool IsAnimating => _isAnimating;

    /// <summary>
    /// Gets the animation speed (units per second).
    /// </summary>
    public float Speed
    {
        get => _speed;
        set => _speed = Math.Max(0, value);
    }

    /// <summary>
    /// Initializes a new animator.
    /// </summary>
    /// <param name="initialValue">Starting value.</param>
    /// <param name="speed">Animation speed (units per second).</param>
    public Animator(float initialValue = 0f, float speed = 5f)
    {
        _currentValue = initialValue;
        _targetValue = initialValue;
        _speed = speed;
        _isAnimating = false;
    }

    /// <summary>
    /// Sets a new target value and starts animating towards it.
    /// </summary>
    /// <param name="targetValue">The value to animate to.</param>
    public void AnimateTo(float targetValue)
    {
        _targetValue = targetValue;
        _isAnimating = Math.Abs(_currentValue - _targetValue) > 0.01f;
    }

    /// <summary>
    /// Immediately sets the value without animation.
    /// </summary>
    /// <param name="value">The value to set.</param>
    public void SetImmediate(float value)
    {
        _currentValue = value;
        _targetValue = value;
        _isAnimating = false;
    }

    /// <summary>
    /// Updates the animator, smoothly interpolating towards the target.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last update (in seconds).</param>
    public void Update(float deltaTime)
    {
        if (!_isAnimating)
            return;

        // Smooth interpolation using exponential easing
        float difference = _targetValue - _currentValue;
        float step = difference * _speed * deltaTime;

        // Stop animating if we're very close to the target
        if (Math.Abs(difference) < 0.01f)
        {
            _currentValue = _targetValue;
            _isAnimating = false;
        }
        else
        {
            _currentValue += step;
        }
    }

    /// <summary>
    /// Resets the animator to a specific value and stops animation.
    /// </summary>
    /// <param name="value">The value to reset to.</param>
    public void Reset(float value = 0f)
    {
        SetImmediate(value);
    }
}




