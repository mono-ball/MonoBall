using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
///     Builder for creating WatchPanel with customizable components.
/// </summary>
public class WatchPanelBuilder
{
    private bool _autoUpdate = true;
    private int _maxLines = 1000;
    private double _updateInterval = 0.5;
    private TextBuffer? _watchBuffer;

    public static WatchPanelBuilder Create()
    {
        return new WatchPanelBuilder();
    }

    public WatchPanelBuilder WithWatchBuffer(TextBuffer buffer)
    {
        _watchBuffer = buffer;
        return this;
    }

    public WatchPanelBuilder WithMaxLines(int maxLines)
    {
        _maxLines = maxLines;
        return this;
    }

    public WatchPanelBuilder WithUpdateInterval(double seconds)
    {
        _updateInterval = Math.Clamp(
            seconds,
            WatchPanel.MinUpdateInterval,
            WatchPanel.MaxUpdateInterval
        );
        return this;
    }

    public WatchPanelBuilder WithAutoUpdate(bool enabled)
    {
        _autoUpdate = enabled;
        return this;
    }

    public WatchPanel Build()
    {
        return new WatchPanel(
            _watchBuffer ?? CreateDefaultWatchBuffer(),
            CreateDefaultStatusBar(),
            _updateInterval,
            _autoUpdate
        );
    }

    private TextBuffer CreateDefaultWatchBuffer()
    {
        return new TextBuffer("watch_buffer")
        {
            // BackgroundColor uses theme fallback - don't set explicitly
            AutoScroll = false,
            MaxLines = _maxLines,
            Constraint = new LayoutConstraint { Anchor = Anchor.StretchTop },
        };
    }

    private static StatusBar CreateDefaultStatusBar()
    {
        return new StatusBar("watch_status")
        {
            Constraint = new LayoutConstraint { Anchor = Anchor.StretchBottom },
        };
    }
}
