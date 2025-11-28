namespace PokeSharp.Engine.UI.Debug.Animation;

/// <summary>
///     Easing function types for animations.
/// </summary>
public enum EasingType
{
    Linear,

    // Quadratic
    QuadIn,
    QuadOut,
    QuadInOut,

    // Cubic
    CubicIn,
    CubicOut,
    CubicInOut,

    // Quartic
    QuartIn,
    QuartOut,
    QuartInOut,

    // Exponential
    ExpoIn,
    ExpoOut,
    ExpoInOut,

    // Circular
    CircIn,
    CircOut,
    CircInOut,

    // Back (overshoots)
    BackIn,
    BackOut,
    BackInOut,

    // Elastic (spring-like)
    ElasticIn,
    ElasticOut,
    ElasticInOut,

    // Bounce
    BounceIn,
    BounceOut,
    BounceInOut,
}

/// <summary>
///     Collection of easing functions for smooth animations.
///     All functions take a normalized time (0-1) and return a normalized value (typically 0-1, but can overshoot).
/// </summary>
public static class EasingFunctions
{
    private const float Pi = MathF.PI;
    private const float HalfPi = Pi / 2f;

    /// <summary>
    ///     Applies the specified easing function to a normalized time value (0-1).
    /// </summary>
    public static float Ease(float t, EasingType easing)
    {
        return easing switch
        {
            EasingType.Linear => Linear(t),
            EasingType.QuadIn => QuadIn(t),
            EasingType.QuadOut => QuadOut(t),
            EasingType.QuadInOut => QuadInOut(t),
            EasingType.CubicIn => CubicIn(t),
            EasingType.CubicOut => CubicOut(t),
            EasingType.CubicInOut => CubicInOut(t),
            EasingType.QuartIn => QuartIn(t),
            EasingType.QuartOut => QuartOut(t),
            EasingType.QuartInOut => QuartInOut(t),
            EasingType.ExpoIn => ExpoIn(t),
            EasingType.ExpoOut => ExpoOut(t),
            EasingType.ExpoInOut => ExpoInOut(t),
            EasingType.CircIn => CircIn(t),
            EasingType.CircOut => CircOut(t),
            EasingType.CircInOut => CircInOut(t),
            EasingType.BackIn => BackIn(t),
            EasingType.BackOut => BackOut(t),
            EasingType.BackInOut => BackInOut(t),
            EasingType.ElasticIn => ElasticIn(t),
            EasingType.ElasticOut => ElasticOut(t),
            EasingType.ElasticInOut => ElasticInOut(t),
            EasingType.BounceIn => BounceIn(t),
            EasingType.BounceOut => BounceOut(t),
            EasingType.BounceInOut => BounceInOut(t),
            _ => Linear(t),
        };
    }

    #region Linear

    public static float Linear(float t)
    {
        return t;
    }

    #endregion

    /// <summary>
    ///     Interpolates between two values using the specified easing function.
    /// </summary>
    public static float Lerp(float from, float to, float t, EasingType easing = EasingType.Linear)
    {
        float easedT = Ease(t, easing);
        return from + ((to - from) * easedT);
    }

    #region Quadratic

    public static float QuadIn(float t)
    {
        return t * t;
    }

    public static float QuadOut(float t)
    {
        return 1f - ((1f - t) * (1f - t));
    }

    public static float QuadInOut(float t)
    {
        return t < 0.5f ? 2f * t * t : 1f - (MathF.Pow((-2f * t) + 2f, 2f) / 2f);
    }

    #endregion

    #region Cubic

    public static float CubicIn(float t)
    {
        return t * t * t;
    }

    public static float CubicOut(float t)
    {
        return 1f - MathF.Pow(1f - t, 3f);
    }

    public static float CubicInOut(float t)
    {
        return t < 0.5f ? 4f * t * t * t : 1f - (MathF.Pow((-2f * t) + 2f, 3f) / 2f);
    }

    #endregion

    #region Quartic

    public static float QuartIn(float t)
    {
        return t * t * t * t;
    }

    public static float QuartOut(float t)
    {
        return 1f - MathF.Pow(1f - t, 4f);
    }

    public static float QuartInOut(float t)
    {
        return t < 0.5f ? 8f * t * t * t * t : 1f - (MathF.Pow((-2f * t) + 2f, 4f) / 2f);
    }

