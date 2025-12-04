using System.Collections;
using System.Text;
using Microsoft.Xna.Framework;
using PokeSharp.Game.Engine.UI.Debug.Components.Base;
using PokeSharp.Game.Engine.UI.Debug.Components.Controls;
using PokeSharp.Game.Engine.UI.Debug.Core;
using PokeSharp.Game.Engine.UI.Debug.Interfaces;
using PokeSharp.Game.Engine.UI.Debug.Layout;
using PokeSharp.Game.Engine.UI.Debug.Utilities;

namespace PokeSharp.Game.Engine.UI.Debug.Components.Debug;

/// <summary>
///     Panel for watching and displaying variable values in real-time.
///     Completely redesigned for better debugging UX.
///     Uses background thread evaluation to prevent blocking the game loop.
///     Implements <see cref="IWatchOperations" /> for command access.
/// </summary>
public class WatchPanel : DebugPanelBase, IDisposable, IWatchOperations
{
    /// <summary>Maximum number of watches allowed</summary>
    public const int MaxWatches = 50;

    /// <summary>Minimum update interval in seconds</summary>
    public const double MinUpdateInterval = 0.1; // 100ms

    /// <summary>Maximum update interval in seconds</summary>
    public const double MaxUpdateInterval = 60.0; // 60 seconds

    private readonly WatchEvaluator _evaluator = new();
    private readonly Dictionary<string, bool> _groupCollapsedState = new(); // Track collapsed groups
    private readonly TextBuffer _watchBuffer;
    private readonly Dictionary<string, WatchEntry> _watches = new();
    private readonly List<string> _watchKeys = new(); // Maintain insertion order

    // Cache for sorted watch lists (invalidated when watches change)
    private List<WatchEntry>? _cachedAllWatches;
    private List<WatchEntry>? _cachedPinnedWatches;
    private List<WatchEntry>? _cachedUnpinnedWatches;
    private bool _disposed;
    private double _lastUpdateTime;
    private bool _watchListDirty = true;

    /// <summary>
    ///     Creates a WatchPanel with the specified components.
    ///     Use <see cref="WatchPanelBuilder" /> to construct instances.
    /// </summary>
    internal WatchPanel(
        TextBuffer watchBuffer,
        StatusBar statusBar,
        double updateInterval,
        bool autoUpdate
    )
        : base(statusBar)
    {
        _watchBuffer = watchBuffer;
        UpdateInterval = updateInterval;
        AutoUpdate = autoUpdate;

        Id = "watch_panel";

        // TextBuffer fills space above StatusBar
        _watchBuffer.Constraint.Anchor = Anchor.StretchTop;

        AddChild(_watchBuffer);

        // Initialize status bar with default content
        UpdateStatusBar();
    }

    /// <summary>Update interval in seconds</summary>
    public double UpdateInterval { get; set; } = 0.5; // Update every 500ms

    /// <summary>Whether to auto-update watches</summary>
    public bool AutoUpdate { get; set; } = true;

    /// <summary>
    ///     Gets the number of watches.
    /// </summary>
    public int Count => _watches.Count;

