using MonoBallFramework.Game.Engine.Scenes.Scenes;
using MonoBallFramework.Game.GameData.Entities;

namespace MonoBallFramework.Game.Engine.Scenes.Factories;

/// <summary>
///     Factory for creating game scenes with proper dependency injection.
///     Scenes should be created through factories, not directly instantiated.
/// </summary>
public interface ISceneFactory
{
    /// <summary>
    ///     Creates a new MapPopupScene instance with all dependencies resolved.
    /// </summary>
    /// <param name="backgroundDefinition">The popup background definition.</param>
    /// <param name="outlineDefinition">The popup outline definition.</param>
    /// <param name="mapName">The map name to display.</param>
    /// <returns>A fully configured MapPopupScene.</returns>
    MapPopupScene CreateMapPopupScene(
        PopupBackgroundEntity backgroundDefinition,
        PopupOutlineEntity outlineDefinition,
        string mapName
    );
}
