namespace MonoBallFramework.Game.Engine.Audio.Core;

/// <summary>
///     Controls audio ducking - temporarily lowering volume of one audio source
///     while another plays (e.g., duck music when Pokemon cry plays).
///     Thread-safe.
/// </summary>
public class DuckingController
{
    private readonly object _lock = new();

    private readonly float _normalVolume = 1.0f;
    private float _currentVolume = 1.0f;
    private float _duckDuration;
    private float _duckVolume;
    private float _fadeTimer;
    private float _holdDuration;
    private float _holdTimer;

    private DuckState _state = DuckState.Normal;

    public float CurrentVolume
    {
        get
        {
            lock (_lock)
            {
                return _currentVolume;
            }
        }
    }

    public bool IsDucking
    {
        get
        {
            lock (_lock)
            {
                return _state != DuckState.Normal;
            }
        }
    }

    /// <summary>
    ///     Start ducking with specified parameters.
    /// </summary>
    /// <param name="duckVolume">Target ducked volume (e.g., 0.33f)</param>
    /// <param name="fadeDuration">Time to fade to duck volume</param>
    /// <param name="holdDuration">Time to hold at duck volume (0 = until Release)</param>
    public void Duck(float duckVolume, float fadeDuration, float holdDuration = 0f)
    {
        lock (_lock)
        {
            _duckVolume = Math.Clamp(duckVolume, 0f, 1f);
            _duckDuration = fadeDuration;
            _holdDuration = holdDuration;
            _state = DuckState.FadingDown;
            _fadeTimer = 0f;
        }
    }

    /// <summary>
    ///     Release the duck and fade back to normal volume.
    /// </summary>
    public void Release(float fadeDuration)
    {
        lock (_lock)
        {
            if (_state == DuckState.Normal)
            {
                return;
            }

            _duckDuration = fadeDuration;
            _state = DuckState.FadingUp;
            _fadeTimer = 0f;
        }
    }

    /// <summary>
    ///     Update the ducking state. Call every frame.
    /// </summary>
    public void Update(float deltaTime)
    {
        lock (_lock)
        {
            switch (_state)
            {
                case DuckState.FadingDown:
                    // Fade from normal to duck volume
                    _fadeTimer += deltaTime;
                    float downProgress = Math.Min(_fadeTimer / _duckDuration, 1f);
                    _currentVolume = Lerp(_normalVolume, _duckVolume, downProgress);
                    if (downProgress >= 1f)
                    {
                        _state = _holdDuration > 0 ? DuckState.Holding : DuckState.Ducked;
                        _holdTimer = 0f;
                    }

                    break;

                case DuckState.Holding:
                    _holdTimer += deltaTime;
                    if (_holdTimer >= _holdDuration)
                    {
                        _state = DuckState.FadingUp;
                        _fadeTimer = 0f;
                    }

                    break;

                case DuckState.FadingUp:
                    _fadeTimer += deltaTime;
                    float upProgress = Math.Min(_fadeTimer / _duckDuration, 1f);
                    _currentVolume = Lerp(_duckVolume, _normalVolume, upProgress);
                    if (upProgress >= 1f)
                    {
                        _currentVolume = _normalVolume;
                        _state = DuckState.Normal;
                    }

                    break;

                case DuckState.Ducked:
                    // Waiting for Release() call
                    break;
            }
        }
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + ((b - a) * t);
    }

    private enum DuckState { Normal, FadingDown, Holding, Ducked, FadingUp }
}
