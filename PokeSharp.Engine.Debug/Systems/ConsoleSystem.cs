using Arch.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Engine.Core.Systems;
using PokeSharp.Engine.Debug.Console.Configuration;
using PokeSharp.Engine.Debug.Console.Features;
using PokeSharp.Engine.Debug.Console.Scripting;
using PokeSharp.Engine.Debug.Console.UI;
using PokeSharp.Engine.Debug.Logging;
using PokeSharp.Engine.Debug.Scenes;
using PokeSharp.Engine.Debug.Scripting;
using PokeSharp.Engine.Debug.Systems.Services;
using PokeSharp.Engine.Scenes;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Game.Scripting.Api;
using static PokeSharp.Engine.Debug.Console.Configuration.ConsoleColors;

namespace PokeSharp.Engine.Debug.Systems;

/// <summary>
/// Console system that manages the Quake-style debug console as a scene.
/// Monitors for toggle key and pushes/pops ConsoleScene onto the scene stack.
/// </summary>
public class ConsoleSystem : IUpdateSystem
{
    private readonly ILogger _logger;
    private readonly World _world;
    private readonly IScriptingApiProvider _apiProvider;
    private readonly SystemManager _systemManager;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly SceneManager _sceneManager;
    private readonly IServiceProvider _services;
    private readonly ConsoleLoggerProvider? _loggerProvider;

    // Core console components
    private QuakeConsole _console = null!;
    private ConsoleScriptEvaluator _evaluator = null!;
    private ConsoleGlobals _globals = null!;

    // Services (following Single Responsibility Principle)
    private IConsoleInputHandler _inputHandler = null!;
    private IConsoleCommandExecutor _commandExecutor = null!;
    private IConsoleAutoCompleteCoordinator _autoCompleteCoordinator = null!;
    private ConsoleCommandHistory _history = null!;

    // State tracking
    private KeyboardState _previousKeyboardState;
    private bool _isConsoleOpen;
    private ConsoleScene? _currentConsoleScene;
    private bool _isProcessingCommand;
    private bool _isProcessingAutoComplete;

    // IUpdateSystem properties
    public int Priority => ConsoleConstants.System.UpdatePriority;
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the console system.
    /// </summary>
    public ConsoleSystem(
        World world,
        IScriptingApiProvider apiProvider,
        GraphicsDevice graphicsDevice,
        SystemManager systemManager,
        SceneManager sceneManager,
        IServiceProvider services,
        ILogger logger,
        ConsoleLoggerProvider? loggerProvider = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _apiProvider = apiProvider ?? throw new ArgumentNullException(nameof(apiProvider));
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _systemManager = systemManager ?? throw new ArgumentNullException(nameof(systemManager));
        _sceneManager = sceneManager ?? throw new ArgumentNullException(nameof(sceneManager));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerProvider = loggerProvider;

        _previousKeyboardState = Keyboard.GetState();
    }

