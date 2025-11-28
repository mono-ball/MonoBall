using FontStashSharp;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Engine.Scenes;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Components.Debug;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Input;
using PokeSharp.Engine.UI.Debug.Interfaces;
using PokeSharp.Engine.UI.Debug.Layout;
using PokeSharp.Engine.UI.Debug.Models;
using PokeSharp.Engine.UI.Debug.Utilities;

namespace PokeSharp.Engine.UI.Debug.Scenes;

/// <summary>
///     Console scene using the new UI framework.
///     This is the modern replacement for the old QuakeConsole.
/// </summary>
public class ConsoleScene : SceneBase
{
    private readonly InputState _inputState = new();

    // Configuration
    private float _consoleHeightPercent = 0.5f; // 50% of screen height by default
    private ConsolePanel? _consolePanel;
    private EntitiesPanel? _entitiesPanel;
    private LogsPanel? _logsPanel;
    private ProfilerPanel? _profilerPanel;
    private StatsPanel? _statsPanel;
    private TabContainer? _tabContainer;
    private UIContext? _uiContext;
    private VariablesPanel? _variablesPanel;
    private WatchPanel? _watchPanel;

    public ConsoleScene(
        GraphicsDevice graphicsDevice,
        IServiceProvider services,
        ILogger<ConsoleScene> logger
    )
        : base(graphicsDevice, services, logger)
    {
        // Console should block input to scenes below
        ExclusiveInput = true;

        // Render scenes below so game is visible behind console
        RenderScenesBelow = true;

        // Let the game keep updating while console is open
        UpdateScenesBelow = true;
    }

    // Panel interface accessors for command system
    /// <summary>Gets the entity operations interface, or null if panel not loaded.</summary>
    public IEntityOperations? EntityOperations => _entitiesPanel;

    /// <summary>Gets the watch operations interface, or null if panel not loaded.</summary>
    public IWatchOperations? WatchOperations => _watchPanel;

    /// <summary>Gets the variable operations interface, or null if panel not loaded.</summary>
    public IVariableOperations? VariableOperations => _variablesPanel;

    /// <summary>Gets the log operations interface, or null if panel not loaded.</summary>
    public ILogOperations? LogOperations => _logsPanel;

    /// <summary>Gets the profiler operations interface, or null if panel not loaded.</summary>
    public IProfilerOperations? ProfilerOperations => _profilerPanel;

    /// <summary>Gets the stats operations interface, or null if panel not loaded.</summary>
    public IStatsOperations? StatsOperations => _statsPanel;

    // Events for integration with ConsoleSystem
    public event Action<string>? OnCommandSubmitted;
    public event Action<string>? OnRequestCompletions;
    public event Action<string, int>? OnRequestParameterHints; // (text, cursorPos)
    public event Action<string>? OnRequestDocumentation; // (completionText)
    public event Action? OnCloseRequested;
    public event Action? OnReady; // Fired after LoadContent completes and LogsPanel exists

    /// <summary>
    ///     Sets the console height as a percentage of screen height.
    /// </summary>
    public void SetHeightPercent(float percent)
    {
        _consoleHeightPercent = Math.Clamp(percent, 0.25f, 1.0f);

        if (_tabContainer != null)
        {
            _tabContainer.Constraint = new LayoutConstraint
            {
                Anchor = Anchor.StretchTop,
                HeightPercent = _consoleHeightPercent,
                Padding = 10f, // Preserve padding
            };
        }
    }

    /// <summary>
    ///     Appends a line to the console output.
    /// </summary>
    public void AppendOutput(string text, Color color, string category = "General")
    {
        _consolePanel?.AppendOutput(text, color, category);
    }

    /// <summary>
    ///     Clears all console output.
    /// </summary>
    public void ClearOutput()
    {
        _consolePanel?.ClearOutput();
    }

