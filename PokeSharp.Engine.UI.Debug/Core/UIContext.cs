using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.UI.Debug.Input;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Core;

/// <summary>
/// Main context for immediate mode UI rendering.
/// Manages per-frame state, input, and coordinate transformations.
/// </summary>
public class UIContext : IDisposable
{
    private readonly UIRenderer _renderer;
    private readonly UIFrame _frame = new();
    private readonly Stack<LayoutContainer> _containerStack = new();
    private LayoutContainer? _rootContainer;
    private InputState _inputState = new();

    // Screen dimensions
    private int _screenWidth;
    private int _screenHeight;

    // Mouse position caching for optimization
    private Point _lastMousePosition;
    private bool _mousePositionChanged;

    // Hover tracking during registration
    private int _currentHoveredZOrder = int.MinValue;
    private string? _currentHoveredId = null;

    private bool _disposed;

    public UIContext(GraphicsDevice graphicsDevice)
    {
        _renderer = new UIRenderer(graphicsDevice);

        var viewport = graphicsDevice.Viewport;
        _screenWidth = viewport.Width;
        _screenHeight = viewport.Height;
    }

    /// <summary>
    /// Gets the current theme from ThemeManager for runtime switching support.
    /// </summary>
    public UITheme Theme => ThemeManager.Current;

    /// <summary>
    /// Gets the renderer.
    /// </summary>
    public UIRenderer Renderer => _renderer;

    /// <summary>
    /// Gets the current input state.
    /// </summary>
    public InputState Input => _inputState;

    /// <summary>
    /// Gets the current frame.
    /// </summary>
    public UIFrame Frame => _frame;

    /// <summary>
    /// Sets the font system for text rendering.
    /// </summary>
    public void SetFontSystem(FontStashSharp.FontSystem fontSystem)
    {
        _renderer.SetFontSystem(fontSystem, ThemeManager.Current.FontSize);
    }

    /// <summary>
    /// Updates screen dimensions (call when window is resized).
    /// </summary>
    public void UpdateScreenSize(int width, int height)
    {
        _screenWidth = width;
        _screenHeight = height;
    }

    /// <summary>
    /// Begins a new UI frame.
    /// </summary>
    public void BeginFrame(InputState inputState)
    {
        _inputState = inputState;

        // Check if mouse position changed (for optimization)
        _mousePositionChanged = _lastMousePosition != inputState.MousePosition;
        _lastMousePosition = inputState.MousePosition;

        _frame.BeginFrame();
        _containerStack.Clear();

        // Reset hover tracking for this frame
        // IMPORTANT: Clear HoveredComponentId so it's only set if mouse is actually over a component
        // This fixes the bug where hover would persist when moving mouse out of interactive areas
        _currentHoveredZOrder = int.MinValue;
        _currentHoveredId = null;
        _frame.HoveredComponentId = null;

        // Create root container (entire screen)
        _rootContainer = new LayoutContainer(new LayoutConstraint
        {
            Anchor = Anchor.Fill,
            Width = _screenWidth,
            Height = _screenHeight
        });

        _rootContainer.ResolveLayout(new LayoutRect(0, 0, _screenWidth, _screenHeight));
        _containerStack.Push(_rootContainer);

        _renderer.Begin();
    }

    /// <summary>
    /// Ends the current UI frame.
    /// </summary>
    public void EndFrame()
    {
        if (_containerStack.Count != 1)
        {
            throw new InvalidOperationException(
                $"Container stack not balanced. Expected 1 item, found {_containerStack.Count}. " +
                "Ensure all BeginPanel/BeginContainer calls have matching End calls."
            );
        }

        _renderer.End();
        _containerStack.Clear();
    }

    /// <summary>
    /// Gets the current container (top of stack).
    /// </summary>
    public LayoutContainer CurrentContainer
    {
        get
        {
            if (_containerStack.Count == 0)
                throw new InvalidOperationException("No active container. Call BeginFrame first.");
            return _containerStack.Peek();
        }
    }

    /// <summary>
    /// Begins a new container with the specified constraint.
    /// Returns the resolved rectangle for the container.
    /// </summary>
    public LayoutRect BeginContainer(string id, LayoutConstraint constraint)
    {
        var parent = CurrentContainer;
        var container = new LayoutContainer(constraint);

        // Resolve layout relative to parent's content area
        container.ResolveLayout(parent.ContentRect);

        // Push onto stack
        _containerStack.Push(container);

        // Track in frame
        _frame.Components[id] = new ComponentFrameState
        {
            Id = id,
            Rect = container.Rect,
            IsInteractive = false,
            ZOrder = _containerStack.Count
        };

        // Apply clipping
        _renderer.PushClip(container.ContentRect);

        return container.Rect;
    }

    /// <summary>
    /// Ends the current container.
    /// Gracefully handles stack imbalance to prevent game crashes.
    /// </summary>
    public void EndContainer()
    {
        if (_containerStack.Count <= 1)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine("[UIContext] EndContainer called on root container - ignoring");
#endif
            return;
        }

