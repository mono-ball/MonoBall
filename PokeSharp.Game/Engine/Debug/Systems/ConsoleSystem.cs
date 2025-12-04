using System.Collections;
using System.Reflection;
using System.Text;
using Arch.Core;
using Arch.Relationships;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Game.Engine.Core.Events;
using PokeSharp.Game.Engine.Core.Services;
using PokeSharp.Game.Engine.Core.Systems;
using PokeSharp.Game.Engine.Debug.Breakpoints;
using PokeSharp.Game.Engine.Debug.Commands;
using PokeSharp.Game.Engine.Debug.Console.Configuration;
using PokeSharp.Game.Engine.Debug.Console.Features;
using PokeSharp.Game.Engine.Debug.Console.Scripting;
using PokeSharp.Game.Engine.Debug.Entities;
using PokeSharp.Game.Engine.Debug.Features;
using PokeSharp.Game.Engine.Debug.Logging;
using PokeSharp.Game.Engine.Debug.Scripting;
using PokeSharp.Game.Engine.Debug.Services;
using PokeSharp.Game.Engine.Scenes;
using PokeSharp.Game.Engine.Systems.Management;
using PokeSharp.Game.Engine.UI.Debug.Components.Controls;
using PokeSharp.Game.Engine.UI.Debug.Components.Debug;
using PokeSharp.Game.Engine.UI.Debug.Core;
using PokeSharp.Game.Engine.UI.Debug.Models;
using PokeSharp.Game.Engine.UI.Debug.Scenes;
using PokeSharp.Game.Engine.UI.Debug.Utilities;
using PokeSharp.Game.Components.Relationships;
using PokeSharp.Game.Scripting.Api;

namespace PokeSharp.Game.Engine.Debug.Systems;

/// <summary>
///     Console system that manages the modern debug console as a scene.
///     Monitors for toggle key and pushes/pops console scene onto the scene stack.
/// </summary>
public class ConsoleSystem : IUpdateSystem
{
    private const int MaxPersistentLogs = 5000;
    private const int CompletionDebounceMs = 50;
    private readonly IScriptingApiProvider _apiProvider;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly object _logBufferLock = new();
    private readonly ILogger _logger;
    private readonly ConsoleLoggerProvider? _loggerProvider;

    // Multi-line input buffer for incomplete statements (like for loops)
    private readonly StringBuilder _multiLineBuffer = new();

    // Persistent log buffer - stores logs even when console is closed
    private readonly List<(
        LogLevel Level,
        string Message,
        string Category,
        DateTime Timestamp
    )> _persistentLogBuffer = new();

    private readonly SceneManager _sceneManager;
    private readonly IServiceProvider _services;
    private readonly SystemManager _systemManager;
    private readonly World _world;

    // Breakpoint manager for conditional game pausing
    private BreakpointManager? _breakpointManager;
    private ConsoleCommandRegistry _commandRegistry = null!;

    // Auto-completion debouncing
    private CancellationTokenSource? _completionCts;
    private ConsoleCompletionProvider _completionProvider = null!;

    // Entity component registry
    private DebugComponentRegistry _componentRegistry = null!;
    private ConsoleScene? _consoleScene;
    private ConsoleDocumentationProvider _documentationProvider = null!;

    // Core console components (shared between console features)
    private ConsoleScriptEvaluator _evaluator = null!;

    // Event Inspector integration
    private EventInspectorAdapter? _eventInspectorAdapter;
    private ConsoleGlobals _globals = null!;
    private bool _isConsoleOpen;
    private bool _isMultiLineMode;

    // Console logging state
    private bool _loggingEnabled;
    private LogLevel _minimumLogLevel = LogLevel.Information;
    private ParameterHintProvider _parameterHintProvider = null!;

    // Console state
    private KeyboardState _previousKeyboardState;
    private List<Assembly>? _referencedAssemblies;

    /// <summary>
    ///     Initializes a new instance of the console system.
    /// </summary>
    public ConsoleSystem(
        World world,
        IScriptingApiProvider apiProvider,
        GraphicsDevice graphicsDevice,
        SystemManager systemManager,
        SceneManager sceneManager,
        IServiceProvider services,
        ILogger logger,
        ConsoleLoggerProvider? loggerProvider = null
    )
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

    // Theme reference
    private static UITheme Theme => UITheme.Dark;

    // IUpdateSystem properties
    public int Priority => ConsoleConstants.System.UpdatePriority;
    public bool Enabled { get; set; } = true;

    public void Initialize(World world)
    {
        try
        {
            // Create command registry
            _commandRegistry = new ConsoleCommandRegistry(_logger);

            // Create component registry for entity detection
            _componentRegistry = DebugComponentRegistryFactory.CreateDefault();

            // Create script evaluator (shared component)
            _evaluator = new ConsoleScriptEvaluator(_logger);

            // Create console globals (script API)
            _globals = new ConsoleGlobals(
                _apiProvider,
                _world,
                _systemManager,
                _graphicsDevice,
                _logger
            );

            // Create breakpoint manager (requires evaluator, globals, and time control)
            ITimeControl? timeControl = GetTimeControl();
            _breakpointManager = new BreakpointManager(_evaluator, _globals, timeControl, _logger);
            _breakpointManager.OnBreakpointHit += OnBreakpointHit;

            // Create parameter hint provider (shared component)
            _parameterHintProvider = new ParameterHintProvider(_logger);
            _parameterHintProvider.SetGlobals(_globals);

            // Create completion provider
            _completionProvider = new ConsoleCompletionProvider(_logger);
            _completionProvider.SetGlobals(_globals);

            // Create documentation provider
            _documentationProvider = new ConsoleDocumentationProvider(_logger);
            _documentationProvider.SetGlobals(_globals);
            _documentationProvider.SetEvaluator(_evaluator);

            // Store references for documentation
            _referencedAssemblies = ConsoleScriptEvaluator.GetDefaultReferences().ToList();
            _documentationProvider.SetReferencedAssemblies(_referencedAssemblies);

            _parameterHintProvider.SetReferences(
                _referencedAssemblies,
                ConsoleScriptEvaluator.GetDefaultImports()
            );

            // Set up console logger if provided
            if (_loggerProvider != null)
            {
                // Logs only go to the Logs panel, not the console output
                // Set up log entry handler for the Logs panel
                _loggerProvider.SetLogEntryHandler(
                    (level, message, category) =>
                    {
                        // Only buffer logs if logging is enabled
                        if (!_loggingEnabled)
                        {
                            return;
                        }

                        // Store in persistent buffer (even when console is closed)
                        lock (_logBufferLock)
                        {
                            _persistentLogBuffer.Add((level, message, category, DateTime.Now));

                            // Trim if buffer is too large
                            while (_persistentLogBuffer.Count > MaxPersistentLogs)
                            {
                                _persistentLogBuffer.RemoveAt(0);
                            }
                        }

                        // Also add to console scene if it's open
                        _consoleScene?.AddLog(level, message, category, DateTime.Now);
                    }
                );

                // Set up log level filter - always capture logs for the persistent buffer
                // The console output writer handles its own visibility check
                _loggerProvider.SetLogLevelFilter(logLevel =>
                {
                    // Always capture logs at or above the minimum level for the persistent buffer
                    return logLevel >= _minimumLogLevel;
                });
            }

            _logger.LogInformation("Console system initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize console system");
        }
    }

