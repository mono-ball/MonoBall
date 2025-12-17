using MonoBallFramework.Game.Engine.UI.Components.Controls;
using MonoBallFramework.Game.Engine.UI.Layout;

namespace MonoBallFramework.Game.Engine.UI.Components.Debug;

/// <summary>
///     Builder for creating EntityFrameworkPanel with customizable components.
/// </summary>
public class EntityFrameworkPanelBuilder
{
    private float _splitRatio = 0.4f;

    public static EntityFrameworkPanelBuilder Create()
    {
        return new EntityFrameworkPanelBuilder();
    }

    /// <summary>
    ///     Sets the split ratio (0-1, ratio for left pane).
    ///     Default is 0.4 (40% for list, 60% for details).
    /// </summary>
    public EntityFrameworkPanelBuilder WithSplitRatio(float ratio)
    {
        _splitRatio = Math.Clamp(ratio, 0.2f, 0.8f);
        return this;
    }

    public EntityFrameworkPanel Build()
    {
        var panel = new EntityFrameworkPanel(CreateDefaultStatusBar());
        panel.SplitRatio = _splitRatio;
        return panel;
    }

    private static StatusBar CreateDefaultStatusBar()
    {
        return new StatusBar("entityframework_status")
        {
            Constraint = new LayoutConstraint { Anchor = Anchor.StretchBottom }
        };
    }
}
