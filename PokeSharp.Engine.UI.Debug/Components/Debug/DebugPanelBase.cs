using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Components.Layout;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
///     Base class for debug panels that follow a common layout pattern:
///     - Content area that fills available space above a status bar
///     - StatusBar anchored at the bottom
///     - Common theming and border handling
/// </summary>
public abstract class DebugPanelBase : Panel
{
    /// <summary>
    ///     Standard padding for all debug panels.
    /// </summary>
    public const int StandardPadding = 8;

    /// <summary>
    ///     Standard line padding for content alignment.
    ///     Should match TextBuffer.LinePadding and StatusBar.Padding.
    /// </summary>
    public const int StandardLinePadding = 5;

    protected readonly StatusBar StatusBar;

    protected DebugPanelBase(StatusBar statusBar)
    {
        StatusBar = statusBar;

        // Standard panel configuration
        BorderThickness = 1;
        Constraint.Padding = StandardPadding;

        // StatusBar anchored to bottom
        StatusBar.Constraint.Anchor = Anchor.StretchBottom;
        StatusBar.Constraint.OffsetY = 0;

        AddChild(StatusBar);
    }

    /// <summary>
    ///     Gets the content component that fills the space above the StatusBar.
    ///     Override this in derived classes to return the appropriate content component.
    /// </summary>
    protected abstract UIComponent GetContentComponent();

    /// <summary>
    ///     Called after layout is calculated to update the status bar.
    ///     Override this to customize status bar content.
    /// </summary>
    protected abstract void UpdateStatusBar();

    protected override void OnRenderContainer(UIContext context)
    {
        // Apply theme colors dynamically for theme switching
        BackgroundColor = ThemeManager.Current.ConsoleBackground;
        BorderColor = ThemeManager.Current.BorderPrimary;

        base.OnRenderContainer(context);

        UIRenderer renderer = context.Renderer;

        // Calculate layout: StatusBar at bottom, content fills remaining space
        float statusBarHeight = StatusBar.GetDesiredHeight(renderer);
        StatusBar.Constraint.Height = statusBarHeight;

        float paddingTop = Constraint.GetPaddingTop();
        float paddingBottom = Constraint.GetPaddingBottom();
        float availableHeight = Rect.Height - paddingTop - paddingBottom;

        UIComponent? contentComponent = GetContentComponent();
        if (contentComponent != null)
        {
            contentComponent.Constraint.Height = availableHeight - statusBarHeight;
        }

        // Update status bar content
        UpdateStatusBar();
    }

    /// <summary>
    ///     Helper to format a stats/hints pair for the status bar.
    /// </summary>
    protected void SetStatusBar(string stats, string hints)
    {
        StatusBar.Set(stats, hints);
    }

    /// <summary>
    ///     Helper to set status bar color based on health state.
    /// </summary>
    protected void SetStatusBarHealthColor(bool isHealthy, bool isWarning = false)
    {
        if (isHealthy)
        {
            StatusBar.ResetStatsColor();
        }
        else if (isWarning)
        {
            StatusBar.StatsColor = ThemeManager.Current.Warning;
        }
        else
        {
            StatusBar.StatsColor = ThemeManager.Current.Error;
        }
    }

    /// <summary>
    ///     Renders children with StatusBar last to ensure it appears on top.
    ///     This matches the old rendering order where StatusBar was added after content.
    /// </summary>
    protected override void OnRenderChildren(UIContext context)
    {
        // Render all children except StatusBar first
        foreach (UIComponent child in Children)
        {
            if (child != StatusBar)
            {
                child.Render(context);
            }
        }

        // Render StatusBar last so it appears on top
        StatusBar.Render(context);
    }
}
