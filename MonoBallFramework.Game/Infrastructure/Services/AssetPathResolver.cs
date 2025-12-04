using Microsoft.Extensions.Options;
using MonoBallFramework.Game.Infrastructure.Configuration;

namespace MonoBallFramework.Game.Infrastructure.Services;

/// <summary>
///     Provides path resolution for game assets, handling both development and published scenarios.
///     Uses the application base directory to ensure assets are found regardless of working directory.
/// </summary>
public interface IAssetPathResolver
{
    /// <summary>
    ///     Gets the resolved root path for all assets (e.g., "/path/to/bin/Debug/Assets").
    /// </summary>
    string AssetRoot { get; }

    /// <summary>
    ///     Gets the resolved path for game data (e.g., "/path/to/bin/Debug/Assets/Data").
    /// </summary>
    string DataPath { get; }

    /// <summary>
    ///     Resolves a relative asset path to an absolute path.
    /// </summary>
    /// <param name="relativePath">Path relative to the asset root (e.g., "Templates" or "Data/NPCs").</param>
    /// <returns>Absolute path to the asset directory or file.</returns>
    string Resolve(string relativePath);

    /// <summary>
    ///     Resolves a path relative to the data directory.
    /// </summary>
    /// <param name="relativePath">Path relative to the data directory (e.g., "Behaviors" or "Maps").</param>
    /// <returns>Absolute path to the data subdirectory or file.</returns>
    string ResolveData(string relativePath);

    /// <summary>
    ///     Checks if a resolved path exists as a directory.
    /// </summary>
    /// <param name="relativePath">Path relative to the asset root.</param>
    /// <returns>True if the directory exists; otherwise, false.</returns>
    bool DirectoryExists(string relativePath);

    /// <summary>
    ///     Checks if a resolved path exists as a file.
    /// </summary>
    /// <param name="relativePath">Path relative to the asset root.</param>
    /// <returns>True if the file exists; otherwise, false.</returns>
    bool FileExists(string relativePath);
}

/// <summary>
///     Default implementation of <see cref="IAssetPathResolver" /> that uses AppContext.BaseDirectory.
/// </summary>
public class AssetPathResolver : IAssetPathResolver
{
    private readonly string _baseDirectory;
    private readonly GameInitializationConfig _config;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AssetPathResolver" /> class.
    /// </summary>
    /// <param name="options">Game configuration options.</param>
    public AssetPathResolver(IOptions<GameConfiguration> options)
    {
        _config = options?.Value?.Initialization ?? new GameInitializationConfig();

        // Use AppContext.BaseDirectory to get the directory where the executable is located
        // This works correctly regardless of the current working directory
        _baseDirectory = AppContext.BaseDirectory;
    }

    /// <inheritdoc />
    public string AssetRoot => Path.Combine(_baseDirectory, _config.AssetRoot);

    /// <inheritdoc />
    public string DataPath => Path.Combine(_baseDirectory, _config.DataPath);

    /// <inheritdoc />
    public string Resolve(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return AssetRoot;
        }

        // If the path starts with the configured asset root, strip it to avoid duplication
        // e.g., "Assets/Templates" becomes "Templates" when combined with AssetRoot
        string normalizedPath = relativePath.Replace('\\', '/');
        string assetRootPrefix = _config.AssetRoot.Replace('\\', '/');

        if (normalizedPath.StartsWith(assetRootPrefix + "/", StringComparison.OrdinalIgnoreCase))
        {
            normalizedPath = normalizedPath[(assetRootPrefix.Length + 1)..];
        }
        else if (normalizedPath.Equals(assetRootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AssetRoot;
        }

        return Path.Combine(AssetRoot, normalizedPath);
    }

    /// <inheritdoc />
    public string ResolveData(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return DataPath;
        }

        return Path.Combine(DataPath, relativePath);
    }

    /// <inheritdoc />
    public bool DirectoryExists(string relativePath)
    {
        return Directory.Exists(Resolve(relativePath));
    }

    /// <inheritdoc />
    public bool FileExists(string relativePath)
    {
        return File.Exists(Resolve(relativePath));
    }
}
