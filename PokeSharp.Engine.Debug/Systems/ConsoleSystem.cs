using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Reflection;
using System.Text;
using PokeSharp.Engine.Core.Services;
using PokeSharp.Engine.Core.Systems;
using PokeSharp.Engine.Debug.Breakpoints;
using PokeSharp.Engine.Debug.Commands;
using PokeSharp.Engine.Debug.Console.Configuration;
using PokeSharp.Engine.Debug.Console.Features;
using PokeSharp.Engine.Debug.Console.Scripting;
using PokeSharp.Engine.Debug.Entities;
using PokeSharp.Engine.Debug.Features;
using PokeSharp.Engine.Debug.Logging;
using PokeSharp.Engine.Debug.Scripting;
using PokeSharp.Engine.Debug.Services;
using PokeSharp.Engine.Scenes;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Game.Scripting.Api;
using PokeSharp.Engine.UI.Debug.Components.Debug;
using PokeSharp.Engine.UI.Debug.Scenes;
using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Utilities;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Models;

namespace PokeSharp.Engine.Debug.Systems;

/// <summary>
/// Console system that manages the modern debug console as a scene.
/// Monitors for toggle key and pushes/pops console scene onto the scene stack.
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

    // Core console components (shared between console features)
    private ConsoleScriptEvaluator _evaluator = null!;
    private ConsoleGlobals _globals = null!;
    private ParameterHintProvider _parameterHintProvider = null!;
    private List<Assembly>? _referencedAssemblies;
    private ConsoleCommandRegistry _commandRegistry = null!;
    private ConsoleCompletionProvider _completionProvider = null!;
    private ConsoleDocumentationProvider _documentationProvider = null!;

    // Console state
    private KeyboardState _previousKeyboardState;
    private bool _isConsoleOpen;
    private ConsoleScene? _consoleScene;

    // Console logging state
    private bool _loggingEnabled = false;
    private Microsoft.Extensions.Logging.LogLevel _minimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Information;

    // Entity component registry
    private DebugComponentRegistry _componentRegistry = null!;

    // Breakpoint manager for conditional game pausing
    private BreakpointManager? _breakpointManager;

    // Persistent log buffer - stores logs even when console is closed
    private readonly List<(Microsoft.Extensions.Logging.LogLevel Level, string Message, string Category, DateTime Timestamp)> _persistentLogBuffer = new();
    private readonly object _logBufferLock = new();
    private const int MaxPersistentLogs = 5000;

    // Auto-completion debouncing
    private CancellationTokenSource? _completionCts;
    private const int CompletionDebounceMs = 50;

    // Multi-line input buffer for incomplete statements (like for loops)
    private readonly StringBuilder _multiLineBuffer = new();
    private bool _isMultiLineMode = false;

    // IUpdateSystem properties
    public int Priority => ConsoleConstants.System.UpdatePriority;
    public bool Enabled { get; set; } = true;

    // Theme reference
    private static UITheme Theme => UITheme.Dark;

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
            // Create command registry
            _commandRegistry = new ConsoleCommandRegistry(_logger);

            // Create component registry for entity detection
            _componentRegistry = DebugComponentRegistryFactory.CreateDefault();

            // Create script evaluator (shared component)
            _evaluator = new ConsoleScriptEvaluator(_logger);

            // Create console globals (script API)
            _globals = new ConsoleGlobals(_apiProvider, _world, _systemManager, _graphicsDevice, _logger);

            // Create breakpoint manager (requires evaluator, globals, and time control)
            var timeControl = GetTimeControl();
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
                _loggerProvider.SetLogEntryHandler((level, message, category) =>
                {
                    // Only buffer logs if logging is enabled
                    if (!_loggingEnabled)
                        return;

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
                });

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
            return;

        try
        {
            var currentKeyboard = Keyboard.GetState();

            // Check for console toggle key (`) when console is closed
            if (!_isConsoleOpen)
            {
                bool isShiftPressed = currentKeyboard.IsKeyDown(Keys.LeftShift) || currentKeyboard.IsKeyDown(Keys.RightShift);
                bool togglePressed = currentKeyboard.IsKeyDown(Keys.OemTilde) &&
                                     _previousKeyboardState.IsKeyUp(Keys.OemTilde) &&
                                     !isShiftPressed;

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
    /// Toggles the console scene on/off.
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
                var consoleLogger = _services.GetRequiredService<ILogger<ConsoleScene>>();

                _consoleScene = new ConsoleScene(
                    _graphicsDevice,
                    _services,
                    consoleLogger
                );

                // Wire up event handlers
                _consoleScene.OnCommandSubmitted += HandleConsoleCommand;
                _consoleScene.OnRequestCompletions += HandleConsoleCompletions;
                _consoleScene.OnRequestParameterHints += HandleConsoleParameterHints;
                _consoleScene.OnRequestDocumentation += HandleConsoleDocumentation;
                _consoleScene.OnCloseRequested += OnConsoleClosed;
                _consoleScene.OnReady += HandleConsoleReady;

                // Wire up Print() output to the console
                _globals.OutputAction = (text) => _consoleScene?.AppendOutput(text, Theme.TextPrimary);

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
    /// Handles the console scene being closed.
    /// </summary>
    private void OnConsoleClosed()
    {
        if (_isConsoleOpen)
        {
            ToggleConsole();
        }
    }

    /// <summary>
    /// Handles the console scene being ready (after LoadContent completes).
    /// This is when LogsPanel exists and can receive buffered logs.
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

        // Welcome message - use theme colors
        var theme = ThemeManager.Current;
        _consoleScene?.AppendOutput("=== PokeSharp Debug Console ===", theme.ConsolePrimary);
        _consoleScene?.AppendOutput("Type 'help' for available commands", theme.TextSecondary);
        _consoleScene?.AppendOutput("Press ` or type 'exit' to close", theme.TextDim);
        _consoleScene?.AppendOutput("", theme.TextPrimary);

        // Execute startup script if it exists
        ExecuteStartupScript();
    }

    /// <summary>
    /// Handles a breakpoint being hit.
    /// </summary>
    private void OnBreakpointHit(IBreakpoint breakpoint)
    {
        // Open the console if not already open
        if (!_isConsoleOpen)
        {
            ToggleConsole();
        }

        // Display breakpoint hit message
        var theme = ThemeManager.Current;
        _consoleScene?.AppendOutput("", theme.TextPrimary);
        _consoleScene?.AppendOutput($"⏸ BREAKPOINT #{breakpoint.Id} HIT", theme.Warning);
        _consoleScene?.AppendOutput($"  Condition: {breakpoint.Description}", theme.TextSecondary);
        _consoleScene?.AppendOutput($"  Hit count: {breakpoint.HitCount}", theme.TextSecondary);
        _consoleScene?.AppendOutput("", theme.TextPrimary);
        _consoleScene?.AppendOutput("Game paused. Use 'resume' or 'step' to continue.", theme.TextDim);
        _consoleScene?.AppendOutput("", theme.TextPrimary);
    }

    /// <summary>
    /// Handles commands submitted from the console.
    /// Supports multi-line input for incomplete statements (like for loops).
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

            var fullCode = _multiLineBuffer.ToString();

            // Check if the code is complete (for multi-line statements like for loops)
            if (!_evaluator.IsCodeComplete(fullCode))
            {
                // Code is incomplete - switch to multi-line mode
                _isMultiLineMode = true;
                _consoleScene?.SetPrompt("... ");
                _logger.LogDebug("Multi-line mode: waiting for more input. Current: {Code}", fullCode);
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
    /// Checks if a command should bypass C# multi-line syntax checking.
    /// This includes built-in commands, aliases, and command-like inputs (to get proper error messages).
    /// </summary>
    private bool IsBuiltInCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return false;

        // Get the first word of the command
        var trimmed = command.TrimStart();
        var spaceIndex = trimmed.IndexOf(' ');
        var firstWord = spaceIndex > 0 ? trimmed[..spaceIndex] : trimmed;

        // Check if it's a registered command
        if (_commandRegistry.GetCommand(firstWord) != null)
            return true;

        // Check if it's an alias
        var aliasManager = _services.GetService<AliasMacroManager>();
        if (aliasManager?.TryExpandAlias(firstWord, out _) == true)
            return true;

        // Check if input looks like a command (simple words) rather than C# code
        // This ensures typos like "itme scale 2.0" give an error instead of entering multi-line mode
        if (LooksLikeCommand(trimmed))
            return true;

        return false;
    }

    /// <summary>
    /// Heuristic to detect if input looks like a console command rather than C# code.
    /// Commands are typically: word [args...] without complex C# syntax.
    /// </summary>
    private static bool LooksLikeCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        // Must start with a letter (command name)
        if (!char.IsLetter(input[0]))
            return false;

        // C# code indicators that suggest this is NOT a simple command
        var csharpIndicators = new[] { "=", "{", "}", "(", ")", "[", "]", "=>", "++", "--", "&&", "||", "<<", ">>", "::" };
        foreach (var indicator in csharpIndicators)
        {
            if (input.Contains(indicator))
                return false;
        }

        // Check for C# keywords that start statements (not command names)
        var firstWord = input.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        var csharpKeywords = new[] { "var", "int", "float", "double", "string", "bool", "if", "else", "for", "foreach", "while", "do", "switch", "try", "catch", "finally", "throw", "return", "class", "struct", "interface", "enum", "namespace", "using", "new", "public", "private", "protected", "static", "async", "await" };
        if (csharpKeywords.Contains(firstWord, StringComparer.Ordinal))
            return false;

        // Looks like a command
        return true;
    }

    /// <summary>
    /// Handles auto-completion requests from the console.
    /// Uses debouncing to prevent excessive requests during fast typing.
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
    /// Gets completions with debouncing to avoid flooding during fast typing.
    /// </summary>
    private async Task GetCompletionsWithDebounceAsync(string partialCommand, CancellationToken ct)
    {
        try
        {
            // Wait for typing to pause (debounce)
            await Task.Delay(CompletionDebounceMs, ct);

            // Get cursor position and completions
            var cursorPosition = _consoleScene?.GetCursorPosition() ?? partialCommand.Length;
            var suggestions = await _completionProvider.GetCompletionsAsync(partialCommand, cursorPosition);

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
    /// Handles parameter hint requests from the console.
    /// </summary>
    private void HandleConsoleParameterHints(string text, int cursorPosition)
    {
        try
        {
            // Check if we're inside a method call
            var methodCallInfo = FindMethodCallAtCursor(text, cursorPosition);
            if (methodCallInfo == null)
            {
                _consoleScene?.ClearParameterHints();
                return;
            }

            // Update parameter hint provider with current script state
            _parameterHintProvider.UpdateScriptState(_evaluator.CurrentState);

            // Get parameter hints from provider (pass text up to the opening paren + opening paren)
            var textForHints = text.Substring(0, methodCallInfo.Value.OpenParenIndex + 1);
            var hints = _parameterHintProvider.GetParameterHints(textForHints, textForHints.Length)!;

            if (hints != null && hints.Overloads.Count > 0)
            {
                // Convert to UI types
                var uiHints = new ParamHints
                {
                    MethodName = hints.MethodName,
                    CurrentOverloadIndex = hints.CurrentOverloadIndex,
                    Overloads = hints.Overloads.Select(overload => new MethodSig
                    {
                        MethodName = overload.MethodName,
                    ReturnType = overload.ReturnType,
                    Parameters = overload.Parameters.Select(param => new ParamInfo
                    {
                        Name = param.Name ?? string.Empty,
                        Type = param.Type,
                        IsOptional = param.IsOptional,
                        DefaultValue = param.DefaultValue ?? string.Empty
                    }).ToList()
                    }).ToList()
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
    /// Finds the method call that the cursor is currently inside.
    /// Returns the method name, opening paren position, and current parameter index.
    /// </summary>
    private (string MethodName, int OpenParenIndex, int ParameterIndex)? FindMethodCallAtCursor(string text, int cursorPosition)
    {
        // Find the last unmatched opening parenthesis before the cursor
        int nestLevel = 0;
        int openParenIndex = -1;

        for (int i = cursorPosition - 1; i >= 0; i--)
        {
            char c = text[i];
            if (c == ')')
                nestLevel++;
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
            methodStartIndex--;

        if (methodStartIndex < 0)
            return null;

        // Find the start of the method name (alphanumeric + underscore)
        int methodEndIndex = methodStartIndex;
        while (methodStartIndex >= 0 && (char.IsLetterOrDigit(text[methodStartIndex]) || text[methodStartIndex] == '_'))
            methodStartIndex--;

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
                nestLevel++;
            else if (c == ')')
                nestLevel--;
            else if (c == ',' && nestLevel == 0)
                parameterIndex++;
        }

        return (methodName, openParenIndex, parameterIndex);
    }


    /// <summary>
    /// Handles documentation requests from the console.
    /// </summary>
    private void HandleConsoleDocumentation(string completionText)
    {
        var doc = _documentationProvider.GetDocumentation(completionText);
        _consoleScene?.SetDocumentation(doc);
    }

    /// <summary>
    /// Executes a command from the console.
    /// Supports command chaining with semicolons (e.g., "clear; help; time").
    /// </summary>
    private async Task ExecuteConsoleCommand(string command)
    {
        try
        {
            // Check for command chaining (semicolon-separated commands)
            // Only split if not inside quotes
            var chainedCommands = SplitChainedCommands(command);
            if (chainedCommands.Count > 1)
            {
                _logger.LogDebug("Executing {Count} chained commands", chainedCommands.Count);
                foreach (var chainedCmd in chainedCommands)
                {
                    var trimmedCmd = chainedCmd.Trim();
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
    /// Splits a command string by semicolons, respecting quoted strings and nested brackets.
    /// This ensures that semicolons inside for loops, method calls, etc. are not split points.
    /// </summary>
    private static List<string> SplitChainedCommands(string input)
    {
        var commands = new List<string>();
        var current = new StringBuilder();
        var inDoubleQuote = false;
        var inSingleQuote = false;
        var parenDepth = 0;   // ()
        var braceDepth = 0;   // {}
        var bracketDepth = 0; // []

        foreach (var c in input)
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
                    case '(': parenDepth++; break;
                    case ')': parenDepth--; break;
                    case '{': braceDepth++; break;
                    case '}': braceDepth--; break;
                    case '[': bracketDepth++; break;
                    case ']': bracketDepth--; break;
                }

                // Only split on semicolons at the top level (not inside any brackets)
                if (c == ';' && parenDepth == 0 && braceDepth == 0 && bracketDepth == 0)
                {
                    // Split point - add current command and start new one
                    var cmd = current.ToString().Trim();
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
        var lastCmd = current.ToString().Trim();
        if (!string.IsNullOrEmpty(lastCmd))
        {
            commands.Add(lastCmd);
        }

        return commands;
    }

    /// <summary>
    /// Executes a single command (no chaining).
    /// </summary>
    private async Task ExecuteSingleCommand(string command)
    {
        try
        {
            // Get dependencies for command execution
            var aliasManager = _services.GetRequiredService<AliasMacroManager>();
            var scriptManager = _services.GetRequiredService<ScriptManager>();
            var bookmarkManager = _services.GetRequiredService<BookmarkedCommandsManager>();
            var watchPresetManager = _services.GetRequiredService<WatchPresetManager>();

            // Try to expand alias first
            if (aliasManager.TryExpandAlias(command, out var expandedCommand))
            {
                _logger.LogDebug("Alias expanded: {Original} -> {Expanded}", command, expandedCommand);
                _consoleScene?.AppendOutput($"[alias] {expandedCommand}", Theme.TextSecondary);
                command = expandedCommand;

                // Check if expanded alias contains chained commands
                var chainedFromAlias = SplitChainedCommands(command);
                if (chainedFromAlias.Count > 1)
                {
                    // Execute chained commands from alias
                    foreach (var chainedCmd in chainedFromAlias)
                    {
                        var trimmedCmd = chainedCmd.Trim();
                        if (!string.IsNullOrEmpty(trimmedCmd))
                        {
                            await ExecuteSingleCommand(trimmedCmd);
                        }
                    }
                    return;
                }
            }

            // Parse command
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return;

            var cmd = parts[0];
            var args = parts.Skip(1).ToArray();

            // Create console context for commands using aggregated services
            var loggingCallbacks = new ConsoleLoggingCallbacks(
                () => _loggingEnabled,
                (enabled) => _loggingEnabled = enabled,
                () => _minimumLogLevel,
                (level) => _minimumLogLevel = level
            );

            // Get time control from DI (may be null if not registered)
            var timeControl = GetTimeControl();

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
                services
            );

            // Try to execute as built-in command first
            var commandExecuted = await _commandRegistry.ExecuteAsync(cmd, args, context);

            if (!commandExecuted)
            {
                // Not a built-in command - try to execute as C# script
                try
                {
                    var result = await _evaluator.EvaluateAsync(command, _globals);

                    // Handle compilation errors
                    if (result.IsCompilationError && result.Errors != null)
                    {
                        _consoleScene?.AppendOutput("Compilation Error:", Theme.Error);
                        foreach (var error in result.Errors)
                        {
                            _consoleScene?.AppendOutput($"  {error.Message}", Theme.Error);
                        }
                        return;
                    }

                    // Handle runtime errors
                    if (result.IsRuntimeError)
                    {
                        _consoleScene?.AppendOutput($"Runtime Error: {result.RuntimeException?.Message ?? "Unknown error"}", Theme.Error);
                        if (result.RuntimeException != null)
                        {
                            _consoleScene?.AppendOutput($"  {result.RuntimeException.GetType().Name}", Theme.TextSecondary);
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
                        _consoleScene?.AppendOutput("Command executed but status unclear", Theme.TextSecondary);
                    }
                }
                catch (Exception ex)
                {
                    // This catch should rarely be hit - log it for debugging
                    _logger.LogError(ex, "Unexpected exception executing command: {Command}", command);
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
    /// Gets the time control interface from DI if available.
    /// </summary>
    /// <returns>The ITimeControl instance, or null if not registered.</returns>
    private ITimeControl? GetTimeControl()
    {
        try
        {
            // Get ITimeControl from DI - this is implemented by IGameTimeService in Game.Systems
            var timeControl = _services.GetService<ITimeControl>();
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
    /// Executes the startup script if it exists.
    /// </summary>
    private void ExecuteStartupScript()
    {
        try
        {
            var scriptContent = StartupScriptLoader.LoadStartupScript();
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
    /// Replays buffered logs to the Logs panel when the console is reopened.
    /// </summary>
    private void ReplayBufferedLogs()
    {
        if (_consoleScene == null)
            return;

        List<(Microsoft.Extensions.Logging.LogLevel Level, string Message, string Category, DateTime Timestamp)> logsToReplay;

        lock (_logBufferLock)
        {
            // Make a copy to avoid holding the lock while adding to the scene
            logsToReplay = new List<(Microsoft.Extensions.Logging.LogLevel, string, string, DateTime)>(_persistentLogBuffer);
        }

        // Replay all buffered logs to the Logs panel with original timestamps
        foreach (var (level, message, category, timestamp) in logsToReplay)
        {
            _consoleScene.AddLog(level, message, category, timestamp);
        }
    }

    /// <summary>
    /// Syncs script-defined variables to the Variables panel.
    /// </summary>
    private void SyncScriptVariables()
    {
        if (_consoleScene == null)
            return;

        // Get all variables from the script evaluator
        foreach (var (name, typeName, valueGetter) in _evaluator.GetVariables())
        {
            _consoleScene.SetScriptVariable(name, typeName, valueGetter);
        }
    }

    /// <summary>
    /// Creates a stats data provider that gathers performance metrics.
    /// Uses reflection to access PerformanceMonitor if available.
    /// </summary>
    private Func<StatsData> CreateStatsProvider()
    {
        // Try to get PerformanceMonitor via reflection (to avoid direct project reference)
        var perfMonitorType = Type.GetType("PokeSharp.Game.Infrastructure.Diagnostics.PerformanceMonitor, PokeSharp.Game");
        object? perfMonitor = null;

        if (perfMonitorType != null)
        {
            try
            {
                // Try to get it from services
                var getServiceMethod = typeof(ServiceProviderServiceExtensions)
                    .GetMethods()
                    .FirstOrDefault(m => m.Name == "GetService" && m.IsGenericMethod && m.GetParameters().Length == 1);

                if (getServiceMethod != null)
                {
                    var genericMethod = getServiceMethod.MakeGenericMethod(perfMonitorType);
                    perfMonitor = genericMethod.Invoke(null, new object[] { _services });
                }
            }
            catch
            {
                // Ignore - will use fallback
            }
        }

        // Cache reflection results for performance
        var fpsProperty = perfMonitorType?.GetProperty("Fps");
        var frameTimeMsProperty = perfMonitorType?.GetProperty("FrameTimeMs");
        var minFrameTimeMsProperty = perfMonitorType?.GetProperty("MinFrameTimeMs");
        var maxFrameTimeMsProperty = perfMonitorType?.GetProperty("MaxFrameTimeMs");
        var memoryMbProperty = perfMonitorType?.GetProperty("MemoryMb");
        var gen0Property = perfMonitorType?.GetProperty("Gen0Collections");
        var gen1Property = perfMonitorType?.GetProperty("Gen1Collections");
        var gen2Property = perfMonitorType?.GetProperty("Gen2Collections");
        var frameCountProperty = perfMonitorType?.GetProperty("FrameCount");

        // Frame time tracking for fallback
        var frameTimeHistory = new List<float>(60);
        var lastFrameTime = DateTime.Now;
        ulong frameCounter = 0;

        return () =>
        {
            var stats = new StatsData();

            // If we have PerformanceMonitor, use it
            if (perfMonitor != null && fpsProperty != null)
            {
                stats.Fps = (float)(fpsProperty.GetValue(perfMonitor) ?? 60f);
                stats.FrameTimeMs = (float)(frameTimeMsProperty?.GetValue(perfMonitor) ?? 16.67f);
                stats.MinFrameTimeMs = (float)(minFrameTimeMsProperty?.GetValue(perfMonitor) ?? 16f);
                stats.MaxFrameTimeMs = (float)(maxFrameTimeMsProperty?.GetValue(perfMonitor) ?? 17f);
                stats.MemoryMB = (double)(memoryMbProperty?.GetValue(perfMonitor) ?? GC.GetTotalMemory(false) / 1024.0 / 1024.0);
                stats.Gen0Collections = (int)(gen0Property?.GetValue(perfMonitor) ?? GC.CollectionCount(0));
                stats.Gen1Collections = (int)(gen1Property?.GetValue(perfMonitor) ?? GC.CollectionCount(1));
                stats.Gen2Collections = (int)(gen2Property?.GetValue(perfMonitor) ?? GC.CollectionCount(2));
                stats.FrameNumber = (ulong)(frameCountProperty?.GetValue(perfMonitor) ?? 0);
            }
            else
            {
                // Fallback: use GC data and simple timing
                var now = DateTime.Now;
                var elapsed = (float)(now - lastFrameTime).TotalMilliseconds;
                lastFrameTime = now;
                frameCounter++;

                // Keep track of frame times
                frameTimeHistory.Add(elapsed);
                if (frameTimeHistory.Count > 60)
                    frameTimeHistory.RemoveAt(0);

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

            // Entity and system counts
            stats.EntityCount = _world.CountEntities(new Arch.Core.QueryDescription());
            stats.SystemCount = _systemManager.GetMetrics()?.Count ?? 0;

            return stats;
        };
    }

    /// <summary>
    /// Gets all entities from the Arch World as EntityInfo objects.
    /// This is the entity provider for the Entities panel.
    /// </summary>
    private IEnumerable<EntityInfo> GetAllEntitiesAsInfo()
    {
        var result = new List<EntityInfo>();

        try
        {
            // Query all entities from the World
            var entities = new List<Entity>();
            _world.Query(new QueryDescription(), (Entity entity) => entities.Add(entity));

            foreach (var entity in entities)
            {
                if (!_world.IsAlive(entity))
                    continue;

                var info = ConvertEntityToInfo(entity);
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
    /// Converts an Arch Entity to an EntityInfo for display.
    /// </summary>
    private EntityInfo ConvertEntityToInfo(Entity entity)
    {
        var info = new EntityInfo
        {
            Id = entity.Id,
            IsActive = _world.IsAlive(entity),
            Components = new List<string>(),
            Properties = new Dictionary<string, string>()
        };

        try
        {
            // Detect components by checking for known types
            var detectedComponents = DetectEntityComponents(entity);
            info.Components = detectedComponents;

            // Determine name and tag based on detected components
            info.Name = DetermineEntityName(entity, detectedComponents);
            info.Tag = DetermineEntityTag(detectedComponents);

            // Get properties
            info.Properties = GetEntityProperties(entity, detectedComponents);
            info.Properties["Components"] = detectedComponents.Count.ToString();
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
    /// Detects which components an entity has using the component registry.
    /// </summary>
    private List<string> DetectEntityComponents(Entity entity)
    {
        return _componentRegistry.DetectComponents(entity);
    }

    /// <summary>
    /// Determines an entity's display name based on its components.
    /// </summary>
    private string DetermineEntityName(Entity entity, List<string> components)
    {
        return _componentRegistry.DetermineEntityName(entity, components);
    }

    /// <summary>
    /// Determines an entity's tag based on its components.
    /// </summary>
    private string? DetermineEntityTag(List<string> components)
    {
        return _componentRegistry.DetermineEntityTag(components);
    }

    /// <summary>
    /// Gets display properties for an entity using the component registry.
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
}

