using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Engine.Core.Events;
using PokeSharp.Engine.UI.Debug.Components.Debug;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Input;

namespace PokeSharp.Game.Infrastructure.Diagnostics;

/// <summary>
///     Wraps EventInspectorPanel with UIContext for standalone rendering.
///     Toggle with F9 key.
/// </summary>
public class EventInspectorOverlay : IDisposable
{
    private readonly EventInspectorPanel _panel;
    private readonly EventInspectorAdapter _adapter;
    private readonly UIContext _uiContext;
    private readonly InputState _inputState;
    private bool _disposed;

    /// <summary>
    ///     Creates a new Event Inspector overlay.
    /// </summary>
    public EventInspectorOverlay(
        GraphicsDevice graphicsDevice,
        EventBus eventBus)
    {
        // Create metrics (disabled by default)
        var metrics = new EventMetrics { IsEnabled = false };

        // Connect metrics to EventBus
        eventBus.Metrics = metrics;

        // Create adapter
        _adapter = new EventInspectorAdapter(eventBus, metrics, maxLogEntries: 100);

        // Create panel
        _panel = new EventInspectorPanelBuilder()
            .WithDataProvider(() => _adapter.GetInspectorData())
            .WithRefreshInterval(2)
            .Build();

        // Configure panel size (800x600 like example)
        _panel.Constraint.Width = 800;
        _panel.Constraint.Height = 600;
        _panel.Visible = false; // Start hidden

        // Create UIContext for rendering
        _uiContext = new UIContext(graphicsDevice);
        _inputState = new InputState();
    }

    /// <summary>
    ///     Whether the overlay is currently visible.
    /// </summary>
    public bool IsVisible => _panel.Visible;

    /// <summary>
    ///     Toggles the overlay visibility and metrics collection.
    /// </summary>
    public void Toggle()
    {
        // Toggle metrics collection
        _adapter.IsEnabled = !_adapter.IsEnabled;

        // Toggle panel visibility
        _panel.Visible = _adapter.IsEnabled;

        // Reset timings when enabling
        if (_adapter.IsEnabled)
        {
            _adapter.ResetTimings();
        }
    }

    /// <summary>
    ///     Draws the Event Inspector overlay if visible.
    /// </summary>
    public void Draw(GraphicsDevice graphicsDevice)
    {
        if (!_panel.Visible)
        {
            return;
        }

        // Update screen size
        _uiContext.UpdateScreenSize(
            graphicsDevice.Viewport.Width,
            graphicsDevice.Viewport.Height
        );

        // Update input state (keyboard for panel controls)
        _inputState.Update();

        bool beginFrameCalled = false;
        try
        {
            // Begin frame
            _uiContext.BeginFrame(_inputState);
            beginFrameCalled = true;

            // Update hover state
            _uiContext.UpdateHoverState();

            // Render panel
            _panel.Render(_uiContext);

            // Update pressed state
            _uiContext.UpdatePressedState();
        }
        finally
        {
            // Always end frame if begun
            if (beginFrameCalled)
            {
                _uiContext.EndFrame();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _uiContext.Dispose();
    }
}