    public void Initialize(World world)
    {
        try
        {
            // Create console configuration
            var config = new ConsoleConfig
            {
                Size = ConsoleSize.Medium,
                FontSize = 16,
                SyntaxHighlightingEnabled = true,
                AutoCompleteEnabled = true,
                PersistHistory = true
            };

            // Create console UI (starts hidden)
            var viewport = _graphicsDevice.Viewport;
            _console = new QuakeConsole(_graphicsDevice, viewport.Width, viewport.Height, config);

            // Create script evaluator
            _evaluator = new ConsoleScriptEvaluator(_logger);

            // Create console globals (script API)
            _globals = new ConsoleGlobals(_apiProvider, _world, _systemManager, _graphicsDevice, _logger);

            // Create history and persistence
            var history = new ConsoleCommandHistory();
            var persistence = new ConsoleHistoryPersistence(_logger);
            _history = history;

            // Create auto-complete
            var autoComplete = new ConsoleAutoComplete(_logger);
            autoComplete.SetGlobals(_globals);
            autoComplete.SetReferences(
                ConsoleScriptEvaluator.GetDefaultReferences(),
                ConsoleScriptEvaluator.GetDefaultImports()
            );

            // Create parameter hint provider
            var parameterHintProvider = new ParameterHintProvider(_logger);
            parameterHintProvider.SetGlobals(_globals);
            parameterHintProvider.SetReferences(
                ConsoleScriptEvaluator.GetDefaultReferences(),
                ConsoleScriptEvaluator.GetDefaultImports()
            );

            // Create script manager
            var scriptManager = new ScriptManager(logger: _logger);

            // Create alias manager
            var aliasesPath = Path.Combine(scriptManager.ScriptsDirectory, "aliases.txt");
            var aliasMacroManager = new AliasMacroManager(aliasesPath, _logger);

            // Create bookmarks manager
            var bookmarksPath = Path.Combine(scriptManager.ScriptsDirectory, ConsoleConstants.Files.BookmarksFileName);
            var bookmarksManager = new BookmarkedCommandsManager(bookmarksPath, _logger);

            // Create history suggestion provider
            var historySuggestionProvider = new HistorySuggestionProvider(history);

            // Create output exporter
            var outputExporter = new OutputExporter(scriptManager.ScriptsDirectory);

            // Initialize services
            _autoCompleteCoordinator = new ConsoleAutoCompleteCoordinator(_console, autoComplete, historySuggestionProvider, _logger);
            _inputHandler = new ConsoleInputHandler(_console, history, persistence, _logger, _autoCompleteCoordinator, parameterHintProvider, bookmarksManager);
            _commandExecutor = new ConsoleCommandExecutor(_console, _evaluator, _globals, aliasMacroManager, scriptManager, outputExporter, history, _logger, bookmarksManager);

            // Load persisted data
            var loadedHistory = persistence.LoadHistory();
            history.LoadHistory(loadedHistory);
            _logger.LogInformation("Loaded {Count} commands from history", loadedHistory.Count);

            aliasMacroManager.LoadAliases();
            bookmarksManager.LoadBookmarks();

            // Set up console logger if provided
            if (_loggerProvider != null)
            {
                _loggerProvider.SetConsoleWriter((message, color) =>
                {
                    // Write logs to console buffer even when closed, so they're visible when reopened
                    if (_console != null && _console.Config.LoggingEnabled)
                    {
                        _console.AppendOutput(message, color);
                    }
                });
                
                // Set up log level filter based on console config
                _loggerProvider.SetLogLevelFilter(logLevel =>
                {
                    if (_console == null) return false;
                    return logLevel >= _console.Config.MinimumLogLevel;
                });
            }

            // Load startup script if enabled
            if (config.AutoLoadStartupScript)
            {
                _ = LoadStartupScriptAsync(scriptManager, config.StartupScriptName);
            }

            // Welcome message with better formatting
            _console.AppendOutput("╔════════════════════════════════════════════════════════════╗", Primary);
            _console.AppendOutput("║              PokeSharp Debug Console                      ║", Info_Dim);
            _console.AppendOutput("╚════════════════════════════════════════════════════════════╝", Primary);
            _console.AppendOutput("", Text_Primary);
            _console.AppendOutput("  Execute C# code in real-time  •  Full game API access", Text_Secondary);
            _console.AppendOutput("", Text_Primary);
            _console.AppendOutput("Quick Start:", Primary);
            _console.AppendOutput("  • Type 'help' for commands and shortcuts", Text_Secondary);
            _console.AppendOutput("  • Type 'Help()' for available API methods", Text_Secondary);
            _console.AppendOutput("  • Press Tab or Ctrl+Space for auto-complete", Text_Secondary);
            _console.AppendOutput("  • Try: Player.GetMoney()", Success);
            _console.AppendOutput("", Text_Primary);
            _console.AppendOutput("Features:", Primary);
            _console.AppendOutput($"  Console size: {config.Size,-15} Syntax highlighting: {(config.SyntaxHighlightingEnabled ? "ON " : "OFF")}", Success);
            _console.AppendOutput($"  Auto-complete: {(config.AutoCompleteEnabled ? "ON " : "OFF"),-14} History: {(config.PersistHistory ? "Persistent" : "Session")}", Success);
            _console.AppendOutput("", Text_Primary);

            _logger.LogInformation("Console system initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize console system");
        }
    }

    public void Update(World world, float deltaTime)
    {
        if (_console == null || !Enabled)
            return;

        try
        {
            // Check for console toggle key (`) when console is closed (not ~ with shift)
            // When console is open, ConsoleScene handles the toggle
            if (!_isConsoleOpen)
            {
                var currentKeyboard = Keyboard.GetState();
                bool isShiftPressed = currentKeyboard.IsKeyDown(Keys.LeftShift) || currentKeyboard.IsKeyDown(Keys.RightShift);
                bool togglePressed = currentKeyboard.IsKeyDown(Keys.OemTilde) && 
                                     _previousKeyboardState.IsKeyUp(Keys.OemTilde) &&
                                     !isShiftPressed;

                if (togglePressed)
                {
                    ToggleConsole();
                }

                _previousKeyboardState = currentKeyboard;
            }
            // Note: When console is open, input handling is done by ConsoleScene
            // via the HandleConsoleInput callback
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating console system");
        }
    }

