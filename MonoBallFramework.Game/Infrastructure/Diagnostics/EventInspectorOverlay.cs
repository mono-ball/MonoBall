using Microsoft.Xna.Framework.Graphics;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.UI.Components.Debug;
using MonoBallFramework.Game.Engine.UI.Core;
using MonoBallFramework.Game.Engine.UI.Input;

namespace MonoBallFramework.Game.Infrastructure.Diagnostics;

/// <summary>
///     Wraps EventInspectorPanel with UIContext for standalone rendering.
///     Toggle with F9 key.
/// </summary>
public class EventInspectorOverlay : IDisposable
{
    private readonly EventInspectorAdapter _adapter;
    private readonly InputState _inputState;
    private readonly EventMetrics _metrics;
    private readonly EventInspectorPanel _panel;
    private readonly UIContext _uiContext;
    private bool _disposed;

    /// <summary>
    ///     Creates a new Event Inspector overlay.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device for rendering.</param>
    /// <param name="eventBus">The event bus to monitor.</param>
    /// <param name="metrics">
    ///     Optional shared EventMetrics instance. If null, creates a new instance.
    ///     Pass the DI-registered singleton to share metrics with ConsoleSystem.
    /// </param>
    public EventInspectorOverlay(GraphicsDevice graphicsDevice, EventBus eventBus, EventMetrics? metrics = null)
    {
        // Use provided metrics or create new (for backwards compatibility)
        _metrics = metrics ?? new EventMetrics { IsEnabled = false };

        // Connect metrics to EventBus (only if not already connected)
        if (eventBus.Metrics == null)
        {
            eventBus.Metrics = _metrics;
        }

        // Create adapter using the shared metrics
        _adapter = new EventInspectorAdapter(eventBus, _metrics);

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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        GC.SuppressFinalize(this);
        _disposed = true;
        _uiContext.Dispose();
    }

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
        _uiContext.UpdateScreenSize(graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height);

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
}
