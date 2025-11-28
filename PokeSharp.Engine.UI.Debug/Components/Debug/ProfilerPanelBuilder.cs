using PokeSharp.Engine.Systems.Management;
using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Interfaces;
using PokeSharp.Engine.UI.Debug.Layout;
using System;
using System.Collections.Generic;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
/// Builder for creating ProfilerPanel with customizable configuration.
/// </summary>
public class ProfilerPanelBuilder
{
    private Func<IReadOnlyDictionary<string, SystemMetrics>?>? _metricsProvider;
    private float _targetFrameTimeMs = 16.67f; // 60 FPS
    private float _warningThresholdMs = 2.0f;  // Warn if system takes >2ms
    private ProfilerSortMode _sortMode = ProfilerSortMode.ByExecutionTime;
    private bool _showOnlyActive = true;
    private float _refreshInterval = 0.1f;

    public static ProfilerPanelBuilder Create() => new();

    /// <summary>
    /// Sets the metrics provider function.
    /// </summary>
    public ProfilerPanelBuilder WithMetricsProvider(Func<IReadOnlyDictionary<string, SystemMetrics>?>? provider)
    {
        _metricsProvider = provider;
        return this;
    }

    /// <summary>
    /// Sets the target frame time in milliseconds (default: 16.67ms for 60fps).
    /// </summary>
    public ProfilerPanelBuilder WithTargetFrameTime(float ms)
    {
        _targetFrameTimeMs = ms;
        return this;
    }

    /// <summary>
    /// Sets the warning threshold in milliseconds (default: 2.0ms).
    /// </summary>
    public ProfilerPanelBuilder WithWarningThreshold(float ms)
    {
        _warningThresholdMs = ms;
        return this;
    }

    /// <summary>
    /// Sets the initial sort mode.
    /// </summary>
    public ProfilerPanelBuilder WithSortMode(ProfilerSortMode mode)
    {
        _sortMode = mode;
        return this;
    }

    /// <summary>
    /// Sets whether to show only active systems initially.
    /// </summary>
    public ProfilerPanelBuilder WithShowOnlyActive(bool showOnlyActive)
    {
        _showOnlyActive = showOnlyActive;
        return this;
    }

    /// <summary>
    /// Sets the refresh interval in seconds.
    /// </summary>
    public ProfilerPanelBuilder WithRefreshInterval(float intervalSeconds)
    {
        _refreshInterval = intervalSeconds;
        return this;
    }

    /// <summary>
    /// Builds the ProfilerPanel.
    /// </summary>
    public ProfilerPanel Build()
    {
        var content = CreateDefaultContent();
        var statusBar = CreateDefaultStatusBar();

        var panel = new ProfilerPanel(content, statusBar);
        panel.SetSortMode(_sortMode);
        panel.SetShowOnlyActive(_showOnlyActive);
        panel.SetRefreshInterval(_refreshInterval);

        if (_metricsProvider != null)
        {
            panel.SetMetricsProvider(_metricsProvider);
        }

        return panel;
    }

    private ProfilerContent CreateDefaultContent()
    {
        return new ProfilerContent("profiler_content", _targetFrameTimeMs, _warningThresholdMs)
        {
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.StretchTop
            }
        };
    }

    private static StatusBar CreateDefaultStatusBar()
    {
        return new StatusBar("profiler_status")
        {
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.StretchBottom
            }
        };
    }
}
