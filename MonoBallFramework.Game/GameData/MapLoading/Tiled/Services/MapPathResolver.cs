using MonoBallFramework.Game.Engine.Content;
using MonoBallFramework.Game.Engine.Rendering.Assets;

namespace MonoBallFramework.Game.GameData.MapLoading.Tiled.Services;

/// <summary>
///     Resolves paths for map loading operations.
///     Handles resolution of map directories and asset roots.
/// </summary>
public class MapPathResolver
{
    private readonly IAssetProvider _assetProvider;
    private readonly IContentProvider _contentProvider;

    public MapPathResolver(IAssetProvider assetProvider, IContentProvider contentProvider)
    {
        _assetProvider = assetProvider;
        _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
    }

    /// <summary>
    ///     Resolves the base directory for map files.
    ///     Uses IContentProvider to resolve the Definitions directory.
    /// </summary>
    /// <returns>Base directory path for maps (the Definitions folder).</returns>
    public string ResolveMapDirectoryBase()
    {
        // Use IContentProvider to get the Definitions directory
        string? definitionsPath = _contentProvider.GetContentDirectory("Definitions");
        if (definitionsPath == null)
        {
            throw new DirectoryNotFoundException(
                "Definitions directory not found. Ensure content provider is configured correctly.");
        }

        return definitionsPath;
    }

    /// <summary>
    ///     Resolves the asset root directory.
    ///     Uses IContentProvider to resolve the Root content type directory.
    /// </summary>
    /// <returns>Asset root directory path.</returns>
    public string ResolveAssetRoot()
    {
        // Use IContentProvider to get the Root directory (the Assets folder)
        string? rootPath = _contentProvider.GetContentDirectory("Root");
        if (rootPath == null)
        {
            throw new DirectoryNotFoundException(
                "Asset root directory not found. Ensure content provider is configured correctly.");
        }

        return rootPath;
    }
}
