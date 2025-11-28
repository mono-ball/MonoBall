namespace PokeSharp.Engine.Scenes;

/// <summary>
///     Thread-safe progress tracking for async initialization operations.
///     Can be updated from background threads and read from the UI thread.
/// </summary>
public class LoadingProgress
{
    private readonly object _lock = new();
    private string _currentStep = string.Empty;
    private Exception? _error;
    private bool _isComplete;
    private float _progress;

    /// <summary>
    ///     Gets or sets the progress value (0.0 to 1.0).
    ///     Values are automatically clamped to the valid range.
    /// </summary>
    public float Progress
    {
        get
        {
            lock (_lock)
            {
                return _progress;
            }
        }
        set
        {
            lock (_lock)
            {
                _progress = Math.Clamp(value, 0.0f, 1.0f);
            }
        }
    }

    /// <summary>
    ///     Gets or sets the current step description.
    /// </summary>
    public string CurrentStep
    {
        get
        {
            lock (_lock)
            {
                return _currentStep;
            }
        }
        set
        {
            lock (_lock)
            {
                _currentStep = value ?? string.Empty;
            }
        }
    }

    /// <summary>
    ///     Gets or sets a value indicating whether initialization is complete.
    /// </summary>
    public bool IsComplete
    {
        get
        {
            lock (_lock)
            {
                return _isComplete;
            }
        }
        set
        {
            lock (_lock)
            {
                _isComplete = value;
            }
        }
    }

    /// <summary>
    ///     Gets or sets any error that occurred during initialization.
    /// </summary>
    public Exception? Error
    {
        get
        {
            lock (_lock)
            {
                return _error;
            }
        }
        set
        {
            lock (_lock)
            {
                _error = value;
            }
        }
    }
}
