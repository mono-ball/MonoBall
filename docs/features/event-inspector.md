# Event Inspector

The Event Inspector is a real-time debug tool that provides comprehensive visibility into the EventBus system.

---

## Features

- **Event Types** - View all registered event types with subscriber counts
- **Active Subscriptions** - See all subscribers with priorities and sources
- **Real-Time Logging** - Watch events as they're published and received
- **Performance Metrics** - Track average and max execution time per event type
- **Per-Handler Tracking** - Monitor individual subscriber performance

---

## Quick Start

### Opening the Event Inspector

1. Press **`~`** to open the debug console
2. Type `tab eventinspector` or press **F4**
3. The Event Inspector panel will open

### Navigation

- **Tab** / **F4** - Switch to Event Inspector
- **↑↓** - Navigate through event list
- **Enter** - Expand/collapse event details
- **Esc** - Close console

---

## Understanding the Display

### Event List

Each event type shows:
```
EventName (subscribers: N)
  ├─ avg: X.XXms  max: X.XXms
  └─ Handlers:
     • HandlerName [priority: N] (X.XXms avg)
```

### Color Coding

Performance thresholds:
- **Green** - Under 1ms (excellent)
- **Yellow** - 1-5ms (good)
- **Orange** - 5-16ms (caution)  
- **Red** - Over 16ms (critical - 60 FPS budget exceeded)

### Custom vs Built-in Events

- **Custom Events** - Events defined by mods or game scripts
- **Built-in Events** - Framework events (movement, collision, etc.)

Filter toggle available at top of panel.

---

## Usage Examples

### Debugging Performance Issues

1. Open Event Inspector
2. Run your game/mod
3. Look for **red** or **orange** events
4. Expand to see which handlers are slow
5. Optimize the slow handlers

### Finding Event Dependencies

1. Find your event type in the list
2. Check subscriber count
3. Expand to see all handlers
4. View handler priorities to understand execution order

### Testing Event Flow

1. Enable real-time logging (toggle at top)
2. Trigger your event
3. Watch it appear in the log
4. Verify all expected handlers received it

---

## Architecture

### Components

**EventMetrics** (`MonoBallFramework.Engine.Core.Events`)
- Collects performance data from EventBus
- Thread-safe concurrent collection
- Minimal overhead when disabled

**EventInspectorAdapter** (`MonoBallFramework.Engine.UI.Core`)
- Bridges EventBus and UI
- Provides formatted data for display
- Manages event logging

**EventInspectorPanel** (`MonoBallFramework.Engine.UI.Components.Debug`)
- Main UI component
- Scrollable display with status bar
- Real-time updates

**EventInspectorContent** (`MonoBallFramework.Engine.UI.Components.Debug`)
- Renders event data with color-coding
- Hierarchical event/subscription view
- Performance summaries

### Integration

The Event Inspector is automatically integrated with the debug console. No setup required!

For custom integration in mods:

```csharp
using MonoBallFramework.Engine.Core.Events;
using MonoBallFramework.Engine.UI.Core;

// Get the EventBus
var eventBus = serviceProvider.GetRequiredService<EventBus>();

// Get metrics interface
var metrics = eventBus as IEventMetrics;

// Check if metrics are available
if (metrics != null)
{
    // Metrics are being collected
    var stats = metrics.GetEventStatistics();
}
```

---

## Performance Impact

The Event Inspector is designed for minimal performance overhead:

- **Disabled**: Zero overhead (no metrics collected)
- **Enabled (closed)**: ~0.01ms per frame (metrics collection only)
- **Enabled (open)**: ~0.1-0.5ms per frame (includes UI rendering)

### Best Practices

1. **Keep closed during normal gameplay** - Only open when debugging
2. **Disable in release builds** - Use conditional compilation
3. **Limit event logging** - Real-time log adds overhead
4. **Clear old metrics** - Use the "Clear" button periodically

---

## Troubleshooting

### "No events registered"

- **Cause**: No event subscriptions active yet
- **Solution**: Wait for game systems to initialize, or trigger some events

### Events not appearing

- **Cause**: Filter set to "Custom Events Only"
- **Solution**: Toggle filter to show all events

### Performance numbers seem wrong

- **Cause**: Metrics were not cleared after game state change
- **Solution**: Click "Clear Metrics" button to reset

### Handler names show as "Unknown"

- **Cause**: Handler registered without name parameter
- **Solution**: When subscribing, provide a handler name:
  ```csharp
  eventBus.Subscribe<MyEvent>(HandleMyEvent, handlerName: "MyModHandler");
  ```

---

## See Also

- [Debug Console](../guides/console-usage.md) - Main debug console documentation
- [Event System](../architecture/event-system.md) - EventBus architecture
- [Performance Profiling](../guides/performance-optimization.md) - Performance optimization guide
- [Modding API](../modding/api-reference.md) - Event API for mod developers

---

**Last Updated**: December 4, 2024

