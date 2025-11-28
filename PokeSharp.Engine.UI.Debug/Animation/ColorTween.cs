using Microsoft.Xna.Framework;

namespace PokeSharp.Engine.UI.Debug.Animation;

/// <summary>
///     Animates a Color value with easing functions.
///     Interpolates R, G, B, and A components independently.
/// </summary>
public class ColorTween
{
    private readonly EasingType _easing;
    private float _currentTime;
    private float _duration;
    private Color _endColor;
    private Color _startColor;

    public ColorTween(
        Color startColor,
        Color endColor,
        float duration,
        EasingType easing = EasingType.Linear
    )
    {
        _startColor = startColor;
        _endColor = endColor;
        _duration = Math.Max(0, duration);
        _easing = easing;
        CurrentColor = startColor;
        _currentTime = 0;
        IsPlaying = false;
        IsComplete = false;
    }

    /// <summary>
    ///     Gets the current animated color.
    /// </summary>
    public Color CurrentColor { get; private set; }

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
    public float NormalizedTime =>
        _duration > 0 ? Math.Clamp(_currentTime / _duration, 0f, 1f) : 1f;

    /// <summary>
    ///     Event fired when the tween completes.
    /// </summary>
    public event Action? OnComplete;

    public void Play()
    {
        _currentTime = 0;
        IsPlaying = true;
        IsComplete = false;
        CurrentColor = _startColor;
    }

    public void Pause()
    {
        IsPlaying = false;
    }

    public void Resume()
    {
        if (!IsComplete)
        {
            IsPlaying = true;
        }
    }

    public void Stop()
    {
        IsPlaying = false;
        IsComplete = false;
        _currentTime = 0;
        CurrentColor = _startColor;
    }

    public void Complete()
    {
        _currentTime = _duration;
        CurrentColor = _endColor;
        IsPlaying = false;
        IsComplete = true;
        OnComplete?.Invoke();
    }

    public void Update(float deltaTime)
    {
        if (!IsPlaying || IsComplete)
        {
            return;
        }

        _currentTime += deltaTime;

        if (_currentTime >= _duration)
        {
            _currentTime = _duration;
            CurrentColor = _endColor;
            IsPlaying = false;
            IsComplete = true;
            OnComplete?.Invoke();
        }
        else
        {
            // Interpolate each color component using easing function
            float t = NormalizedTime;
            float easedT = EasingFunctions.Ease(t, _easing);

            byte r = (byte)MathHelper.Lerp(_startColor.R, _endColor.R, easedT);
            byte g = (byte)MathHelper.Lerp(_startColor.G, _endColor.G, easedT);
            byte b = (byte)MathHelper.Lerp(_startColor.B, _endColor.B, easedT);
            byte a = (byte)MathHelper.Lerp(_startColor.A, _endColor.A, easedT);

            CurrentColor = new Color(r, g, b, a);
        }
    }

    public void SetTarget(Color newEndColor, float? newDuration = null)
    {
        _startColor = CurrentColor;
        _endColor = newEndColor;

        if (newDuration.HasValue)
        {
            _duration = Math.Max(0, newDuration.Value);
        }

        _currentTime = 0;
        IsPlaying = true;
        IsComplete = false;
    }

    public static ColorTween FadeIn(
        Color color,
        float duration,
        EasingType easing = EasingType.QuadOut
    )
    {
        var transparent = new Color(color.R, color.G, color.B, (byte)0);
        var tween = new ColorTween(transparent, color, duration, easing);
        tween.Play();
        return tween;
    }

    public static ColorTween FadeOut(
        Color color,
        float duration,
        EasingType easing = EasingType.QuadIn
    )
    {
        var transparent = new Color(color.R, color.G, color.B, (byte)0);
        var tween = new ColorTween(color, transparent, duration, easing);
        tween.Play();
        return tween;
    }
}
