using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Tmx;

namespace MonoBallFramework.Game.GameData.MapLoading.Tiled.Core;

/// <summary>
///     Provides access to TMX document loading functionality.
///     This interface breaks the circular dependency between MapLoader and MapPreparer.
/// </summary>
public interface ITmxDocumentProvider
{
    /// <summary>
    ///     Gets a cached TMX document or loads and caches it asynchronously if not already loaded.
    ///     Uses async file I/O and background thread for JSON parsing to avoid blocking the main thread.
    /// </summary>
    /// <param name="mapId">The map identifier to load.</param>
    /// <returns>The parsed TmxDocument (either from cache or freshly loaded).</returns>
    /// <exception cref="InvalidOperationException">If MapEntityService is not configured.</exception>
    /// <exception cref="FileNotFoundException">If map definition or file is not found.</exception>
    Task<TmxDocument> GetOrLoadTmxDocumentAsync(GameMapId mapId);

    /// <summary>
    ///     Gets a cached TMX document or loads and caches it if not already loaded.
    ///     This avoids redundant file reads and JSON parsing during map transitions.
    /// </summary>
    /// <param name="fullPath">The full path to the TMX JSON file.</param>
    /// <returns>The parsed TmxDocument (either from cache or freshly loaded).</returns>
    TmxDocument GetOrLoadTmxDocument(string fullPath);
}
