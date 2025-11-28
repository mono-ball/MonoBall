using Microsoft.Xna.Framework;

namespace PokeSharp.Engine.Scenes;

/// <summary>
///     Interface for game scenes that follow MonoGame's lifecycle pattern.
///     Scenes manage their own content and rendering.
/// </summary>
public interface IScene : IDisposable
{
    /// <summary>
    ///     Gets a value indicating whether this scene has been disposed.
    /// </summary>
    bool IsDisposed { get; }

    /// <summary>
    ///     Gets a value indicating whether this scene has been initialized.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    ///     Gets a value indicating whether this scene's content has been loaded.
    /// </summary>
    bool IsContentLoaded { get; }

    /// <summary>
    ///     Gets a value indicating whether the scene below this one should be rendered.
    ///     When true, the base scene (and any scenes below) will be rendered before this scene.
    ///     When false, only this scene will be rendered (full-screen scenes like menus).
    ///     Default is false (full-screen).
    /// </summary>
    bool RenderScenesBelow { get; }

    /// <summary>
    ///     Gets a value indicating whether the scenes below this one should be updated.
    ///     When true, the base scene (and any scenes below) will receive Update calls.
    ///     When false, only this scene will be updated (pauses lower scenes).
    ///     Default is false (lower scenes are paused).
    /// </summary>
    bool UpdateScenesBelow { get; }

    /// <summary>
    ///     Gets a value indicating whether this scene takes exclusive input.
    ///     When true, input handling will not fall through to scenes below this one.
    ///     When false, input will be processed by this scene and then fall through to scenes below.
    ///     Default is true (exclusive input).
    /// </summary>
    bool ExclusiveInput { get; }

    /// <summary>
    ///     Initializes the scene. Called once when the scene becomes active.
    ///     MonoGame will automatically call LoadContent() after this method.
    /// </summary>
    void Initialize();

    /// <summary>
    ///     Loads scene-specific content. Called automatically by MonoGame after Initialize().
    /// </summary>
    void LoadContent();

    /// <summary>
    ///     Unloads scene-specific content. Called when the scene is being disposed.
    /// </summary>
    void UnloadContent();

    /// <summary>
    ///     Updates the scene logic.
    /// </summary>
    /// <param name="gameTime">Provides timing information.</param>
    void Update(GameTime gameTime);

    /// <summary>
    ///     Draws the scene.
    /// </summary>
    /// <param name="gameTime">Provides timing information.</param>
    void Draw(GameTime gameTime);
}

