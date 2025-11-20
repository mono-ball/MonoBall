using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Engine.Debug.Console.Features;
using PokeSharp.Engine.Debug.Console.UI;
using PokeSharp.Engine.Debug.Systems.Services;
using PokeSharp.Engine.Scenes;

namespace PokeSharp.Engine.Debug.Scenes;

/// <summary>
///     Scene that displays the Quake-style debug console as an overlay.
///     Renders the scene below and captures all input.
///     Follows dependency injection pattern by receiving services instead of callbacks.
/// </summary>
public class ConsoleScene : SceneBase
{
    private readonly QuakeConsole _console;
    private readonly IConsoleInputHandler _inputHandler;
    private readonly IConsoleAutoCompleteCoordinator _autoCompleteCoordinator;
    private readonly ConsoleCommandHistory _history;
    private readonly SceneManager _sceneManager;

    private KeyboardState _previousKeyboardState;
    private MouseState _previousMouseState;
    private bool _isProcessingAutoComplete;

    /// <summary>
    ///     Initializes a new instance of the ConsoleScene class.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device.</param>
    /// <param name="services">The service provider.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="console">The QuakeConsole UI instance.</param>
    /// <param name="inputHandler">The console input handler service.</param>
    /// <param name="autoCompleteCoordinator">The auto-complete coordinator service.</param>
    /// <param name="history">The command history service.</param>
    /// <param name="sceneManager">The scene manager for closing the console.</param>
    public ConsoleScene(
        GraphicsDevice graphicsDevice,
        IServiceProvider services,
        ILogger<ConsoleScene> logger,
        QuakeConsole console,
        IConsoleInputHandler inputHandler,
        IConsoleAutoCompleteCoordinator autoCompleteCoordinator,
        ConsoleCommandHistory history,
        SceneManager sceneManager
    )
        : base(graphicsDevice, services, logger)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _inputHandler = inputHandler ?? throw new ArgumentNullException(nameof(inputHandler));
        _autoCompleteCoordinator = autoCompleteCoordinator ?? throw new ArgumentNullException(nameof(autoCompleteCoordinator));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _sceneManager = sceneManager ?? throw new ArgumentNullException(nameof(sceneManager));

        _previousKeyboardState = Keyboard.GetState();
        _previousMouseState = Mouse.GetState();
    }

    /// <summary>
    ///     Gets whether the scene below should be rendered.
    ///     Console is an overlay, so we render the game scene below.
    /// </summary>
    public override bool RenderScenesBelow => true;

    /// <summary>
    ///     Gets whether this scene takes exclusive input.
    ///     Console captures all input, preventing it from reaching the game scene below.
    /// </summary>
    public override bool ExclusiveInput => true;

    /// <summary>
    ///     Initializes the console scene.
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();

        // Show the console when scene is pushed
        _console.Show();
        Logger.LogDebug("Console scene initialized and console shown");
    }

    /// <summary>
    ///     Updates the console scene.
    /// </summary>
    /// <param name="gameTime">Provides timing information.</param>
    public override void Update(GameTime gameTime)
    {
        var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var currentKeyboard = Keyboard.GetState();
        var currentMouse = Mouse.GetState();

        // Check for console toggle key (`) only (not ~ with shift)
        bool isShiftPressed = currentKeyboard.IsKeyDown(Keys.LeftShift) || currentKeyboard.IsKeyDown(Keys.RightShift);
        if (WasKeyPressed(currentKeyboard, Keys.OemTilde) && !isShiftPressed)
        {
            // Close the console scene by popping from stack
            Logger.LogDebug("Console toggle key pressed, closing console");
            OnClosed(); // Notify subscribers before closing
            _sceneManager.PopScene();
            return;
        }

        try
        {
            // Handle console input using the input handler service
            var inputResult = _inputHandler.HandleInput(
                deltaTime,
                currentKeyboard,
                _previousKeyboardState,
                currentMouse,
                _previousMouseState);

            // Handle input result
            if (inputResult.ShouldCloseConsole)
            {
                // Input handler wants to close console (e.g., Escape key pressed)
                Logger.LogDebug("Input handler requested console close");
                OnClosed();
                _sceneManager.PopScene();
                return;
            }

            if (inputResult.ShouldExecuteCommand && !string.IsNullOrWhiteSpace(inputResult.Command))
            {
                // Command execution will be handled by ConsoleSystem via event/callback
                // Scene raises event that system listens to
                OnCommandRequested(inputResult.Command);
            }

            if (inputResult.ShouldTriggerAutoComplete)
            {
                _ = TriggerAutoCompleteAsync();
            }

            // Update auto-complete coordinator timing
            _autoCompleteCoordinator.Update(deltaTime);

            // Check for delayed auto-complete
            if (_autoCompleteCoordinator.ShouldTriggerDelayedAutoComplete())
            {
                _ = TriggerAutoCompleteAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling console input in scene");
        }

        // Update console animation and processing
        _console.Update(deltaTime);

        // Store previous states
        _previousKeyboardState = currentKeyboard;
        _previousMouseState = currentMouse;
    }

    /// <summary>
    ///     Event raised when a command should be executed.
    ///     ConsoleSystem subscribes to this event.
    /// </summary>
    public event Action<string>? CommandRequested;

    /// <summary>
    ///     Event raised when the console scene is closed.
    ///     ConsoleSystem subscribes to this event to reset its state.
    /// </summary>
    public event Action? Closed;

    /// <summary>
    ///     Raises the CommandRequested event.
    /// </summary>
    private void OnCommandRequested(string command)
    {
        CommandRequested?.Invoke(command);
    }

    /// <summary>
    ///     Raises the Closed event.
    /// </summary>
    private void OnClosed()
    {
        Closed?.Invoke();
    }

    /// <summary>
    ///     Triggers auto-completion for the current input.
    /// </summary>
    private async Task TriggerAutoCompleteAsync()
    {
        if (_isProcessingAutoComplete)
        {
            Logger.LogDebug("Auto-complete already in progress, skipping request");
            return;
        }

        _isProcessingAutoComplete = true;

        try
        {
            var code = _console.GetInputText();
            var cursorPos = _console.Input.CursorPosition;

            _console.SetAutoCompleteLoading(true);

            var suggestions = await _autoCompleteCoordinator.GetCompletionsAsync(code, cursorPos);

            if (suggestions.Count > 0)
            {
                _console.SetAutoCompleteSuggestions(suggestions, code);
            }
            else
            {
                _console.SetAutoCompleteLoading(false);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error triggering auto-complete");
            _console.SetAutoCompleteLoading(false);
        }
        finally
        {
            _isProcessingAutoComplete = false;
        }
    }

    /// <summary>
    ///     Draws the console overlay.
    ///     The game scene below is rendered first (handled by SceneManager).
    /// </summary>
    /// <param name="gameTime">Provides timing information.</param>
    public override void Draw(GameTime gameTime)
    {
        // No need to clear - we're drawing on top of the scene below
        _console.Render();
    }

    /// <summary>
    ///     Cleans up console resources.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _console.Hide();
            // Note: Don't dispose _console here as it's owned by ConsoleSystem
            Logger.LogDebug("Console scene disposed");

            // Raise Closed event to notify ConsoleSystem
            OnClosed();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    ///     Checks if a key was just pressed (not held).
    /// </summary>
    private bool WasKeyPressed(KeyboardState currentState, Keys key)
    {
        return currentState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
    }
}