    /// <summary>
    ///     Sets the input prompt (e.g., "> " for normal, "... " for multi-line mode).
    /// </summary>
    public void SetPrompt(string prompt)
    {
        _consolePanel?.SetPrompt(prompt);
    }

    /// <summary>
    ///     Sets auto-completion suggestions.
    /// </summary>
    public void SetCompletions(List<string> completions)
    {
        _consolePanel?.SetCompletions(completions);
    }

    public void SetCompletions(List<SuggestionItem> suggestions)
    {
        _consolePanel?.SetCompletions(suggestions);
    }

    /// <summary>
    ///     Gets the current cursor position in the command input.
    /// </summary>
    public int GetCursorPosition()
    {
        return _consolePanel?.GetCursorPosition() ?? 0;
    }

    /// <summary>
    ///     Sets parameter hints for the current method call.
    /// </summary>
    public void SetParameterHints(ParamHints hints, int currentParameterIndex = 0)
    {
        _consolePanel?.SetParameterHints(hints, currentParameterIndex);
    }

    /// <summary>
    ///     Clears parameter hints.
    /// </summary>
    public void ClearParameterHints()
    {
        _consolePanel?.ClearParameterHints();
    }

    /// <summary>
    ///     Sets documentation for display.
    /// </summary>
    public void SetDocumentation(DocInfo doc)
    {
        _consolePanel?.SetDocumentation(doc);
    }

    /// <summary>
    ///     Clears documentation.
    /// </summary>
    public void ClearDocumentation()
    {
        _consolePanel?.ClearDocumentation();
    }

    /// <summary>
    ///     Gets the active tab index.
    /// </summary>
    public int GetActiveTab()
    {
        return _tabContainer?.ActiveTabIndex ?? 0;
    }

    /// <summary>
    ///     Exports console output to a string.
    /// </summary>
    public string ExportConsoleOutput()
    {
        return _consolePanel?.ExportOutputToString() ?? string.Empty;
    }

    /// <summary>
    ///     Copies console output to clipboard.
    /// </summary>
    public void CopyConsoleOutputToClipboard()
    {
        _consolePanel?.CopyOutputToClipboard();
    }

    /// <summary>
    ///     Gets console output statistics.
    /// </summary>
    public (int TotalLines, int FilteredLines) GetConsoleOutputStats()
    {
        return _consolePanel?.GetOutputStats() ?? (0, 0);
    }

