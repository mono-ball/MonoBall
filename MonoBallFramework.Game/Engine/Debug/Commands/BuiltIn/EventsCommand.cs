using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.UI.Core;
using MonoBallFramework.Game.Engine.UI.Interfaces;
using MonoBallFramework.Game.Engine.UI.Models;

namespace MonoBallFramework.Game.Engine.Debug.Commands.BuiltIn;

/// <summary>
///     Command for viewing and managing event system statistics and diagnostics.
/// </summary>
[ConsoleCommand("events", "View event system statistics and diagnostics")]
public class EventsCommand : IConsoleCommand
{
    public string Name => "events";
    public string Description => "View event system statistics and diagnostics";

    public string Usage =>
        @"events                 - Show event system summary
events show            - Switch to Events tab
events pools           - Show detailed pool statistics
events list            - List all event types with metrics
events stats <type>    - Show statistics for specific event type
events export          - Export event list to clipboard";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        UITheme theme = context.Theme;
        string subCommand = args.Length > 0 ? args[0].ToLower() : "";

        switch (subCommand)
        {
            case "show":
                context.SwitchToTab(ConsoleTabs.Events.Index);
                context.WriteLine("Switched to Events tab.", theme.Success);
                break;

            case "pools":
            case "pool":
                ShowPoolStats(context);
                break;

            case "list":
                ShowEventList(context);
                break;

            case "stats":
                if (args.Length > 1)
                {
                    ShowEventStats(context, args[1]);
                }
                else
                {
                    context.WriteLine("Usage: events stats <event-type>", theme.Warning);
                    context.WriteLine("Example: events stats MovementStartedEvent", theme.TextDim);
                }

                break;

            case "export":
                ExportEvents(context);
                break;

            case "":
                ShowSummary(context);
                break;

            default:
                context.WriteLine($"Unknown events subcommand: {subCommand}", theme.Error);
                context.WriteLine(Usage, theme.TextSecondary);
                break;
        }

