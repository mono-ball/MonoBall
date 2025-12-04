using PokeSharp.Game.Engine.Rendering.Assets;

namespace PokeSharp.Game.Data.MapLoading.Tiled.Services;

/// <summary>
///     Resolves paths for map loading operations.
///     Handles resolution of map directories and asset roots.
/// </summary>
public class MapPathResolver
{
    private readonly IAssetProvider _assetProvider;

    public MapPathResolver(IAssetProvider assetProvider)
    {
        _assetProvider = assetProvider;
    }

    /// <summary>
    ///     Resolves the base directory for map files.
    ///     Uses AssetManager's AssetRoot if available, otherwise falls back to a default path.
    /// </summary>
    /// <returns>Base directory path for maps.</returns>
    public string ResolveMapDirectoryBase()
    {
        if (_assetProvider is AssetManager assetManager)
        {
            return Path.Combine(assetManager.AssetRoot, "Data", "Maps");
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Data", "Maps");
    }

    /// <summary>
    ///     Resolves the asset root directory.
    ///     Uses AssetManager's AssetRoot if available, otherwise falls back to a default path.
    /// </summary>
    /// <returns>Asset root directory path.</returns>
    public string ResolveAssetRoot()
    {
        if (_assetProvider is AssetManager assetManager)
        {
            return assetManager.AssetRoot;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "Assets");
    }
}
