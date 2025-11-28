using System.Threading.Tasks;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Interfaces;

namespace PokeSharp.Engine.Debug.Commands.BuiltIn;

/// <summary>
/// Command to control the system profiler panel.
/// Also accessible via alias "perf".
/// </summary>
[ConsoleCommand("profiler", "Control system profiler display")]
public class ProfilerCommand : IConsoleCommand
{
    public string Name => "profiler";
    public string Description => "Control system profiler display and view system metrics";
    public string Usage => @"profiler                   - Show profiler statistics
profiler show              - Switch to Profiler tab
profiler sort <mode>       - Set sort mode (time, avg, max, name)
profiler active <on|off>   - Show only active systems
profiler refresh           - Force refresh metrics
profiler system <name>     - Show detailed metrics for a system
profiler list              - List all tracked systems

Keyboard: Ctrl+6 to open Profiler tab directly";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        var theme = context.Theme;
        var profiler = context.Profiler;

        // Check if profiler is available
        if (profiler == null)
        {
            context.WriteLine("Profiler panel not available.", theme.Error);
            return Task.CompletedTask;
        }

        var subCommand = args.Length > 0 ? args[0].ToLower() : "";

        switch (subCommand)
        {
            case "show":
                // Switch to Profiler tab
                context.SwitchToTab(ConsoleTabs.Profiler.Index);
                context.WriteLine("Switched to Profiler tab", theme.Success);
                break;

            case "sort":
                HandleSort(context, args, profiler);
                break;

            case "active":
                HandleActive(context, args, profiler);
                break;

            case "refresh":
                profiler.Refresh();
                context.WriteLine("Profiler metrics refreshed.", theme.Success);
                break;

            case "system":
                profiler.Refresh();
                HandleSystem(context, args, profiler);
                break;

            case "list":
                profiler.Refresh();
                HandleList(context, profiler);
                break;

            case "":
            default:
                // Refresh before showing to get latest data
                profiler.Refresh();
                ShowStatistics(context, profiler);
                break;
        }

