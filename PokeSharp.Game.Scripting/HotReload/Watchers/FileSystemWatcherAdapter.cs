using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Game.Scripting.HotReload.Watchers;

/// <summary>
///     Primary watcher using FileSystemWatcher (0% CPU overhead, 90-99% reliable).
///     Includes debouncing and stability checks.
/// </summary>
public class FileSystemWatcherAdapter : IScriptWatcher
{
    private readonly TimeSpan _debounceDelay = TimeSpan.FromMilliseconds(300);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _debounceTimers = new();
    private readonly ILogger<FileSystemWatcherAdapter> _logger;
    private readonly TimeSpan _stabilityCheckDelay = TimeSpan.FromMilliseconds(100);
    private FileSystemWatcher? _watcher;

    public FileSystemWatcherAdapter(ILogger<FileSystemWatcherAdapter> logger)
    {
        _logger = logger;
    }

    public event EventHandler<ScriptChangedEventArgs>? Changed;
    public event EventHandler<ScriptWatcherErrorEventArgs>? Error;

    public WatcherStatus Status { get; private set; } = WatcherStatus.Stopped;

    public double CpuOverheadPercent => 0.0; // FileSystemWatcher uses OS events
    public int ReliabilityScore => 90; // 90% reliable (can miss changes on network drives)

    public Task StartAsync(
        string directory,
        string filter,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            Status = WatcherStatus.Starting;

            _watcher = new FileSystemWatcher(directory)
            {
                Filter = filter,
                NotifyFilter =
                    NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                EnableRaisingEvents = false,
                IncludeSubdirectories = true,
            };

            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Renamed += OnFileRenamed;
            _watcher.Error += OnWatcherError;

            _watcher.EnableRaisingEvents = true;
            Status = WatcherStatus.Running;

            _logger.LogInformation(
                "FileSystemWatcher started for {Directory} with filter {Filter}",
                directory,
                filter
            );
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Status = WatcherStatus.Error;
            _logger.LogError(ex, "Failed to start FileSystemWatcher");
            Error?.Invoke(
                this,
                new ScriptWatcherErrorEventArgs
                {
                    Exception = ex,
                    Message = "Failed to start FileSystemWatcher",
                    IsCritical = true,
                }
            );
            throw;
        }
    }

    /// <summary>
    ///     Stops the file watcher and disposes all CancellationTokenSource instances.
    /// </summary>
    public Task StopAsync()
    {
        Status = WatcherStatus.Stopping;

        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnFileChanged;
            _watcher.Created -= OnFileChanged;
            _watcher.Renamed -= OnFileRenamed;
            _watcher.Error -= OnWatcherError;
            _watcher.Dispose();
            _watcher = null;
        }

        // Cancel and dispose all pending debounce timers to prevent resource leaks
        foreach (CancellationTokenSource cts in _debounceTimers.Values)
        {
            try
            {
                cts.Cancel();
                cts.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }
        }

        _debounceTimers.Clear();

        Status = WatcherStatus.Stopped;
        _logger.LogInformation("FileSystemWatcher stopped and all resources disposed");
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Disposes the watcher and ensures all resources are cleaned up.
    /// </summary>
    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    ///     Handles file change events with debouncing and proper CancellationTokenSource disposal.
    /// </summary>
    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce: if file is saved multiple times rapidly, only process once
        if (_debounceTimers.TryGetValue(e.FullPath, out CancellationTokenSource? existingCts))
        {
            try
            {
                existingCts.Cancel();
                existingCts.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }
        }

        var cts = new CancellationTokenSource();
        _debounceTimers[e.FullPath] = cts;

        _ = Task.Delay(_debounceDelay, cts.Token)
            .ContinueWith(
                async _ =>
                {
                    try
                    {
                        if (!cts.Token.IsCancellationRequested)
                        {
                            await CheckStabilityAndNotify(e.FullPath, e.ChangeType.ToString());

                            // Remove and dispose the CancellationTokenSource
                            if (
                                _debounceTimers.TryRemove(
                                    e.FullPath,
                                    out CancellationTokenSource? removedCts
                                )
                            )
                            {
                                removedCts.Dispose();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Error in debounce continuation for {FilePath}",
                            e.FullPath
                        );
                    }
                },
                TaskScheduler.Default
            );
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        OnFileChanged(sender, e);
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(), "FileSystemWatcher error");
        Error?.Invoke(
            this,
            new ScriptWatcherErrorEventArgs
            {
                Exception = e.GetException(),
                Message = "FileSystemWatcher encountered an error",
                IsCritical = false,
            }
        );
    }

    private async Task CheckStabilityAndNotify(string filePath, string changeType)
    {
        try
        {
            // Wait for file to stabilize (no longer being written)
            long previousSize = -1;
            for (int i = 0; i < 3; i++) // Max 3 checks = 300ms
            {
                if (!File.Exists(filePath))
                {
                    return; // File was deleted
                }

                try
                {
                    var fileInfo = new FileInfo(filePath);
                    long currentSize = fileInfo.Length;

                    if (currentSize == previousSize)
                    {
                        break; // File size stabilized
                    }

                    previousSize = currentSize;
                    await Task.Delay(_stabilityCheckDelay);
                }
                catch (IOException)
                {
                    // File is locked, wait and retry
                    await Task.Delay(_stabilityCheckDelay);
                }
            }

            // Final check: try to open file (ensures it's not locked)
            try
            {
                using FileStream stream = File.Open(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite
                );
                var fileInfo = new FileInfo(filePath);

                Changed?.Invoke(
                    this,
                    new ScriptChangedEventArgs
                    {
                        FilePath = filePath,
                        ChangeTime = DateTime.UtcNow,
                        FileSize = fileInfo.Length,
                        ChangeType = changeType,
                    }
                );

                _logger.LogDebug("File change detected and stabilized: {FilePath}", filePath);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(
                    ex,
                    "File still locked after stability checks: {FilePath}",
                    filePath
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking file stability: {FilePath}", filePath);
        }
    }
}