    /// <summary>
    /// Toggles the console scene on/off.
    /// </summary>
    private void ToggleConsole()
    {
        if (_isConsoleOpen)
        {
            // Close console - pop the scene
            _logger.LogDebug("Closing console");
            _sceneManager.PopScene();
            CloseConsole();
        }
        else
        {
            // Open console - push the scene
            _logger.LogDebug("Opening console");
            var consoleLogger = _services.GetRequiredService<ILogger<ConsoleScene>>();

            // Create console scene with proper dependencies (no callbacks)
            _currentConsoleScene = new ConsoleScene(
                _graphicsDevice,
                _services,
                consoleLogger,
                _console,
                _inputHandler,
                _autoCompleteCoordinator,
                _history,
                _sceneManager);

            // Subscribe to scene events
            _currentConsoleScene.CommandRequested += OnConsoleCommandRequested;
            _currentConsoleScene.Closed += OnConsoleClosed;

            _sceneManager.PushScene(_currentConsoleScene);
            _isConsoleOpen = true;
        }
    }

    /// <summary>
    /// Handles the console scene being closed.
    /// </summary>
    private void OnConsoleClosed()
    {
        _logger.LogDebug("Console closed event received");
        CloseConsole();
    }

    /// <summary>
    /// Cleans up console state and unsubscribes from events.
    /// </summary>
    private void CloseConsole()
    {
        _isConsoleOpen = false;

        // Unsubscribe from scene events
        if (_currentConsoleScene != null)
        {
            _currentConsoleScene.CommandRequested -= OnConsoleCommandRequested;
            _currentConsoleScene.Closed -= OnConsoleClosed;
            _currentConsoleScene = null;
        }
    }

    /// <summary>
    /// Handles command execution requests from the console scene.
    /// </summary>
    private void OnConsoleCommandRequested(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return;

        _logger.LogDebug("Command requested from console scene: {Command}", command);
        _ = ExecuteCommandAsync(command);
    }

    /// <summary>
    /// Executes a console command.
    /// </summary>
    private async Task ExecuteCommandAsync(string command)
    {
        if (_isProcessingCommand)
            return;

        _isProcessingCommand = true;

        try
        {
            // Add command to history BEFORE execution so it's available immediately
            _history.Add(command);
            _logger.LogDebug("Added command to history: {Command}", command);

            var result = await _commandExecutor.ExecuteAsync(command);

            // Update auto-complete with latest script state
            if (!result.IsBuiltInCommand)
            {
                _autoCompleteCoordinator.UpdateScriptState(_evaluator.CurrentState);
            }

            _console.ClearInput();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command");
        }
        finally
        {
            _isProcessingCommand = false;
        }
    }

    /// <summary>
    /// Triggers auto-completion for the current input.
    /// Protected against race conditions with concurrent execution flag.
    /// </summary>
    private async Task TriggerAutoCompleteAsync()
    {
        // Prevent concurrent autocomplete requests (race condition protection)
        if (_isProcessingAutoComplete)
        {
            _logger.LogDebug("Auto-complete already in progress, skipping request");
            return;
        }

        _isProcessingAutoComplete = true;

        try
        {
            var code = _console.GetInputText();
            var cursorPos = _console.Input.CursorPosition;

            // Show loading indicator (for async operations that may take time)
            _console.SetAutoCompleteLoading(true);

            var suggestions = await _autoCompleteCoordinator.GetCompletionsAsync(code, cursorPos);

            if (suggestions.Count > 0)
            {
                // Pass the current code to SetAutoCompleteSuggestions for initial filtering
                // This will also clear the loading state
                _console.SetAutoCompleteSuggestions(suggestions, code);
            }
            else
            {
                // Clear loading state if no suggestions found
                _console.SetAutoCompleteLoading(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering auto-complete");
            _console.SetAutoCompleteLoading(false);
        }
        finally
        {
            _isProcessingAutoComplete = false;
        }
    }

    /// <summary>
    /// Loads the startup script if it exists.
    /// </summary>
    private async Task LoadStartupScriptAsync(ScriptManager scriptManager, string scriptName)
    {
        try
        {
            if (!scriptManager.ScriptExists(scriptName))
            {
                _logger.LogDebug("Startup script not found: {ScriptName}", scriptName);
                return;
            }

            var scriptResult = scriptManager.LoadScript(scriptName);
            if (scriptResult.IsSuccess)
            {
                _logger.LogInformation("Loading startup script: {ScriptName}", scriptName);
                var result = await _evaluator.EvaluateAsync(scriptResult.Value!, _globals);

                if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Output) && result.Output != "null")
                {
                    _console.AppendOutput($"Startup: {result.Output}", Success);
                }
                else if (result.IsCompilationError)
                {
                    _console.AppendOutput($"Startup script has compilation errors", Error);
                }
            }
            else
            {
                _logger.LogDebug("Startup script not available: {Error}", scriptResult.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load startup script: {ScriptName}", scriptName);
        }
    }
}