    /// <summary>
    ///     Disposes the watch panel and stops the background evaluator.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _evaluator.Dispose();
    }

    /// <summary>
    ///     Checks if a watch's alert is currently triggered.
    /// </summary>
    public bool IsAlertActive(string name)
    {
        if (name == "*")
        {
            // Wildcard - check if ANY watch has an active alert
            return _watches.Values.Any(w => w.AlertTriggered);
        }

        if (!_watches.TryGetValue(name, out WatchEntry? entry))
        {
            return false;
        }

        return entry.AlertTriggered;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // IWatchOperations Explicit Interface Implementation
    // ═══════════════════════════════════════════════════════════════════════════

    bool IWatchOperations.Add(
        string name,
        string expression,
        Func<object?> valueGetter,
        string? group,
        string? condition,
        Func<bool>? conditionEvaluator
    )
    {
        return AddWatch(name, expression, valueGetter, group, condition, conditionEvaluator);
    }

    bool IWatchOperations.Remove(string name)
    {
        return RemoveWatch(name);
    }

    void IWatchOperations.Clear()
    {
        ClearWatches();
    }

    int IWatchOperations.Count => Count;

    bool IWatchOperations.AutoUpdate
    {
        get => AutoUpdate;
        set => AutoUpdate = value;
    }

    double IWatchOperations.UpdateInterval
    {
        get => UpdateInterval;
        set => UpdateInterval = value;
    }

    bool IWatchOperations.Pin(string name)
    {
        return PinWatch(name);
    }

    bool IWatchOperations.Unpin(string name)
    {
        return UnpinWatch(name);
    }

    bool IWatchOperations.IsPinned(string name)
    {
        return IsWatchPinned(name);
    }

    bool IWatchOperations.CollapseGroup(string groupName)
    {
        return CollapseGroup(groupName);
    }

    bool IWatchOperations.ExpandGroup(string groupName)
    {
        return ExpandGroup(groupName);
    }

    bool IWatchOperations.ToggleGroup(string groupName)
    {
        return ToggleGroup(groupName);
    }

    IEnumerable<string> IWatchOperations.GetGroups()
    {
        return GetGroups();
    }

    bool IWatchOperations.SetAlert(string name, string alertType, object? threshold)
    {
        return SetAlert(name, alertType, threshold);
    }

    bool IWatchOperations.RemoveAlert(string name)
    {
        return RemoveAlert(name);
    }

    IEnumerable<(
        string Name,
        string AlertType,
        bool Triggered
    )> IWatchOperations.GetWatchesWithAlerts()
    {
        return GetWatchesWithAlerts();
    }

    bool IWatchOperations.ClearAlertStatus(string name)
    {
        return ClearAlertStatus(name);
    }

    bool IWatchOperations.SetComparison(
        string watchName,
        string compareWithName,
        string comparisonLabel
    )
    {
        return SetComparison(watchName, compareWithName, comparisonLabel);
    }

    bool IWatchOperations.RemoveComparison(string name)
    {
        return RemoveComparison(name);
    }

    IEnumerable<(string Name, string ComparedWith)> IWatchOperations.GetWatchesWithComparisons()
    {
        return GetWatchesWithComparisons();
    }

    string IWatchOperations.ExportToCsv()
    {
        return ExportToCsv();
    }

    void IWatchOperations.CopyToClipboard(bool asCsv)
    {
        CopyToClipboard(asCsv);
    }

    (
        int Total,
        int Pinned,
        int WithErrors,
        int WithAlerts,
        int Groups
    ) IWatchOperations.GetStatistics()
    {
        return GetStatistics();
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
        )> Watches,
        double UpdateInterval,
        bool AutoUpdateEnabled
    )? IWatchOperations.ExportConfiguration()
    {
        return ExportConfiguration();
    }

    protected override UIComponent GetContentComponent()
    {
        return _watchBuffer;
    }

    /// <summary>
    ///     Adds a watch expression.
    /// </summary>
    /// <param name="name">Display name for the watch</param>
    /// <param name="expression">The expression being watched (for display)</param>
    /// <param name="valueGetter">Function that returns the current value</param>
    /// <param name="group">Optional group name for organization</param>
    /// <param name="condition">Optional condition expression</param>
    /// <param name="conditionEvaluator">Optional function to evaluate condition</param>
    /// <param name="alertType">Optional alert type (above/below/equals/changes)</param>
    /// <param name="alertThreshold">Optional threshold value for alert</param>
    /// <param name="alertCallback">Optional callback when alert triggers</param>
    /// <returns>True if added successfully, false if limit reached</returns>
    public bool AddWatch(
        string name,
        string expression,
        Func<object?> valueGetter,
        string? group = null,
        string? condition = null,
        Func<bool>? conditionEvaluator = null,
        string? alertType = null,
        object? alertThreshold = null,
        Action<string, object?, object?>? alertCallback = null
    )
    {
        if (_watches.ContainsKey(name))
        {
            // Update existing watch
            WatchEntry entry = _watches[name];
            entry.Expression = expression;
            entry.ValueGetter = valueGetter;
            entry.Group = group;
            entry.Condition = condition;
            entry.ConditionEvaluator = conditionEvaluator;
            entry.AlertType = alertType;
            entry.AlertThreshold = alertThreshold;
            entry.AlertCallback = alertCallback;
            entry.HasError = false;
            entry.ErrorMessage = null;
            UpdateWatchDisplay();
            return true;
        }

        // Check if we've reached the limit
        if (_watches.Count >= MaxWatches)
        {
            return false; // Limit reached
        }

        // Add new watch
        _watches.Add(
            name,
            new WatchEntry
            {
                Name = name,
                Expression = expression,
                ValueGetter = valueGetter,
                LastUpdated = DateTime.Now,
                IsPinned = false,
                Group = group,
                Condition = condition,
                ConditionEvaluator = conditionEvaluator,
                ConditionMet = true,
                AlertType = alertType,
                AlertThreshold = alertThreshold,
                AlertCallback = alertCallback,
                AlertTriggered = false,
            }
        );
        _watchKeys.Add(name);

        // Initialize group as expanded by default
        if (!string.IsNullOrEmpty(group) && !_groupCollapsedState.ContainsKey(group))
        {
            _groupCollapsedState[group] = false; // Expanded
        }

        _watchListDirty = true;
        UpdateWatchDisplay();
        return true;
    }

    /// <summary>
    ///     Pins a watch to the top of the list.
    /// </summary>
    public bool PinWatch(string name)
    {
        if (!_watches.ContainsKey(name))
        {
            return false;
        }

        _watches[name].IsPinned = true;
        _watchListDirty = true;
        UpdateWatchDisplay();
        return true;
    }

    /// <summary>
    ///     Unpins a watch.
    /// </summary>
    public bool UnpinWatch(string name)
    {
        if (!_watches.ContainsKey(name))
        {
            return false;
        }

        _watches[name].IsPinned = false;
        _watchListDirty = true;
        UpdateWatchDisplay();
        return true;
    }

    /// <summary>
    ///     Checks if a watch is pinned.
    /// </summary>
    public bool IsWatchPinned(string name)
    {
        return _watches.ContainsKey(name) && _watches[name].IsPinned;
    }

    /// <summary>
    ///     Collapses a group.
    /// </summary>
    public bool CollapseGroup(string groupName)
    {
        if (_groupCollapsedState.ContainsKey(groupName))
        {
            _groupCollapsedState[groupName] = true;
            UpdateWatchDisplay();
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Expands a group.
    /// </summary>
    public bool ExpandGroup(string groupName)
    {
        if (_groupCollapsedState.ContainsKey(groupName))
        {
            _groupCollapsedState[groupName] = false;
            UpdateWatchDisplay();
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Toggles a group's collapsed state.
    /// </summary>
    public bool ToggleGroup(string groupName)
    {
        if (_groupCollapsedState.ContainsKey(groupName))
        {
            _groupCollapsedState[groupName] = !_groupCollapsedState[groupName];
            UpdateWatchDisplay();
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Checks if a group is collapsed.
    /// </summary>
    public bool IsGroupCollapsed(string groupName)
    {
        return _groupCollapsedState.TryGetValue(groupName, out bool collapsed) && collapsed;
    }

    /// <summary>
    ///     Gets all group names.
    /// </summary>
    public IEnumerable<string> GetGroups()
    {
        return _watches
            .Values.Where(w => !string.IsNullOrEmpty(w.Group))
            .Select(w => w.Group!)
            .Distinct()
            .OrderBy(g => g);
    }

    /// <summary>
    ///     Sets an alert on a watch.
    /// </summary>
    public bool SetAlert(
        string name,
        string alertType,
        object? threshold,
        Action<string, object?, object?>? callback = null
    )
    {
        if (!_watches.ContainsKey(name))
        {
            return false;
        }

        WatchEntry entry = _watches[name];
        entry.AlertType = alertType;
        entry.AlertThreshold = threshold;
        entry.AlertCallback = callback;
        entry.AlertTriggered = false;
        entry.LastAlertTime = null;

        UpdateWatchDisplay();
        return true;
    }

    /// <summary>
    ///     Removes an alert from a watch.
    /// </summary>
    public bool RemoveAlert(string name)
    {
        if (!_watches.ContainsKey(name))
        {
            return false;
        }

        WatchEntry entry = _watches[name];
        entry.AlertType = null;
        entry.AlertThreshold = null;
        entry.AlertCallback = null;
        entry.AlertTriggered = false;
        entry.LastAlertTime = null;

        UpdateWatchDisplay();
        return true;
    }

    /// <summary>
    ///     Gets all watches with active alerts.
    /// </summary>
    public IEnumerable<(string Name, string AlertType, bool Triggered)> GetWatchesWithAlerts()
    {
        return _watches
            .Values.Where(w => w.AlertType != null)
            .Select(w => (w.Name, w.AlertType!, w.AlertTriggered))
            .OrderBy(w => w.Name);
    }

    /// <summary>
    ///     Clears alert triggered status for a watch.
    /// </summary>
    public bool ClearAlertStatus(string name)
    {
        if (!_watches.ContainsKey(name))
        {
            return false;
        }

        WatchEntry entry = _watches[name];
        entry.AlertTriggered = false;

        UpdateWatchDisplay();
        return true;
    }

    /// <summary>
    ///     Sets up a comparison between two watches.
    /// </summary>
    public bool SetComparison(
        string watchName,
        string compareWithName,
        string comparisonLabel = "Expected"
    )
    {
        if (!_watches.ContainsKey(watchName) || !_watches.ContainsKey(compareWithName))
        {
            return false;
        }

        WatchEntry entry = _watches[watchName];
        entry.ComparisonWith = compareWithName;
        entry.ComparisonLabel = comparisonLabel;

        UpdateWatchDisplay();
        return true;
    }

    /// <summary>
    ///     Removes comparison from a watch.
    /// </summary>
    public bool RemoveComparison(string name)
    {
        if (!_watches.ContainsKey(name))
        {
            return false;
        }

        WatchEntry entry = _watches[name];
        entry.ComparisonWith = null;
        entry.ComparisonLabel = null;
        entry.ComparisonValue = null;
        entry.ComparisonDiff = null;

        UpdateWatchDisplay();
        return true;
    }

    /// <summary>
    ///     Gets all watches with active comparisons.
    /// </summary>
    public IEnumerable<(string Name, string ComparedWith)> GetWatchesWithComparisons()
    {
        return _watches
            .Values.Where(w => w.ComparisonWith != null)
            .Select(w => (w.Name, w.ComparisonWith!))
            .OrderBy(w => w.Name);
    }

    /// <summary>
    ///     Removes a watch expression.
    /// </summary>
    public bool RemoveWatch(string name)
    {
        if (_watches.Remove(name))
        {
            _watchKeys.Remove(name);
            _watchListDirty = true;
            UpdateWatchDisplay();
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Clears all watches.
    /// </summary>
    public void ClearWatches()
    {
        _watches.Clear();
        _watchKeys.Clear();
        _watchBuffer.Clear();
        _watchListDirty = true;
        _evaluator.ClearPending(); // Clear any pending evaluations
        UpdateWatchDisplay();
    }

    /// <summary>
    ///     Exports current watch configuration for preset saving.
    /// </summary>
    public (
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
    ) ExportConfiguration()
    {
        var watches =
            new List<(
                string,
                string,
                string?,
                string?,
                bool,
                string?,
                object?,
                string?,
                string?
            )>();

        foreach (string key in _watchKeys)
        {
            WatchEntry entry = _watches[key];
            watches.Add(
                (
                    entry.Name,
                    entry.Expression,
                    entry.Group,
                    entry.Condition,
                    entry.IsPinned,
                    entry.AlertType,
                    entry.AlertThreshold,
                    entry.ComparisonWith,
                    entry.ComparisonLabel
                )
            );
        }

        return (watches, UpdateInterval, AutoUpdate);
    }

    /// <summary>
    ///     Updates all watch values using background thread evaluation.
    ///     Collects results from previous evaluations and queues new ones.
    /// </summary>
    private void UpdateWatchValues()
    {
        // First, collect any results from previous evaluations
        CollectEvaluationResults();

        // Then queue new evaluations for all watches
        foreach (string key in _watchKeys)
        {
            WatchEntry entry = _watches[key];
            _evaluator.QueueEvaluation(key, entry.ValueGetter, entry.ConditionEvaluator);
        }
    }

    /// <summary>
    ///     Collects results from background evaluations and updates watch entries.
    /// </summary>
    private void CollectEvaluationResults()
    {
        foreach (WatchEvaluator.EvaluationResult result in _evaluator.CollectResults())
        {
            if (!_watches.TryGetValue(result.WatchName, out WatchEntry? entry))
            {
                continue; // Watch was removed
            }

            // Update condition state
            entry.ConditionMet = result.ConditionMet;

            // Only update value if condition is met
            if (result.ConditionMet)
            {
                if (result.HasError)
                {
                    entry.HasError = true;
                    entry.ErrorMessage = result.ErrorMessage;
                }
                else
                {
                    // Store previous value for change detection
                    entry.PreviousValue = entry.LastValue;

                    // Update with new value
                    entry.LastValue = result.Value;
                    entry.LastUpdated = result.EvaluatedAt;
                    entry.UpdateCount++;
                    entry.HasError = false;
                    entry.ErrorMessage = null;

                    // Track history if value changed
                    if (
                        entry.PreviousValue != null
                        && !Equals(entry.LastValue, entry.PreviousValue)
                    )
                    {
                        entry.History.Add((result.EvaluatedAt, entry.LastValue));

                        // Trim history if needed
                        if (entry.History.Count > entry.MaxHistorySize)
                        {
                            entry.History.RemoveAt(0);
                        }
                    }

                    // Check alerts
                    CheckAlert(entry);

                    // Update comparison if configured
                    UpdateComparison(entry);
                }
            }
        }
    }

    /// <summary>
    ///     Updates comparison values for a watch entry.
    /// </summary>
    private void UpdateComparison(WatchEntry entry)
    {
        if (string.IsNullOrEmpty(entry.ComparisonWith))
        {
            return;
        }

        if (!_watches.ContainsKey(entry.ComparisonWith))
        {
            entry.ComparisonValue = null;
            entry.ComparisonDiff = null;
            return;
        }

        WatchEntry compareWatch = _watches[entry.ComparisonWith];
        entry.ComparisonValue = compareWatch.LastValue;

        // Calculate difference if both values are numeric
        try
        {
            if (entry.LastValue is IConvertible && compareWatch.LastValue is IConvertible)
            {
                double num1 = Convert.ToDouble(entry.LastValue);
                double num2 = Convert.ToDouble(compareWatch.LastValue);
                double diff = num1 - num2;
                double percentDiff = num2 != 0 ? diff / num2 * 100.0 : 0;

                entry.ComparisonDiff = (diff, percentDiff);
            }
            else
            {
                // Non-numeric comparison
                entry.ComparisonDiff = Equals(entry.LastValue, compareWatch.LastValue)
                    ? "EQUAL"
                    : "DIFFERENT";
            }
        }
        catch
        {
            entry.ComparisonDiff = null;
        }
    }

    /// <summary>
    ///     Checks if a watch entry triggers its alert condition.
    /// </summary>
    private void CheckAlert(WatchEntry entry)
    {
        if (string.IsNullOrEmpty(entry.AlertType) || entry.LastValue == null)
        {
            return;
        }

        bool shouldTrigger = false;

        try
        {
            switch (entry.AlertType.ToLower())
            {
                case "changes":
                    // Alert on any change
                    shouldTrigger =
                        entry.PreviousValue != null
                        && !Equals(entry.LastValue, entry.PreviousValue);
                    break;

                case "above":
                case "below":
                case "equals":
                    // Need threshold for these
                    if (entry.AlertThreshold == null)
                    {
                        return;
                    }

                    // Try to convert to comparable values
                    if (
                        TryCompareValues(
                            entry.LastValue,
                            entry.AlertThreshold,
                            entry.AlertType.ToLower(),
                            out shouldTrigger
                        )
                    )
                    {
                        // Successfully compared
                    }

                    break;
            }

            if (shouldTrigger)
            {
                entry.AlertTriggered = true;
                entry.LastAlertTime = DateTime.Now;

                // Invoke callback if provided
                entry.AlertCallback?.Invoke(entry.Name, entry.LastValue, entry.AlertThreshold);
            }
            else
            {
                entry.AlertTriggered = false;
            }
        }
        catch
        {
            // Alert check failed, ignore
        }
    }

    /// <summary>
    ///     Tries to compare two values for alert checking.
    /// </summary>
    private bool TryCompareValues(
        object? value,
        object? threshold,
        string comparison,
        out bool result
    )
    {
        result = false;

        try
        {
            // Try numeric comparison
            if (value is IConvertible && threshold is IConvertible)
            {
                double numValue = Convert.ToDouble(value);
                double numThreshold = Convert.ToDouble(threshold);

                result = comparison switch
                {
                    "above" => numValue > numThreshold,
                    "below" => numValue < numThreshold,
                    "equals" => Math.Abs(numValue - numThreshold) < 0.0001,
                    _ => false,
                };
                return true;
            }

            // Try string comparison
            if (value != null && threshold != null)
            {
                string? strValue = value.ToString();
                string? strThreshold = threshold.ToString();

                result = comparison switch
                {
                    "equals" => strValue == strThreshold,
                    _ => false,
                };
                return true;
            }
        }
        catch
        {
            // Comparison failed
        }

        return false;
    }

    /// <summary>
    ///     Displays watches organized by groups.
    /// </summary>
    private void DisplayWatchesByGroup()
    {
        int displayIndex = 1;

        // Get all watches sorted by pinned status first (cached to avoid LINQ allocations every frame)
        if (_watchListDirty || _cachedAllWatches == null)
        {
            _cachedAllWatches = _watchKeys.Select(k => _watches[k]).ToList();
            _cachedPinnedWatches = _cachedAllWatches.Where(w => w.IsPinned).ToList();
            _cachedUnpinnedWatches = _cachedAllWatches.Where(w => !w.IsPinned).ToList();
            _watchListDirty = false;
        }

        List<WatchEntry>? pinnedWatches = _cachedPinnedWatches!;
        List<WatchEntry>? unpinnedWatches = _cachedUnpinnedWatches!;

        // Display pinned watches first (regardless of group)
        if (pinnedWatches.Any())
        {
            _watchBuffer.AppendLine(
                $"  {NerdFontIcons.Pinned} PINNED",
                ThemeManager.Current.Warning
            );
            _watchBuffer.AppendLine("", ThemeManager.Current.TextDim);

            foreach (WatchEntry entry in pinnedWatches.OrderBy(w => _watchKeys.IndexOf(w.Name)))
            {
                DisplayWatch(entry, ref displayIndex);
            }
        }

        // Group unpinned watches
        IOrderedEnumerable<IGrouping<string, WatchEntry>> groupedWatches = unpinnedWatches
            .GroupBy(w => w.Group ?? "")
            .OrderBy(g => string.IsNullOrEmpty(g.Key) ? "zzz" : g.Key); // Ungrouped last

        foreach (IGrouping<string, WatchEntry> group in groupedWatches)
        {
            if (!string.IsNullOrEmpty(group.Key))
            {
                // Display group header
                bool isCollapsed = IsGroupCollapsed(group.Key);
                string collapseIndicator = isCollapsed
                    ? NerdFontIcons.Collapsed
                    : NerdFontIcons.Expanded;
                int groupCount = group.Count();

                _watchBuffer.AppendLine(
                    $"  {collapseIndicator} {group.Key.ToUpper()} ({groupCount} watch{(groupCount == 1 ? "" : "es")})",
                    ThemeManager.Current.Info
                );

                if (!isCollapsed)
                {
                    _watchBuffer.AppendLine("", ThemeManager.Current.TextDim);
                    foreach (WatchEntry entry in group.OrderBy(w => _watchKeys.IndexOf(w.Name)))
                    {
                        DisplayWatch(entry, ref displayIndex, "    ");
                    }
                }
                else
                {
                    _watchBuffer.AppendLine("", ThemeManager.Current.TextDim);
                }
            }
            else
            {
                // Display ungrouped watches
                foreach (WatchEntry entry in group.OrderBy(w => _watchKeys.IndexOf(w.Name)))
                {
                    DisplayWatch(entry, ref displayIndex);
                }
            }
        }
    }

    /// <summary>
    ///     Displays a single watch entry.
    /// </summary>
    private void DisplayWatch(WatchEntry entry, ref int displayIndex, string indent = "  ")
    {
        // Watch header with index and indicators
        string conditionalIndicator = entry.Condition != null ? " [COND]" : "";
        string alertIndicator = entry.AlertType != null ? " [ALERT]" : "";
        string alertTriggeredIndicator = entry.AlertTriggered ? " [!]" : "";

        Color nameColor =
            entry.IsPinned ? ThemeManager.Current.Warning
            : entry.AlertTriggered ? ThemeManager.Current.Error
            : ThemeManager.Current.BorderFocus;

        _watchBuffer.AppendLine(
            $"{indent}[{displayIndex}] {entry.Name}{conditionalIndicator}{alertIndicator}{alertTriggeredIndicator}",
            nameColor
        );

        // Expression
        _watchBuffer.AppendLine(
            $"{indent}    Expression: {entry.Expression}",
            ThemeManager.Current.TextSecondary
        );

        // Alert status (if alert configured)
        if (entry.AlertType != null)
        {
            string? alertDesc = entry.AlertType.ToLower() switch
            {
                "above" => $"> {FormatValue(entry.AlertThreshold)}",
                "below" => $"< {FormatValue(entry.AlertThreshold)}",
                "equals" => $"== {FormatValue(entry.AlertThreshold)}",
                "changes" => "on any change",
                _ => entry.AlertType,
            };

            string alertStatus = entry.AlertTriggered ? "TRIGGERED" : "watching";
            Color alertColor = entry.AlertTriggered
                ? ThemeManager.Current.Error
                : ThemeManager.Current.TextSecondary;

            string lastAlertInfo = entry.LastAlertTime.HasValue
                ? $" (last: {(DateTime.Now - entry.LastAlertTime.Value).TotalSeconds:F1}s ago)"
                : "";

            _watchBuffer.AppendLine(
                $"{indent}    Alert:      {alertDesc} [{alertStatus}]{lastAlertInfo}",
                alertColor
            );
        }

        // Condition status (if conditional)
        if (entry.Condition != null)
        {
            string condStatus = entry.ConditionMet ? "TRUE" : "FALSE (skipped)";
            Color condColor = entry.ConditionMet
                ? ThemeManager.Current.Success
                : ThemeManager.Current.TextDim;
            _watchBuffer.AppendLine(
                $"{indent}    Condition:  {entry.Condition} = {condStatus}",
                condColor
            );
        }

        // Value or error (only if condition met or no condition)
        if (!entry.ConditionMet && entry.Condition != null)
        {
            _watchBuffer.AppendLine(
                $"{indent}    Value:      <waiting for condition>",
                ThemeManager.Current.TextDim
            );
        }
        else if (entry.HasError)
        {
            _watchBuffer.AppendLine($"{indent}    Value:      <ERROR>", ThemeManager.Current.Error);
            _watchBuffer.AppendLine(
                $"{indent}    Error:      {entry.ErrorMessage}",
                ThemeManager.Current.Error
            );
        }
        else
        {
            string valueStr = FormatValue(entry.LastValue);
            Color valueColor = ThemeManager.Current.TextPrimary;

            // Check if value changed
            string changeIndicator = "";
            if (entry.PreviousValue != null && !Equals(entry.LastValue, entry.PreviousValue))
            {
                string prevStr = FormatValue(entry.PreviousValue);
                changeIndicator = $"  (was: {prevStr})";
                valueColor = ThemeManager.Current.Warning; // Highlight changed values
            }

            _watchBuffer.AppendLine(
                $"{indent}    Value:      {valueStr}{changeIndicator}",
                valueColor
            );

            // Show history if available
            if (entry.History.Count > 0)
            {
                string historyStr =
                    $"{entry.History.Count} change{(entry.History.Count == 1 ? "" : "s")} tracked";
                _watchBuffer.AppendLine(
                    $"{indent}    History:    {historyStr}",
                    ThemeManager.Current.TextDim
                );
            }

            // Show comparison if configured
            if (!string.IsNullOrEmpty(entry.ComparisonWith))
            {
                string compareLabel = entry.ComparisonLabel ?? "Compare";
                string compareValue = FormatValue(entry.ComparisonValue);

                _watchBuffer.AppendLine(
                    $"{indent}    {compareLabel}: {compareValue}",
                    ThemeManager.Current.TextSecondary
                );

                // Show difference if calculated
                if (entry.ComparisonDiff != null)
                {
                    string diffStr = "";
                    Color diffColor = ThemeManager.Current.TextDim;

                    if (entry.ComparisonDiff is (double diff, double percent))
                    {
                        // Numeric difference
                        string sign = diff > 0 ? "+" : "";
                        string percentSign = percent > 0 ? "+" : "";
                        diffStr = $"{sign}{diff:F2} ({percentSign}{percent:F1}%)";

                        // Color code the difference
                        diffColor =
                            Math.Abs(diff) < 0.001 ? ThemeManager.Current.Success
                            : // Equal
                            diff > 0 ? ThemeManager.Current.Warning
                            : // Higher
                            ThemeManager.Current.Info; // Lower
                    }
                    else if (entry.ComparisonDiff is string str)
                    {
                        // Non-numeric comparison
                        diffStr = str;
                        diffColor =
                            str == "EQUAL"
                                ? ThemeManager.Current.Success
                                : ThemeManager.Current.Warning;
                    }

                    _watchBuffer.AppendLine($"{indent}    Difference: {diffStr}", diffColor);
                }
            }
        }

        // Metadata
        double timeSinceUpdate = (DateTime.Now - entry.LastUpdated).TotalSeconds;
        string updateInfo = timeSinceUpdate < 1 ? "just now" : $"{timeSinceUpdate:F1}s ago";
        _watchBuffer.AppendLine(
            $"{indent}    Updated:    {updateInfo} ({entry.UpdateCount} times)",
            ThemeManager.Current.TextDim
        );

        // Separator between watches
        _watchBuffer.AppendLine("", ThemeManager.Current.TextDim);

        displayIndex++;
    }

    /// <summary>
    ///     Forces an immediate update of all watch values.
    /// </summary>
    public void UpdateWatchDisplay()
    {
        // Preserve scroll position and auto-scroll state during update
        int previousScrollOffset = _watchBuffer.ScrollOffset;
        bool previousAutoScroll = _watchBuffer.AutoScroll;

        _watchBuffer.Clear();

        if (_watchKeys.Count == 0)
        {
            _watchBuffer.AppendLine("  No watches defined.", ThemeManager.Current.TextDim);
            return;
        }

        // Update all watch values (evaluate conditions and values)
        UpdateWatchValues();

        // Display watches organized by groups
        DisplayWatchesByGroup();

        // Update status bar
        UpdateStatusBar();

        // Restore scroll position and auto-scroll state after update
        _watchBuffer.SetScrollOffset(previousScrollOffset);
        _watchBuffer.AutoScroll = previousAutoScroll;
    }

    /// <summary>
    ///     Updates the status bar with current watch stats.
    /// </summary>
    protected override void UpdateStatusBar()
    {
        int errorCount = _watches.Values.Count(w => w.HasError);
        int pinnedCount = _watches.Values.Count(w => w.IsPinned);
        int alertCount = _watches.Values.Count(w => w.AlertTriggered);

        // Build stats text
        string stats = $"Watches: {_watchKeys.Count}";
        if (pinnedCount > 0)
        {
            stats += $" | Pinned: {pinnedCount}";
        }

        if (errorCount > 0)
        {
            stats += $" | Errors: {errorCount}";
        }

        if (alertCount > 0)
        {
            stats += $" | [!] Alerts: {alertCount}";
        }

        // Build hints text
        string hints = AutoUpdate ? $"Auto: {UpdateInterval:F1}s" : "Manual";

        SetStatusBar(stats, hints);
        SetStatusBarHealthColor(errorCount == 0, errorCount > 0);
    }

    /// <summary>
    ///     Formats a value for display.
    /// </summary>
    private string FormatValue(object? value)
    {
        if (value == null)
        {
            return "<null>";
        }

        Type type = value.GetType();

        // Handle primitives
        if (type.IsPrimitive || type == typeof(string))
        {
            if (value is bool b)
            {
                return b ? "true" : "false";
            }

            if (value is float f)
            {
                return f.ToString("F2");
            }

            if (value is double d)
            {
                return d.ToString("F2");
            }

            if (value is string s)
            {
                return $"\"{s}\"";
            }

            return value.ToString() ?? "<null>";
        }

        // Handle Vector2
        if (value is Vector2 v2)
        {
            return $"({v2.X:F2}, {v2.Y:F2})";
        }

        // Handle Vector3
        if (value is Vector3 v3)
        {
            return $"({v3.X:F2}, {v3.Y:F2}, {v3.Z:F2})";
        }

        // Handle Point
        if (value is Point p)
        {
            return $"({p.X}, {p.Y})";
        }

        // Handle Color
        if (value is Color color)
        {
            return $"RGBA({color.R}, {color.G}, {color.B}, {color.A})";
        }

        // Handle collections
        if (value is ICollection collection)
        {
            return $"[{collection.Count} items]";
        }

        // Handle DateTime
        if (value is DateTime dt)
        {
            return dt.ToString("HH:mm:ss.fff");
        }

        // Handle TimeSpan
        if (value is TimeSpan ts)
        {
            return $"{ts.TotalSeconds:F2}s";
        }

        // Default: type name + ToString
        string? str = value.ToString();
        if (str == type.FullName || str == type.Name)
        {
            // ToString() just returns type name, not helpful
            return $"<{type.Name}>";
        }

        return str ?? $"<{type.Name}>";
    }

    protected override void OnRenderContainer(UIContext context)
    {
        base.OnRenderContainer(context);

        // Auto-update if enabled
        if (AutoUpdate && context.Input?.GameTime != null)
        {
            double currentTime = context.Input.GameTime.TotalGameTime.TotalSeconds;
            if (currentTime - _lastUpdateTime >= UpdateInterval)
            {
                _lastUpdateTime = currentTime;
                UpdateWatchDisplay();
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Export Methods
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Exports all watches to CSV format.
    /// </summary>
    public string ExportToCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Name,Expression,Value,Group,IsPinned,HasError,LastUpdated");

        foreach (string key in _watchKeys)
        {
            if (!_watches.TryGetValue(key, out WatchEntry? watch))
            {
                continue;
            }

            string valueStr = watch.HasError
                ? $"ERROR: {watch.ErrorMessage}"
                : FormatValue(watch.LastValue)?.Replace("\"", "\"\"") ?? "";

            string group = watch.Group?.Replace("\"", "\"\"") ?? "";
            string lastUpdated = watch.LastUpdated.ToString("yyyy-MM-dd HH:mm:ss.fff");

            sb.AppendLine(
                $"\"{watch.Name}\",\"{watch.Expression}\",\"{valueStr}\",\"{group}\",{watch.IsPinned},{watch.HasError},\"{lastUpdated}\""
            );
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Exports all watches to a formatted string.
    /// </summary>
    public string ExportToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Watch Export - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"# Total: {_watches.Count} watches");
        sb.AppendLine();

        // Group by group name
        IOrderedEnumerable<IGrouping<string, WatchEntry>> groups = _watches
            .Values.GroupBy(w => w.Group ?? "(ungrouped)")
            .OrderBy(g => g.Key);

        foreach (IGrouping<string, WatchEntry> group in groups)
        {
            if (!string.IsNullOrEmpty(group.Key) && group.Key != "(ungrouped)")
            {
                sb.AppendLine($"[{group.Key}]");
            }

            foreach (WatchEntry watch in group.OrderBy(w => w.IsPinned ? 0 : 1).ThenBy(w => w.Name))
            {
                string pin = watch.IsPinned ? "* " : "  ";
                string value = watch.HasError
                    ? $"ERROR: {watch.ErrorMessage}"
                    : FormatValue(watch.LastValue) ?? "null";

                sb.AppendLine($"{pin}{watch.Name, -20} = {value}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Copies watches to clipboard.
    /// </summary>
    public void CopyToClipboard(bool asCsv = false)
    {
        string text = asCsv ? ExportToCsv() : ExportToString();
        ClipboardManager.SetText(text);
    }

    /// <summary>
    ///     Gets watch statistics.
    /// </summary>
    public (int Total, int Pinned, int WithErrors, int WithAlerts, int Groups) GetStatistics()
    {
        return (
            _watches.Count,
            _watches.Values.Count(w => w.IsPinned),
            _watches.Values.Count(w => w.HasError),
            _watches.Values.Count(w => w.AlertTriggered),
            _watches.Values.Select(w => w.Group).Where(g => g != null).Distinct().Count()
        );
    }

    public class WatchEntry
    {
        public string Name { get; set; } = string.Empty;
        public string Expression { get; set; } = string.Empty;
        public Func<object?> ValueGetter { get; set; } = () => null;
        public object? LastValue { get; set; }
        public object? PreviousValue { get; set; }
        public DateTime LastUpdated { get; set; }
        public bool HasError { get; set; }
        public string? ErrorMessage { get; set; }
        public int UpdateCount { get; set; }
        public bool IsPinned { get; set; }

        // Group support
        public string? Group { get; set; }

        // Conditional watch support
        public string? Condition { get; set; }
        public Func<bool>? ConditionEvaluator { get; set; }
        public bool ConditionMet { get; set; } = true;

        // History tracking
        public List<(DateTime Timestamp, object? Value)> History { get; set; } = new();
        public int MaxHistorySize { get; set; } = 10; // Keep last 10 changes

        // Alert/Threshold support
        public string? AlertType { get; set; } // "above", "below", "equals", "changes"
        public object? AlertThreshold { get; set; }
        public bool AlertTriggered { get; set; }
        public DateTime? LastAlertTime { get; set; }
        public Action<string, object?, object?>? AlertCallback { get; set; }

        // Comparison support
        public string? ComparisonWith { get; set; } // Name of watch to compare with
        public string? ComparisonLabel { get; set; } // Label for comparison (e.g., "Expected")
        public object? ComparisonValue { get; set; }
        public object? ComparisonDiff { get; set; }
    }
}
