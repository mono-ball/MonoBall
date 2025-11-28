namespace PokeSharp.Game.Scripting.HotReload;

/// <summary>
///     Interface for watching script files for changes.
///     Abstracts FileSystemWatcher vs polling implementations.
/// </summary>
public interface IScriptWatcher : IDisposable
{
    /// <summary>
    ///     Current watcher status.
    /// </summary>
    WatcherStatus Status { get; }

    /// <summary>
    ///     Estimated CPU overhead (0-100%).
    /// </summary>
    double CpuOverheadPercent { get; }

    /// <summary>
    ///     Reliability score (0-100%).
    /// </summary>
    int ReliabilityScore { get; }

    /// <summary>
    ///     Fired when a script file changes and passes stability checks.
    /// </summary>
    event EventHandler<ScriptChangedEventArgs> Changed;

    /// <summary>
    ///     Fired when the watcher encounters an error.
    /// </summary>
    event EventHandler<ScriptWatcherErrorEventArgs> Error;

    /// <summary>
    ///     Start watching the specified directory.
    /// </summary>
    Task StartAsync(string directory, string filter, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Stop watching and release resources.
    /// </summary>
    Task StopAsync();
}
