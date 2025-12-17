using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.UI.Components.Debug;
using MonoBallFramework.Game.Engine.UI.Core;
using MonoBallFramework.Game.Engine.UI.Models;
using Timer = System.Timers.Timer;

namespace MonoBallFramework.Game.Engine.UI.Examples;

/// <summary>
///     Example demonstrating how to integrate the Event Inspector into your debug UI.
/// </summary>
public static class EventInspectorExample
{
    /// <summary>
    ///     Creates and configures an Event Inspector panel for debugging event bus activity.
    /// </summary>
    /// <param name="eventBus">The EventBus instance to monitor.</param>
    /// <param name="enabledByDefault">Whether metrics collection should start enabled.</param>
    /// <returns>Configured EventInspectorPanel ready to add to a scene.</returns>
    public static (EventInspectorPanel Panel, EventInspectorAdapter Adapter) CreateEventInspector(
        EventBus eventBus,
        bool enabledByDefault = false
    )
    {
        // Step 1: Create the metrics collector
        var metrics = new EventMetrics { IsEnabled = enabledByDefault };

        // Step 2: Create the adapter that bridges EventBus and UI
        var adapter = new EventInspectorAdapter(eventBus, metrics);

        // Step 3: Build the UI panel
        EventInspectorPanel panel = new EventInspectorPanelBuilder()
            .WithDataProvider(() => adapter.GetInspectorData())
            .WithRefreshInterval(2) // Update every 2 frames (~30 FPS at 60 FPS base)
            .Build();

        // Step 4: Configure layout
        panel.Constraint.Width = 800;
        panel.Constraint.Height = 600;

        // Optional: Start hidden until explicitly enabled
        panel.Visible = enabledByDefault;

        return (panel, adapter);
    }

    /// <summary>
    ///     Example of how to toggle the Event Inspector with a keyboard shortcut.
    /// </summary>
    public static void SetupToggleKey(
        EventInspectorPanel panel,
        EventInspectorAdapter adapter,
        Action<Action> onKeyF9Pressed
    )
    {
        onKeyF9Pressed(() =>
        {
            // Toggle metrics collection
            adapter.IsEnabled = !adapter.IsEnabled;

            // Toggle panel visibility
            panel.Visible = adapter.IsEnabled;

            // Optional: Clear old data when re-enabling
            if (adapter.IsEnabled)
            {
                adapter.ResetTimings();
            }
        });
    }

    /// <summary>
    ///     Example of periodic metric cleanup to prevent memory buildup.
    /// </summary>
    public static void SetupPeriodicCleanup(EventInspectorAdapter adapter, TimeSpan cleanupInterval)
    {
        var timer = new Timer(cleanupInterval.TotalMilliseconds);
        timer.Elapsed += (sender, e) =>
        {
            if (adapter.IsEnabled)
            {
                adapter.ResetTimings(); // Keep subscriber counts, clear timing data
            }
        };
        timer.AutoReset = true;
        timer.Start();
    }

    /// <summary>
    ///     Example of conditional metrics based on build configuration.
    /// </summary>
    public static EventInspectorPanel CreateDebugOnlyInspector(EventBus eventBus)
    {
        (EventInspectorPanel panel, EventInspectorAdapter adapter) = CreateEventInspector(eventBus);

#if DEBUG
        adapter.IsEnabled = true;
        panel.Visible = true;
#else
        adapter.IsEnabled = false;
        panel.Visible = false;
#endif

        return panel;
    }

    /// <summary>
    ///     Example of dynamic refresh rate adjustment based on event activity.
    /// </summary>
    public static void SetupDynamicRefreshRate(
        EventInspectorPanel panel,
        EventInspectorAdapter adapter
    )
    {
        // Check every second if we should adjust refresh rate
        var timer = new Timer(1000);
        timer.Elapsed += (sender, e) =>
        {
            if (!adapter.IsEnabled)
            {
                return;
            }

            EventInspectorData data = adapter.GetInspectorData();
            long totalPublishCount = data.Events.Sum(e => e.PublishCount);

            // High activity: update more frequently
            if (totalPublishCount > 1000)
            {
                panel.SetRefreshInterval(1); // Every frame
            }
            // Medium activity: moderate refresh
            else if (totalPublishCount > 100)
            {
                panel.SetRefreshInterval(2); // Every 2 frames
            }
            // Low activity: slower refresh
            else
            {
                panel.SetRefreshInterval(5); // Every 5 frames
            }
        };
        timer.AutoReset = true;
        timer.Start();
    }

    /// <summary>
    ///     Example of exporting metrics to a log file.
    /// </summary>
    public static void ExportMetricsToLog(EventInspectorAdapter adapter, string logPath)
    {
        EventInspectorData data = adapter.GetInspectorData();

        using var writer = new StreamWriter(logPath);
        writer.WriteLine($"Event Inspector Report - {DateTime.Now}");
        writer.WriteLine(new string('=', 80));
        writer.WriteLine();

        writer.WriteLine($"Total Event Types: {data.Events.Count}");
        writer.WriteLine($"Total Subscribers: {data.Events.Sum(e => e.SubscriberCount)}");
        writer.WriteLine();

        writer.WriteLine("Event Performance Metrics:");
        writer.WriteLine(new string('-', 80));

        foreach (EventTypeInfo evt in data.Events.OrderByDescending(e => e.AverageTimeMs))
        {
            writer.WriteLine($"{evt.EventTypeName}:");
            writer.WriteLine($"  Subscribers: {evt.SubscriberCount}");
            writer.WriteLine($"  Publish Count: {evt.PublishCount}");
            writer.WriteLine($"  Avg Time: {evt.AverageTimeMs:F3}ms");
            writer.WriteLine($"  Max Time: {evt.MaxTimeMs:F3}ms");

            if (evt.Subscriptions.Any())
            {
                writer.WriteLine("  Handlers:");
                foreach (
                    SubscriptionInfo sub in evt.Subscriptions.OrderByDescending(s =>
                        s.AverageTimeMs
                    )
                )
                {
                    writer.WriteLine(
                        $"    [#{sub.HandlerId}] {sub.Source ?? "Unknown"} - "
                        + $"{sub.AverageTimeMs:F3}ms avg ({sub.InvocationCount} calls)"
                    );
                }
            }

            writer.WriteLine();
        }

        writer.WriteLine();
        writer.WriteLine("Recent Events:");
        writer.WriteLine(new string('-', 80));

        foreach (EventLogEntry entry in data.RecentEvents.TakeLast(50))
        {
            writer.WriteLine(
                $"[{entry.Timestamp:HH:mm:ss.fff}] {entry.Operation} "
                + $"{entry.EventType} - {entry.DurationMs:F3}ms"
            );
        }
    }
}
