using MonoBallFramework.Game.Engine.UI.Components.Controls;
using MonoBallFramework.Game.Engine.UI.Layout;
using MonoBallFramework.Game.Engine.UI.Models;

namespace MonoBallFramework.Game.Engine.UI.Components.Debug;

/// <summary>
///     Builder for creating EntitiesPanelDualPane with customizable options.
/// </summary>
public class EntitiesPanelDualPaneBuilder
{
    private bool _autoUpdate = true;
    private Func<int, EntityInfo, EntityInfo?>? _entityDetailLoader;
    private Func<IEnumerable<EntityInfo>>? _entityProvider;
    private float _splitRatio = 0.4f;
    private double _updateInterval = 2.0;

    public static EntitiesPanelDualPaneBuilder Create()
    {
        return new EntitiesPanelDualPaneBuilder();
    }

    /// <summary>
    ///     Sets the entity provider function.
    /// </summary>
    public EntitiesPanelDualPaneBuilder WithEntityProvider(Func<IEnumerable<EntityInfo>>? provider)
    {
        _entityProvider = provider;
        return this;
    }

    /// <summary>
    ///     Sets the entity detail loader for lazy loading.
    /// </summary>
    public EntitiesPanelDualPaneBuilder WithEntityDetailLoader(Func<int, EntityInfo, EntityInfo?>? loader)
    {
        _entityDetailLoader = loader;
        return this;
    }

    /// <summary>
    ///     Enables or disables auto-update.
    /// </summary>
    public EntitiesPanelDualPaneBuilder WithAutoUpdate(bool enabled)
    {
        _autoUpdate = enabled;
        return this;
    }

    /// <summary>
    ///     Sets the update interval in seconds.
    /// </summary>
    public EntitiesPanelDualPaneBuilder WithUpdateInterval(double intervalSeconds)
    {
        _updateInterval = intervalSeconds;
        return this;
    }

    /// <summary>
    ///     Sets the split ratio (0-1, ratio for left pane).
    ///     Default is 0.4 (40% for entity list, 60% for details).
    /// </summary>
    public EntitiesPanelDualPaneBuilder WithSplitRatio(float ratio)
    {
        _splitRatio = Math.Clamp(ratio, 0.2f, 0.8f);
        return this;
    }

    /// <summary>
    ///     Builds the EntitiesPanelDualPane.
    /// </summary>
    public EntitiesPanelDualPane Build()
    {
        var statusBar = new StatusBar("entities_dual_status")
        {
            Constraint = new LayoutConstraint { Anchor = Anchor.StretchBottom }
        };

        var panel = new EntitiesPanelDualPane(statusBar)
        {
            AutoUpdate = _autoUpdate, UpdateInterval = _updateInterval, SplitRatio = _splitRatio
        };

        if (_entityProvider != null)
        {
            panel.SetEntityProvider(_entityProvider);
        }

        if (_entityDetailLoader != null)
        {
            panel.SetEntityDetailLoader(_entityDetailLoader);
        }

        return panel;
    }
}
