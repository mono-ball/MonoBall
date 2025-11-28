using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Base;

/// <summary>
///     Base class for UI components that can contain other components.
///     Handles child layout and rendering.
/// </summary>
public abstract class UIContainer : UIComponent
{
    /// <summary>Child components</summary>
    protected readonly List<UIComponent> Children = new();

    /// <summary>
    ///     Adds a child component.
    /// </summary>
    public void AddChild(UIComponent child)
    {
        if (child == null)
        {
            throw new ArgumentNullException(nameof(child));
        }

        Children.Add(child);
    }

    /// <summary>
    ///     Removes a child component.
    /// </summary>
    public void RemoveChild(UIComponent child)
    {
        Children.Remove(child);
    }

    /// <summary>
    ///     Removes all child components.
    /// </summary>
    public void ClearChildren()
    {
        Children.Clear();
    }

    /// <summary>
    ///     Renders this container and its children.
    /// </summary>
    protected override void OnRender(UIContext context)
    {
        // Render container background/borders
        OnRenderContainer(context);

        // Calculate content rect by applying padding to this container's rect
        float paddingLeft = Constraint.GetPaddingLeft();
        float paddingTop = Constraint.GetPaddingTop();
        float paddingRight = Constraint.GetPaddingRight();
        float paddingBottom = Constraint.GetPaddingBottom();

        // Begin container for children (sets up coordinate space and clipping)
        // The content area is the container's rect minus padding
        // Calculate offsets relative to parent's ContentRect
        LayoutRect parentContentRect = context.CurrentContainer.ContentRect;
        float relativeOffsetX = Rect.X - parentContentRect.X + paddingLeft;
        float relativeOffsetY = Rect.Y - parentContentRect.Y + paddingTop;

        LayoutRect contentRect = context.BeginContainer(
            Id + "_content",
            new LayoutConstraint
            {
                Anchor = Anchor.TopLeft,
                OffsetX = relativeOffsetX,
                OffsetY = relativeOffsetY,
                Width = Rect.Width - paddingLeft - paddingRight,
                Height = Rect.Height - paddingTop - paddingBottom,
            }
        );

        // Render children
        OnRenderChildren(context);

        // End container
        context.EndContainer();
    }

    /// <summary>
    ///     Override to render the container itself (background, borders, etc.).
    /// </summary>
    protected virtual void OnRenderContainer(UIContext context) { }

    /// <summary>
    ///     Override to customize child rendering.
    ///     Default implementation renders all children in order.
    /// </summary>
    protected virtual void OnRenderChildren(UIContext context)
    {
        foreach (UIComponent child in Children)
        {
            child.Render(context);
        }
    }
}