    public void Update(World world, float deltaTime)
    {
        if (!Enabled)
        {
            return;
        }

        try
        {
            KeyboardState currentKeyboard = Keyboard.GetState();

            // Check for console toggle key (`) when console is closed
            if (!_isConsoleOpen)
            {
                bool isShiftPressed =
                    currentKeyboard.IsKeyDown(Keys.LeftShift)
                    || currentKeyboard.IsKeyDown(Keys.RightShift);
                bool togglePressed =
                    currentKeyboard.IsKeyDown(Keys.OemTilde)
                    && _previousKeyboardState.IsKeyUp(Keys.OemTilde)
                    && !isShiftPressed;

                if (togglePressed)
                {
                    ToggleConsole();
                }
            }

            _previousKeyboardState = currentKeyboard;

            // Evaluate breakpoints (runs every frame, even when console is closed)
            // This allows breakpoints to pause the game at any time
            _breakpointManager?.EvaluateBreakpoints();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating console system");
        }
    }

    /// <summary>
    ///     Toggles the console scene on/off.
    /// </summary>
    private void ToggleConsole()
    {
        if (_isConsoleOpen)
        {
            // Close console - pop the scene
            if (_consoleScene != null)
            {
                _sceneManager.PopScene();
                _consoleScene.OnCommandSubmitted -= HandleConsoleCommand;
                _consoleScene.OnRequestCompletions -= HandleConsoleCompletions;
                _consoleScene.OnRequestParameterHints -= HandleConsoleParameterHints;
                _consoleScene.OnRequestDocumentation -= HandleConsoleDocumentation;
                _consoleScene.OnCloseRequested -= OnConsoleClosed;
                _consoleScene.OnReady -= HandleConsoleReady;
                _consoleScene = null;

                // Cancel any pending completion requests
                _completionCts?.Cancel();
                _completionCts?.Dispose();
                _completionCts = null;

                // Clear Print() output action
                _globals.OutputAction = null;
            }

            _isConsoleOpen = false;
        }
        else
        {
            // Open console - push the scene
            try
            {
                ILogger<ConsoleScene> consoleLogger = _services.GetRequiredService<
                    ILogger<ConsoleScene>
                >();

                _consoleScene = new ConsoleScene(_graphicsDevice, _services, consoleLogger);

                // Wire up event handlers
                _consoleScene.OnCommandSubmitted += HandleConsoleCommand;
                _consoleScene.OnRequestCompletions += HandleConsoleCompletions;
                _consoleScene.OnRequestParameterHints += HandleConsoleParameterHints;
                _consoleScene.OnRequestDocumentation += HandleConsoleDocumentation;
                _consoleScene.OnCloseRequested += OnConsoleClosed;
                _consoleScene.OnReady += HandleConsoleReady;

                // Wire up Print() output to the console
                _globals.OutputAction = text =>
                    _consoleScene?.AppendOutput(text, Theme.TextPrimary);

                // Set console height to 50% (medium size)
                _consoleScene.SetHeightPercent(0.5f);

                // Push scene - LoadContent() will fire OnReady when complete
                _sceneManager.PushScene(_consoleScene);
                _isConsoleOpen = true;

                // Note: Buffered logs, welcome messages, and startup script are handled in HandleConsoleReady

                _logger.LogInformation("Console opened successfully. Press ` to close.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open console");
                _isConsoleOpen = false;
                _consoleScene = null;
            }
        }
    }

    /// <summary>
    ///     Handles the console scene being closed.
    /// </summary>
    private void OnConsoleClosed()
    {
        if (_isConsoleOpen)
        {
            ToggleConsole();
        }
    }

