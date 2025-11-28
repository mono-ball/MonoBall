using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Core.Services;
using PokeSharp.Engine.Debug.Breakpoints;
using PokeSharp.Engine.Debug.Common;
using PokeSharp.Engine.Debug.Console.Scripting;
using PokeSharp.Engine.Debug.Features;
using PokeSharp.Engine.UI.Debug.Components.Debug;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Interfaces;
using PokeSharp.Engine.UI.Debug.Models;
using PokeSharp.Engine.UI.Debug.Scenes;

namespace PokeSharp.Engine.Debug.Commands;

/// <summary>
///     Implementation of IConsoleContext that provides services to command execution.
/// </summary>
public class ConsoleContext : IConsoleContext
{
    private readonly Action _closeAction;
    private readonly ConsoleScene _consoleScene;
    private readonly ConsoleLoggingCallbacks _loggingCallbacks;
    private readonly ConsoleServices _services;

    /// <summary>
    ///     Creates a new ConsoleContext with aggregated services.
    ///     This is the preferred constructor for new code.
    /// </summary>
    /// <param name="consoleScene">The console scene for output and UI operations.</param>
    /// <param name="closeAction">Action to close the console.</param>
    /// <param name="loggingCallbacks">Callbacks for logging control.</param>
    /// <param name="timeControl">Optional time control interface (null if unavailable).</param>
    /// <param name="services">Aggregated console services.</param>
    public ConsoleContext(
        ConsoleScene consoleScene,
        Action closeAction,
        ConsoleLoggingCallbacks loggingCallbacks,
        ITimeControl? timeControl,
        ConsoleServices services
    )
    {
        _consoleScene = consoleScene ?? throw new ArgumentNullException(nameof(consoleScene));
        _closeAction = closeAction ?? throw new ArgumentNullException(nameof(closeAction));
        _loggingCallbacks =
            loggingCallbacks ?? throw new ArgumentNullException(nameof(loggingCallbacks));
        TimeControl = timeControl; // Can be null - time control is optional
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    ///     Creates a new ConsoleContext without time control.
    /// </summary>
    public ConsoleContext(
        ConsoleScene consoleScene,
        Action closeAction,
        ConsoleLoggingCallbacks loggingCallbacks,
        ConsoleServices services
    )
        : this(consoleScene, closeAction, loggingCallbacks, null, services) { }

    public bool EntityAutoRefresh
    {
        get => Entities.AutoRefresh;
        set => Entities.AutoRefresh = value;
    }

    public float EntityRefreshInterval
    {
        get => Entities.RefreshInterval;
        set => Entities.RefreshInterval = value;
    }

    public float EntityHighlightDuration
    {
        get => Entities.HighlightDuration;
        set => Entities.HighlightDuration = value;
    }

    public int? SelectedEntityId => Entities.SelectedId;

    public UITheme Theme => UITheme.Dark;

    // Panel operation interfaces - expose panels directly for commands
    // These are non-nullable because panels are always created with the console (in LoadContent)
    // Commands can only run after the console is visible, so panels are guaranteed to exist
    public IEntityOperations Entities =>
        _consoleScene.EntityOperations
        ?? throw new InvalidOperationException("Entities panel not initialized");

    public IWatchOperations Watches =>
        _consoleScene.WatchOperations
        ?? throw new InvalidOperationException("Watches panel not initialized");

    public IVariableOperations Variables =>
        _consoleScene.VariableOperations
        ?? throw new InvalidOperationException("Variables panel not initialized");

    public ILogOperations Logs =>
        _consoleScene.LogOperations
        ?? throw new InvalidOperationException("Logs panel not initialized");

    // External dependencies - nullable because they may not be available
    // TimeControl: requires ITimeControl service in DI
    // Profiler: requires SystemMetrics provider
    // Breakpoints: requires time control and script evaluator
    public IProfilerOperations? Profiler => _consoleScene.ProfilerOperations;
    public IStatsOperations? Stats => _consoleScene.StatsOperations;
    public IBreakpointOperations? Breakpoints => _services.BreakpointManager;

    public void WriteLine(string text)
    {
        _consoleScene.AppendOutput(text, Theme.TextPrimary);
    }

    public void WriteLine(string text, Color color)
    {
        _consoleScene.AppendOutput(text, color);
    }

    public void Clear()
    {
        _consoleScene.ClearOutput();
    }

    public bool IsLoggingEnabled => _loggingCallbacks.IsLoggingEnabled();

    public void SetLoggingEnabled(bool enabled)
    {
        _loggingCallbacks.SetLoggingEnabled(enabled);
    }

    public LogLevel MinimumLogLevel => _loggingCallbacks.GetLogLevel();

    public void SetMinimumLogLevel(LogLevel level)
    {
        _loggingCallbacks.SetLogLevel(level);
    }

    public void Close()
    {
        _closeAction();
    }

    public IEnumerable<IConsoleCommand> GetAllCommands()
    {
        return _services.CommandRegistry.GetAllCommands();
    }

    public IConsoleCommand? GetCommand(string name)
    {
        return _services.CommandRegistry.GetCommand(name);
    }

    public IReadOnlyList<string> GetCommandHistory()
    {
        return _consoleScene.GetCommandHistory();
    }

    public void ClearCommandHistory()
    {
        _consoleScene.ClearCommandHistory();
    }

    public void SaveCommandHistory()
    {
        _consoleScene.SaveCommandHistory();
    }

    public void LoadCommandHistory()
    {
        _consoleScene.LoadCommandHistory();
    }

    public bool DefineAlias(string name, string command)
    {
        bool result = _services.AliasManager.DefineAlias(name, command);
        if (result)
        {
            _services.AliasManager.SaveAliases();
        }

        return result;
    }

    public bool RemoveAlias(string name)
    {
        bool result = _services.AliasManager.RemoveAlias(name);
        if (result)
        {
            _services.AliasManager.SaveAliases();
        }

        return result;
    }

    public IReadOnlyDictionary<string, string> GetAllAliases()
    {
        return _services.AliasManager.GetAllAliases();
    }

    public List<string> ListScripts()
    {
        return _services.ScriptManager.ListScripts();
    }

    public string GetScriptsDirectory()
    {
        return _services.ScriptManager.ScriptsDirectory;
    }

    public string? LoadScript(string filename)
    {
        Result<string> result = _services.ScriptManager.LoadScript(filename);
        return result.IsSuccess ? result.Value : null;
    }

    public bool SaveScript(string filename, string content)
    {
        Result result = _services.ScriptManager.SaveScript(filename, content);
        return result.IsSuccess;
    }

    public async Task ExecuteScriptAsync(string scriptContent)
    {
        EvaluationResult result = await _services.ScriptEvaluator.EvaluateAsync(
            scriptContent,
            _services.ScriptGlobals
        );

        // Handle compilation errors
        if (result.IsCompilationError && result.Errors != null)
        {
            WriteLine("Compilation Error:", Theme.Error);
            foreach (FormattedError error in result.Errors)
            {
                WriteLine($"  {error.Message}", Theme.Error);
            }

            return;
        }

        // Handle runtime errors
        if (result.IsRuntimeError)
        {
            WriteLine(
                $"Runtime Error: {result.RuntimeException?.Message ?? "Unknown error"}",
                Theme.Error
            );
            if (result.RuntimeException != null)
            {
                WriteLine($"  {result.RuntimeException.GetType().Name}", Theme.TextSecondary);
            }

            return;
        }

        // Display result if there is one
        if (!string.IsNullOrWhiteSpace(result.Output))
        {
            WriteLine(result.Output, Theme.Success);
        }
    }

    public void ResetScriptState()
    {
        _services.ScriptEvaluator.Reset();
    }

    public IReadOnlyDictionary<int, string> GetAllBookmarks()
    {
        return _services.BookmarkManager.GetAllBookmarks();
    }

    public string? GetBookmark(int fkeyNumber)
    {
        return _services.BookmarkManager.GetBookmark(fkeyNumber);
    }

    public bool SetBookmark(int fkeyNumber, string command)
    {
        bool result = _services.BookmarkManager.BookmarkCommand(fkeyNumber, command);
        if (result)
        {
            _services.BookmarkManager.SaveBookmarks(); // Auto-save on change
        }

        return result;
    }

    public bool RemoveBookmark(int fkeyNumber)
    {
        bool result = _services.BookmarkManager.RemoveBookmark(fkeyNumber);
        if (result)
        {
            _services.BookmarkManager.SaveBookmarks(); // Auto-save on change
        }

        return result;
    }

    public void ClearAllBookmarks()
    {
        _services.BookmarkManager.ClearAll();
        _services.BookmarkManager.SaveBookmarks(); // Auto-save on change
    }

    public bool SaveBookmarks()
    {
        return _services.BookmarkManager.SaveBookmarks();
    }

    public int LoadBookmarks()
    {
        return _services.BookmarkManager.LoadBookmarks();
    }

    public bool AddWatch(string name, string expression)
    {
        return AddWatch(name, expression, null, null);
    }

    public bool AddWatch(string name, string expression, string? group, string? condition)
    {
        // Create value evaluator lambda
        Func<object?> valueGetter = () =>
        {
            try
            {
                Task<EvaluationResult> task = _services.ScriptEvaluator.EvaluateAsync(
                    expression,
                    _services.ScriptGlobals
                );
                task.Wait();
                EvaluationResult result = task.Result;

                if (result.IsSuccess)
                {
                    return string.IsNullOrWhiteSpace(result.Output) ? "<null>" : result.Output;
                }

                if (result.Errors != null && result.Errors.Count > 0)
                {
                    return $"<error: {result.Errors[0].Message}>";
                }

                return "<error: evaluation failed>";
            }
            catch (Exception ex)
            {
                return $"<error: {ex.Message}>";
            }
        };

        // Create condition evaluator lambda if condition provided
        Func<bool>? conditionEvaluator = null;
        if (!string.IsNullOrWhiteSpace(condition))
        {
            conditionEvaluator = () =>
            {
                try
                {
                    Task<EvaluationResult> task = _services.ScriptEvaluator.EvaluateAsync(
                        condition,
                        _services.ScriptGlobals
                    );
                    task.Wait();
                    EvaluationResult result = task.Result;

                    if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Output))
                    {
                        // Try to parse as boolean
                        if (bool.TryParse(result.Output.Trim(), out bool boolResult))
                        {
                            return boolResult;
                        }

                        // Non-empty result treated as true
                        return true;
                    }

                    return false;
                }
                catch
                {
                    return false;
                }
            };
        }

