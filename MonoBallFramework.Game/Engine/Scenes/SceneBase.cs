using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace MonoBallFramework.Game.Engine.Scenes;

/// <summary>
///     Base class for game scenes that provides common functionality.
///     Follows MonoGame's lifecycle pattern with proper disposal.
///     Uses State Pattern for lifecycle management instead of boolean flags.
/// </summary>
public abstract class SceneBase : IScene
{
    private readonly object _lock = new();
    private SceneState _state = SceneState.Uninitialized;

    /// <summary>
    ///     Initializes a new instance of the SceneBase class.
    ///     All dependencies should be passed through the derived class constructor.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device for rendering.</param>
    /// <param name="logger">The logger for this scene.</param>
    /// <param name="contentRootDirectory">The root directory for content loading (default: "Content").</param>
    protected SceneBase(
        GraphicsDevice graphicsDevice,
        ILogger logger,
        string contentRootDirectory = "Content"
    )
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrEmpty(contentRootDirectory);

        GraphicsDevice = graphicsDevice;
        Logger = logger;

        // Create a minimal service container for ContentManager
        // ContentManager requires IServiceProvider but only uses IGraphicsDeviceService
        var serviceContainer = new GameServiceContainer();
        serviceContainer.AddService(typeof(IGraphicsDeviceService), new GraphicsDeviceServiceShim(graphicsDevice));
        Content = new ContentManager(serviceContainer, contentRootDirectory);
    }

    /// <summary>
    ///     Gets the per-scene content manager.
    /// </summary>
    protected ContentManager Content { get; }

    /// <summary>
    ///     Gets the graphics device.
    /// </summary>
    protected GraphicsDevice GraphicsDevice { get; }

    /// <summary>
    ///     Gets the logger for this scene.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    ///     Gets the current lifecycle state of this scene.
    /// </summary>
    public SceneState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
        private set
        {
            lock (_lock)
            {
                // Validate state transition
                SceneStateTransitions.ValidateTransition(_state, value);

                SceneState oldState = _state;
                _state = value;

                Logger.LogDebug(
                    "Scene {SceneType} state transition: {OldState} → {NewState}",
                    GetType().Name,
                    oldState,
                    value
                );
            }
        }
    }

    /// <summary>
    ///     Gets a value indicating whether this scene has been disposed.
    /// </summary>
    public bool IsDisposed => State == SceneState.Disposed;

    /// <summary>
    ///     Gets a value indicating whether this scene has been initialized.
    /// </summary>
    public bool IsInitialized => State >= SceneState.Initialized;

    /// <summary>
    ///     Gets a value indicating whether this scene's content has been loaded.
    /// </summary>
    public bool IsContentLoaded => State >= SceneState.ContentLoaded;

    /// <summary>
    ///     Gets or sets a value indicating whether the scene below this one should be rendered.
    ///     When true, the base scene (and any scenes below) will be rendered before this scene.
    ///     When false, only this scene will be rendered (full-screen scenes like menus).
    ///     Default is false (full-screen).
    /// </summary>
    public virtual bool RenderScenesBelow { get; protected set; } = false;

    /// <summary>
    ///     Gets or sets a value indicating whether the scenes below this one should be updated.
    ///     When true, the base scene (and any scenes below) will receive Update calls.
    ///     When false, only this scene will be updated (pauses lower scenes).
    ///     Default is false (lower scenes are paused).
    /// </summary>
    public virtual bool UpdateScenesBelow { get; protected set; } = false;

    /// <summary>
    ///     Gets or sets a value indicating whether this scene takes exclusive input.
    ///     When true, input handling will not fall through to scenes below this one.
    ///     When false, input will be processed by this scene and then fall through to scenes below.
    ///     Default is true (exclusive input).
    /// </summary>
    public virtual bool ExclusiveInput { get; protected set; } = true;

    /// <summary>
    ///     Initializes the scene. Called once when the scene becomes active.
    ///     MonoGame will automatically call LoadContent() after this method.
    /// </summary>
    public virtual void Initialize()
    {
        if (!SceneStateTransitions.CanInitialize(State))
        {
            throw new InvalidOperationException(
                $"Cannot initialize scene in state {State}. Expected state: {SceneState.Uninitialized}"
            );
        }

        State = SceneState.Initializing;

        // Perform initialization (override in derived classes)
        OnInitialize();

        State = SceneState.Initialized;
    }

    /// <summary>
    ///     Loads scene-specific content. Called automatically by MonoGame after Initialize().
    /// </summary>
    public virtual void LoadContent()
    {
        if (!SceneStateTransitions.CanLoadContent(State))
        {
            throw new InvalidOperationException(
                $"Cannot load content in state {State}. Expected state: {SceneState.Initialized}"
            );
        }

        State = SceneState.LoadingContent;

        // Perform content loading (override in derived classes)
        OnLoadContent();

        State = SceneState.ContentLoaded;
    }

    /// <summary>
    ///     Unloads scene-specific content. Called when the scene is being disposed.
    /// </summary>
    public virtual void UnloadContent()
    {
        if (State >= SceneState.ContentLoaded && State < SceneState.Disposing)
        {
            Content.Unload();
            Logger.LogDebug("Scene {SceneType} content unloaded", GetType().Name);
        }
    }

    /// <summary>
    ///     Updates the scene logic.
    /// </summary>
    /// <param name="gameTime">Provides timing information.</param>
    public abstract void Update(GameTime gameTime);

    /// <summary>
    ///     Draws the scene.
    /// </summary>
    /// <param name="gameTime">Provides timing information.</param>
    public abstract void Draw(GameTime gameTime);

    /// <summary>
    ///     Disposes the scene and releases all resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Override this method in derived classes to perform initialization.
    ///     State transitions are handled automatically by the base class.
    /// </summary>
    protected virtual void OnInitialize()
    {
        // Default: No initialization needed
    }

    /// <summary>
    ///     Override this method in derived classes to load content.
    ///     State transitions are handled automatically by the base class.
    /// </summary>
    protected virtual void OnLoadContent()
    {
        // Default: No content to load
    }

    /// <summary>
    ///     Disposes the scene and releases all resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (State == SceneState.Disposed)
        {
            return;
        }

        State = SceneState.Disposing;

        if (disposing)
        {
            UnloadContent();
            Content.Dispose();
        }

        State = SceneState.Disposed;
    }

    /// <summary>
    ///     Finalizer for safety - ensures resources are cleaned up even if Dispose() is not called.
    /// </summary>
    ~SceneBase()
    {
        Dispose(false);
    }
}

/// <summary>
///     Minimal IGraphicsDeviceService implementation for ContentManager.
///     ContentManager only needs the GraphicsDevice property.
/// </summary>
internal sealed class GraphicsDeviceServiceShim : IGraphicsDeviceService
{
    public GraphicsDeviceServiceShim(GraphicsDevice graphicsDevice)
    {
        GraphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
    }

    public GraphicsDevice GraphicsDevice { get; }

#pragma warning disable CS0067 // Events are required by interface but never used
    public event EventHandler<EventArgs>? DeviceCreated;
    public event EventHandler<EventArgs>? DeviceDisposing;
    public event EventHandler<EventArgs>? DeviceReset;
    public event EventHandler<EventArgs>? DeviceResetting;
#pragma warning restore CS0067
}
