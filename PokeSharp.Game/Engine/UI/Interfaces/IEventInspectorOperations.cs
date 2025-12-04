using PokeSharp.Game.Engine.UI.Debug.Models;

namespace PokeSharp.Game.Engine.UI.Debug.Interfaces;

/// <summary>
///     Interface for event inspector panel operations exposed to console commands.
/// </summary>
public interface IEventInspectorOperations
{
    /// <summary>
    ///     Gets whether a data provider is currently set.
    /// </summary>
    bool HasProvider { get; }

    /// <summary>
    ///     Sets the event inspector data provider.
    /// </summary>
    void SetDataProvider(Func<EventInspectorData>? provider);

    /// <summary>
    ///     Forces an immediate refresh of event data.
    /// </summary>
    void Refresh();

    /// <summary>
    ///     Sets the refresh interval in frames.
    /// </summary>
    void SetRefreshInterval(int frameInterval);

    /// <summary>
    ///     Gets the current refresh interval in frames.
    /// </summary>
    int GetRefreshInterval();

    /// <summary>
    ///     Toggles the subscription details view.
    /// </summary>
    void ToggleSubscriptions();

    /// <summary>
    ///     Selects the next event in the list.
    /// </summary>
    void SelectNextEvent();

    /// <summary>
    ///     Selects the previous event in the list.
    /// </summary>
    void SelectPreviousEvent();

    /// <summary>
    ///     Scrolls up by the specified number of lines.
    /// </summary>
    void ScrollUp(int lines = 1);

    /// <summary>
    ///     Scrolls down by the specified number of lines.
    /// </summary>
    void ScrollDown(int lines = 1);

    /// <summary>
    ///     Gets consolidated statistics about tracked events.
    /// </summary>
    (
        int EventCount,
        int TotalSubscribers,
        double SlowestEventMs,
        string SlowestEventName
    ) GetStatistics();

    /// <summary>
    ///     Gets the current event inspector data including all event types.
    /// </summary>
    EventInspectorData GetData();

    /// <summary>
    ///     Exports event inspector data to a formatted string.
    /// </summary>
    string ExportToString();

    /// <summary>
    ///     Exports event inspector data to CSV format.
    /// </summary>
    string ExportToCsv();

    /// <summary>
    ///     Copies event inspector data to clipboard.
    /// </summary>
    void CopyToClipboard(bool asCsv = false);
}
