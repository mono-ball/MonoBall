namespace PokeSharp.Engine.UI.Debug.Layout;

/// <summary>
///     Defines layout constraints for positioning and sizing a UI element.
///     These constraints are resolved into absolute coordinates during layout resolution.
/// </summary>
public class LayoutConstraint
{
    private Anchor _anchor = Anchor.TopLeft;
    private float? _height;
    private float? _heightPercent;
    private Thickness _margin;
    private float? _maxHeight;
    private float? _maxWidth;
    private float? _minHeight;
    private float? _minWidth;
    private float _offsetX;
    private float _offsetY;
    private Thickness _padding;
    private float? _width;
    private float? _widthPercent;

    /// <summary>Dirty flag - true when any constraint property has changed</summary>
    public bool IsDirty { get; private set; } = true;

    /// <summary>Anchor point for positioning</summary>
    public Anchor Anchor
    {
        get => _anchor;
        set
        {
            if (_anchor != value)
            {
                _anchor = value;
                MarkDirty();
            }
        }
    }

    /// <summary>X offset from anchor point (in pixels)</summary>
    public float OffsetX
    {
        get => _offsetX;
        set
        {
            if (_offsetX != value)
            {
                _offsetX = value;
                MarkDirty();
            }
        }
    }

    /// <summary>Y offset from anchor point (in pixels)</summary>
    public float OffsetY
    {
        get => _offsetY;
        set
        {
            if (_offsetY != value)
            {
                _offsetY = value;
                MarkDirty();
            }
        }
    }

    /// <summary>Width in pixels (null for auto-sizing)</summary>
    public float? Width
    {
        get => _width;
        set
        {
            if (_width != value)
            {
                _width = value;
                MarkDirty();
            }
        }
    }

    /// <summary>Height in pixels (null for auto-sizing)</summary>
    public float? Height
    {
        get => _height;
        set
        {
            if (_height != value)
            {
                _height = value;
                MarkDirty();
            }
        }
    }

    /// <summary>Width as percentage of parent (0.0-1.0, overrides Width if set)</summary>
    public float? WidthPercent
    {
        get => _widthPercent;
        set
        {
            if (_widthPercent != value)
            {
                _widthPercent = value;
                MarkDirty();
            }
        }
    }

    /// <summary>Height as percentage of parent (0.0-1.0, overrides Height if set)</summary>
    public float? HeightPercent
    {
        get => _heightPercent;
        set
        {
            if (_heightPercent != value)
            {
                _heightPercent = value;
                MarkDirty();
            }
        }
    }

    /// <summary>Minimum width constraint</summary>
    public float? MinWidth
    {
        get => _minWidth;
        set
        {
            if (_minWidth != value)
            {
                _minWidth = value;
                MarkDirty();
            }
        }
    }

    /// <summary>Minimum height constraint</summary>
    public float? MinHeight
    {
        get => _minHeight;
        set
        {
            if (_minHeight != value)
            {
                _minHeight = value;
                MarkDirty();
            }
        }
    }

    /// <summary>Maximum width constraint</summary>
    public float? MaxWidth
    {
        get => _maxWidth;
        set
        {
            if (_maxWidth != value)
            {
                _maxWidth = value;
                MarkDirty();
            }
        }
    }

    /// <summary>Maximum height constraint</summary>
    public float? MaxHeight
    {
        get => _maxHeight;
        set
        {
            if (_maxHeight != value)
            {
                _maxHeight = value;
                MarkDirty();
            }
        }
    }

    /// <summary>
    ///     Margin (space outside the element).
    ///     Accepts a float (uniform) or Thickness (individual sides).
    /// </summary>
    public Thickness Margin
    {
        get => _margin;
        set
        {
            if (_margin != value)
            {
                _margin = value;
                MarkDirty();
            }
        }
    }

    /// <summary>
    ///     Padding (space inside the element).
    ///     Accepts a float (uniform) or Thickness (individual sides).
    /// </summary>
    public Thickness Padding
    {
        get => _padding;
        set
        {
            if (_padding != value)
            {
                _padding = value;
                MarkDirty();
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Convenience properties for setting individual sides
    // These modify the underlying Thickness while maintaining the other sides
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Left margin (convenience setter, modifies Margin.Left)</summary>
    public float MarginLeft
    {
        get => _margin.Left;
        set => Margin = new Thickness(value, _margin.Top, _margin.Right, _margin.Bottom);
    }

    /// <summary>Top margin (convenience setter, modifies Margin.Top)</summary>
    public float MarginTop
    {
        get => _margin.Top;
        set => Margin = new Thickness(_margin.Left, value, _margin.Right, _margin.Bottom);
    }

    /// <summary>Right margin (convenience setter, modifies Margin.Right)</summary>
    public float MarginRight
    {
        get => _margin.Right;
        set => Margin = new Thickness(_margin.Left, _margin.Top, value, _margin.Bottom);
    }

    /// <summary>Bottom margin (convenience setter, modifies Margin.Bottom)</summary>
    public float MarginBottom
    {
        get => _margin.Bottom;
        set => Margin = new Thickness(_margin.Left, _margin.Top, _margin.Right, value);
    }

    /// <summary>Left padding (convenience setter, modifies Padding.Left)</summary>
    public float PaddingLeft
    {
        get => _padding.Left;
        set => Padding = new Thickness(value, _padding.Top, _padding.Right, _padding.Bottom);
    }

    /// <summary>Top padding (convenience setter, modifies Padding.Top)</summary>
    public float PaddingTop
    {
        get => _padding.Top;
        set => Padding = new Thickness(_padding.Left, value, _padding.Right, _padding.Bottom);
    }

    /// <summary>Right padding (convenience setter, modifies Padding.Right)</summary>
    public float PaddingRight
    {
        get => _padding.Right;
        set => Padding = new Thickness(_padding.Left, _padding.Top, value, _padding.Bottom);
    }

    /// <summary>Bottom padding (convenience setter, modifies Padding.Bottom)</summary>
    public float PaddingBottom
    {
        get => _padding.Bottom;
        set => Padding = new Thickness(_padding.Left, _padding.Top, _padding.Right, value);
    }

    /// <summary>Clears the dirty flag after layout resolution</summary>
    public void ClearDirty()
    {
        IsDirty = false;
    }

    private void MarkDirty()
    {
        IsDirty = true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Convenience accessor methods (for LayoutResolver compatibility)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Gets the left margin</summary>
    public float GetMarginLeft()
    {
        return _margin.Left;
    }

    /// <summary>Gets the top margin</summary>
    public float GetMarginTop()
    {
        return _margin.Top;
    }

    /// <summary>Gets the right margin</summary>
    public float GetMarginRight()
    {
        return _margin.Right;
    }

    /// <summary>Gets the bottom margin</summary>
    public float GetMarginBottom()
    {
        return _margin.Bottom;
    }

    /// <summary>Gets the left padding</summary>
    public float GetPaddingLeft()
    {
        return _padding.Left;
    }

    /// <summary>Gets the top padding</summary>
    public float GetPaddingTop()
    {
        return _padding.Top;
    }

    /// <summary>Gets the right padding</summary>
    public float GetPaddingRight()
    {
        return _padding.Right;
    }

    /// <summary>Gets the bottom padding</summary>
    public float GetPaddingBottom()
    {
        return _padding.Bottom;
    }
}