        _containerStack.Pop();
        _renderer.PopClip();
    }

    /// <summary>
    /// Registers a component for input handling.
    /// Performs incremental hover detection as components are registered.
    /// </summary>
    public void RegisterComponent(string id, LayoutRect rect, bool interactive = false, string? parentId = null)
    {
        var zOrder = _containerStack.Count;

        _frame.Components[id] = new ComponentFrameState
        {
            Id = id,
            ParentId = parentId,
            Rect = rect,
            IsInteractive = interactive,
            ZOrder = zOrder
        };

        // Incremental hover detection: check if mouse is over this component
        // This ensures hover state is always current when components render
        if (interactive && _frame.CapturedComponentId == null)
        {
            var mousePos = _inputState.MousePosition;

            // If mouse is over this component and it has higher Z-order than current hover
            if (rect.Contains(mousePos) && zOrder > _currentHoveredZOrder)
            {
                _currentHoveredId = id;
                _currentHoveredZOrder = zOrder;
                _frame.HoveredComponentId = id;
            }
        }
    }

    /// <summary>
    /// Checks if a component is hovered.
    /// </summary>
    public bool IsHovered(string id)
    {
        return _frame.HoveredComponentId == id;
    }

    /// <summary>
    /// Checks if a component has focus.
    /// </summary>
    public bool IsFocused(string id)
    {
        return _frame.FocusedComponentId == id;
    }

    /// <summary>
    /// Checks if a component is being pressed.
    /// </summary>
    public bool IsPressed(string id)
    {
        return _frame.PressedComponentId == id;
    }

    /// <summary>
    /// Sets focus to a component.
    /// </summary>
    public void SetFocus(string id)
    {
        _frame.FocusedComponentId = id;
    }

    /// <summary>
    /// Clears focus.
    /// </summary>
    public void ClearFocus()
    {
        _frame.FocusedComponentId = null;
    }

    /// <summary>
    /// Captures all input to a specific component.
    /// Useful for drag operations that need to receive input even when mouse moves outside component bounds.
    /// </summary>
    public void CaptureInput(string id)
    {
        _frame.CapturedComponentId = id;
    }

    /// <summary>
    /// Releases input capture.
    /// </summary>
    public void ReleaseCapture()
    {
        _frame.CapturedComponentId = null;
    }

    /// <summary>
    /// Checks if a component has captured input.
    /// </summary>
    public bool HasCapture(string id)
    {
        return _frame.CapturedComponentId == id;
    }

    /// <summary>
    /// Gets the parent ID of a component for event bubbling.
    /// </summary>
    public string? GetParentId(string id)
    {
        return _frame.Components.TryGetValue(id, out var component) ? component.ParentId : null;
    }

    /// <summary>
    /// Updates hover state only (not pressed state).
    /// Called before rendering to provide current hover state to components.
    /// </summary>
    public void UpdateHoverState()
    {
        // If input is captured, hover state goes to captured component
        // This overrides incremental hover detection
        if (_frame.CapturedComponentId != null)
        {
            _frame.HoveredComponentId = _frame.CapturedComponentId;
            return;
        }

        if (_mousePositionChanged)
        {
            // Only recalculate if mouse moved
            // This is a fallback for when called before components are registered
            // (using previous frame's component positions)
            var mousePos = _inputState.MousePosition;
            ComponentFrameState? hoveredComponent = null;
            int highestZ = int.MinValue;

            foreach (var component in _frame.Components.Values)
            {
                if (component.IsInteractive && component.Rect.Contains(mousePos) && component.ZOrder > highestZ)
                {
                    hoveredComponent = component;
                    highestZ = component.ZOrder;
                }
            }

            // Always update hover state when mouse moves - either to a new component or to null
            _frame.HoveredComponentId = hoveredComponent?.Id;
        }
        // If mouse hasn't moved, keep previous hover state (optimization)
    }

    /// <summary>
    /// Updates pressed state based on button transitions and current hover state.
    /// Called after rendering to ensure pressed state uses correct hover.
    /// </summary>
    public void UpdatePressedState()
    {
        // Update pressed state (always check for button state changes)
        if (_inputState.IsMouseButtonPressed(MouseButton.Left))
        {
            _frame.PressedComponentId = _frame.HoveredComponentId;
        }
        else if (_inputState.IsMouseButtonReleased(MouseButton.Left))
        {
            _frame.PressedComponentId = null;
        }
    }

    /// <summary>
    /// Updates both hover and pressed state based on mouse position.
    /// Note: Hover state is also updated incrementally during RegisterComponent() for better performance.
    /// </summary>
    public void UpdateInteractionState()
    {
        UpdateHoverState();
        UpdatePressedState();
    }

    /// <summary>
    /// Disposes the UIContext and releases all resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected implementation of Dispose pattern.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Dispose managed resources
            _renderer?.Dispose();
        }

        // No unmanaged resources to dispose

        _disposed = true;
    }
}

