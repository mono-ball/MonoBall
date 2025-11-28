using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Game.Scripting.HotReload.Watchers;

/// <summary>
///     Factory for creating the optimal IScriptWatcher based on platform and path characteristics.
/// </summary>
public class WatcherFactory
{
    private readonly ILogger<WatcherFactory> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public WatcherFactory(ILogger<WatcherFactory> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    ///     Create the best watcher for the given directory.
    /// </summary>
    public IScriptWatcher CreateWatcher(string directory)
    {
        PathAnalysis analysis = AnalyzePath(directory);

        _logger.LogInformation(
            "Path analysis for {Directory}: Platform={Platform}, IsNetworkPath={IsNetwork}, "
                + "IsDockerVolume={IsDocker}, IsWSL2={IsWSL2}, RecommendPolling={RecommendPolling}",
            directory,
            analysis.Platform,
            analysis.IsNetworkPath,
            analysis.IsDockerVolume,
            analysis.IsWSL2,
            analysis.RecommendPolling
        );

        if (analysis.RecommendPolling)
        {
            _logger.LogInformation("Using PollingWatcher (100% reliable, ~4% CPU)");
            return new PollingWatcher(_loggerFactory.CreateLogger<PollingWatcher>());
        }

        _logger.LogInformation("Using FileSystemWatcherAdapter (90-99% reliable, 0% CPU)");
        return new FileSystemWatcherAdapter(
            _loggerFactory.CreateLogger<FileSystemWatcherAdapter>()
        );
    }

    private PathAnalysis AnalyzePath(string path)
    {
        var analysis = new PathAnalysis { Platform = GetPlatformName() };

        try
        {
            string fullPath = Path.GetFullPath(path);

            // Check for network paths
            analysis.IsNetworkPath = IsNetworkPath(fullPath);

            // Check for Docker volumes
            analysis.IsDockerVolume = IsDockerVolume(fullPath);

            // Check for WSL2
            analysis.IsWSL2 = IsWSL2Path(fullPath);

            // Recommendation: use polling if any of these conditions are true
            analysis.RecommendPolling =
                analysis.IsNetworkPath || analysis.IsDockerVolume || analysis.IsWSL2;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to analyze path {Path}, defaulting to FileSystemWatcher",
                path
            );
        }

        return analysis;
    }

    private static bool IsNetworkPath(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        // UNC paths (\\server\share) or mapped network drives
        {
            return path.StartsWith(@"\\") || IsNetworkDriveWindows(path);
        }

        // Linux/macOS: check for NFS, SMB, CIFS mounts
        return (
                path.StartsWith("/mnt/")
                && !path.StartsWith("/mnt/c/")
                && !path.StartsWith("/mnt/wsl")
            )
            || path.StartsWith("/net/")
            || path.Contains("/nfs/")
            || path.Contains("/smb/");
    }

    private static bool IsNetworkDriveWindows(string path)
    {
        try
        {
            if (path.Length < 2 || path[1] != ':')
            {
                return false;
            }

            var drive = new DriveInfo(path.Substring(0, 2));
            return drive.DriveType == DriveType.Network;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDockerVolume(string path)
    {
        // Check for Docker-specific paths
        return path.Contains("/var/lib/docker/")
            || path.Contains("/docker/")
            || File.Exists("/.dockerenv")
            || File.Exists("/run/.containerenv"); // Podman
    }

    private static bool IsWSL2Path(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return false;
        }

        // WSL2 mounts Windows drives under /mnt/c, /mnt/d, etc.
        if (path.StartsWith("/mnt/") && path.Length > 6 && path[6] == '/')
        {
            char driveLetter = path[5];
            return char.IsLetter(driveLetter);
        }

        // Check for WSL-specific kernel version
        try
        {
            string version = File.ReadAllText("/proc/version");
            return version.Contains("microsoft", StringComparison.OrdinalIgnoreCase)
                || version.Contains("WSL", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string GetPlatformName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "Windows";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return IsWSL2Path("/proc") ? "WSL2" : "Linux";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "macOS";
        }

        return "Unknown";
    }

    private class PathAnalysis
    {
        public string Platform { get; set; } = string.Empty;
        public bool IsNetworkPath { get; set; }
        public bool IsDockerVolume { get; set; }
        public bool IsWSL2 { get; set; }
        public bool RecommendPolling { get; set; }
    }
}
