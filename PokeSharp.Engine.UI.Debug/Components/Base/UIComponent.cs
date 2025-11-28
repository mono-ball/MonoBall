using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Input;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Base;

/// <summary>
///     Base class for all UI components in the immediate mode system.
///     Components define constraints, then render using resolved rectangles.
/// </summary>
public abstract class UIComponent
{
    /// <summary>Cached layout rectangle</summary>
    private LayoutRect _cachedRect;

    /// <summary>Layout constraint for this component</summary>
    private LayoutConstraint _constraint = new();

    /// <summary>Last parent rect used for layout - to detect resize</summary>
    private LayoutRect _lastParentRect;

    /// <summary>Layout dirty flag - true when layout needs recalculation</summary>
    private bool _layoutDirty = true;

    /// <summary>Unique identifier for this component</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Whether this component is visible</summary>
    public bool Visible { get; set; } = true;

    /// <summary>Whether this component is enabled</summary>
    public bool Enabled { get; set; } = true;

    public LayoutConstraint Constraint
    {
        get => _constraint;
        set
        {
            if (_constraint != value)
            {
                _constraint = value;
                InvalidateLayout();
            }
        }
    }

    /// <summary>Resolved rectangle for this component (set during layout)</summary>
    protected LayoutRect Rect { get; private set; }

    /// <summary>The UI context this component is rendered in</summary>
    protected UIContext? Context { get; private set; }

    /// <summary>
    ///     Gets the current theme.
    /// </summary>
    protected UITheme Theme => Context?.Theme ?? UITheme.Dark;

    /// <summary>
    ///     Gets the renderer.
    /// </summary>
    protected UIRenderer Renderer =>
        Context?.Renderer ?? throw new InvalidOperationException("No context");

    /// <summary>
    ///     Renders this component using the immediate mode API.
    /// </summary>
    public void Render(UIContext context)
    {
        if (!Visible)
        {
            return;
        }

        Context = context;

        try
        {
            // Resolve layout (with caching)
            LayoutRect parentRect = context.CurrentContainer.ContentRect;

            // Invalidate if parent rect changed (window resize) or constraint changed
            if (_layoutDirty || Constraint.IsDirty || parentRect != _lastParentRect)
            {
                _cachedRect = LayoutResolver.Resolve(Constraint, parentRect, GetContentSize());
                _lastParentRect = parentRect;
                _layoutDirty = false;
                Constraint.ClearDirty();
            }

            Rect = _cachedRect;

            // Register with frame for input handling
            context.RegisterComponent(Id, Rect, IsInteractive());

            // Render
            OnRender(context);
        }
        finally
        {
            // Always clear context to prevent memory leaks and stale state
            Context = null;
        }
    }

    /// <summary>
    ///     Marks the layout as dirty, forcing recalculation on next render.
    ///     Call this when component properties change that affect layout.
    /// </summary>
    protected void InvalidateLayout()
    {
        _layoutDirty = true;
    }

    /// <summary>
    ///     Override to provide content-based auto-sizing.
    ///     Returns null for explicit sizes.
    /// </summary>
    protected virtual (float width, float height)? GetContentSize()
    {
        return null;
    }

    /// <summary>
    ///     Override to specify if this component is interactive (responds to input).
    /// </summary>
    protected virtual bool IsInteractive()
    {
        return false;
    }

    /// <summary>
    ///     Override to implement rendering logic.
    /// </summary>
    protected abstract void OnRender(UIContext context);

    /// <summary>
    ///     Override to handle input events.
    /// </summary>
    protected virtual void OnInput(InputEvent inputEvent) { }

    /// <summary>
    ///     Checks if this component is currently hovered.
    /// </summary>
    protected bool IsHovered()
    {
        return Context?.IsHovered(Id) ?? false;
    }

    /// <summary>
    ///     Checks if this component currently has focus.
    /// </summary>
    protected bool IsFocused()
    {
        return Context?.IsFocused(Id) ?? false;
    }

    /// <summary>
    ///     Checks if this component is currently being pressed.
    /// </summary>
    protected bool IsPressed()
    {
        return Context?.IsPressed(Id) ?? false;
    }
}