        return Task.CompletedTask;
    }

    private static void HandleSort(IConsoleContext context, string[] args, IProfilerOperations profiler)
    {
        var theme = context.Theme;

        if (args.Length < 2)
        {
            context.WriteLine($"Current sort mode: {profiler.GetSortMode()}", theme.Info);
            context.WriteLine("Available modes: time, avg, max, name", theme.TextSecondary);
            return;
        }

        var modeStr = args[1].ToLower();
        ProfilerSortMode? mode = modeStr switch
        {
            "time" or "exec" or "last" => ProfilerSortMode.ByExecutionTime,
            "avg" or "average" => ProfilerSortMode.ByAverageTime,
            "max" or "peak" => ProfilerSortMode.ByMaxTime,
            "name" or "alpha" => ProfilerSortMode.ByName,
            _ => null
        };

        if (mode == null)
        {
            context.WriteLine($"Unknown sort mode: {modeStr}", theme.Error);
            context.WriteLine("Available modes: time, avg, max, name", theme.TextSecondary);
            return;
        }

        profiler.SetSortMode(mode.Value);
        context.WriteLine($"Sort mode set to: {mode.Value}", theme.Success);
    }

    private static void HandleActive(IConsoleContext context, string[] args, IProfilerOperations profiler)
    {
        var theme = context.Theme;

        if (args.Length < 2)
        {
            var current = profiler.GetShowOnlyActive() ? "on" : "off";
            context.WriteLine($"Show only active systems: {current}", theme.Info);
            return;
        }

        var value = args[1].ToLower();
        var showActive = value switch
        {
            "on" or "true" or "1" or "yes" => true,
            "off" or "false" or "0" or "no" => false,
            _ => (bool?)null
        };

        if (showActive == null)
        {
            context.WriteLine($"Invalid value: {value}. Use on/off.", theme.Error);
            return;
        }

        profiler.SetShowOnlyActive(showActive.Value);
        context.WriteLine($"Show only active systems: {(showActive.Value ? "on" : "off")}", theme.Success);
    }

    private static void HandleSystem(IConsoleContext context, string[] args, IProfilerOperations profiler)
    {
        var theme = context.Theme;

        if (args.Length < 2)
        {
            context.WriteLine("Usage: profiler system <name>", theme.Warning);
            return;
        }

        var systemName = args[1];
        var metrics = profiler.GetSystemMetrics(systemName);

        if (metrics == null)
        {
            context.WriteLine($"System not found: {systemName}", theme.Error);
            context.WriteLine("Use 'profiler list' to see all systems.", theme.TextSecondary);
            return;
        }

        context.WriteLine($"═══ {systemName} ═══", theme.Info);
        context.WriteLine($"  Last:    {metrics.Value.LastMs:F3} ms", theme.TextPrimary);
        context.WriteLine($"  Average: {metrics.Value.AvgMs:F3} ms", theme.TextPrimary);
        context.WriteLine($"  Max:     {metrics.Value.MaxMs:F3} ms", theme.Warning);
        context.WriteLine($"  Updates: {metrics.Value.UpdateCount:N0}", theme.TextSecondary);
    }

    private static void HandleList(IConsoleContext context, IProfilerOperations profiler)
    {
        var theme = context.Theme;
        var systems = profiler.GetSystemNames();
        var count = 0;

        context.WriteLine("Tracked Systems:", theme.Info);
        foreach (var name in systems)
        {
            var metrics = profiler.GetSystemMetrics(name);
            if (metrics != null)
            {
                var color = metrics.Value.LastMs > 2.0 ? theme.Warning :
                           metrics.Value.LastMs > 0.5 ? theme.TextPrimary : theme.TextSecondary;
                context.WriteLine($"  {name}: {metrics.Value.LastMs:F2}ms", color);
                count++;
            }
        }

        if (count == 0)
        {
            context.WriteLine("  No systems tracked yet.", theme.TextSecondary);
        }
        else
        {
            context.WriteLine($"Total: {count} systems", theme.TextSecondary);
        }
    }

    private static void ShowStatistics(IConsoleContext context, IProfilerOperations profiler)
    {
        var theme = context.Theme;
        var (systemCount, totalMs, maxMs, slowest) = profiler.GetStatistics();

        context.WriteLine("═══ System Profiler ═══", theme.Info);

        if (systemCount == 0)
        {
            context.WriteLine("  No systems tracked yet.", theme.Warning);
            context.WriteLine("", theme.TextPrimary);
            context.WriteLine("Possible reasons:", theme.TextSecondary);
            context.WriteLine("  • Game hasn't started yet (still on title/menu)", theme.TextSecondary);
            context.WriteLine("  • Systems haven't executed a frame yet", theme.TextSecondary);
            context.WriteLine("  • 'Active only' filter is hiding systems with 0 updates", theme.TextSecondary);
            context.WriteLine("", theme.TextPrimary);
            context.WriteLine("Try:", theme.TextDim);
            context.WriteLine("  profiler active off    Show all systems (not just active)", theme.TextDim);
            context.WriteLine("  profiler show          Open Profiler tab to watch live", theme.TextDim);
        }
        else
        {
            context.WriteLine($"  Systems:  {systemCount}", theme.TextPrimary);
            context.WriteLine($"  Total:    {totalMs:F2} ms", theme.TextPrimary);
            context.WriteLine($"  Max:      {maxMs:F2} ms", maxMs > 2.0 ? theme.Warning : theme.TextPrimary);
            context.WriteLine($"  Slowest:  {slowest}", theme.TextSecondary);
            context.WriteLine($"  Sort:     {profiler.GetSortMode()}", theme.TextSecondary);
            context.WriteLine($"  Active:   {(profiler.GetShowOnlyActive() ? "only" : "all")}", theme.TextSecondary);
            context.WriteLine("", theme.TextPrimary);
            context.WriteLine("Use 'profiler show' or Ctrl+6 to open the Profiler tab.", theme.TextDim);
        }
    }
}

