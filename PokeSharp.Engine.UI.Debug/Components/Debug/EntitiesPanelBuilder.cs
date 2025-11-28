using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Layout;
using PokeSharp.Engine.UI.Debug.Models;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
///     Builder for creating EntitiesPanel with customizable components.
/// </summary>
public class EntitiesPanelBuilder
{
    private bool _autoRefresh = true;
    private TextBuffer? _entityListBuffer;
    private Func<IEnumerable<EntityInfo>>? _entityProvider;
    private int _maxLines = 50000;
    private float _refreshInterval = 1.0f;

    public static EntitiesPanelBuilder Create()
    {
        return new EntitiesPanelBuilder();
    }

    /// <summary>
    ///     Sets a custom entity list buffer.
    /// </summary>
    public EntitiesPanelBuilder WithEntityListBuffer(TextBuffer buffer)
    {
        _entityListBuffer = buffer;
        return this;
    }

    /// <summary>
    ///     Sets the entity provider function.
    /// </summary>
    public EntitiesPanelBuilder WithEntityProvider(Func<IEnumerable<EntityInfo>>? provider)
    {
        _entityProvider = provider;
        return this;
    }

    /// <summary>
    ///     Sets the maximum number of lines in the buffer.
    /// </summary>
    public EntitiesPanelBuilder WithMaxLines(int maxLines)
    {
        _maxLines = maxLines;
        return this;
    }

    /// <summary>
    ///     Enables or disables auto-refresh.
    /// </summary>
    public EntitiesPanelBuilder WithAutoRefresh(bool enabled)
    {
        _autoRefresh = enabled;
        return this;
    }

    /// <summary>
    ///     Sets the auto-refresh interval in seconds.
    /// </summary>
    public EntitiesPanelBuilder WithRefreshInterval(float intervalSeconds)
    {
        _refreshInterval = intervalSeconds;
        return this;
    }

    /// <summary>
    ///     Builds the EntitiesPanel.
    /// </summary>
    public EntitiesPanel Build()
    {
        var panel = new EntitiesPanel(
            _entityListBuffer ?? CreateDefaultEntityListBuffer(),
            CreateDefaultStatusBar()
        );

        panel.AutoRefresh = _autoRefresh;
        panel.RefreshInterval = _refreshInterval;

        if (_entityProvider != null)
        {
            panel.SetEntityProvider(_entityProvider);
        }

        return panel;
    }

    private TextBuffer CreateDefaultEntityListBuffer()
    {
        return new TextBuffer("entities_buffer")
        {
            // BackgroundColor uses theme fallback - don't set explicitly
            AutoScroll = false,
            MaxLines = _maxLines,
            Constraint = new LayoutConstraint { Anchor = Anchor.StretchTop },
        };
    }

    private static StatusBar CreateDefaultStatusBar()
    {
        return new StatusBar("entities_status")
        {
            Constraint = new LayoutConstraint { Anchor = Anchor.StretchBottom },
        };
    }
}
