using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoBallFramework.Game.Engine.Core.Services;

namespace MonoBallFramework.Game.Engine.Scenes;

/// <summary>
///     Manages scene transitions and the current active scene.
///     Uses a two-step transition pattern to prevent mid-frame scene changes.
///     Implements IInputBlocker to allow systems to check if input is blocked by a stacked scene.
/// </summary>
public class SceneManager : IInputBlocker
{
    private readonly ILogger<SceneManager> _logger;
    private readonly Stack<IScene> _sceneStack = [];
    private IScene? _currentScene;
    private bool _isPushOperation;
    private IScene? _nextScene;
    private bool _popRequested;
    private IScene? _sceneToRemove;

    /// <summary>
    ///     Initializes a new instance of the SceneManager class.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device (required for creating scene ContentManager instances).</param>
    /// <param name="services">The service provider for dependency injection.</param>
    /// <param name="logger">The logger for scene transitions.</param>
    public SceneManager(
        GraphicsDevice graphicsDevice,
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
    public GraphicsDevice GraphicsDevice { get; }

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
            return _sceneStack.Count > 0 ? _sceneStack.Peek() : _currentScene;
        }
    }

    /// <summary>
    ///     Gets a value indicating whether input is blocked for scenes below the top of the stack.
    ///     Returns true if there is a stacked scene with ExclusiveInput = true.
    ///     Scenes should check this property before processing input if they are updated
    ///     while a scene above them has exclusive input.
    /// </summary>
    public bool IsInputBlocked =>
        // If there's at least one stacked scene with ExclusiveInput, input is blocked for scenes below
        _sceneStack.Count > 0
        && _sceneStack.Any(s => s.ExclusiveInput);

    /// <summary>
    ///     Checks if a scene of the specified type exists on the scene stack.
    /// </summary>
    /// <typeparam name="T">The type of scene to check for.</typeparam>
    /// <returns>True if a scene of type T exists on the stack, false otherwise.</returns>
    public bool HasSceneOfType<T>() where T : IScene
    {
        return _sceneStack.Any(s => s is T);
    }

    /// <summary>
    ///     Removes all scenes of the specified type from the scene stack.
    ///     This is useful for preventing duplicate overlays (e.g., multiple popups).
    /// </summary>
    /// <typeparam name="T">The type of scene to remove.</typeparam>
    /// <returns>The number of scenes removed.</returns>
    public int RemoveScenesOfType<T>() where T : IScene
    {
        if (_sceneStack.Count == 0)
        {
            return 0;
        }

        // Find all scenes of type T
        var scenesToRemove = _sceneStack.Where(s => s is T).ToList();

        if (scenesToRemove.Count == 0)
        {
            return 0;
        }

        // Create new stack without the scenes to remove
        var tempStack = new Stack<IScene>();

        // Pop all scenes into temp stack
        while (_sceneStack.Count > 0)
        {
            IScene scene = _sceneStack.Pop();

            // Only keep if not in removal list
            if (!scenesToRemove.Contains(scene))
            {
                tempStack.Push(scene);
            }
            else
            {
                // Dispose the removed scene
                scene.Dispose();
                _logger.LogInformation(
                    "Removed and disposed scene of type {SceneType} from stack",
                    scene.GetType().Name
                );
            }
        }

        // Restore stack (in reverse order to maintain original order)
        while (tempStack.Count > 0)
        {
            _sceneStack.Push(tempStack.Pop());
        }

        return scenesToRemove.Count;
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
    ///     Removes a specific scene from the stack. The removal will occur at the start of the next Update cycle.
    ///     This is safer than PopScene() when overlay scenes may have been pushed on top,
    ///     as it ensures only the intended scene is removed.
    /// </summary>
    /// <param name="scene">The specific scene instance to remove from the stack.</param>
    public void RemoveScene(IScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        if (_sceneStack.Count == 0)
        {
            _logger.LogWarning("Attempted to remove scene {SceneType} from empty stack", scene.GetType().Name);
            return;
        }

        if (!_sceneStack.Contains(scene))
        {
            _logger.LogWarning("Scene {SceneType} not found in stack", scene.GetType().Name);
            return;
        }

        _logger.LogInformation("Queuing removal of specific scene: {SceneType}", scene.GetType().Name);
        _sceneToRemove = scene;
    }

    /// <summary>
    ///     Processes a pending specific scene removal. Called during Update.
    /// </summary>
    private void ProcessRemoveScene()
    {
        if (_sceneToRemove == null)
        {
            return;
        }

        IScene sceneToRemove = _sceneToRemove;
        _sceneToRemove = null;

        if (!_sceneStack.Contains(sceneToRemove))
        {
            _logger.LogWarning("Scene {SceneType} no longer in stack during removal", sceneToRemove.GetType().Name);
            return;
        }

        _logger.LogInformation("Removing specific scene from stack: {SceneType}", sceneToRemove.GetType().Name);

        // Create new stack without the scene to remove
        var tempStack = new Stack<IScene>();

        // Pop all scenes into temp stack (reverses order)
        while (_sceneStack.Count > 0)
        {
            IScene scene = _sceneStack.Pop();

            // Only keep if not the scene to remove
            if (scene != sceneToRemove)
            {
                tempStack.Push(scene);
            }
        }

        // Restore stack (re-reverses to original order, minus removed scene)
        while (tempStack.Count > 0)
        {
            _sceneStack.Push(tempStack.Pop());
        }

        // Dispose the removed scene
        sceneToRemove.Dispose();
        _logger.LogInformation("Scene {SceneType} removed and disposed", sceneToRemove.GetType().Name);
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
        IScene previousScene = _sceneStack.Pop();
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

        // Handle specific scene removal (before push/change)
        if (_sceneToRemove != null)
        {
            ProcessRemoveScene();
        }

        // Handle scene transition at START of update cycle (two-step pattern)
        if (_nextScene != null)
        {
            try
            {
                IScene? sceneToTransition = _nextScene;
                bool isPush = _isPushOperation;
                _nextScene = null; // Clear before processing to prevent re-entry
                _isPushOperation = false;

                if (isPush)
                {
                    // Push to stack
                    _sceneStack.Push(sceneToTransition);
                    sceneToTransition.Initialize();
                    // Manually call LoadContent() since MonoGame only does this for the main Game class
                    sceneToTransition.LoadContent();
                    _logger.LogInformation(
                        "Scene {SceneType} pushed onto stack",
                        sceneToTransition.GetType().Name
                    );
                }
                else
                {
                    // Clear stack when changing base scene
                    while (_sceneStack.Count > 0)
                    {
                        IScene stackedScene = _sceneStack.Pop();
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

                    _logger.LogInformation(
                        "Scene transitioned to {SceneType}",
                        _currentScene.GetType().Name
                    );
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
            IScene topScene = _sceneStack.Peek();

            // If top scene allows scenes below to update, update from bottom to top
            if (topScene.UpdateScenesBelow)
            {
                // Update base scene first
                _currentScene?.Update(gameTime);

                // Update all stacked scenes in order
                foreach (IScene scene in _sceneStack)
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
        IScene[] stackSnapshot = _sceneStack.ToArray();

        // If we have stacked scenes, determine what to render based on the top scene
        if (stackSnapshot.Length > 0)
        {
            // Get the top scene (most recently pushed - first in array since Stack iterates LIFO)
            IScene topScene = stackSnapshot[0];

            // If top scene wants to render scenes below, render from bottom to top
            if (topScene.RenderScenesBelow)
            {
                // Render base scene first
                _currentScene?.Draw(gameTime);

                // Render all stacked scenes in order (bottom to top = reverse of snapshot)
                for (int i = stackSnapshot.Length - 1; i >= 0; i--)
                {
                    stackSnapshot[i].Draw(gameTime);
                }
            }
            else
            {
                // Top scene is full-screen, only render it (and any above it that are also full-screen)
                // Find the first full-screen scene from the bottom
                bool foundFullScreen = false;
                for (int i = stackSnapshot.Length - 1; i >= 0; i--)
                {
                    IScene scene = stackSnapshot[i];
                    if (!scene.RenderScenesBelow)
                    {
                        // Found first full-screen scene, render from here up (to index 0)
                        for (int j = i; j >= 0; j--)
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
