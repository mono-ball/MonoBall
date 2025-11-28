using System;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace PokeSharp.Engine.Scenes;

/// <summary>
///     Base class for game scenes that provides common functionality.
///     Follows MonoGame's lifecycle pattern with proper disposal.
/// </summary>
public abstract class SceneBase : IScene
{
    private readonly object _lock = new();
    private bool _disposed;
    private bool _isInitialized;
    private bool _isContentLoaded;

    /// <summary>
    ///     Initializes a new instance of the SceneBase class.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device for rendering.</param>
    /// <param name="services">The service provider for dependency injection.</param>
    /// <param name="logger">The logger for this scene.</param>
    /// <param name="contentRootDirectory">The root directory for content loading (default: "Content").</param>
    protected SceneBase(
        GraphicsDevice graphicsDevice,
        IServiceProvider services,
        ILogger logger,
        string contentRootDirectory = "Content"
    )
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrEmpty(contentRootDirectory);

        GraphicsDevice = graphicsDevice;
        Services = services;
        Logger = logger;
        Content = new ContentManager(services, contentRootDirectory);
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
    ///     Gets the service provider for dependency injection.
    /// </summary>
    protected IServiceProvider Services { get; }

    /// <summary>
    ///     Gets the logger for this scene.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    ///     Gets a value indicating whether this scene has been disposed.
    /// </summary>
    public bool IsDisposed
    {
        get
        {
            lock (_lock)
            {
                return _disposed;
            }
        }
        private set
        {
            lock (_lock)
            {
                _disposed = value;
            }
        }
    }

    /// <summary>
    ///     Gets a value indicating whether this scene has been initialized.
    /// </summary>
    public bool IsInitialized
    {
        get
        {
            lock (_lock)
            {
                return _isInitialized;
            }
        }
        private set
        {
            lock (_lock)
            {
                _isInitialized = value;
            }
        }
    }

    /// <summary>
    ///     Gets a value indicating whether this scene's content has been loaded.
    /// </summary>
    public bool IsContentLoaded
    {
        get
        {
            lock (_lock)
            {
                return _isContentLoaded;
            }
        }
        private set
        {
            lock (_lock)
            {
                _isContentLoaded = value;
            }
        }
    }

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
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(SceneBase));

        if (IsInitialized)
            return;

        IsInitialized = true;
        Logger.LogDebug("Scene {SceneType} initialized", GetType().Name);
    }

    /// <summary>
    ///     Loads scene-specific content. Called automatically by MonoGame after Initialize().
    /// </summary>
    public virtual void LoadContent()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(SceneBase));

        if (IsContentLoaded)
            return;

        IsContentLoaded = true;
        Logger.LogDebug("Scene {SceneType} content loaded", GetType().Name);
    }

    /// <summary>
    ///     Unloads scene-specific content. Called when the scene is being disposed.
    /// </summary>
    public virtual void UnloadContent()
    {
        if (IsContentLoaded)
        {
            Content.Unload();
            IsContentLoaded = false;
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
    ///     Disposes the scene and releases all resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (IsDisposed)
            return;

        if (disposing)
        {
            UnloadContent();
            Content.Dispose();
            Logger.LogDebug("Scene {SceneType} disposed", GetType().Name);
        }

        IsDisposed = true;
    }

    /// <summary>
    ///     Finalizer for safety - ensures resources are cleaned up even if Dispose() is not called.
    /// </summary>
    ~SceneBase()
    {
        Dispose(false);
    }
}

