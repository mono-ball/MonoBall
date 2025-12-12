using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace MonoBallFramework.Game.Engine.Audio.Services;

/// <summary>
/// Extension methods for NAudio wave player operations.
/// Provides thread-safe utilities for stopping and disposing wave players.
/// </summary>
public static class WavePlayerExtensions
{
    /// <summary>
    /// Safely stops and disposes a wave player, handling all cleanup including event unsubscription.
    /// </summary>
    /// <param name="waveOut">Reference to the wave player to stop. Will be set to null after disposal.</param>
    /// <param name="handler">Optional event handler to unsubscribe from PlaybackStopped event.</param>
    /// <param name="logger">Optional logger for error reporting.</param>
    public static void SafeStop(ref IWavePlayer? waveOut, EventHandler<StoppedEventArgs>? handler = null, ILogger? logger = null)
    {
        if (waveOut == null)
            return;

        try
        {
            if (handler != null)
                waveOut.PlaybackStopped -= handler;

            waveOut.Stop();
            waveOut.Dispose();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error stopping wave output");
        }
        finally
        {
            waveOut = null;
        }
    }
}
