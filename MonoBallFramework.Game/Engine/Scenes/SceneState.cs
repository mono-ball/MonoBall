namespace MonoBallFramework.Game.Engine.Scenes;

/// <summary>
///     Represents the current lifecycle state of a scene.
///     Uses State Pattern to enforce valid state transitions.
/// </summary>
public enum SceneState
{
    /// <summary>
    ///     Scene has been created but not yet initialized.
    ///     Valid transitions: → Initializing
    /// </summary>
    Uninitialized,

    /// <summary>
    ///     Scene is currently being initialized.
    ///     Valid transitions: → Initialized
    /// </summary>
    Initializing,

    /// <summary>
    ///     Scene has been initialized but content not yet loaded.
    ///     Valid transitions: → LoadingContent
    /// </summary>
    Initialized,

    /// <summary>
    ///     Scene is currently loading content.
    ///     Valid transitions: → ContentLoaded
    /// </summary>
    LoadingContent,

    /// <summary>
    ///     Scene content has been loaded and is ready to run.
    ///     Valid transitions: → Running, Disposing
    /// </summary>
    ContentLoaded,

    /// <summary>
    ///     Scene is actively running (updating and rendering).
    ///     Valid transitions: → Disposing
    /// </summary>
    Running,

    /// <summary>
    ///     Scene is being disposed.
    ///     Valid transitions: → Disposed
    /// </summary>
    Disposing,

    /// <summary>
    ///     Scene has been disposed and cannot be used.
    ///     Valid transitions: None (terminal state)
    /// </summary>
    Disposed
}