        return Task.CompletedTask;
    }

    private static void ShowSummary(IConsoleContext context)
    {
        UITheme theme = context.Theme;
        IEventBus? eventBus = context.EventBus;

        if (eventBus == null)
        {
            context.WriteLine("EventBus not available.", theme.Warning);
            return;
        }

        // Get pool statistics
        IReadOnlyCollection<EventPoolStatistics> poolStats = eventBus.GetPoolStatistics();
        long totalPublished = poolStats.Sum(s => s.TotalRented);
        long totalCreated = poolStats.Sum(s => s.TotalCreated);
        long totalInUse = poolStats.Sum(s => s.CurrentlyInUse);
        double avgReuseRate = poolStats.Count > 0 ? poolStats.Average(s => s.ReuseRate) : 0.0;

        // Get registered event types
        IReadOnlyCollection<Type> registeredTypes = eventBus.GetRegisteredEventTypes();

        context.WriteLine("═══ Event System Summary ═══", theme.Info);
        context.WriteLine(
            $"  Event Types:    {poolStats.Count} pooled, {registeredTypes.Count} registered",
            theme.TextPrimary
        );
        context.WriteLine($"  Published:      {totalPublished:N0} events", theme.TextPrimary);

        // Reuse rate with color coding
        Color reuseColor =
            avgReuseRate >= 0.95 ? theme.Success
            : avgReuseRate >= 0.80 ? theme.Warning
            : theme.Error;
        context.WriteLine($"  Reuse Rate:     {avgReuseRate:P0}", reuseColor);

        // Saved allocations
        long saved = totalPublished - totalCreated;
        if (saved > 0)
        {
            context.WriteLine($"  Saved:          {saved:N0} allocations", theme.Success);
        }

        // In-flight with warning
        Color inUseColor =
            totalInUse < 10 ? theme.Success
            : totalInUse < 50 ? theme.Warning
            : theme.Error;
        context.WriteLine($"  In Flight:      {totalInUse}", inUseColor);

        if (totalInUse >= 50)
        {
            context.WriteLine(
                "  ⚠ Warning: High in-flight count - check for leaks!",
                theme.Warning
            );
        }

        context.WriteLine("\nUse 'tab stats' for pool details (press 8)", theme.TextDim);
        context.WriteLine("Use 'events list' for event inspector summary", theme.TextDim);
        context.WriteLine("Use 'events show' to switch to Events tab (press 6)", theme.TextDim);
    }

    private static void ShowPoolStats(IConsoleContext context)
    {
        UITheme theme = context.Theme;
        IEventBus? eventBus = context.EventBus;

        if (eventBus == null)
        {
            context.WriteLine("EventBus not available.", theme.Warning);
            return;
        }

        var poolStats = eventBus.GetPoolStatistics().OrderByDescending(s => s.TotalRented).ToList();

        if (poolStats.Count == 0)
        {
            context.WriteLine("No event pools found.", theme.Warning);
            context.WriteLine(
                "Event pools are created lazily when events are published.",
                theme.TextDim
            );
            return;
        }

        context.WriteLine("═══ Event Pool Statistics ═══", theme.Info);
        context.WriteLine(
            $"{"Event Type",-30} {"Rented",10} {"Created",10} {"Reuse",8} {"In Use",8}",
            theme.TextSecondary
        );
        context.WriteLine(new string('─', 74), theme.BorderPrimary);

        foreach (EventPoolStatistics stat in poolStats)
        {
            Color reuseColor =
                stat.ReuseRate >= 0.95 ? theme.Success
                : stat.ReuseRate >= 0.80 ? theme.Warning
                : theme.Error;

            Color inUseColor =
                stat.CurrentlyInUse < 5 ? theme.Success
                : stat.CurrentlyInUse < 20 ? theme.Warning
                : theme.Error;

            // Truncate long event names
            string eventName = stat.EventType.Replace("Event", "");
            if (eventName.Length > 28)
            {
                eventName = eventName.Substring(0, 25) + "...";
            }

            // Format row with proper spacing
            context.WriteLine(
                $"{eventName,-30} {stat.TotalRented,10:N0} {stat.TotalCreated,10:N0} {stat.ReuseRate,7:P0} {stat.CurrentlyInUse,8}",
                theme.TextPrimary
            );
        }

        long totalSaved = poolStats.Sum(s => s.TotalRented - s.TotalCreated);
        context.WriteLine(new string('─', 74), theme.BorderPrimary);
        context.WriteLine($"Total allocations saved: {totalSaved:N0}", theme.Success);
    }

    private static void ShowEventList(IConsoleContext context)
    {
        UITheme theme = context.Theme;
        IEventInspectorOperations? inspector = context.EventInspector;

        if (inspector == null)
        {
            context.WriteLine("Event Inspector not available.", theme.Warning);
            context.WriteLine("Use 'events show' to switch to Events tab", theme.TextDim);
            return;
        }

        EventInspectorData data = inspector.GetData();
        var events = data.Events.OrderBy(e => e.EventTypeName).ToList();

        if (events.Count == 0)
        {
            context.WriteLine("No events found.", theme.Warning);
            context.WriteLine(
                "Events appear after they are published or subscribed to.",
                theme.TextDim
            );
            return;
        }

        context.WriteLine("═══ All Event Types ═══", theme.Info);
        context.WriteLine(
            $"{"Event Type",-35} {"Subs",5} {"Count",10} {"Avg ms",8}",
            theme.TextSecondary
        );
        context.WriteLine(new string('─', 64), theme.BorderPrimary);

        foreach (EventTypeInfo evt in events)
        {
            // Truncate long names
            string eventName = evt.EventTypeName.Replace("Event", "");
            if (eventName.Length > 33)
            {
                eventName = eventName.Substring(0, 30) + "...";
            }

            Color nameColor = evt.IsCustom ? theme.Info : theme.TextPrimary;
            Color subsColor = evt.SubscriberCount > 0 ? theme.Success : theme.TextDim;
            Color countColor =
                evt.PublishCount > 1000 ? theme.Warning
                : evt.PublishCount > 0 ? theme.TextSecondary
                : theme.TextDim;

            string subsText = $"{evt.SubscriberCount,5}";
            string countText = $"{evt.PublishCount,10:N0}";
            string avgText = evt.PublishCount > 0 ? $"{evt.AverageTimeMs,7:F2}" : "     -";

            Color timeColor =
                evt.AverageTimeMs >= 1.0 ? theme.Error
                : evt.AverageTimeMs >= 0.1 ? theme.Warning
                : evt.PublishCount > 0 ? theme.Success
                : theme.TextDim;

            context.WriteLine($"{eventName,-35} {subsText} {countText} {avgText}", nameColor);
        }

        context.WriteLine(new string('─', 64), theme.BorderPrimary);
        int customCount = events.Count(e => e.IsCustom);
        context.WriteLine(
            $"Total: {events.Count} event types ({customCount} custom)",
            theme.TextSecondary
        );
    }

    private static void ShowEventStats(IConsoleContext context, string eventTypeName)
    {
        UITheme theme = context.Theme;
        context.WriteLine($"═══ {eventTypeName} ═══", theme.Info);
        context.WriteLine("Switching to Events tab for detailed view...", theme.TextSecondary);
        context.SwitchToTab(ConsoleTabs.Events.Index);
        context.WriteLine("Tip: Use sort buttons to find your event", theme.TextDim);
    }

    private static void ExportEvents(IConsoleContext context)
    {
        UITheme theme = context.Theme;
        IEventInspectorOperations? inspector = context.EventInspector;

        if (inspector == null)
        {
            context.WriteLine("Event Inspector not available.", theme.Warning);
            return;
        }

        inspector.CopyToClipboard();
        context.WriteLine("Event list exported to clipboard.", theme.Success);
        context.WriteLine("Use 'events show' for interactive view", theme.TextDim);
    }
}
