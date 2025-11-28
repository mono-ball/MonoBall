using System.Collections.Generic;

namespace PokeSharp.Engine.UI.Debug.Layout;

/// <summary>
/// Manages layout resolution for a container and its children.
/// Handles coordinate space transformations and child layout.
/// </summary>
public class LayoutContainer
{
    /// <summary>The resolved rectangle for this container</summary>
    public LayoutRect Rect { get; private set; }

    /// <summary>The content area (inside padding)</summary>
    public LayoutRect ContentRect { get; private set; }

    /// <summary>The constraint that produced this layout</summary>
    public LayoutConstraint Constraint { get; }

    /// <summary>Child containers</summary>
    private readonly List<LayoutContainer> _children = new();

    public IReadOnlyList<LayoutContainer> Children => _children;

    public LayoutContainer(LayoutConstraint constraint)
    {
        Constraint = constraint;
    }

    /// <summary>
    /// Resolves this container's layout relative to a parent rectangle.
    /// </summary>
    public void ResolveLayout(LayoutRect parentRect, (float width, float height)? contentSize = null)
    {
        Rect = LayoutResolver.Resolve(Constraint, parentRect, contentSize);

        // Calculate content area by applying padding
        ContentRect = Rect.Shrink(
            Constraint.GetPaddingLeft(),
            Constraint.GetPaddingTop(),
            Constraint.GetPaddingRight(),
            Constraint.GetPaddingBottom()
        );
    }

    /// <summary>
    /// Adds a child container.
    /// </summary>
    public LayoutContainer AddChild(LayoutConstraint constraint)
    {
        var child = new LayoutContainer(constraint);
        _children.Add(child);
        return child;
    }

    /// <summary>
    /// Removes a child container.
    /// </summary>
    public void RemoveChild(LayoutContainer child)
    {
        _children.Remove(child);
    }

    /// <summary>
    /// Clears all children.
    /// </summary>
    public void ClearChildren()
    {
        _children.Clear();
    }

    /// <summary>
    /// Resolves layout for all children using this container's content area as parent.
    /// </summary>
    public void ResolveChildLayouts()
    {
        foreach (var child in _children)
        {
            child.ResolveLayout(ContentRect);
            child.ResolveChildLayouts(); // Recursive
        }
    }

    /// <summary>
    /// Converts a point from screen space to this container's local space.
    /// </summary>
    public (float x, float y) ScreenToLocal(float screenX, float screenY)
    {
        return (screenX - Rect.X, screenY - Rect.Y);
    }

    /// <summary>
    /// Converts a point from this container's local space to screen space.
    /// </summary>
    public (float x, float y) LocalToScreen(float localX, float localY)
    {
        return (localX + Rect.X, localY + Rect.Y);
    }

    /// <summary>
    /// Checks if a screen point is inside this container.
    /// </summary>
    public bool ContainsScreenPoint(float x, float y)
    {
        return Rect.Contains(x, y);
    }
}




