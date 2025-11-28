using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Controls;

/// <summary>
///     Horizontal container for tab buttons.
/// </summary>
public class TabBar : UIContainer
{
    /// <summary>Background color for the tab bar</summary>
    public Color? BackgroundColor { get; set; }

    /// <summary>Border color for the tab bar</summary>
    public Color? BorderColor { get; set; }

    /// <summary>Border thickness</summary>
    public int BorderThickness { get; set; } = 1;

    /// <summary>Spacing between tabs</summary>
    public float TabSpacing { get; set; } = 2f;

    /// <summary>Currently active tab index</summary>
    public int ActiveTabIndex { get; set; }

    /// <summary>Gets the number of tabs in this tab bar</summary>
    public int TabCount => Children.Count;

    /// <summary>Event triggered when active tab changes</summary>
    public Action<int>? OnTabChanged { get; set; }

    /// <summary>
    ///     Adds a tab to the bar.
    /// </summary>
    public void AddTab(string text, string id)
    {
        int tabIndex = Children.Count;
        var tab = new Tab
        {
            Id = id,
            Text = text,
            IsActive = tabIndex == ActiveTabIndex,
            OnClick = () => SetActiveTab(tabIndex),
        };
        AddChild(tab);
    }

    /// <summary>
    ///     Sets the active tab by index.
    /// </summary>
    public void SetActiveTab(int index)
    {
        if (index < 0 || index >= Children.Count)
        {
            return;
        }

        if (ActiveTabIndex == index)
        {
            return;
        }

        ActiveTabIndex = index;

        // Update tab active states
        for (int i = 0; i < Children.Count; i++)
        {
            if (Children[i] is Tab tab)
            {
                tab.IsActive = i == ActiveTabIndex;
            }
        }

        OnTabChanged?.Invoke(index);
    }

    protected override void OnRenderContainer(UIContext context)
    {
        // Draw background
        if (BackgroundColor.HasValue)
        {
            context.Renderer.DrawRectangle(Rect, BackgroundColor.Value);
        }

        // Draw bottom border to separate from content
        if (BorderColor.HasValue && BorderThickness > 0)
        {
            var borderRect = new LayoutRect(
                Rect.X,
                Rect.Y + Rect.Height - BorderThickness,
                Rect.Width,
                BorderThickness
            );
            context.Renderer.DrawRectangle(borderRect, BorderColor.Value);
        }
    }

    protected override void OnRenderChildren(UIContext context)
    {
        // Layout tabs horizontally
        float currentX = 0;

        foreach (UIComponent child in Children)
        {
            if (child is Tab tab && tab.Visible)
            {
                // Measure text size using the context's renderer
                Vector2 textSize = context.Renderer.MeasureText(tab.Text);
                float tabWidth = Math.Max(tab.MinWidth, textSize.X + (tab.Padding * 2));
                int tabHeight = context.Theme.ButtonHeight;

                // Update tab constraint for horizontal layout
                tab.Constraint = new LayoutConstraint
                {
                    Anchor = Anchor.TopLeft,
                    OffsetX = currentX,
                    OffsetY = 0,
                    Width = tabWidth,
                    Height = tabHeight,
                };

                // Move to next position
                currentX += tabWidth + TabSpacing;
            }
        }

        // Let base class handle actual rendering
        base.OnRenderChildren(context);
    }

    protected override (float width, float height)? GetContentSize()
    {
        // Don't auto-size - let constraint determine size
        // This avoids calling GetContentSize on tabs before Context is available
        return null;
    }
}
