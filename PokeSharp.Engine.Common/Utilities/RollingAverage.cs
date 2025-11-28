namespace PokeSharp.Engine.Common.Utilities;

/// <summary>
///     Tracks a rolling average over a fixed window of samples.
///     Uses a circular buffer for O(1) operations.
/// </summary>
public class RollingAverage
{
    private readonly float[] _samples;
    private int _currentIndex;
    private float _sum;

    /// <summary>
    ///     Initializes a new instance of the RollingAverage class.
    /// </summary>
    /// <param name="windowSize">Number of samples to track.</param>
    public RollingAverage(int windowSize)
    {
        if (windowSize <= 0)
        {
            throw new ArgumentException("Window size must be positive", nameof(windowSize));
        }

        _samples = new float[windowSize];
        _currentIndex = 0;
        Count = 0;
        _sum = 0;
    }

    /// <summary>
    ///     Gets the current average of all samples in the window.
    /// </summary>
    public float Average => Count > 0 ? _sum / Count : 0;

    /// <summary>
    ///     Gets the minimum value in the current window.
    /// </summary>
    public float Min
    {
        get
        {
            if (Count == 0)
            {
                return 0;
            }

            float min = _samples[0];
            for (int i = 1; i < Count; i++)
            {
                if (_samples[i] < min)
                {
                    min = _samples[i];
                }
            }

            return min;
        }
    }

    /// <summary>
    ///     Gets the maximum value in the current window.
    /// </summary>
    public float Max
    {
        get
        {
            if (Count == 0)
            {
                return 0;
            }

            float max = _samples[0];
            for (int i = 1; i < Count; i++)
            {
                if (_samples[i] > max)
                {
                    max = _samples[i];
                }
            }

            return max;
        }
    }

    /// <summary>
    ///     Gets the number of samples currently in the window.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    ///     Adds a new sample to the rolling average.
    /// </summary>
    /// <param name="value">The value to add.</param>
    public void Add(float value)
    {
        // If buffer is full, subtract the oldest value from sum
        if (Count == _samples.Length)
        {
            _sum -= _samples[_currentIndex];
        }
        else
        {
            Count++;
        }

        // Add new value
        _samples[_currentIndex] = value;
        _sum += value;

        // Move to next index (circular)
        _currentIndex = (_currentIndex + 1) % _samples.Length;
    }

    /// <summary>
    ///     Resets the rolling average to initial state.
    /// </summary>
    public void Reset()
    {
        _currentIndex = 0;
        Count = 0;
        _sum = 0;
        Array.Clear(_samples, 0, _samples.Length);
    }
}
