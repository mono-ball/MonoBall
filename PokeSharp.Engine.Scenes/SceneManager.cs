using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Core.Services;

namespace PokeSharp.Engine.Scenes;

/// <summary>
///     Manages scene transitions and the current active scene.
///     Uses a two-step transition pattern to prevent mid-frame scene changes.
///     Implements IInputBlocker to allow systems to check if input is blocked by a stacked scene.
/// </summary>
public class SceneManager : IInputBlocker
{
    private readonly ILogger<SceneManager> _logger;
    private IScene? _currentScene;
    private IScene? _nextScene;
    private bool _isPushOperation;
    private bool _popRequested;
    private readonly Stack<IScene> _sceneStack = new();

    /// <summary>
    ///     Initializes a new instance of the SceneManager class.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device (required for creating scene ContentManager instances).</param>
    /// <param name="services">The service provider for dependency injection.</param>
    /// <param name="logger">The logger for scene transitions.</param>
    public SceneManager(
        Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice,
        IServiceProvider services,
        ILogger<SceneManager> logger
    )
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(logger);

        GraphicsDevice = graphicsDevice;
        Services = services;
        _logger = logger;
    }

    /// <summary>
    ///     Gets the graphics device.
    /// </summary>
    public Microsoft.Xna.Framework.Graphics.GraphicsDevice GraphicsDevice { get; }

    /// <summary>
    ///     Gets the service provider.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    ///     Gets the current active scene.
    ///     If a scene stack has items, returns the top of the stack.
    ///     Otherwise, returns the current scene.
    /// </summary>
    public IScene? CurrentScene
    {
        get
        {
            if (_sceneStack.Count > 0)
                return _sceneStack.Peek();

            return _currentScene;
        }
    }

    /// <summary>
    ///     Gets a value indicating whether input is blocked for scenes below the top of the stack.
    ///     Returns true if there is a stacked scene with ExclusiveInput = true.
    ///     Scenes should check this property before processing input if they are updated
    ///     while a scene above them has exclusive input.
    /// </summary>
    public bool IsInputBlocked
    {
        get
        {
            // If there's at least one stacked scene with ExclusiveInput, input is blocked for scenes below
            return _sceneStack.Count > 0 && _sceneStack.Any(s => s.ExclusiveInput);
        }
    }

    /// <summary>
    ///     Changes to a new scene. The transition will occur at the start of the next Update cycle.
    /// </summary>
    /// <param name="newScene">The scene to transition to.</param>
    public void ChangeScene(IScene newScene)
    {
        ArgumentNullException.ThrowIfNull(newScene);

        _logger.LogInformation("Queuing scene change to {SceneType}", newScene.GetType().Name);
        _nextScene = newScene;
        _isPushOperation = false;
    }

    /// <summary>
    ///     Pushes a scene onto the stack (for overlays like pause menus).
    ///     The transition will occur at the start of the next Update cycle.
    /// </summary>
    /// <param name="scene">The scene to push onto the stack.</param>
    public void PushScene(IScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        _logger.LogInformation("Queuing scene push to stack: {SceneType}", scene.GetType().Name);
        _nextScene = scene;
        _isPushOperation = true;
    }

    /// <summary>
    ///     Pops a scene from the stack. The transition will occur at the start of the next Update cycle.
    /// </summary>
    public void PopScene()
    {
        if (_sceneStack.Count == 0)
        {
            _logger.LogWarning("Attempted to pop scene from empty stack");
            return;
        }

        _logger.LogInformation("Queuing scene pop from stack");
        _popRequested = true;
    }

    /// <summary>
    ///     Processes a pending pop operation. Called during Update.
    /// </summary>
    private void ProcessPopScene()
    {
        if (_sceneStack.Count == 0)
        {
            _logger.LogWarning("Pop requested but stack is empty");
            return;
        }

        _logger.LogInformation("Popping scene from stack");
        var previousScene = _sceneStack.Pop();
        previousScene.Dispose();

        // If stack is now empty, we need to handle this
        // For now, we'll keep the previous scene active
        if (_sceneStack.Count == 0 && _currentScene != null)
        {
            _logger.LogInformation("Stack is now empty, keeping current scene active");
        }
    }

    /// <summary>
    ///     Updates the current scene and handles scene transitions.
    ///     Transitions are processed at the start of the update cycle.
    /// </summary>
    /// <param name="gameTime">Provides timing information.</param>
    public void Update(GameTime gameTime)
    {
        // Handle pop request first (before push/change)
        if (_popRequested)
        {
            _popRequested = false;
            ProcessPopScene();
        }

        // Handle scene transition at START of update cycle (two-step pattern)
        if (_nextScene != null)
        {
            try
            {
                var sceneToTransition = _nextScene;
                var isPush = _isPushOperation;
                _nextScene = null; // Clear before processing to prevent re-entry
                _isPushOperation = false;

                if (isPush)
                {
                    // Push to stack
                    _sceneStack.Push(sceneToTransition);
                    sceneToTransition.Initialize();
                    // Manually call LoadContent() since MonoGame only does this for the main Game class
                    sceneToTransition.LoadContent();
                    _logger.LogInformation("Scene {SceneType} pushed onto stack", sceneToTransition.GetType().Name);
                }
                else
                {
                    // Clear stack when changing base scene
                    while (_sceneStack.Count > 0)
                    {
                        var stackedScene = _sceneStack.Pop();
                        stackedScene.Dispose();
                    }

                    // Dispose current scene if it exists
                    if (_currentScene != null)
                    {
                        _currentScene.Dispose();
                        _currentScene = null;
                    }

                    // Set new scene and initialize it
                    _currentScene = sceneToTransition;
                    _currentScene.Initialize();
                    // Manually call LoadContent() since MonoGame only does this for the main Game class
                    _currentScene.LoadContent();

                    _logger.LogInformation("Scene transitioned to {SceneType}", _currentScene.GetType().Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to transition to new scene");
                _nextScene = null;
                _isPushOperation = false;
                // Keep current scene active on error (if it exists)
                if (_currentScene == null && _sceneStack.Count == 0)
                {
                    _logger.LogCritical("No current scene available after failed transition");
                }
            }
        }

        // Update scenes based on UpdateScenesBelow property
        if (_sceneStack.Count > 0)
        {
            // Get the top scene (most recently pushed)
            var topScene = _sceneStack.Peek();

            // If top scene allows scenes below to update, update from bottom to top
            if (topScene.UpdateScenesBelow)
            {
                // Update base scene first
                if (_currentScene != null)
                {
                    _currentScene.Update(gameTime);
                }

                // Update all stacked scenes in order
                foreach (var scene in _sceneStack)
                {
                    scene.Update(gameTime);
                }
            }
            else
            {
                // Top scene pauses lower scenes, only update it
                topScene.Update(gameTime);
            }
        }
        else
        {
            // No stacked scenes, just update the current scene
            _currentScene?.Update(gameTime);
        }
    }

    /// <summary>
    ///     Draws all active scenes in order, respecting each scene's RenderScenesBelow property.
    ///     Scenes with RenderScenesBelow=true will render the scene(s) below them first.
    ///     Scenes with RenderScenesBelow=false will only render themselves (full-screen).
    ///     Always draws, even during initialization, so loading scenes can render.
    /// </summary>
    /// <param name="gameTime">Provides timing information.</param>
    public void Draw(GameTime gameTime)
    {
        // Take a snapshot of the stack to avoid "collection was modified" errors
        // This is important because scene rendering might trigger state changes
        var stackSnapshot = _sceneStack.ToArray();

        // If we have stacked scenes, determine what to render based on the top scene
        if (stackSnapshot.Length > 0)
        {
            // Get the top scene (most recently pushed - first in array since Stack iterates LIFO)
            var topScene = stackSnapshot[0];

            // If top scene wants to render scenes below, render from bottom to top
            if (topScene.RenderScenesBelow)
            {
                // Render base scene first
                if (_currentScene != null)
                {
                    _currentScene.Draw(gameTime);
                }

                // Render all stacked scenes in order (bottom to top = reverse of snapshot)
                for (var i = stackSnapshot.Length - 1; i >= 0; i--)
                {
                    stackSnapshot[i].Draw(gameTime);
                }
            }
            else
            {
                // Top scene is full-screen, only render it (and any above it that are also full-screen)
                // Find the first full-screen scene from the bottom
                var foundFullScreen = false;
                for (var i = stackSnapshot.Length - 1; i >= 0; i--)
                {
                    var scene = stackSnapshot[i];
                    if (!scene.RenderScenesBelow)
                    {
                        // Found first full-screen scene, render from here up (to index 0)
                        for (var j = i; j >= 0; j--)
                        {
                            stackSnapshot[j].Draw(gameTime);
                        }
                        foundFullScreen = true;
                        break;
                    }
                }

                // If no full-screen scene found (shouldn't happen since top is full-screen), render top
                if (!foundFullScreen)
                {
                    topScene.Draw(gameTime);
                }
            }
        }
        else
        {
            // No stacked scenes, just render the current scene
            _currentScene?.Draw(gameTime);
        }
    }
}