    /// <summary>
    ///     Handles the console scene being ready (after LoadContent completes).
    ///     This is when LogsPanel exists and can receive buffered logs.
    /// </summary>
    private void HandleConsoleReady()
    {
        // Replay buffered logs if logging is enabled
        if (_loggingEnabled)
        {
            ReplayBufferedLogs();
        }

        // Set up entity provider for the Entities panel
        _consoleScene?.SetEntityProvider(GetAllEntitiesAsInfo);

        // Set up system metrics provider for the Profiler panel
        _consoleScene?.SetSystemMetricsProvider(() => _systemManager.GetMetrics());

        // Set up stats provider for the Stats panel
        _consoleScene?.SetStatsProvider(CreateStatsProvider());

        // Set up Event Inspector provider for the Events panel
        try
        {
            IEventBus eventBus = _services.GetRequiredService<IEventBus>();
            if (eventBus is EventBus concreteEventBus)
            {
                var eventMetrics = new EventMetrics { IsEnabled = true }; // Enabled for Phase 6 testing
                _eventInspectorAdapter = new EventInspectorAdapter(concreteEventBus, eventMetrics);
                _consoleScene?.SetEventInspectorProvider(() =>
                    _eventInspectorAdapter.GetInspectorData()
                );
                _logger.LogDebug("Event Inspector initialized successfully");
            }
            else
            {
                _logger.LogWarning(
                    "EventBus is not the concrete type, Event Inspector will not be available"
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to initialize Event Inspector - feature will be unavailable"
            );
        }

        // Welcome message - use theme colors
        UITheme theme = ThemeManager.Current;
        _consoleScene?.AppendOutput("=== PokeSharp Debug Console ===", theme.ConsolePrimary);
        _consoleScene?.AppendOutput("Type 'help' for available commands", theme.TextSecondary);
        _consoleScene?.AppendOutput("Press ` or type 'exit' to close", theme.TextDim);
        _consoleScene?.AppendOutput("", theme.TextPrimary);

        // Execute startup script if it exists
        ExecuteStartupScript();
    }

    /// <summary>
    ///     Handles a breakpoint being hit.
    /// </summary>
    private void OnBreakpointHit(IBreakpoint breakpoint)
    {
        // Open the console if not already open
        if (!_isConsoleOpen)
        {
            ToggleConsole();
        }

        // Display breakpoint hit message
        UITheme theme = ThemeManager.Current;
        _consoleScene?.AppendOutput("", theme.TextPrimary);
        _consoleScene?.AppendOutput($"⏸ BREAKPOINT #{breakpoint.Id} HIT", theme.Warning);
        _consoleScene?.AppendOutput($"  Condition: {breakpoint.Description}", theme.TextSecondary);
        _consoleScene?.AppendOutput($"  Hit count: {breakpoint.HitCount}", theme.TextSecondary);
        _consoleScene?.AppendOutput("", theme.TextPrimary);
        _consoleScene?.AppendOutput(
            "Game paused. Use 'resume' or 'step' to continue.",
            theme.TextDim
        );
        _consoleScene?.AppendOutput("", theme.TextPrimary);
    }

    /// <summary>
    ///     Handles commands submitted from the console.
    ///     Supports multi-line input for incomplete statements (like for loops).
    /// </summary>
    private void HandleConsoleCommand(string command)
    {
        try
        {
            // Handle multi-line continuation
            if (_isMultiLineMode)
            {
                // Check for empty line to cancel multi-line mode
                if (string.IsNullOrWhiteSpace(command))
                {
                    _consoleScene?.AppendOutput("Multi-line input cancelled.", Theme.TextSecondary);
                    _multiLineBuffer.Clear();
                    _isMultiLineMode = false;
                    _consoleScene?.SetPrompt("> ");
                    return;
                }

                // Add the new line to buffer
                _multiLineBuffer.AppendLine(command);
            }
            else
            {
                // Check if this is a built-in command - if so, execute immediately without multi-line check
                // Built-in commands should not go through C# syntax validation
                if (IsBuiltInCommand(command))
                {
                    _ = ExecuteConsoleCommand(command);
                    return;
                }

                // Start fresh for C# scripting
                _multiLineBuffer.Clear();
                _multiLineBuffer.Append(command);
            }

            string fullCode = _multiLineBuffer.ToString();

            // Check if the code is complete (for multi-line statements like for loops)
            if (!_evaluator.IsCodeComplete(fullCode))
            {
                // Code is incomplete - switch to multi-line mode
                _isMultiLineMode = true;
                _consoleScene?.SetPrompt("... ");
                _logger.LogDebug(
                    "Multi-line mode: waiting for more input. Current: {Code}",
                    fullCode
                );
                return;
            }

            // Code is complete - execute it
            _isMultiLineMode = false;
            _multiLineBuffer.Clear();
            _consoleScene?.SetPrompt("> ");

            _ = ExecuteConsoleCommand(fullCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command from console: {Command}", command);
            _consoleScene?.AppendOutput($"Error: {ex.Message}", Theme.Error);
            _isMultiLineMode = false;
            _multiLineBuffer.Clear();
            _consoleScene?.SetPrompt("> ");
        }
    }

    /// <summary>
    ///     Checks if a command should bypass C# multi-line syntax checking.
    ///     This includes built-in commands, aliases, and command-like inputs (to get proper error messages).
    /// </summary>
    private bool IsBuiltInCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        // Get the first word of the command
        string trimmed = command.TrimStart();
        int spaceIndex = trimmed.IndexOf(' ');
        string firstWord = spaceIndex > 0 ? trimmed[..spaceIndex] : trimmed;

        // Check if it's a registered command
        if (_commandRegistry.GetCommand(firstWord) != null)
        {
            return true;
        }

        // Check if it's an alias
        AliasMacroManager? aliasManager = _services.GetService<AliasMacroManager>();
        if (aliasManager?.TryExpandAlias(firstWord, out _) == true)
        {
            return true;
        }

        // Check if input looks like a command (simple words) rather than C# code
        // This ensures typos like "itme scale 2.0" give an error instead of entering multi-line mode
        if (LooksLikeCommand(trimmed))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Heuristic to detect if input looks like a console command rather than C# code.
    ///     Commands are typically: word [args...] without complex C# syntax.
    /// </summary>
    private static bool LooksLikeCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        // Must start with a letter (command name)
        if (!char.IsLetter(input[0]))
        {
            return false;
        }

        // C# code indicators that suggest this is NOT a simple command
        string[] csharpIndicators = new[]
        {
            "=",
            "{",
            "}",
            "(",
            ")",
            "[",
            "]",
            "=>",
            "++",
            "--",
            "&&",
            "||",
            "<<",
            ">>",
            "::",
        };
        foreach (string indicator in csharpIndicators)
        {
            if (input.Contains(indicator))
            {
                return false;
            }
        }

        // Check for C# keywords that start statements (not command names)
        string firstWord =
            input.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        string[] csharpKeywords = new[]
        {
            "var",
            "int",
            "float",
            "double",
            "string",
            "bool",
            "if",
            "else",
            "for",
            "foreach",
            "while",
            "do",
            "switch",
            "try",
            "catch",
            "finally",
            "throw",
            "return",
            "class",
            "struct",
            "interface",
            "enum",
            "namespace",
            "using",
            "new",
            "public",
            "private",
            "protected",
            "static",
            "async",
            "await",
        };
        if (csharpKeywords.Contains(firstWord, StringComparer.Ordinal))
        {
            return false;
        }

        // Looks like a command
        return true;
    }

    /// <summary>
    ///     Handles auto-completion requests from the console.
    ///     Uses debouncing to prevent excessive requests during fast typing.
    /// </summary>
    private void HandleConsoleCompletions(string partialCommand)
    {
        // Cancel any pending completion request
        _completionCts?.Cancel();
        _completionCts = new CancellationTokenSource();

        // Fire and forget with proper error handling (not async void)
        _ = GetCompletionsWithDebounceAsync(partialCommand, _completionCts.Token);
    }

    /// <summary>
    ///     Gets completions with debouncing to avoid flooding during fast typing.
    /// </summary>
    private async Task GetCompletionsWithDebounceAsync(string partialCommand, CancellationToken ct)
    {
        try
        {
            // Wait for typing to pause (debounce)
            await Task.Delay(CompletionDebounceMs, ct);

            // Get cursor position and completions
            int cursorPosition = _consoleScene?.GetCursorPosition() ?? partialCommand.Length;
            List<SuggestionItem> suggestions = await _completionProvider.GetCompletionsAsync(
                partialCommand,
                cursorPosition
            );

            // Only update UI if this request wasn't cancelled
            if (!ct.IsCancellationRequested)
            {
                _consoleScene?.SetCompletions(suggestions);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when typing quickly - new request cancelled this one
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting completions for: {PartialCommand}", partialCommand);
        }
    }

    /// <summary>
    ///     Handles parameter hint requests from the console.
    /// </summary>
    private void HandleConsoleParameterHints(string text, int cursorPosition)
    {
        try
        {
            // Check if we're inside a method call
            (string MethodName, int OpenParenIndex, int ParameterIndex)? methodCallInfo =
                FindMethodCallAtCursor(text, cursorPosition);
            if (methodCallInfo == null)
            {
                _consoleScene?.ClearParameterHints();
                return;
            }

            // Update parameter hint provider with current script state
            _parameterHintProvider.UpdateScriptState(_evaluator.CurrentState);

            // Get parameter hints from provider (pass text up to the opening paren + opening paren)
            string textForHints = text.Substring(0, methodCallInfo.Value.OpenParenIndex + 1);
            ParameterHintInfo? hints = _parameterHintProvider.GetParameterHints(
                textForHints,
                textForHints.Length
            )!;

            if (hints != null && hints.Overloads.Count > 0)
            {
                // Convert to UI types
                var uiHints = new ParamHints
                {
                    MethodName = hints.MethodName,
                    CurrentOverloadIndex = hints.CurrentOverloadIndex,
                    Overloads = hints
                        .Overloads.Select(overload => new MethodSig
                        {
                            MethodName = overload.MethodName,
                            ReturnType = overload.ReturnType,
                            Parameters = overload
                                .Parameters.Select(param => new ParamInfo
                                {
                                    Name = param.Name ?? string.Empty,
                                    Type = param.Type,
                                    IsOptional = param.IsOptional,
                                    DefaultValue = param.DefaultValue ?? string.Empty,
                                })
                                .ToList(),
                        })
                        .ToList(),
                };

                _consoleScene?.SetParameterHints(uiHints, methodCallInfo.Value.ParameterIndex);
            }
            else
            {
                _consoleScene?.ClearParameterHints();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting parameter hints for console");
        }
    }

    /// <summary>
    ///     Finds the method call that the cursor is currently inside.
    ///     Returns the method name, opening paren position, and current parameter index.
    /// </summary>
    private (string MethodName, int OpenParenIndex, int ParameterIndex)? FindMethodCallAtCursor(
        string text,
        int cursorPosition
    )
    {
        // Find the last unmatched opening parenthesis before the cursor
        int nestLevel = 0;
        int openParenIndex = -1;

        for (int i = cursorPosition - 1; i >= 0; i--)
        {
            char c = text[i];
            if (c == ')')
            {
                nestLevel++;
            }
            else if (c == '(')
            {
                if (nestLevel == 0)
                {
                    openParenIndex = i;
                    break;
                }

                nestLevel--;
            }
        }

        if (openParenIndex == -1)
        {
            return null;
        }

        // Extract method name before the opening paren
        // Handle both direct calls (Method() and member calls (obj.Method())
        int methodStartIndex = openParenIndex - 1;

        // Skip whitespace before paren
        while (methodStartIndex >= 0 && char.IsWhiteSpace(text[methodStartIndex]))
        {
            methodStartIndex--;
        }

        if (methodStartIndex < 0)
        {
            return null;
        }

        // Find the start of the method name (alphanumeric + underscore)
        int methodEndIndex = methodStartIndex;
        while (
            methodStartIndex >= 0
            && (char.IsLetterOrDigit(text[methodStartIndex]) || text[methodStartIndex] == '_')
        )
        {
            methodStartIndex--;
        }

        methodStartIndex++; // Move back to first char of method name

        if (methodStartIndex > methodEndIndex)
        {
            return null;
        }

        string methodName = text.Substring(methodStartIndex, methodEndIndex - methodStartIndex + 1);

        // Count commas between opening paren and cursor to determine parameter index
        int parameterIndex = 0;
        nestLevel = 0;

        for (int i = openParenIndex + 1; i < cursorPosition && i < text.Length; i++)
        {
            char c = text[i];
            if (c == '(')
            {
                nestLevel++;
            }
            else if (c == ')')
            {
                nestLevel--;
            }
            else if (c == ',' && nestLevel == 0)
            {
                parameterIndex++;
            }
        }

        return (methodName, openParenIndex, parameterIndex);
    }

    /// <summary>
    ///     Handles documentation requests from the console.
    /// </summary>
    private void HandleConsoleDocumentation(string completionText)
    {
        DocInfo doc = _documentationProvider.GetDocumentation(completionText);
        _consoleScene?.SetDocumentation(doc);
    }

    /// <summary>
    ///     Executes a command from the console.
    ///     Supports command chaining with semicolons (e.g., "clear; help; time").
    /// </summary>
    private async Task ExecuteConsoleCommand(string command)
    {
        try
        {
            // Check for command chaining (semicolon-separated commands)
            // Only split if not inside quotes
            List<string> chainedCommands = SplitChainedCommands(command);
            if (chainedCommands.Count > 1)
            {
                _logger.LogDebug("Executing {Count} chained commands", chainedCommands.Count);
                foreach (string chainedCmd in chainedCommands)
                {
                    string trimmedCmd = chainedCmd.Trim();
                    if (!string.IsNullOrEmpty(trimmedCmd))
                    {
                        await ExecuteSingleCommand(trimmedCmd);
                    }
                }

                return;
            }

            // Single command - execute directly
            await ExecuteSingleCommand(command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command in console");
            _consoleScene?.AppendOutput($"Error: {ex.Message}", Theme.Error);
        }
    }

    /// <summary>
    ///     Splits a command string by semicolons, respecting quoted strings and nested brackets.
    ///     This ensures that semicolons inside for loops, method calls, etc. are not split points.
    /// </summary>
    private static List<string> SplitChainedCommands(string input)
    {
        var commands = new List<string>();
        var current = new StringBuilder();
        bool inDoubleQuote = false;
        bool inSingleQuote = false;
        int parenDepth = 0; // ()
        int braceDepth = 0; // {}
        int bracketDepth = 0; // []

        foreach (char c in input)
        {
            if (c == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
                current.Append(c);
            }
            else if (c == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
                current.Append(c);
            }
            else if (!inDoubleQuote && !inSingleQuote)
            {
                // Track bracket depth
                switch (c)
                {
                    case '(':
                        parenDepth++;
                        break;
                    case ')':
                        parenDepth--;
                        break;
                    case '{':
                        braceDepth++;
                        break;
                    case '}':
                        braceDepth--;
                        break;
                    case '[':
                        bracketDepth++;
                        break;
                    case ']':
                        bracketDepth--;
                        break;
                }

                // Only split on semicolons at the top level (not inside any brackets)
                if (c == ';' && parenDepth == 0 && braceDepth == 0 && bracketDepth == 0)
                {
                    // Split point - add current command and start new one
                    string cmd = current.ToString().Trim();
                    if (!string.IsNullOrEmpty(cmd))
                    {
                        commands.Add(cmd);
                    }

                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                current.Append(c);
            }
        }

        // Add final command
        string lastCmd = current.ToString().Trim();
        if (!string.IsNullOrEmpty(lastCmd))
        {
            commands.Add(lastCmd);
        }

        return commands;
    }

    /// <summary>
    ///     Executes a single command (no chaining).
    /// </summary>
    private async Task ExecuteSingleCommand(string command)
    {
        try
        {
            // Get dependencies for command execution
            AliasMacroManager aliasManager = _services.GetRequiredService<AliasMacroManager>();
            ScriptManager scriptManager = _services.GetRequiredService<ScriptManager>();
            BookmarkedCommandsManager bookmarkManager =
                _services.GetRequiredService<BookmarkedCommandsManager>();
            WatchPresetManager watchPresetManager =
                _services.GetRequiredService<WatchPresetManager>();

            // Try to expand alias first
            if (aliasManager.TryExpandAlias(command, out string expandedCommand))
            {
                _logger.LogDebug(
                    "Alias expanded: {Original} -> {Expanded}",
                    command,
                    expandedCommand
                );
                _consoleScene?.AppendOutput($"[alias] {expandedCommand}", Theme.TextSecondary);
                command = expandedCommand;

                // Check if expanded alias contains chained commands
                List<string> chainedFromAlias = SplitChainedCommands(command);
                if (chainedFromAlias.Count > 1)
                {
                    // Execute chained commands from alias
                    foreach (string chainedCmd in chainedFromAlias)
                    {
                        string trimmedCmd = chainedCmd.Trim();
                        if (!string.IsNullOrEmpty(trimmedCmd))
                        {
                            await ExecuteSingleCommand(trimmedCmd);
                        }
                    }

                    return;
                }
            }

            // Parse command
            string[] parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return;
            }

            string cmd = parts[0];
            string[] args = parts.Skip(1).ToArray();

            // Create console context for commands using aggregated services
            var loggingCallbacks = new ConsoleLoggingCallbacks(
                () => _loggingEnabled,
                enabled => _loggingEnabled = enabled,
                () => _minimumLogLevel,
                level => _minimumLogLevel = level
            );

            // Get time control from DI (may be null if not registered)
            ITimeControl? timeControl = GetTimeControl();

            var services = new ConsoleServices(
                _commandRegistry,
                aliasManager,
                scriptManager,
                _evaluator,
                _globals,
                bookmarkManager,
                watchPresetManager,
                _breakpointManager
            );

            var context = new ConsoleContext(
                _consoleScene!,
                () => ToggleConsole(),
                loggingCallbacks,
                timeControl,
                services,
                _services
            );

            // Try to execute as built-in command first
            bool commandExecuted = await _commandRegistry.ExecuteAsync(cmd, args, context);

            if (!commandExecuted)
            {
                // Not a built-in command - try to execute as C# script
                try
                {
                    EvaluationResult result = await _evaluator.EvaluateAsync(command, _globals);

                    // Handle compilation errors
                    if (result.IsCompilationError && result.Errors != null)
                    {
                        _consoleScene?.AppendOutput("Compilation Error:", Theme.Error);
                        foreach (FormattedError error in result.Errors)
                        {
                            _consoleScene?.AppendOutput($"  {error.Message}", Theme.Error);
                        }

                        return;
                    }

                    // Handle runtime errors
                    if (result.IsRuntimeError)
                    {
                        _consoleScene?.AppendOutput(
                            $"Runtime Error: {result.RuntimeException?.Message ?? "Unknown error"}",
                            Theme.Error
                        );
                        if (result.RuntimeException != null)
                        {
                            _consoleScene?.AppendOutput(
                                $"  {result.RuntimeException.GetType().Name}",
                                Theme.TextSecondary
                            );
                        }

                        return;
                    }

                    // Handle successful execution
                    if (result.IsSuccess)
                    {
                        // Display output if available
                        if (!string.IsNullOrEmpty(result.Output) && result.Output != "null")
                        {
                            _consoleScene?.AppendOutput(result.Output, Theme.Success);
                        }
                        // If no output, that's fine (statement executed successfully but returned nothing)

                        // Sync script variables to the Variables panel
                        SyncScriptVariables();
                    }
                    else
                    {
                        // Fallback for unexpected state
                        _consoleScene?.AppendOutput(
                            "Command executed but status unclear",
                            Theme.TextSecondary
                        );
                    }
                }
                catch (Exception ex)
                {
                    // This catch should rarely be hit - log it for debugging
                    _logger.LogError(
                        ex,
                        "Unexpected exception executing command: {Command}",
                        command
                    );
                    _consoleScene?.AppendOutput($"Error: {ex.Message}", Theme.Error);
                    _consoleScene?.AppendOutput($"Type: {ex.GetType().Name}", Theme.TextSecondary);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command in console");
            _consoleScene?.AppendOutput($"Error: {ex.Message}", Theme.Error);
        }
    }

    /// <summary>
    ///     Gets the time control interface from DI if available.
    /// </summary>
    /// <returns>The ITimeControl instance, or null if not registered.</returns>
    private ITimeControl? GetTimeControl()
    {
        try
        {
            // Get ITimeControl from DI - this is implemented by IGameTimeService in Game.Systems
            ITimeControl? timeControl = _services.GetService<ITimeControl>();
            if (timeControl == null)
            {
                _logger.LogDebug("ITimeControl not registered in DI, time control disabled");
                return null;
            }

            _logger.LogDebug("ITimeControl resolved successfully, time control enabled");
            return timeControl;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get time control from DI");
            return null;
        }
    }

    /// <summary>
    ///     Executes the startup script if it exists.
    /// </summary>
    private void ExecuteStartupScript()
    {
        try
        {
            string? scriptContent = StartupScriptLoader.LoadStartupScript();
            if (string.IsNullOrWhiteSpace(scriptContent))
            {
                return;
            }

            // Execute the startup script
            _ = ExecuteConsoleCommand(scriptContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing startup script");
            _consoleScene?.AppendOutput($"Startup script error: {ex.Message}", Theme.Error);
        }
    }

    /// <summary>
    ///     Replays buffered logs to the Logs panel when the console is reopened.
    /// </summary>
    private void ReplayBufferedLogs()
    {
        if (_consoleScene == null)
        {
            return;
        }

        List<(LogLevel Level, string Message, string Category, DateTime Timestamp)> logsToReplay;

        lock (_logBufferLock)
        {
            // Make a copy to avoid holding the lock while adding to the scene
            logsToReplay = new List<(LogLevel, string, string, DateTime)>(_persistentLogBuffer);
        }

        // Replay all buffered logs to the Logs panel with original timestamps
        foreach (
            (LogLevel level, string message, string category, DateTime timestamp) in logsToReplay
        )
        {
            _consoleScene.AddLog(level, message, category, timestamp);
        }
    }

    /// <summary>
    ///     Syncs script-defined variables to the Variables panel.
    /// </summary>
    private void SyncScriptVariables()
    {
        if (_consoleScene == null)
        {
            return;
        }

        // Get all variables from the script evaluator
        foreach (
            (string name, string typeName, Func<object?> valueGetter) in _evaluator.GetVariables()
        )
        {
            _consoleScene.SetScriptVariable(name, typeName, valueGetter);
        }
    }

    /// <summary>
    ///     Creates a stats data provider that gathers performance metrics.
    ///     Uses reflection to access PerformanceMonitor if available.
    /// </summary>
    private Func<StatsData> CreateStatsProvider()
    {
        // Try to get PerformanceMonitor via reflection (to avoid direct project reference)
        var perfMonitorType = Type.GetType(
            "PokeSharp.Game.Infrastructure.Diagnostics.PerformanceMonitor, PokeSharp.Game"
        );
        object? perfMonitor = null;

        if (perfMonitorType != null)
        {
            try
            {
                // Try to get it from services
                MethodInfo? getServiceMethod = typeof(ServiceProviderServiceExtensions)
                    .GetMethods()
                    .FirstOrDefault(m =>
                        m.Name == "GetService" && m.IsGenericMethod && m.GetParameters().Length == 1
                    );

                if (getServiceMethod != null)
                {
                    MethodInfo genericMethod = getServiceMethod.MakeGenericMethod(perfMonitorType);
                    perfMonitor = genericMethod.Invoke(null, new object[] { _services });
                }
            }
            catch
            {
                // Ignore - will use fallback
            }
        }

        // Cache reflection results for performance
        PropertyInfo? fpsProperty = perfMonitorType?.GetProperty("Fps");
        PropertyInfo? frameTimeMsProperty = perfMonitorType?.GetProperty("FrameTimeMs");
        PropertyInfo? minFrameTimeMsProperty = perfMonitorType?.GetProperty("MinFrameTimeMs");
        PropertyInfo? maxFrameTimeMsProperty = perfMonitorType?.GetProperty("MaxFrameTimeMs");
        PropertyInfo? memoryMbProperty = perfMonitorType?.GetProperty("MemoryMb");
        PropertyInfo? gen0Property = perfMonitorType?.GetProperty("Gen0Collections");
        PropertyInfo? gen1Property = perfMonitorType?.GetProperty("Gen1Collections");
        PropertyInfo? gen2Property = perfMonitorType?.GetProperty("Gen2Collections");
        PropertyInfo? frameCountProperty = perfMonitorType?.GetProperty("FrameCount");

        // Frame time tracking for fallback
        var frameTimeHistory = new List<float>(60);
        DateTime lastFrameTime = DateTime.Now;
        ulong frameCounter = 0;

        return () =>
        {
            var stats = new StatsData();

            // If we have PerformanceMonitor, use it
            if (perfMonitor != null && fpsProperty != null)
            {
                stats.Fps = (float)(fpsProperty.GetValue(perfMonitor) ?? 60f);
                stats.FrameTimeMs = (float)(frameTimeMsProperty?.GetValue(perfMonitor) ?? 16.67f);
                stats.MinFrameTimeMs = (float)(
                    minFrameTimeMsProperty?.GetValue(perfMonitor) ?? 16f
                );
                stats.MaxFrameTimeMs = (float)(
                    maxFrameTimeMsProperty?.GetValue(perfMonitor) ?? 17f
                );
                stats.MemoryMB = (double)(
                    memoryMbProperty?.GetValue(perfMonitor)
                    ?? GC.GetTotalMemory(false) / 1024.0 / 1024.0
                );
                stats.Gen0Collections = (int)(
                    gen0Property?.GetValue(perfMonitor) ?? GC.CollectionCount(0)
                );
                stats.Gen1Collections = (int)(
                    gen1Property?.GetValue(perfMonitor) ?? GC.CollectionCount(1)
                );
                stats.Gen2Collections = (int)(
                    gen2Property?.GetValue(perfMonitor) ?? GC.CollectionCount(2)
                );
                stats.FrameNumber = (ulong)(frameCountProperty?.GetValue(perfMonitor) ?? 0);
            }
            else
            {
                // Fallback: use GC data and simple timing
                DateTime now = DateTime.Now;
                float elapsed = (float)(now - lastFrameTime).TotalMilliseconds;
                lastFrameTime = now;
                frameCounter++;

                // Keep track of frame times
                frameTimeHistory.Add(elapsed);
                if (frameTimeHistory.Count > 60)
                {
                    frameTimeHistory.RemoveAt(0);
                }

                stats.Fps = elapsed > 0 ? 1000f / elapsed : 60f;
                stats.FrameTimeMs = elapsed;
                stats.MinFrameTimeMs = frameTimeHistory.Count > 0 ? frameTimeHistory.Min() : 16f;
                stats.MaxFrameTimeMs = frameTimeHistory.Count > 0 ? frameTimeHistory.Max() : 17f;
                stats.MemoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
                stats.Gen0Collections = GC.CollectionCount(0);
                stats.Gen1Collections = GC.CollectionCount(1);
                stats.Gen2Collections = GC.CollectionCount(2);
                stats.FrameNumber = frameCounter;
            }

            // Entity stats
            stats.EntityCount = _world.CountEntities(new QueryDescription());
            stats.ArchetypeCount = _world.Archetypes.Count;

            // System stats
            IReadOnlyDictionary<string, SystemMetrics>? systemMetrics = _systemManager.GetMetrics();
            stats.SystemCount = systemMetrics?.Count ?? 0;

            if (systemMetrics != null && systemMetrics.Count > 0)
            {
                double totalTime = 0;
                string? slowestName = null;
                double slowestTime = 0;

                foreach (KeyValuePair<string, SystemMetrics> kvp in systemMetrics)
                {
                    totalTime += kvp.Value.LastUpdateMs;
                    if (kvp.Value.LastUpdateMs > slowestTime)
                    {
                        slowestTime = kvp.Value.LastUpdateMs;
                        slowestName = kvp.Key;
                    }
                }

                stats.TotalSystemTimeMs = totalTime;
                stats.SlowestSystemName = slowestName;
                stats.SlowestSystemTimeMs = slowestTime;
            }

            // Pool stats (via reflection to avoid direct reference)
            TryPopulatePoolStats(stats);

            // Event pool stats (via reflection to avoid direct reference)
            TryPopulateEventPoolStats(stats);

            return stats;
        };
    }

    /// <summary>
    ///     Attempts to populate pool statistics via reflection.
    /// </summary>
    private void TryPopulatePoolStats(StatsData stats)
    {
        try
        {
            var poolManagerType = Type.GetType(
                "PokeSharp.Engine.Systems.Pooling.EntityPoolManager, PokeSharp.Engine.Systems"
            );

            if (poolManagerType == null)
            {
                return;
            }

            // Try to get EntityPoolManager from services
            MethodInfo? getServiceMethod = typeof(ServiceProviderServiceExtensions)
                .GetMethods()
                .FirstOrDefault(m =>
                    m.Name == "GetService" && m.IsGenericMethod && m.GetParameters().Length == 1
                );

            if (getServiceMethod == null)
            {
                return;
            }

            MethodInfo genericMethod = getServiceMethod.MakeGenericMethod(poolManagerType);
            object? poolManager = genericMethod.Invoke(null, new object[] { _services });

            if (poolManager == null)
            {
                return;
            }

            // Call GetStatistics()
            MethodInfo? getStatsMethod = poolManagerType.GetMethod("GetStatistics");
            if (getStatsMethod == null)
            {
                return;
            }

            object? aggregateStats = getStatsMethod.Invoke(poolManager, null);
            if (aggregateStats == null)
            {
                return;
            }

            // Get fields from AggregatePoolStatistics struct
            Type statsType = aggregateStats.GetType();
            stats.PoolCount = (int)(
                statsType.GetField("TotalPools")?.GetValue(aggregateStats) ?? 0
            );
            stats.PooledActive = (int)(
                statsType.GetField("TotalActive")?.GetValue(aggregateStats) ?? 0
            );
            stats.PooledAvailable = (int)(
                statsType.GetField("TotalAvailable")?.GetValue(aggregateStats) ?? 0
            );

            // Calculate reuse rate
            PropertyInfo? reuseRateProp = statsType.GetProperty("OverallReuseRate");
            stats.PoolReuseRate = (float)(reuseRateProp?.GetValue(aggregateStats) ?? 0f);
        }
        catch
        {
            // Silently fail - pool stats are optional
        }
    }

    /// <summary>
    ///     Attempts to populate event pool statistics via reflection.
    /// </summary>
    private void TryPopulateEventPoolStats(StatsData stats)
    {
        try
        {
            // Get IEventBus from services
            IEventBus? eventBus = _services.GetService<IEventBus>();
            if (eventBus == null)
            {
                return;
            }

            // Call GetPoolStatistics() on EventBus
            MethodInfo? getStatsMethod = eventBus.GetType().GetMethod("GetPoolStatistics");
            if (getStatsMethod == null)
            {
                return;
            }

            object? poolStatsObj = getStatsMethod.Invoke(eventBus, null);
            if (poolStatsObj == null)
            {
                return;
            }

            // poolStatsObj should be IReadOnlyList<EventPoolStatistics>
            if (poolStatsObj is not IEnumerable enumerable)
            {
                return;
            }

            long totalRented = 0;
            long totalCreated = 0;
            long totalInUse = 0;
            int poolCount = 0;
            string? hotEventType = null;
            long hotEventRented = 0;

            foreach (object? statObj in enumerable)
            {
                if (statObj == null)
                {
                    continue;
                }

                Type statType = statObj.GetType();
                string? eventType = statType.GetProperty("EventType")?.GetValue(statObj) as string;
                long rented = (long)(statType.GetProperty("TotalRented")?.GetValue(statObj) ?? 0L);
                long created = (long)(
                    statType.GetProperty("TotalCreated")?.GetValue(statObj) ?? 0L
                );
                long inUse = (long)(
                    statType.GetProperty("CurrentlyInUse")?.GetValue(statObj) ?? 0L
                );

                totalRented += rented;
                totalCreated += created;
                totalInUse += inUse;
                poolCount++;

                // Track hottest event type
                if (rented > hotEventRented)
                {
                    hotEventRented = rented;
                    hotEventType = eventType;
                }
            }

            stats.EventPoolCount = poolCount;
            stats.EventPoolTotalRented = totalRented;
            stats.EventPoolTotalCreated = totalCreated;
            stats.EventPoolCurrentlyInUse = totalInUse;
            stats.EventPoolAvgReuseRate =
                totalRented > 0 ? 1.0 - ((double)totalCreated / totalRented) : 0.0;
            stats.MostUsedEventType = hotEventType;
            stats.MostUsedEventRented = hotEventRented;
        }
        catch
        {
            // Silently fail - event pool stats are optional
        }
    }

    /// <summary>
    ///     Gets all entities from the Arch World as EntityInfo objects.
    ///     This is the entity provider for the Entities panel.
    /// </summary>
    private IEnumerable<EntityInfo> GetAllEntitiesAsInfo()
    {
        var result = new List<EntityInfo>();

        try
        {
            // Query all entities from the World
            var entities = new List<Entity>();
            _world.Query(new QueryDescription(), entity => entities.Add(entity));

            // PERFORMANCE FIX: Build caches and inverse relationship indexes in single pass
            // This reduces O(N²) to O(N) by avoiding per-entity world queries
            Dictionary<int, EntityCacheEntry> entityCache = BuildEntityCache(entities);
            InverseRelationshipIndex inverseRelationships = BuildInverseRelationshipIndex(entities);

            foreach (Entity entity in entities)
            {
                if (!_world.IsAlive(entity))
                {
                    continue;
                }

                EntityInfo info = ConvertEntityToInfo(entity, entityCache, inverseRelationships);
                result.Add(info);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error querying entities from World");
        }

        return result;
    }

    /// <summary>
    ///     Builds a cache of entity names and component lists to avoid repeated expensive lookups.
    /// </summary>
    private Dictionary<int, EntityCacheEntry> BuildEntityCache(List<Entity> entities)
    {
        var cache = new Dictionary<int, EntityCacheEntry>();

        foreach (Entity entity in entities)
        {
            if (!_world.IsAlive(entity))
            {
                continue;
            }

            try
            {
                List<string> components = DetectEntityComponents(entity);
                string name = DetermineEntityName(entity, components);

                cache[entity.Id] = new EntityCacheEntry { Name = name, Components = components };
            }
            catch
            {
                cache[entity.Id] = new EntityCacheEntry
                {
                    Name = $"Entity_{entity.Id}",
                    Components = new List<string>(),
                };
            }
        }

        return cache;
    }

    /// <summary>
    ///     Builds inverse relationship indexes in a single pass over all entities.
    ///     This eliminates the need for per-entity world queries (O(N²) → O(N)).
    /// </summary>
    private InverseRelationshipIndex BuildInverseRelationshipIndex(List<Entity> entities)
    {
        var index = new InverseRelationshipIndex();

        foreach (Entity entity in entities)
        {
            if (!_world.IsAlive(entity))
            {
                continue;
            }

            try
            {
                // Index ParentOf relationships (forward: parent → children)
                if (entity.HasRelationship<ParentOf>())
                {
                    ref Relationship<ParentOf> children = ref entity.GetRelationships<ParentOf>();
                    foreach (KeyValuePair<Entity, ParentOf> kvp in children)
                    {
                        Entity childEntity = kvp.Key;
                        if (!_world.IsAlive(childEntity))
                        {
                            continue;
                        }

                        // Build inverse: child → parent
                        if (!index.ParentOf.ContainsKey(childEntity.Id))
                        {
                            index.ParentOf[childEntity.Id] = new List<Entity>();
                        }

                        index.ParentOf[childEntity.Id].Add(entity);
                    }
                }

                // Index OwnerOf relationships (forward: owner → owned)
                if (entity.HasRelationship<OwnerOf>())
                {
                    ref Relationship<OwnerOf> ownedEntities =
                        ref entity.GetRelationships<OwnerOf>();
                    foreach (KeyValuePair<Entity, OwnerOf> kvp in ownedEntities)
                    {
                        Entity ownedEntity = kvp.Key;
                        if (!_world.IsAlive(ownedEntity))
                        {
                            continue;
                        }

                        // Build inverse: owned → owner
                        if (!index.OwnerOf.ContainsKey(ownedEntity.Id))
                        {
                            index.OwnerOf[ownedEntity.Id] = new List<Entity>();
                        }

                        index.OwnerOf[ownedEntity.Id].Add(entity);
                    }
                }

                // NOTE: All relationships now use ParentOf for consistency.
                // Maps are parents of tiles, NPCs, warps, etc.
            }
            catch
            {
                // Skip entities with relationship errors
            }
        }

        return index;
    }

    /// <summary>
    ///     Converts an Arch Entity to an EntityInfo for display.
    /// </summary>
    private EntityInfo ConvertEntityToInfo(
        Entity entity,
        Dictionary<int, EntityCacheEntry> entityCache,
        InverseRelationshipIndex inverseRelationships
    )
    {
        var info = new EntityInfo
        {
            Id = entity.Id,
            IsActive = _world.IsAlive(entity),
            Components = new List<string>(),
            Properties = new Dictionary<string, string>(),
            ComponentData = new Dictionary<string, Dictionary<string, string>>(),
            Relationships = new Dictionary<string, List<EntityRelationship>>(),
        };

        try
        {
            // Use cached component detection
            EntityCacheEntry cacheEntry = entityCache.GetValueOrDefault(
                entity.Id,
                new EntityCacheEntry
                {
                    Name = $"Entity_{entity.Id}",
                    Components = new List<string>(),
                }
            );

            info.Components = cacheEntry.Components;
            info.Name = cacheEntry.Name;
            info.Tag = DetermineEntityTag(cacheEntry.Components);

            // Get properties (simple flattened view)
            info.Properties = GetEntityProperties(entity, cacheEntry.Components);
            info.Properties["Components"] = cacheEntry.Components.Count.ToString();

            // Get component data (structured by component name) using Arch's built-in inspection
            info.ComponentData = _componentRegistry.GetComponentData(entity);

            // Extract relationships using cached indexes (no more O(N²) world queries!)
            ExtractEntityRelationships(entity, info, entityCache, inverseRelationships);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error reading entity {EntityId} components", entity.Id);
            info.Name = $"Entity_{entity.Id}";
            info.Components = new List<string> { "[Error reading components]" };
        }

        return info;
    }

    /// <summary>
    ///     Detects which components an entity has using the component registry.
    /// </summary>
    private List<string> DetectEntityComponents(Entity entity)
    {
        return _componentRegistry.DetectComponents(entity);
    }

    /// <summary>
    ///     Determines an entity's display name based on its components.
    /// </summary>
    private string DetermineEntityName(Entity entity, List<string> components)
    {
        return _componentRegistry.DetermineEntityName(entity, components);
    }

    /// <summary>
    ///     Determines an entity's tag based on its components.
    /// </summary>
    private string? DetermineEntityTag(List<string> components)
    {
        return _componentRegistry.DetermineEntityTag(components);
    }

    /// <summary>
    ///     Extracts relationship data from an entity using Arch.Relationships.
    ///     PERFORMANCE: Uses cached indexes to avoid O(N²) world queries.
    /// </summary>
    private void ExtractEntityRelationships(
        Entity entity,
        EntityInfo info,
        Dictionary<int, EntityCacheEntry> entityCache,
        InverseRelationshipIndex inverseRelationships
    )
    {
        // Extract ParentOf relationships (forward: this entity → children)
        if (entity.HasRelationship<ParentOf>())
        {
            var parentOfList = new List<EntityRelationship>();
            ref Relationship<ParentOf> children = ref entity.GetRelationships<ParentOf>();

            foreach (KeyValuePair<Entity, ParentOf> kvp in children)
            {
                Entity childEntity = kvp.Key;
                if (!_world.IsAlive(childEntity))
                {
                    continue;
                }

                var rel = new EntityRelationship
                {
                    EntityId = childEntity.Id,
                    // Use cached name instead of expensive DetectEntityComponents call
                    EntityName =
                        entityCache.GetValueOrDefault(childEntity.Id)?.Name
                        ?? $"Entity_{childEntity.Id}",
                    IsValid = true,
                    Metadata = new Dictionary<string, string>(),
                };

                // Get relationship data
                ParentOf relationshipData = entity.GetRelationship<ParentOf>(childEntity);
                rel.Metadata["EstablishedAt"] = relationshipData.EstablishedAt.ToString(
                    "yyyy-MM-dd HH:mm:ss"
                );
                if (!string.IsNullOrEmpty(relationshipData.Metadata))
                {
                    rel.Metadata["Info"] = relationshipData.Metadata;
                }

                parentOfList.Add(rel);
            }

            if (parentOfList.Count > 0)
            {
                info.Relationships["Children"] = parentOfList;
            }
        }

        // Extract OwnerOf relationships (forward: this entity → owned entities)
        if (entity.HasRelationship<OwnerOf>())
        {
            var ownerOfList = new List<EntityRelationship>();
            ref Relationship<OwnerOf> ownedEntities = ref entity.GetRelationships<OwnerOf>();

            foreach (KeyValuePair<Entity, OwnerOf> kvp in ownedEntities)
            {
                Entity ownedEntity = kvp.Key;
                if (!_world.IsAlive(ownedEntity))
                {
                    continue;
                }

                var rel = new EntityRelationship
                {
                    EntityId = ownedEntity.Id,
                    EntityName =
                        entityCache.GetValueOrDefault(ownedEntity.Id)?.Name
                        ?? $"Entity_{ownedEntity.Id}",
                    IsValid = true,
                    Metadata = new Dictionary<string, string>(),
                };

                // Get relationship data
                OwnerOf relationshipData = entity.GetRelationship<OwnerOf>(ownedEntity);
                rel.Metadata["Type"] = relationshipData.Type.ToString();
                rel.Metadata["AcquiredAt"] = relationshipData.AcquiredAt.ToString(
                    "yyyy-MM-dd HH:mm:ss"
                );
                if (!string.IsNullOrEmpty(relationshipData.Metadata))
                {
                    rel.Metadata["Info"] = relationshipData.Metadata;
                }

                ownerOfList.Add(rel);
            }

            if (ownerOfList.Count > 0)
            {
                info.Relationships["Owns"] = ownerOfList;
            }
        }

        // MapContains has been replaced with ParentOf relationships
        // Map entities now use standard parent-child hierarchy

        // PERFORMANCE FIX: Use inverse index instead of O(N) world query
        // Find parent relationships (where this entity is a child)
        if (inverseRelationships.ParentOf.TryGetValue(entity.Id, out List<Entity>? parents))
        {
            var parentList = new List<EntityRelationship>();

            foreach (Entity potentialParent in parents)
            {
                if (!_world.IsAlive(potentialParent))
                {
                    continue;
                }

                var rel = new EntityRelationship
                {
                    EntityId = potentialParent.Id,
                    EntityName =
                        entityCache.GetValueOrDefault(potentialParent.Id)?.Name
                        ?? $"Entity_{potentialParent.Id}",
                    IsValid = true,
                    Metadata = new Dictionary<string, string>(),
                };

                ParentOf relationshipData = potentialParent.GetRelationship<ParentOf>(entity);
                rel.Metadata["EstablishedAt"] = relationshipData.EstablishedAt.ToString(
                    "yyyy-MM-dd HH:mm:ss"
                );
                if (!string.IsNullOrEmpty(relationshipData.Metadata))
                {
                    rel.Metadata["Info"] = relationshipData.Metadata;
                }

                parentList.Add(rel);
            }

            if (parentList.Count > 0)
            {
                info.Relationships["Parent"] = parentList;
            }
        }

        // PERFORMANCE FIX: Use inverse index instead of O(N) world query
        // Find owner relationships (where this entity is owned)
        if (inverseRelationships.OwnerOf.TryGetValue(entity.Id, out List<Entity>? owners))
        {
            var ownerList = new List<EntityRelationship>();

            foreach (Entity potentialOwner in owners)
            {
                if (!_world.IsAlive(potentialOwner))
                {
                    continue;
                }

                var rel = new EntityRelationship
                {
                    EntityId = potentialOwner.Id,
                    EntityName =
                        entityCache.GetValueOrDefault(potentialOwner.Id)?.Name
                        ?? $"Entity_{potentialOwner.Id}",
                    IsValid = true,
                    Metadata = new Dictionary<string, string>(),
                };

                OwnerOf relationshipData = potentialOwner.GetRelationship<OwnerOf>(entity);
                rel.Metadata["Type"] = relationshipData.Type.ToString();
                rel.Metadata["AcquiredAt"] = relationshipData.AcquiredAt.ToString(
                    "yyyy-MM-dd HH:mm:ss"
                );
                if (!string.IsNullOrEmpty(relationshipData.Metadata))
                {
                    rel.Metadata["Info"] = relationshipData.Metadata;
                }

                ownerList.Add(rel);
            }

            if (ownerList.Count > 0)
            {
                info.Relationships["OwnedBy"] = ownerList;
            }
        }

        // NOTE: With Arch.Relationships, MapContains is bidirectional automatically.
        // We don't need to create "BelongsToMap" inverse relationships manually.
        // The relationship can be queried in both directions natively.
    }

    /// <summary>
    ///     Gets display properties for an entity using the component registry.
    /// </summary>
    private Dictionary<string, string> GetEntityProperties(Entity entity, List<string> components)
    {
        try
        {
            return _componentRegistry.GetSimpleProperties(entity);
        }
        catch
        {
            // Ignore errors reading properties
            return new Dictionary<string, string>();
        }
    }

    /// <summary>
    ///     Cache entry for entity metadata to avoid repeated expensive lookups.
    /// </summary>
    private class EntityCacheEntry
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Components { get; set; } = new();
    }

    /// <summary>
    ///     Inverse relationship index for efficient lookups without O(N²) world queries.
    /// </summary>
    /// <remarks>
    ///     Only includes relationships where inverse lookup is needed.
    ///     MapContains is bidirectional in Arch.Relationships, so no index needed.
    /// </remarks>
    private class InverseRelationshipIndex
    {
        // Maps child entity ID → list of parent entities
        public Dictionary<int, List<Entity>> ParentOf { get; } = new();

        // Maps owned entity ID → list of owner entities
        public Dictionary<int, List<Entity>> OwnerOf { get; } = new();
    }
}
