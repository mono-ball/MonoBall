using System;
using Microsoft.Xna.Framework;
using Microsoft.Extensions.DependencyInjection;
using PokeSharp.Engine.Core.Services;
using PokeSharp.Engine.Debug.Breakpoints;
using PokeSharp.Engine.UI.Debug.Components.Debug;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Interfaces;
using PokeSharp.Engine.UI.Debug.Scenes;
using PokeSharp.Engine.Debug.Console.Features;
using PokeSharp.Engine.Debug.Console.Scripting;
using PokeSharp.Engine.Debug.Scripting;
using PokeSharp.Engine.Debug.Features;

namespace PokeSharp.Engine.Debug.Commands;

/// <summary>
/// Implementation of IConsoleContext that provides services to command execution.
/// </summary>
public class ConsoleContext : IConsoleContext
{
    private readonly ConsoleScene _consoleScene;
    private readonly Action _closeAction;
    private readonly ConsoleLoggingCallbacks _loggingCallbacks;
    private readonly ITimeControl? _timeControl;
    private readonly ConsoleServices _services;

    /// <summary>
    /// Creates a new ConsoleContext with aggregated services.
    /// This is the preferred constructor for new code.
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
        ConsoleServices services)
    {
        _consoleScene = consoleScene ?? throw new ArgumentNullException(nameof(consoleScene));
        _closeAction = closeAction ?? throw new ArgumentNullException(nameof(closeAction));
        _loggingCallbacks = loggingCallbacks ?? throw new ArgumentNullException(nameof(loggingCallbacks));
        _timeControl = timeControl; // Can be null - time control is optional
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// Creates a new ConsoleContext without time control.
    /// </summary>
    public ConsoleContext(
        ConsoleScene consoleScene,
        Action closeAction,
        ConsoleLoggingCallbacks loggingCallbacks,
        ConsoleServices services)
        : this(consoleScene, closeAction, loggingCallbacks, null, services)
    {
    }

    public UITheme Theme => UITheme.Dark;

    // Panel operation interfaces - expose panels directly for commands
    // These are non-nullable because panels are always created with the console (in LoadContent)
    // Commands can only run after the console is visible, so panels are guaranteed to exist
    public IEntityOperations Entities => _consoleScene.EntityOperations
        ?? throw new InvalidOperationException("Entities panel not initialized");
    public IWatchOperations Watches => _consoleScene.WatchOperations
        ?? throw new InvalidOperationException("Watches panel not initialized");
    public IVariableOperations Variables => _consoleScene.VariableOperations
        ?? throw new InvalidOperationException("Variables panel not initialized");
    public ILogOperations Logs => _consoleScene.LogOperations
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

    public Microsoft.Extensions.Logging.LogLevel MinimumLogLevel => _loggingCallbacks.GetLogLevel();

    public void SetMinimumLogLevel(Microsoft.Extensions.Logging.LogLevel level)
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
        var result = _services.AliasManager.DefineAlias(name, command);
        if (result)
        {
            _services.AliasManager.SaveAliases();
        }
        return result;
    }

    public bool RemoveAlias(string name)
    {
        var result = _services.AliasManager.RemoveAlias(name);
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
        var result = _services.ScriptManager.LoadScript(filename);
        return result.IsSuccess ? result.Value : null;
    }

    public bool SaveScript(string filename, string content)
    {
        var result = _services.ScriptManager.SaveScript(filename, content);
        return result.IsSuccess;
    }

    public async Task ExecuteScriptAsync(string scriptContent)
    {
        var result = await _services.ScriptEvaluator.EvaluateAsync(scriptContent, _services.ScriptGlobals);

        // Handle compilation errors
        if (result.IsCompilationError && result.Errors != null)
        {
            WriteLine("Compilation Error:", Theme.Error);
            foreach (var error in result.Errors)
            {
                WriteLine($"  {error.Message}", Theme.Error);
            }
            return;
        }

        // Handle runtime errors
        if (result.IsRuntimeError)
        {
            WriteLine($"Runtime Error: {result.RuntimeException?.Message ?? "Unknown error"}", Theme.Error);
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
        var result = _services.BookmarkManager.BookmarkCommand(fkeyNumber, command);
        if (result) _services.BookmarkManager.SaveBookmarks(); // Auto-save on change
        return result;
    }

    public bool RemoveBookmark(int fkeyNumber)
    {
        var result = _services.BookmarkManager.RemoveBookmark(fkeyNumber);
        if (result) _services.BookmarkManager.SaveBookmarks(); // Auto-save on change
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
                var task = _services.ScriptEvaluator.EvaluateAsync(expression, _services.ScriptGlobals);
                task.Wait();
                var result = task.Result;

                if (result.IsSuccess)
                {
                    return string.IsNullOrWhiteSpace(result.Output) ? "<null>" : result.Output;
                }
                else
                {
                    if (result.Errors != null && result.Errors.Count > 0)
                    {
                        return $"<error: {result.Errors[0].Message}>";
                    }
                    return "<error: evaluation failed>";
                }
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
                    var task = _services.ScriptEvaluator.EvaluateAsync(condition, _services.ScriptGlobals);
                    task.Wait();
                    var result = task.Result;

                    if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Output))
                    {
                        // Try to parse as boolean
                        if (bool.TryParse(result.Output.Trim(), out var boolResult))
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

    public bool RemoveWatch(string name) => Watches.Remove(name);

    public void ClearWatches() => Watches.Clear();

    public bool ToggleWatchAutoUpdate()
    {
        Watches.AutoUpdate = !Watches.AutoUpdate;
        return Watches.AutoUpdate;
    }

    public int GetWatchCount() => Watches.Count;

    public bool PinWatch(string name) => Watches.Pin(name);

    public bool UnpinWatch(string name) => Watches.Unpin(name);

    public bool IsWatchPinned(string name) => Watches.IsPinned(name);

    public bool SetWatchInterval(double intervalSeconds)
    {
        if (intervalSeconds < WatchPanel.MinUpdateInterval || intervalSeconds > WatchPanel.MaxUpdateInterval)
            return false;
        Watches.UpdateInterval = intervalSeconds;
        return true;
    }

    public bool CollapseWatchGroup(string groupName) => Watches.CollapseGroup(groupName);

    public bool ExpandWatchGroup(string groupName) => Watches.ExpandGroup(groupName);

    public bool ToggleWatchGroup(string groupName) => Watches.ToggleGroup(groupName);

    public IEnumerable<string> GetWatchGroups() => Watches.GetGroups();

    public bool SetWatchAlert(string name, string alertType, object? threshold) => Watches.SetAlert(name, alertType, threshold);

    public bool RemoveWatchAlert(string name) => Watches.RemoveAlert(name);

    public IEnumerable<(string Name, string AlertType, bool Triggered)> GetWatchesWithAlerts() => Watches.GetWatchesWithAlerts();

    public bool ClearWatchAlertStatus(string name) => Watches.ClearAlertStatus(name);

    public bool SetWatchComparison(string watchName, string compareWithName, string comparisonLabel = "Expected")
        => Watches.SetComparison(watchName, compareWithName, comparisonLabel);

    public bool RemoveWatchComparison(string name) => Watches.RemoveComparison(name);

    public IEnumerable<(string Name, string ComparedWith)> GetWatchesWithComparisons() => Watches.GetWatchesWithComparisons();

    public void SetLogFilter(Microsoft.Extensions.Logging.LogLevel level) => Logs.SetFilterLevel(level);

    public void SetLogSearch(string? searchText) => Logs.SetSearch(searchText);

    public void AddLog(Microsoft.Extensions.Logging.LogLevel level, string message, string category = "General")
        => Logs.Add(level, message, category);

    public void ClearLogs() => Logs.Clear();

    public int GetLogCount() => Logs.Count;

    public void SetLogCategoryFilter(IEnumerable<string>? categories) => Logs.SetCategoryFilter(categories);

    public void ClearLogCategoryFilter() => Logs.ClearCategoryFilter();

    public IEnumerable<string> GetLogCategories() => Logs.GetCategories();

    public Dictionary<string, int> GetLogCategoryCounts() => Logs.GetCategoryCounts();

    public string ExportLogs(bool includeTimestamp = true, bool includeLevel = true, bool includeCategory = false)
        => Logs.Export(includeTimestamp, includeLevel, includeCategory);

    public string ExportLogsToCsv() => Logs.ExportToCsv();

    public void CopyLogsToClipboard() => Logs.CopyToClipboard();

    public (int Total, int Filtered, int Errors, int Warnings, int LastMinute, int Categories) GetLogStatistics()
        => Logs.GetStatistics();

    public Dictionary<Microsoft.Extensions.Logging.LogLevel, int> GetLogLevelCounts() => Logs.GetLevelCounts();

    public bool SaveWatchPreset(string name, string description)
    {
        try
        {
            var config = Watches.ExportConfiguration();
            if (config == null)
                return false;

            var (watches, updateInterval, autoUpdateEnabled) = config.Value;

            var preset = new WatchPreset
            {
                Name = name,
                Description = description,
                CreatedAt = DateTime.Now,
                UpdateInterval = updateInterval,
                AutoUpdateEnabled = autoUpdateEnabled,
                Watches = watches.Select(w => new WatchPresetEntry
                {
                    Name = w.Name,
                    Expression = w.Expression,
                    Group = w.Group,
                    Condition = w.Condition,
                    IsPinned = w.IsPinned,
                    Alert = w.AlertType != null ? new WatchAlertConfig
                    {
                        Type = w.AlertType,
                        Threshold = w.AlertThreshold?.ToString()
                    } : null,
                    Comparison = w.ComparisonWith != null ? new WatchComparisonConfig
                    {
                        CompareWith = w.ComparisonWith,
                        Label = w.ComparisonLabel ?? "Expected"
                    } : null
                }).ToList()
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
            var preset = _services.WatchPresetManager.LoadPreset(name);
            if (preset == null)
                return false;

            // Import configuration (clears watches and sets update settings)
            Watches.Clear();
            Watches.UpdateInterval = preset.UpdateInterval;
            Watches.AutoUpdate = preset.AutoUpdateEnabled;

            // Add watches from preset
            foreach (var watch in preset.Watches)
            {
                // Create value getter for the expression
                System.Func<object?> valueGetter = () =>
                {
                    try
                    {
                        var result = _services.ScriptEvaluator.EvaluateAsync(watch.Expression, _services.ScriptGlobals).Result;
                        return result.IsSuccess ? result.Output : $"<error: {result.Errors?[0].Message ?? "evaluation failed"}>";
                    }
                    catch (Exception ex)
                    {
                        return $"<error: {ex.Message}>";
                    }
                };

                // Create condition evaluator if condition exists
                System.Func<bool>? conditionEvaluator = null;
                if (!string.IsNullOrEmpty(watch.Condition))
                {
                    conditionEvaluator = () =>
                    {
                        try
                        {
                            var result = _services.ScriptEvaluator.EvaluateAsync(watch.Condition, _services.ScriptGlobals).Result;
                            if (!result.IsSuccess) return false;
                            // Output is a string representation, parse it as boolean
                            if (string.IsNullOrEmpty(result.Output)) return false;
                            if (bool.TryParse(result.Output, out var boolValue)) return boolValue;
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
                        if (double.TryParse(watch.Alert.Threshold, out var numThreshold))
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
                    SetWatchComparison(watch.Name, watch.Comparison.CompareWith, watch.Comparison.Label);
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

    public IEnumerable<(string Name, string Description, int WatchCount, DateTime CreatedAt)> ListWatchPresets()
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

    public string ExportWatchesToCsv() => Watches.ExportToCsv();

    public void CopyWatchesToClipboard(bool asCsv = false) => Watches.CopyToClipboard(asCsv);

    public (int Total, int Pinned, int WithErrors, int WithAlerts, int Groups) GetWatchStatistics() => Watches.GetStatistics();

    // ═══════════════════════════════════════════════════════════════════════════
    // Variables Tab
    // ═══════════════════════════════════════════════════════════════════════════

    public (int Variables, int Globals, int Pinned, int Expanded) GetVariableStatistics() => Variables.GetStatistics();

    public IEnumerable<string> GetVariableNames() => Variables.GetNames();

    public object? GetVariableValue(string name) => Variables.GetValue(name);

    public void SetVariableSearchFilter(string filter) => Variables.SetSearchFilter(filter);

    public void ClearVariableSearchFilter() => Variables.ClearSearchFilter();

    public bool ExpandVariable(string path)
    {
        Variables.Expand(path);
        return true;
    }

    public void CollapseVariable(string path) => Variables.Collapse(path);

    public void ExpandAllVariables() => Variables.ExpandAll();

    public void CollapseAllVariables() => Variables.CollapseAll();

    public void PinVariable(string name) => Variables.Pin(name);

    public void UnpinVariable(string name) => Variables.Unpin(name);

    public void ClearVariables() => Variables.Clear();

    // ═══════════════════════════════════════════════════════════════════════════
    // Entities Tab
    // ═══════════════════════════════════════════════════════════════════════════

    public void RefreshEntities() => Entities.Refresh();

    public void SetEntityTagFilter(string tag) => Entities.SetTagFilter(tag);

    public void SetEntitySearchFilter(string search) => Entities.SetSearchFilter(search);

    public void SetEntityComponentFilter(string componentName) => Entities.SetComponentFilter(componentName);

    public void ClearEntityFilters() => Entities.ClearFilters();

    public (string Tag, string Search, string Component) GetEntityFilters() => Entities.GetFilters();

    public void SelectEntity(int entityId) => Entities.Select(entityId);

    public void ExpandEntity(int entityId) => Entities.Expand(entityId);

    public void CollapseEntity(int entityId) => Entities.Collapse(entityId);

    public bool ToggleEntity(int entityId) => Entities.Toggle(entityId);

    public void ExpandAllEntities() => Entities.ExpandAll();

    public void CollapseAllEntities() => Entities.CollapseAll();

    public void PinEntity(int entityId) => Entities.Pin(entityId);

    public void UnpinEntity(int entityId) => Entities.Unpin(entityId);

    public (int Total, int Filtered, int Pinned, int Expanded) GetEntityStatistics() => Entities.GetStatistics();

    public Dictionary<string, int> GetEntityTagCounts() => Entities.GetTagCounts();

    public IEnumerable<string> GetEntityComponentNames() => Entities.GetComponentNames();

    public IEnumerable<string> GetEntityTags() => Entities.GetTags();

    public PokeSharp.Engine.UI.Debug.Models.EntityInfo? FindEntity(int entityId) => Entities.Find(entityId);

    public IEnumerable<PokeSharp.Engine.UI.Debug.Models.EntityInfo> FindEntitiesByName(string name) => Entities.FindByName(name);

    public (int Spawned, int Removed, int CurrentlyHighlighted) GetEntitySessionStats() => Entities.GetSessionStats();

    public void ClearEntitySessionStats() => Entities.ClearSessionStats();

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

    public IEnumerable<int> GetNewEntityIds() => Entities.GetNewEntityIds();

    public string ExportEntitiesToText(bool includeComponents = true, bool includeProperties = true)
        => Entities.ExportToText(includeComponents, includeProperties);

    public string ExportEntitiesToCsv() => Entities.ExportToCsv();

    public string? ExportSelectedEntity() => Entities.ExportSelected();

    public void CopyEntitiesToClipboard(bool asCsv = false) => Entities.CopyToClipboard(asCsv);

    public int? SelectedEntityId => Entities.SelectedId;

    // ═══════════════════════════════════════════════════════════════════════════
    // Time Control
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets the time control interface, or null if time control is not available.
    /// </summary>
    public ITimeControl? TimeControl => _timeControl;
}

