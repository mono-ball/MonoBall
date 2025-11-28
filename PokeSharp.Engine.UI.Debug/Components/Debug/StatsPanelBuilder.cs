using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Layout;
using System;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
/// Builder for creating StatsPanel with customizable configuration.
/// </summary>
public class StatsPanelBuilder
{
    private Func<StatsData>? _statsProvider;
    private int _refreshInterval = 2; // frames

    public static StatsPanelBuilder Create() => new();

    /// <summary>
    /// Sets the stats data provider function that will be called periodically to refresh stats.
    /// </summary>
    public StatsPanelBuilder WithStatsProvider(Func<StatsData>? provider)
    {
        _statsProvider = provider;
        return this;
    }

    /// <summary>
    /// Sets the refresh interval in frames (1-60).
    /// Lower values = more frequent updates.
    /// Default: 2 (~30fps updates at 60fps game).
    /// </summary>
    public StatsPanelBuilder WithRefreshInterval(int frameInterval)
    {
        _refreshInterval = Math.Clamp(frameInterval, 1, 60);
        return this;
    }

    public StatsPanel Build()
    {
        var content = CreateDefaultContent();
        var statusBar = CreateDefaultStatusBar();

        var panel = new StatsPanel(content, statusBar);
        panel.SetRefreshInterval(_refreshInterval);

        if (_statsProvider != null)
        {
            panel.SetStatsProvider(_statsProvider);
        }

        return panel;
    }

    private static StatsContent CreateDefaultContent()
    {
        return new StatsContent("stats_content")
        {
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.StretchTop
            }
        };
    }

    private static StatusBar CreateDefaultStatusBar()
    {
        return new StatusBar("stats_status")
        {
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.StretchBottom
            }
        };
    }
}