    /// <summary>
    ///     Sets a script-defined variable in the Variables panel.
    /// </summary>
    public void SetScriptVariable(string name, string typeName, Func<object?> valueGetter)
    {
        _variablesPanel?.SetVariable(name, typeName, valueGetter);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Entities Tab Methods (Setup only - operations via EntityOperations interface)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Sets the entity provider function for the Entities panel.
    /// </summary>
    public void SetEntityProvider(Func<IEnumerable<EntityInfo>>? provider)
    {
        _entitiesPanel?.SetEntityProvider(provider);
    }

    /// <summary>
    ///     Sets entities directly (alternative to using a provider).
    /// </summary>
    public void SetEntities(IEnumerable<EntityInfo> entities)
    {
        _entitiesPanel?.SetEntities(entities);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Profiler Tab Methods (Setup only - operations via ProfilerOperations interface)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Sets the system metrics provider function for the Profiler panel.
    /// </summary>
    public void SetSystemMetricsProvider(
        Func<IReadOnlyDictionary<string, SystemMetrics>?>? provider
    )
    {
        _profilerPanel?.SetMetricsProvider(provider);
    }

    /// <summary>
    ///     Sets the stats data provider function for the Stats panel.
    /// </summary>
    public void SetStatsProvider(Func<StatsData>? provider)
    {
        _statsPanel?.SetStatsProvider(provider);
    }

    /// <summary>
    ///     Sets the active tab by index.
    /// </summary>
    public void SetActiveTab(int index)
    {
        _tabContainer?.SetActiveTab(index);
    }

    /// <summary>
    ///     Adds a log entry with a specific timestamp (used for replaying buffered logs).
    ///     This is kept because ILogOperations.Add doesn't support custom timestamps.
    /// </summary>
    public void AddLog(LogLevel level, string message, string category, DateTime timestamp)
    {
        _logsPanel?.AddLog(level, message, category, timestamp);
    }

    /// <summary>
    ///     Gets the command history.
    /// </summary>
    public IReadOnlyList<string> GetCommandHistory()
    {
        return _consolePanel?.GetCommandHistory() ?? Array.Empty<string>();
    }

    /// <summary>
    ///     Clears the command history.
    /// </summary>
    public void ClearCommandHistory()
    {
        _consolePanel?.ClearCommandHistory();
    }

    /// <summary>
    ///     Saves the command history to disk.
    /// </summary>
    public void SaveCommandHistory()
    {
        _consolePanel?.SaveCommandHistory();
    }

    /// <summary>
    ///     Loads the command history from disk.
    /// </summary>
    public void LoadCommandHistory()
    {
        _consolePanel?.LoadCommandHistory();
    }

    public override void Initialize()
    {
        base.Initialize();

        try
        {
            _uiContext = new UIContext(GraphicsDevice);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize ConsoleScene UI context");
            throw;
        }
    }

    public override void LoadContent()
    {
        base.LoadContent();

        try
        {
            // Load font system (bundled Iosevka Nerd Font with system fallback)
            FontSystem? fontSystem = FontLoader.LoadFont();
            if (fontSystem == null)
            {
                Logger.LogError(
                    "Failed to load font system for console. Bundled: {Available}",
                    FontLoader.IsBundledFontAvailable()
                );
                throw new InvalidOperationException("Font system loading failed");
            }

            _uiContext?.SetFontSystem(fontSystem);

            // Create tab container
            _tabContainer = new TabContainer
            {
                Id = "debug_tab_container",
                // Colors set dynamically by TabContainer.OnRenderContainer for theme switching
                BorderThickness = 1,
                Constraint = new LayoutConstraint
                {
                    Anchor = Anchor.StretchTop,
                    HeightPercent = _consoleHeightPercent,
                    Padding = 10f, // Apply padding to create space around console content
                },
            };
            // Create console panel via builder
            _consolePanel = ConsolePanelBuilder.Create().Build();
            _consolePanel.BackgroundColor = Color.Transparent; // Tab container provides background
            _consolePanel.BorderColor = Color.Transparent; // No border - container has it
            _consolePanel.BorderThickness = 0;
            _consolePanel.Constraint = new LayoutConstraint { Anchor = Anchor.Fill, Padding = 0 };
            // Wire up console events
            _consolePanel.OnCommandSubmitted = cmd => OnCommandSubmitted?.Invoke(cmd);
            _consolePanel.OnRequestCompletions = text => OnRequestCompletions?.Invoke(text);
            _consolePanel.OnRequestParameterHints = (text, cursorPos) =>
                OnRequestParameterHints?.Invoke(text, cursorPos);
            _consolePanel.OnRequestDocumentation = completionText =>
                OnRequestDocumentation?.Invoke(completionText);
            _consolePanel.OnCloseRequested = () => OnCloseRequested?.Invoke();
            _consolePanel.OnSizeChanged = size => SetHeightPercent(size.GetHeightPercent());

            // Create watch panel
            _watchPanel = WatchPanelBuilder.Create().Build();
            _watchPanel.BackgroundColor = Color.Transparent;
            _watchPanel.BorderColor = Color.Transparent;
            _watchPanel.BorderThickness = 0;
            _watchPanel.Constraint = new LayoutConstraint { Anchor = Anchor.Fill, Padding = 0 };

            // Create logs panel
            _logsPanel = LogsPanelBuilder.Create().Build();
            _logsPanel.BackgroundColor = Color.Transparent;
            _logsPanel.BorderColor = Color.Transparent;
            _logsPanel.BorderThickness = 0;
            _logsPanel.Constraint = new LayoutConstraint { Anchor = Anchor.Fill, Padding = 0 };

            // Create variables panel
            _variablesPanel = VariablesPanelBuilder.Create().Build();
            _variablesPanel.BackgroundColor = Color.Transparent;
            _variablesPanel.BorderColor = Color.Transparent;
            _variablesPanel.BorderThickness = 0;
            _variablesPanel.Constraint = new LayoutConstraint { Anchor = Anchor.Fill, Padding = 0 };

            // Set up global variables display
            _variablesPanel.SetGlobals(
                new[]
                {
                    new VariablesPanel.GlobalInfo
                    {
                        Name = "Player",
                        TypeName = "IPlayerScripting",
                        Description = "Player control and state API",
                    },
                    new VariablesPanel.GlobalInfo
                    {
                        Name = "World",
                        TypeName = "Arch.Core.World",
                        Description = "ECS World instance",
                    },
                    new VariablesPanel.GlobalInfo
                    {
                        Name = "Api",
                        TypeName = "IScriptingApiProvider",
                        Description = "Scripting API provider",
                    },
                    new VariablesPanel.GlobalInfo
                    {
                        Name = "Systems",
                        TypeName = "SystemManager",
                        Description = "ECS system manager",
                    },
                }
            );

            // Create entities panel
            _entitiesPanel = EntitiesPanelBuilder
                .Create()
                .WithAutoRefresh(true)
                .WithRefreshInterval(1.0f)
                .Build();
            _entitiesPanel.BackgroundColor = Color.Transparent;
            _entitiesPanel.BorderColor = Color.Transparent;
            _entitiesPanel.BorderThickness = 0;
            _entitiesPanel.Constraint = new LayoutConstraint { Anchor = Anchor.Fill, Padding = 0 };

            // Create profiler panel
            _profilerPanel = ProfilerPanelBuilder
                .Create()
                .WithTargetFrameTime(16.67f) // 60 FPS
                .WithWarningThreshold(2.0f) // Warn if >2ms
                .WithRefreshInterval(0.1f) // 10 FPS refresh
                .Build();
            _profilerPanel.BackgroundColor = Color.Transparent;
            _profilerPanel.BorderColor = Color.Transparent;
            _profilerPanel.BorderThickness = 0;
            _profilerPanel.Constraint = new LayoutConstraint { Anchor = Anchor.Fill, Padding = 0 };

            // Create stats panel
            _statsPanel = StatsPanelBuilder.Create().Build();
            _statsPanel.BackgroundColor = Color.Transparent;
            _statsPanel.BorderColor = Color.Transparent;
            _statsPanel.BorderThickness = 0;
            _statsPanel.Constraint = new LayoutConstraint { Anchor = Anchor.Fill, Padding = 0 };

            // Add tabs to container
            _tabContainer.AddTab("Console", _consolePanel);
            _tabContainer.AddTab("Watch", _watchPanel);
            _tabContainer.AddTab("Logs", _logsPanel);
            _tabContainer.AddTab("Variables", _variablesPanel);
            _tabContainer.AddTab("Entities", _entitiesPanel);
            _tabContainer.AddTab("Profiler", _profilerPanel);
            _tabContainer.AddTab("Stats", _statsPanel);
            _tabContainer.SetActiveTab(0);

            // Show the console
            _consolePanel.Show();

            // Fire OnReady event - LogsPanel now exists and can receive buffered logs
            OnReady?.Invoke();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load ConsoleScene content");
            throw;
        }
    }

    public override void Update(GameTime gameTime)
    {
        if (_uiContext == null || _consolePanel == null)
        {
            return;
        }

        try
        {
            _inputState.GameTime = gameTime; // Set GameTime for cursor blinking
            _inputState.Update();

            // Handle escape to close - when NOT on Console tab, close directly
            // (Console tab handles its own escape for dismissing overlays first)
            if (_inputState.IsKeyPressed(Keys.Escape))
            {
                if (_tabContainer?.ActiveTabIndex != 0)
                {
                    // Not on Console tab - close immediately
                    OnCloseRequested?.Invoke();
                }
                // Console tab escape is handled by ConsolePanel
            }

            // Handle tab switching shortcuts (Ctrl+1 through Ctrl+4)
            HandleTabShortcuts();

            // Process deferred close requests (safe to do during Update, not Draw)
            _consolePanel.ProcessDeferredClose();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating console input state");
        }
    }

    /// <summary>
    ///     Handles keyboard shortcuts for tab switching and font size.
    /// </summary>
    private void HandleTabShortcuts()
    {
        if (_tabContainer == null)
        {
            return;
        }

        // Check if Ctrl is held
        if (!_inputState.IsCtrlDown())
        {
            return;
        }

        // Tab switching: Check for tab shortcuts using centralized definitions
        foreach (ConsoleTabs.TabDefinition tab in ConsoleTabs.All)
        {
            if (tab.Shortcut.HasValue && _inputState.IsKeyPressed(tab.Shortcut.Value))
            {
                _tabContainer.SetActiveTab(tab.Index);
                Logger.LogDebug(
                    "Switched to {TabName} tab (Ctrl+{KeyNum})",
                    tab.Name,
                    tab.Index + 1
                );
                return;
            }
        }

        // Font size controls: Ctrl+Plus, Ctrl+Minus, Ctrl+0
        if (_inputState.IsKeyPressed(Keys.OemPlus) || _inputState.IsKeyPressed(Keys.Add))
        {
            _uiContext?.Renderer.IncreaseFontSize();
            Logger.LogDebug("Font size increased to {FontSize}", _uiContext?.Renderer.FontSize);
        }
        else if (_inputState.IsKeyPressed(Keys.OemMinus) || _inputState.IsKeyPressed(Keys.Subtract))
        {
            _uiContext?.Renderer.DecreaseFontSize();
            Logger.LogDebug("Font size decreased to {FontSize}", _uiContext?.Renderer.FontSize);
        }
        else if (_inputState.IsKeyPressed(Keys.D0))
        {
            _uiContext?.Renderer.ResetFontSize();
            Logger.LogDebug("Font size reset to default");
        }
    }

    public override void Draw(GameTime gameTime)
    {
        if (_uiContext == null || _tabContainer == null)
        {
            Logger.LogWarning("Cannot draw: UIContext or TabContainer is null");
            return;
        }

        bool beginFrameCalled = false;
        try
        {
            // Don't clear - let the game render behind us
            // The console panel has a semi-transparent background

            // Update screen size in case window was resized
            _uiContext.UpdateScreenSize(
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height
            );

            // Begin frame and update input state
            _uiContext.BeginFrame(_inputState);
            beginFrameCalled = true;

            // CRITICAL: Update ONLY hover state before rendering
            // This provides immediate hover feedback using previous frame's positions
            // Incremental updates during RegisterComponent() will refine this with current positions
            _uiContext.UpdateHoverState();

            // Render tab container UI (includes all tabs and active content)
            // Components register during render and get accurate hover state via incremental updates
            _tabContainer.Render(_uiContext);

            // CRITICAL: Update pressed state AFTER rendering
            // This ensures pressed state is set based on correct hover state from incremental updates
            // This is the key fix - pressed state must use hover state AFTER components register
            _uiContext.UpdatePressedState();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error rendering console");
        }
        finally
        {
            // CRITICAL: Always call EndFrame if BeginFrame was called, even if an exception occurred
            if (beginFrameCalled)
            {
                try
                {
                    _uiContext.EndFrame();
                }
                catch (Exception endEx)
                {
                    Logger.LogError(endEx, "Error in EndFrame");
                }
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _watchPanel?.Dispose();
            _uiContext?.Dispose();
        }

        base.Dispose(disposing);
    }
}
