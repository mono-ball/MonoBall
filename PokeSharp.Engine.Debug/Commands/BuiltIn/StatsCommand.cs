using PokeSharp.Engine.UI.Debug.Core;

namespace PokeSharp.Engine.Debug.Commands.BuiltIn;

/// <summary>
/// Command for viewing and interacting with performance statistics.
/// </summary>
[ConsoleCommand("stats", "View performance statistics")]
public class StatsCommand : IConsoleCommand
{
    public string Name => "stats";
    public string Description => "View performance statistics";
    public string Usage => @"stats                  - Show current performance stats
stats show             - Switch to Stats tab
stats fps              - Show current FPS
stats frame            - Show frame time statistics
stats memory           - Show memory usage details
stats gc               - Show garbage collection stats
stats summary          - Show brief performance summary";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        var theme = context.Theme;
        var stats = context.Stats;
        var subCommand = args.Length > 0 ? args[0].ToLower() : "";

        if (stats == null)
        {
            context.WriteLine("Stats panel not available.", theme.Warning);
            return Task.CompletedTask;
        }

        switch (subCommand)
        {
            case "show":
            case "":
                if (subCommand == "show")
                {
                    context.SwitchToTab(ConsoleTabs.Stats.Index);
                    context.WriteLine("Switched to Stats tab.", theme.Success);
                }
                else
                {
                    ShowSummary(context, stats);
                }
                break;

            case "fps":
                ShowFps(context, stats);
                break;

            case "frame":
                ShowFrameTime(context, stats);
                break;

            case "memory":
            case "mem":
                ShowMemory(context, stats);
                break;

            case "gc":
                ShowGC(context, stats);
                break;

            case "summary":
                ShowSummary(context, stats);
                break;

            default:
                context.WriteLine($"Unknown stats subcommand: {subCommand}", theme.Error);
                context.WriteLine(Usage, theme.TextSecondary);
                break;
        }

        return Task.CompletedTask;
    }

    private static void ShowFps(IConsoleContext context, UI.Debug.Interfaces.IStatsOperations stats)
    {
        var theme = context.Theme;
        var fps = stats.CurrentFps;
        var fpsColor = fps >= 55 ? theme.Success : fps >= 30 ? theme.Warning : theme.Error;

        context.WriteLine("═══ FPS ═══", theme.Info);
        context.WriteLine($"  Current: {fps:F1} FPS", fpsColor);

        // Rating
        var rating = fps >= 60 ? "Excellent" : fps >= 55 ? "Good" : fps >= 30 ? "Acceptable" : "Poor";
        context.WriteLine($"  Rating:  {rating}", fpsColor);
    }

    private static void ShowFrameTime(IConsoleContext context, UI.Debug.Interfaces.IStatsOperations stats)
    {
        var theme = context.Theme;
        var (min, max, avg) = stats.FrameTimeStats;
        var current = stats.CurrentFrameTimeMs;

        var frameColor = current <= 16.67f ? theme.Success : current <= 25f ? theme.Warning : theme.Error;

        context.WriteLine("═══ Frame Time ═══", theme.Info);
        context.WriteLine($"  Current: {current:F2}ms", frameColor);
        context.WriteLine($"  Average: {avg:F2}ms", theme.TextPrimary);
        context.WriteLine($"  Min:     {min:F2}ms", theme.TextSecondary);
        context.WriteLine($"  Max:     {max:F2}ms", max > 25f ? theme.Warning : theme.TextSecondary);
        context.WriteLine($"  Budget:  16.67ms (60 FPS)", theme.TextDim);

        // Budget usage
        var budgetPercent = (current / 16.67f) * 100f;
        var budgetColor = budgetPercent <= 80 ? theme.Success : budgetPercent <= 100 ? theme.Warning : theme.Error;
        context.WriteLine($"  Usage:   {budgetPercent:F0}% of budget", budgetColor);
    }

    private static void ShowMemory(IConsoleContext context, UI.Debug.Interfaces.IStatsOperations stats)
    {
        var theme = context.Theme;
        var memMB = stats.CurrentMemoryMB;
        var memColor = memMB < 256 ? theme.Success : memMB < 512 ? theme.Warning : theme.Error;

        context.WriteLine("═══ Memory ═══", theme.Info);
        context.WriteLine($"  Heap:    {memMB:F1} MB", memColor);

        // Rating
        var rating = memMB < 128 ? "Low" : memMB < 256 ? "Normal" : memMB < 512 ? "Elevated" : "High";
        context.WriteLine($"  Status:  {rating}", memColor);
    }

    private static void ShowGC(IConsoleContext context, UI.Debug.Interfaces.IStatsOperations stats)
    {
        var theme = context.Theme;
        var (gen0, gen1, gen2) = stats.GCCollections;

        context.WriteLine("═══ Garbage Collection ═══", theme.Info);
        context.WriteLine($"  Gen0: {gen0} collections", theme.TextPrimary);
        context.WriteLine($"  Gen1: {gen1} collections", gen1 > 10 ? theme.Warning : theme.TextSecondary);
        context.WriteLine($"  Gen2: {gen2} collections", gen2 > 0 ? theme.Error : theme.TextSecondary);

        context.WriteLine("", theme.TextSecondary);
        context.WriteLine("  Gen0 = Short-lived objects (frequent, normal)", theme.TextDim);
        context.WriteLine("  Gen1 = Medium-lived objects (moderate)", theme.TextDim);
        context.WriteLine("  Gen2 = Long-lived objects (indicates memory pressure)", theme.TextDim);
    }

    private static void ShowSummary(IConsoleContext context, UI.Debug.Interfaces.IStatsOperations stats)
    {
        var theme = context.Theme;
        var fps = stats.CurrentFps;
        var frameTime = stats.CurrentFrameTimeMs;
        var memMB = stats.CurrentMemoryMB;
        var entities = stats.CurrentEntityCount;
        var (gen0, gen1, gen2) = stats.GCCollections;

        var fpsColor = fps >= 55 ? theme.Success : fps >= 30 ? theme.Warning : theme.Error;
        var frameColor = frameTime <= 16.67f ? theme.Success : frameTime <= 25f ? theme.Warning : theme.Error;
        var memColor = memMB < 256 ? theme.Success : memMB < 512 ? theme.Warning : theme.Error;

        context.WriteLine("═══ Performance Summary ═══", theme.Info);
        context.WriteLine($"  FPS:        {fps:F1}", fpsColor);
        context.WriteLine($"  Frame:      {frameTime:F2}ms", frameColor);
        context.WriteLine($"  Memory:     {memMB:F1} MB", memColor);
        context.WriteLine($"  Entities:   {entities:N0}", theme.TextPrimary);
        context.WriteLine($"  GC:         Gen0={gen0} Gen1={gen1} Gen2={gen2}", gen2 > 0 ? theme.Warning : theme.TextSecondary);

        // Overall health
        var isHealthy = fps >= 55 && memMB < 512 && gen2 < 5;
        context.WriteLine($"  Status:     {(isHealthy ? "✓ Healthy" : "⚠ Check profiler")}", isHealthy ? theme.Success : theme.Warning);
    }
}