    #endregion

    #region Exponential

    public static float ExpoIn(float t)
    {
        return t == 0f ? 0f : MathF.Pow(2f, (10f * t) - 10f);
    }

    public static float ExpoOut(float t)
    {
        return t == 1f ? 1f : 1f - MathF.Pow(2f, -10f * t);
    }

    public static float ExpoInOut(float t)
    {
        return t == 0f ? 0f
            : t == 1f ? 1f
            : t < 0.5f ? MathF.Pow(2f, (20f * t) - 10f) / 2f
            : (2f - MathF.Pow(2f, (-20f * t) + 10f)) / 2f;
    }

    #endregion

    #region Circular

    public static float CircIn(float t)
    {
        return 1f - MathF.Sqrt(1f - (t * t));
    }

    public static float CircOut(float t)
    {
        return MathF.Sqrt(1f - MathF.Pow(t - 1f, 2f));
    }

    public static float CircInOut(float t)
    {
        return t < 0.5f
            ? (1f - MathF.Sqrt(1f - MathF.Pow(2f * t, 2f))) / 2f
            : (MathF.Sqrt(1f - MathF.Pow((-2f * t) + 2f, 2f)) + 1f) / 2f;
    }

    #endregion

    #region Back (Overshoot)

    private const float BackC1 = 1.70158f;
    private const float BackC2 = BackC1 * 1.525f;
    private const float BackC3 = BackC1 + 1f;

    public static float BackIn(float t)
    {
        return (BackC3 * t * t * t) - (BackC1 * t * t);
    }

    public static float BackOut(float t)
    {
        return 1f + (BackC3 * MathF.Pow(t - 1f, 3f)) + (BackC1 * MathF.Pow(t - 1f, 2f));
    }

    public static float BackInOut(float t)
    {
        return t < 0.5f
            ? MathF.Pow(2f * t, 2f) * (((BackC2 + 1f) * 2f * t) - BackC2) / 2f
            : ((MathF.Pow((2f * t) - 2f, 2f) * (((BackC2 + 1f) * ((t * 2f) - 2f)) + BackC2)) + 2f)
                / 2f;
    }

    #endregion

    #region Elastic (Spring)

    private const float ElasticC4 = 2f * Pi / 3f;
    private const float ElasticC5 = 2f * Pi / 4.5f;

    public static float ElasticIn(float t)
    {
        return t == 0f ? 0f
            : t == 1f ? 1f
            : -MathF.Pow(2f, (10f * t) - 10f) * MathF.Sin(((t * 10f) - 10.75f) * ElasticC4);
    }

    public static float ElasticOut(float t)
    {
        return t == 0f ? 0f
            : t == 1f ? 1f
            : (MathF.Pow(2f, -10f * t) * MathF.Sin(((t * 10f) - 0.75f) * ElasticC4)) + 1f;
    }

    public static float ElasticInOut(float t)
    {
        return t == 0f ? 0f
            : t == 1f ? 1f
            : t < 0.5f
                ? -(MathF.Pow(2f, (20f * t) - 10f) * MathF.Sin(((20f * t) - 11.125f) * ElasticC5))
                    / 2f
            : (MathF.Pow(2f, (-20f * t) + 10f) * MathF.Sin(((20f * t) - 11.125f) * ElasticC5) / 2f)
                + 1f;
    }

    #endregion

    #region Bounce

    private const float BounceN1 = 7.5625f;
    private const float BounceD1 = 2.75f;

    public static float BounceOut(float t)
    {
        if (t < 1f / BounceD1)
        {
            return BounceN1 * t * t;
        }

        if (t < 2f / BounceD1)
        {
            return (BounceN1 * (t -= 1.5f / BounceD1) * t) + 0.75f;
        }

        if (t < 2.5f / BounceD1)
        {
            return (BounceN1 * (t -= 2.25f / BounceD1) * t) + 0.9375f;
        }

        return (BounceN1 * (t -= 2.625f / BounceD1) * t) + 0.984375f;
    }

    public static float BounceIn(float t)
    {
        return 1f - BounceOut(1f - t);
    }

    public static float BounceInOut(float t)
    {
        return t < 0.5f
            ? (1f - BounceOut(1f - (2f * t))) / 2f
            : (1f + BounceOut((2f * t) - 1f)) / 2f;
    }

    #endregion
}