        return Watches.Add(name, expression, valueGetter, group, condition, conditionEvaluator);
    }

    public bool SaveWatchPreset(string name, string description)
    {
        try
        {
            (
                List<(
                    string Name,
                    string Expression,
                    string? Group,
                    string? Condition,
                    bool IsPinned,
                    string? AlertType,
                    object? AlertThreshold,
                    string? ComparisonWith,
                    string? ComparisonLabel
                )> Watches,
                double UpdateInterval,
                bool AutoUpdateEnabled
            )? config = Watches.ExportConfiguration();
            if (config == null)
            {
                return false;
            }

            (
                List<(
                    string Name,
                    string Expression,
                    string? Group,
                    string? Condition,
                    bool IsPinned,
                    string? AlertType,
                    object? AlertThreshold,
                    string? ComparisonWith,
                    string? ComparisonLabel
                )> watches,
                double updateInterval,
                bool autoUpdateEnabled
            ) = config.Value;

            var preset = new WatchPreset
            {
                Name = name,
                Description = description,
                CreatedAt = DateTime.Now,
                UpdateInterval = updateInterval,
                AutoUpdateEnabled = autoUpdateEnabled,
                Watches = watches
                    .Select(w => new WatchPresetEntry
                    {
                        Name = w.Name,
                        Expression = w.Expression,
                        Group = w.Group,
                        Condition = w.Condition,
                        IsPinned = w.IsPinned,
                        Alert =
                            w.AlertType != null
                                ? new WatchAlertConfig
                                {
                                    Type = w.AlertType,
                                    Threshold = w.AlertThreshold?.ToString(),
                                }
                                : null,
                        Comparison =
                            w.ComparisonWith != null
                                ? new WatchComparisonConfig
                                {
                                    CompareWith = w.ComparisonWith,
                                    Label = w.ComparisonLabel ?? "Expected",
                                }
                                : null,
                    })
                    .ToList(),
            };

            return _services.WatchPresetManager.SavePreset(preset);
        }
        catch
        {
            return false;
        }
    }

    public bool LoadWatchPreset(string name)
    {
        try
        {
            WatchPreset? preset = _services.WatchPresetManager.LoadPreset(name);
            if (preset == null)
            {
                return false;
            }

            // Import configuration (clears watches and sets update settings)
            Watches.Clear();
            Watches.UpdateInterval = preset.UpdateInterval;
            Watches.AutoUpdate = preset.AutoUpdateEnabled;

            // Add watches from preset
            foreach (WatchPresetEntry watch in preset.Watches)
            {
                // Create value getter for the expression
                Func<object?> valueGetter = () =>
                {
                    try
                    {
                        EvaluationResult result = _services
                            .ScriptEvaluator.EvaluateAsync(
                                watch.Expression,
                                _services.ScriptGlobals
                            )
                            .Result;
                        return result.IsSuccess
                            ? result.Output
                            : $"<error: {result.Errors?[0].Message ?? "evaluation failed"}>";
                    }
                    catch (Exception ex)
                    {
                        return $"<error: {ex.Message}>";
                    }
                };

                // Create condition evaluator if condition exists
                Func<bool>? conditionEvaluator = null;
                if (!string.IsNullOrEmpty(watch.Condition))
                {
                    conditionEvaluator = () =>
                    {
                        try
                        {
                            EvaluationResult result = _services
                                .ScriptEvaluator.EvaluateAsync(
                                    watch.Condition,
                                    _services.ScriptGlobals
                                )
                                .Result;
                            if (!result.IsSuccess)
                            {
                                return false;
                            }

                            // Output is a string representation, parse it as boolean
                            if (string.IsNullOrEmpty(result.Output))
                            {
                                return false;
                            }

                            if (bool.TryParse(result.Output, out bool boolValue))
                            {
                                return boolValue;
                            }

                            // Consider "true" (case-insensitive) or non-empty strings as true
                            return result.Output.Equals("true", StringComparison.OrdinalIgnoreCase);
                        }
                        catch
                        {
                            return false;
                        }
                    };
                }

                // Add the watch
                AddWatch(watch.Name, watch.Expression, watch.Group, watch.Condition);

                // Pin if needed
                if (watch.IsPinned)
                {
                    PinWatch(watch.Name);
                }

                // Set up alert if configured
                if (watch.Alert != null)
                {
                    object? alertThreshold = null;
                    if (watch.Alert.Threshold != null)
                    {
                        if (double.TryParse(watch.Alert.Threshold, out double numThreshold))
                        {
                            alertThreshold = numThreshold;
                        }
                        else
                        {
                            alertThreshold = watch.Alert.Threshold;
                        }
                    }

                    SetWatchAlert(watch.Name, watch.Alert.Type, alertThreshold);
                }

                // Set up comparison if configured
                if (watch.Comparison != null)
                {
                    SetWatchComparison(
                        watch.Name,
                        watch.Comparison.CompareWith,
                        watch.Comparison.Label
                    );
                }
            }

            WriteLine($"Loaded preset '{name}' ({preset.Watches.Count} watches)", Theme.Success);
            return true;
        }
        catch (Exception ex)
        {
            WriteLine($"Failed to load preset '{name}': {ex.Message}", Theme.Error);
            return false;
        }
    }

    public IEnumerable<(
        string Name,
        string Description,
        int WatchCount,
        DateTime CreatedAt
    )> ListWatchPresets()
    {
        return _services.WatchPresetManager.ListPresets();
    }

    public bool DeleteWatchPreset(string name)
    {
        return _services.WatchPresetManager.DeletePreset(name);
    }

    public bool WatchPresetExists(string name)
    {
        return _services.WatchPresetManager.PresetExists(name);
    }

    public void CreateBuiltInWatchPresets()
    {
        _services.WatchPresetManager.CreateBuiltInPresets();
    }

    public void SwitchToTab(int tabIndex)
    {
        _consoleScene.SetActiveTab(tabIndex);
    }

    public int GetActiveTab()
    {
        return _consoleScene.GetActiveTab();
    }

    public void SetConsoleHeight(float heightPercent)
    {
        _consoleScene.SetHeightPercent(heightPercent);
    }

    public string ExportConsoleOutput()
    {
        return _consoleScene.ExportConsoleOutput();
    }

    public void CopyConsoleOutputToClipboard()
    {
        _consoleScene.CopyConsoleOutputToClipboard();
    }

    public (int TotalLines, int FilteredLines) GetConsoleOutputStats()
    {
        return _consoleScene.GetConsoleOutputStats();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Time Control
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Gets the time control interface, or null if time control is not available.
    /// </summary>
    public ITimeControl? TimeControl { get; }

    public bool RemoveWatch(string name)
    {
        return Watches.Remove(name);
    }

    public void ClearWatches()
    {
        Watches.Clear();
    }

    public bool ToggleWatchAutoUpdate()
    {
        Watches.AutoUpdate = !Watches.AutoUpdate;
        return Watches.AutoUpdate;
    }

    public int GetWatchCount()
    {
        return Watches.Count;
    }

    public bool PinWatch(string name)
    {
        return Watches.Pin(name);
    }

    public bool UnpinWatch(string name)
    {
        return Watches.Unpin(name);
    }

    public bool IsWatchPinned(string name)
    {
        return Watches.IsPinned(name);
    }

    public bool SetWatchInterval(double intervalSeconds)
    {
        if (
            intervalSeconds < WatchPanel.MinUpdateInterval
            || intervalSeconds > WatchPanel.MaxUpdateInterval
        )
        {
            return false;
        }

        Watches.UpdateInterval = intervalSeconds;
        return true;
    }

    public bool CollapseWatchGroup(string groupName)
    {
        return Watches.CollapseGroup(groupName);
    }

    public bool ExpandWatchGroup(string groupName)
    {
        return Watches.ExpandGroup(groupName);
    }

    public bool ToggleWatchGroup(string groupName)
    {
        return Watches.ToggleGroup(groupName);
    }

    public IEnumerable<string> GetWatchGroups()
    {
        return Watches.GetGroups();
    }

    public bool SetWatchAlert(string name, string alertType, object? threshold)
    {
        return Watches.SetAlert(name, alertType, threshold);
    }

    public bool RemoveWatchAlert(string name)
    {
        return Watches.RemoveAlert(name);
    }

    public IEnumerable<(string Name, string AlertType, bool Triggered)> GetWatchesWithAlerts()
    {
        return Watches.GetWatchesWithAlerts();
    }

    public bool ClearWatchAlertStatus(string name)
    {
        return Watches.ClearAlertStatus(name);
    }

    public bool SetWatchComparison(
        string watchName,
        string compareWithName,
        string comparisonLabel = "Expected"
    )
    {
        return Watches.SetComparison(watchName, compareWithName, comparisonLabel);
    }

    public bool RemoveWatchComparison(string name)
    {
        return Watches.RemoveComparison(name);
    }

    public IEnumerable<(string Name, string ComparedWith)> GetWatchesWithComparisons()
    {
        return Watches.GetWatchesWithComparisons();
    }

    public void SetLogFilter(LogLevel level)
    {
        Logs.SetFilterLevel(level);
    }

    public void SetLogSearch(string? searchText)
    {
        Logs.SetSearch(searchText);
    }

    public void AddLog(LogLevel level, string message, string category = "General")
    {
        Logs.Add(level, message, category);
    }

    public void ClearLogs()
    {
        Logs.Clear();
    }

    public int GetLogCount()
    {
        return Logs.Count;
    }

    public void SetLogCategoryFilter(IEnumerable<string>? categories)
    {
        Logs.SetCategoryFilter(categories);
    }

    public void ClearLogCategoryFilter()
    {
        Logs.ClearCategoryFilter();
    }

    public IEnumerable<string> GetLogCategories()
    {
        return Logs.GetCategories();
    }

    public Dictionary<string, int> GetLogCategoryCounts()
    {
        return Logs.GetCategoryCounts();
    }

    public string ExportLogs(
        bool includeTimestamp = true,
        bool includeLevel = true,
        bool includeCategory = false
    )
    {
        return Logs.Export(includeTimestamp, includeLevel, includeCategory);
    }

    public string ExportLogsToCsv()
    {
        return Logs.ExportToCsv();
    }

    public void CopyLogsToClipboard()
    {
        Logs.CopyToClipboard();
    }

    public (
        int Total,
        int Filtered,
        int Errors,
        int Warnings,
        int LastMinute,
        int Categories
    ) GetLogStatistics()
    {
        return Logs.GetStatistics();
    }

    public Dictionary<LogLevel, int> GetLogLevelCounts()
    {
        return Logs.GetLevelCounts();
    }

    public string ExportWatchesToCsv()
    {
        return Watches.ExportToCsv();
    }

    public void CopyWatchesToClipboard(bool asCsv = false)
    {
        Watches.CopyToClipboard(asCsv);
    }

    public (int Total, int Pinned, int WithErrors, int WithAlerts, int Groups) GetWatchStatistics()
    {
        return Watches.GetStatistics();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Variables Tab
    // ═══════════════════════════════════════════════════════════════════════════

    public (int Variables, int Globals, int Pinned, int Expanded) GetVariableStatistics()
    {
        return Variables.GetStatistics();
    }

    public IEnumerable<string> GetVariableNames()
    {
        return Variables.GetNames();
    }

    public object? GetVariableValue(string name)
    {
        return Variables.GetValue(name);
    }

    public void SetVariableSearchFilter(string filter)
    {
        Variables.SetSearchFilter(filter);
    }

    public void ClearVariableSearchFilter()
    {
        Variables.ClearSearchFilter();
    }

    public bool ExpandVariable(string path)
    {
        Variables.Expand(path);
        return true;
    }

    public void CollapseVariable(string path)
    {
        Variables.Collapse(path);
    }

    public void ExpandAllVariables()
    {
        Variables.ExpandAll();
    }

    public void CollapseAllVariables()
    {
        Variables.CollapseAll();
    }

    public void PinVariable(string name)
    {
        Variables.Pin(name);
    }

    public void UnpinVariable(string name)
    {
        Variables.Unpin(name);
    }

    public void ClearVariables()
    {
        Variables.Clear();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Entities Tab
    // ═══════════════════════════════════════════════════════════════════════════

    public void RefreshEntities()
    {
        Entities.Refresh();
    }

    public void SetEntityTagFilter(string tag)
    {
        Entities.SetTagFilter(tag);
    }

    public void SetEntitySearchFilter(string search)
    {
        Entities.SetSearchFilter(search);
    }

    public void SetEntityComponentFilter(string componentName)
    {
        Entities.SetComponentFilter(componentName);
    }

    public void ClearEntityFilters()
    {
        Entities.ClearFilters();
    }

    public (string Tag, string Search, string Component) GetEntityFilters()
    {
        return Entities.GetFilters();
    }

    public void SelectEntity(int entityId)
    {
        Entities.Select(entityId);
    }

    public void ExpandEntity(int entityId)
    {
        Entities.Expand(entityId);
    }

    public void CollapseEntity(int entityId)
    {
        Entities.Collapse(entityId);
    }

    public bool ToggleEntity(int entityId)
    {
        return Entities.Toggle(entityId);
    }

    public void ExpandAllEntities()
    {
        Entities.ExpandAll();
    }

    public void CollapseAllEntities()
    {
        Entities.CollapseAll();
    }

    public void PinEntity(int entityId)
    {
        Entities.Pin(entityId);
    }

    public void UnpinEntity(int entityId)
    {
        Entities.Unpin(entityId);
    }

    public (int Total, int Filtered, int Pinned, int Expanded) GetEntityStatistics()
    {
        return Entities.GetStatistics();
    }

    public Dictionary<string, int> GetEntityTagCounts()
    {
        return Entities.GetTagCounts();
    }

    public IEnumerable<string> GetEntityComponentNames()
    {
        return Entities.GetComponentNames();
    }

    public IEnumerable<string> GetEntityTags()
    {
        return Entities.GetTags();
    }

    public EntityInfo? FindEntity(int entityId)
    {
        return Entities.Find(entityId);
    }

    public IEnumerable<EntityInfo> FindEntitiesByName(string name)
    {
        return Entities.FindByName(name);
    }

    public (int Spawned, int Removed, int CurrentlyHighlighted) GetEntitySessionStats()
    {
        return Entities.GetSessionStats();
    }

    public void ClearEntitySessionStats()
    {
        Entities.ClearSessionStats();
    }

    public IEnumerable<int> GetNewEntityIds()
    {
        return Entities.GetNewEntityIds();
    }

    public string ExportEntitiesToText(bool includeComponents = true, bool includeProperties = true)
    {
        return Entities.ExportToText(includeComponents, includeProperties);
    }

    public string ExportEntitiesToCsv()
    {
        return Entities.ExportToCsv();
    }

    public string? ExportSelectedEntity()
    {
        return Entities.ExportSelected();
    }

    public void CopyEntitiesToClipboard(bool asCsv = false)
    {
        Entities.CopyToClipboard(asCsv);
    }
}
