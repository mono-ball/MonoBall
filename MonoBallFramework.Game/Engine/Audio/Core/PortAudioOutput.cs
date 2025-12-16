using PortAudioSharp;

namespace MonoBallFramework.Game.Engine.Audio.Core;

/// <summary>
/// PortAudio-based audio output for cross-platform audio playback.
/// Manages a PortAudio stream and pulls samples from an ISampleProvider.
/// </summary>
public class PortAudioOutput : IDisposable
{
    private static bool _portAudioInitialized;
    private static readonly object _initLock = new();

    private readonly ISampleProvider _source;
    private readonly AudioFormat _format;
    private PortAudioSharp.Stream? _stream;
    private readonly object _streamLock = new();
    private volatile bool _isPlaying;
    private volatile bool _isPaused;
    private bool _disposed;

    /// <summary>
    /// Event raised when playback stops (either normally or due to error).
    /// </summary>
    public event EventHandler<PlaybackStoppedEventArgs>? PlaybackStopped;

    /// <summary>
    /// Creates a new PortAudio output.
    /// </summary>
    /// <param name="source">The sample provider to read audio from.</param>
    public PortAudioOutput(ISampleProvider source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _format = source.Format;

        EnsurePortAudioInitialized();
    }

    /// <summary>
    /// Gets the current playback state.
    /// </summary>
    public PlaybackState PlaybackState
    {
        get
        {
            if (_disposed) return PlaybackState.Stopped;
            if (_isPaused) return PlaybackState.Paused;
            if (_isPlaying) return PlaybackState.Playing;
            return PlaybackState.Stopped;
        }
    }

    /// <summary>
    /// Initializes the PortAudio stream and starts playback.
    /// </summary>
    public void Play()
    {
        lock (_streamLock)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PortAudioOutput));

            if (_isPlaying && !_isPaused)
                return;

            if (_isPaused && _stream != null)
            {
                // Resume from pause
                _stream.Start();
                _isPaused = false;
                return;
            }

            // Create new stream
            var outputParams = new StreamParameters
            {
                device = PortAudio.DefaultOutputDevice,
                channelCount = _format.Channels,
                sampleFormat = SampleFormat.Float32,
                suggestedLatency = PortAudio.GetDeviceInfo(PortAudio.DefaultOutputDevice).defaultLowOutputLatency
            };

            _stream = new PortAudioSharp.Stream(
                inParams: null,
                outParams: outputParams,
                sampleRate: _format.SampleRate,
                framesPerBuffer: 1024,
                streamFlags: StreamFlags.ClipOff,
                callback: AudioCallback,
                userData: IntPtr.Zero
            );

            _stream.Start();
            _isPlaying = true;
            _isPaused = false;
        }
    }

    /// <summary>
    /// Pauses playback without closing the stream.
    /// </summary>
    public void Pause()
    {
        lock (_streamLock)
        {
            if (_disposed || !_isPlaying || _isPaused)
                return;

            _stream?.Stop();
            _isPaused = true;
        }
    }

    /// <summary>
    /// Stops playback and closes the stream.
    /// </summary>
    public void Stop()
    {
        lock (_streamLock)
        {
            StopInternal(null);
        }
    }

    private void StopInternal(Exception? exception)
    {
        if (!_isPlaying && !_isPaused)
            return;

        try
        {
            _stream?.Stop();
            _stream?.Dispose();
        }
        catch
        {
            // Ignore errors during cleanup
        }

        _stream = null;
        _isPlaying = false;
        _isPaused = false;

        PlaybackStopped?.Invoke(this, new PlaybackStoppedEventArgs(exception));
    }

    private StreamCallbackResult AudioCallback(
        IntPtr input,
        IntPtr output,
        uint frameCount,
        ref StreamCallbackTimeInfo timeInfo,
        StreamCallbackFlags statusFlags,
        IntPtr userData)
    {
        try
        {
            int samplesNeeded = (int)frameCount * _format.Channels;
            var buffer = new float[samplesNeeded];

            int samplesRead = _source.Read(buffer, 0, samplesNeeded);

            // Fill any remaining samples with silence
            if (samplesRead < samplesNeeded)
            {
                Array.Clear(buffer, samplesRead, samplesNeeded - samplesRead);
            }

            // Copy to output buffer using marshalling (safe code)
            System.Runtime.InteropServices.Marshal.Copy(buffer, 0, output, samplesNeeded);

            return StreamCallbackResult.Continue;
        }
        catch (Exception)
        {
            return StreamCallbackResult.Abort;
        }
    }

    private static void EnsurePortAudioInitialized()
    {
        lock (_initLock)
        {
            if (!_portAudioInitialized)
            {
                PortAudio.Initialize();
                _portAudioInitialized = true;

                // Register for cleanup on process exit
                AppDomain.CurrentDomain.ProcessExit += (s, e) =>
                {
                    if (_portAudioInitialized)
                    {
                        try
                        {
                            PortAudio.Terminate();
                        }
                        catch
                        {
                            // Ignore errors during shutdown
                        }
                    }
                };
            }
        }
    }

    /// <summary>
    /// Disposes the PortAudio output and releases resources.
    /// </summary>
    public void Dispose()
    {
        lock (_streamLock)
        {
            if (_disposed)
                return;

            _disposed = true;
            StopInternal(null);
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Playback state enumeration.
/// </summary>
public enum PlaybackState
{
    Stopped,
    Playing,
    Paused
}

/// <summary>
/// Event args for playback stopped events.
/// </summary>
public class PlaybackStoppedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the exception that caused playback to stop, or null if stopped normally.
    /// </summary>
    public Exception? Exception { get; }

    public PlaybackStoppedEventArgs(Exception? exception = null)
    {
        Exception = exception;
    }
}

