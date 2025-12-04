using PokeSharp.Game.Engine.UI.Debug.Components.Controls;
using PokeSharp.Game.Engine.UI.Debug.Models;

namespace PokeSharp.Game.Engine.UI.Debug.Components.Debug;

/// <summary>
///     Builder for constructing EventInspectorPanel instances with proper component hierarchy.
/// </summary>
public class EventInspectorPanelBuilder
{
    private Func<EventInspectorData>? _dataProvider;
    private int _refreshInterval = 30; // Update every 0.5 seconds (matches content default)

    /// <summary>
    ///     Sets the data provider function.
    /// </summary>
    public EventInspectorPanelBuilder WithDataProvider(Func<EventInspectorData> provider)
    {
        _dataProvider = provider;
        return this;
    }

    /// <summary>
    ///     Sets the refresh interval in frames.
    /// </summary>
    /// <param name="frameInterval">
    ///     Number of frames between refreshes.
    ///     Default is 30 (~2fps at 60fps). Use 1 for every frame (high CPU cost).
    /// </param>
    public EventInspectorPanelBuilder WithRefreshInterval(int frameInterval)
    {
        _refreshInterval = Math.Max(1, frameInterval);
        return this;
    }

    /// <summary>
    ///     Builds the EventInspectorPanel with all configured components.
    /// </summary>
    public EventInspectorPanel Build()
    {
        // Create components
        var content = new EventInspectorContent();
        content.SetDataProvider(_dataProvider);
        content.SetRefreshInterval(_refreshInterval);

        var statusBar = new StatusBar("event_inspector_status");

        // Assemble panel
        var panel = new EventInspectorPanel(content, statusBar);

        return panel;
    }
}
