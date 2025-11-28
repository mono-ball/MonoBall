using Microsoft.Extensions.Logging;
using PokeSharp.Engine.UI.Debug.Core;
using System;
using System.Threading.Tasks;

namespace PokeSharp.Engine.Debug.Commands.BuiltIn;

[ConsoleCommand("log", "Manage and view system logs")]
public class LogCommand : IConsoleCommand
{
    public string Name => "log";
    public string Description => "Manage and view system logs";
    public string Usage => @"log [subcommand]
  (no args)         Show log count and status summary
  show              Switch to Logs tab
  on|off            Enable or disable logging to console
  minlevel <level>  Set minimum capture level (Trace|Debug|Info|Warning|Error|Critical)
  level <level>     Filter displayed logs by level
  clear             Clear all logs
  category <name>   Filter by category (or 'all' to show all)
  categories        List all available categories with counts
  search <text>     Search logs by text (no args to clear)
  stats             Show log statistics
  export [csv]      Copy logs to clipboard (use 'csv' for CSV format)";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        var theme = context.Theme;

        if (args.Length == 0)
        {
            // Show log count and status summary
            var logCount = context.Logs.Count;
            var (total, filtered, errors, warnings, lastMinute, categories) = context.Logs.GetStatistics();

            context.WriteLine($"Logging: {(context.IsLoggingEnabled ? "ON" : "OFF")} (min level: {context.MinimumLogLevel})", theme.Info);
            context.WriteLine($"Logs: {logCount} total", theme.Info);
            if (errors > 0)
                context.WriteLine($"  Errors: {errors}", theme.Error);
            if (warnings > 0)
                context.WriteLine($"  Warnings: {warnings}", theme.Warning);
            if (categories > 0)
                context.WriteLine($"  Categories: {categories}", theme.TextSecondary);
            context.WriteLine("", theme.TextPrimary);
            context.WriteLine("Use 'log show' to switch to the Logs tab", theme.TextDim);
        }
        else if (args[0].Equals("show", StringComparison.OrdinalIgnoreCase))
        {
            context.SwitchToTab(ConsoleTabs.Logs.Index);
            context.WriteLine("Switched to Logs tab", theme.Success);
        }
        else if (args[0].Equals("on", StringComparison.OrdinalIgnoreCase) ||
                 args[0].Equals("enable", StringComparison.OrdinalIgnoreCase) ||
                 args[0].Equals("true", StringComparison.OrdinalIgnoreCase) ||
                 args[0].Equals("1", StringComparison.OrdinalIgnoreCase))
        {
            context.SetLoggingEnabled(true);
            context.WriteLine("Logging enabled", theme.Success);
            context.WriteLine($"Capturing logs at level {context.MinimumLogLevel} and above", theme.TextSecondary);
        }
        else if (args[0].Equals("off", StringComparison.OrdinalIgnoreCase) ||
                 args[0].Equals("disable", StringComparison.OrdinalIgnoreCase) ||
                 args[0].Equals("false", StringComparison.OrdinalIgnoreCase) ||
                 args[0].Equals("0", StringComparison.OrdinalIgnoreCase))
        {
            context.SetLoggingEnabled(false);
            context.WriteLine("Logging disabled", theme.Info);
        }
        else if (args[0].Equals("minlevel", StringComparison.OrdinalIgnoreCase))
        {
            // Set minimum capture level
            if (args.Length < 2)
            {
                context.WriteLine($"Current minimum log level: {context.MinimumLogLevel}", theme.Info);
                context.WriteLine("Usage: log minlevel <level>", theme.TextSecondary);
                context.WriteLine("Levels: Trace, Debug, Information, Warning, Error, Critical", theme.TextSecondary);
                return Task.CompletedTask;
            }

            var levelStr = args[1];
            if (Enum.TryParse<LogLevel>(levelStr, ignoreCase: true, out var level))
            {
                context.SetMinimumLogLevel(level);
                context.WriteLine($"Minimum log level set to: {level}", theme.Success);
            }
            else
            {
                context.WriteLine($"Invalid log level: '{levelStr}'", theme.Error);
                context.WriteLine("Valid levels: Trace, Debug, Information, Warning, Error, Critical", theme.TextSecondary);
            }
        }
        else if (args[0].Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            context.Logs.Clear();
            context.WriteLine("All logs cleared", theme.Success);
        }
        else if (args[0].Equals("level", StringComparison.OrdinalIgnoreCase) ||
                 args[0].Equals("filter", StringComparison.OrdinalIgnoreCase)) // Keep 'filter' as alias
        {
            // Set filter level
            if (args.Length < 2)
            {
                context.WriteLine("Usage: log level <level>", theme.Warning);
                context.WriteLine("Levels: Trace, Debug, Information, Warning, Error, Critical", theme.TextSecondary);
                return Task.CompletedTask;
            }

            var levelStr = args[1];
            if (Enum.TryParse<LogLevel>(levelStr, ignoreCase: true, out var level))
            {
                context.Logs.SetFilterLevel(level);
                context.WriteLine($"Log level filter set to: {level}", theme.Success);
                context.WriteLine("Switch to Logs tab (Ctrl+3) to view filtered logs", theme.TextSecondary);
            }
            else
            {
                context.WriteLine($"Invalid log level: '{levelStr}'", theme.Error);
                context.WriteLine("Valid levels: Trace, Debug, Information, Warning, Error, Critical", theme.TextSecondary);
            }
        }
        else if (args[0].Equals("category", StringComparison.OrdinalIgnoreCase))
        {
            // Set category filter
            if (args.Length < 2)
            {
                context.WriteLine("Usage: log category <name> [name2 ...]", theme.Warning);
                context.WriteLine("       log category all  (show all categories)", theme.TextSecondary);
                return Task.CompletedTask;
            }

            if (args[1].Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                context.Logs.ClearCategoryFilter();
                context.WriteLine("Category filter cleared (showing all)", theme.Success);
            }
            else
            {
                var categories = args.Skip(1).ToArray();
                context.Logs.SetCategoryFilter(categories);
                context.WriteLine($"Filtering by categories: {string.Join(", ", categories)}", theme.Success);
                context.WriteLine("Switch to Logs tab (Ctrl+3) to view filtered logs", theme.TextSecondary);
            }
        }
        else if (args[0].Equals("categories", StringComparison.OrdinalIgnoreCase))
        {
            // List available categories
            var counts = context.Logs.GetCategoryCounts();
            if (counts.Count == 0)
            {
                context.WriteLine("No categories found (no logs yet)", theme.TextSecondary);
                return Task.CompletedTask;
            }

            context.WriteLine("Available log categories:", theme.Info);
            foreach (var (category, count) in counts.OrderByDescending(kvp => kvp.Value))
            {
                context.WriteLine($"  {category,-20} ({count} logs)", theme.TextPrimary);
            }
            context.WriteLine("", theme.TextPrimary);
            context.WriteLine("Use 'log category <name>' to filter", theme.TextSecondary);
        }
        else if (args[0].Equals("search", StringComparison.OrdinalIgnoreCase))
        {
            // Set search filter
            if (args.Length < 2)
            {
                // Clear search
                context.Logs.SetSearch(null);
                context.WriteLine("Log search filter cleared", theme.Success);
            }
            else
            {
                var searchText = string.Join(" ", args.Skip(1));
                context.Logs.SetSearch(searchText);
                context.WriteLine($"Log search filter set to: '{searchText}'", theme.Success);
                context.WriteLine("Switch to Logs tab (Ctrl+3) to view filtered logs", theme.TextSecondary);
            }
        }
        else if (args[0].Equals("stats", StringComparison.OrdinalIgnoreCase))
        {
            // Show log statistics
            var (total, filtered, errors, warnings, lastMinute, categories) = context.Logs.GetStatistics();
            var levelCounts = context.Logs.GetLevelCounts();

            context.WriteLine("═══════════════════════════════════════", theme.Info);
            context.WriteLine("          LOG STATISTICS", theme.Info);
            context.WriteLine("═══════════════════════════════════════", theme.Info);
            context.WriteLine($"  Total Logs:       {total,6}", theme.TextPrimary);
            context.WriteLine($"  Filtered View:    {filtered,6}", theme.TextPrimary);
            context.WriteLine($"  Categories:       {categories,6}", theme.TextPrimary);
            context.WriteLine($"  Last Minute:      {lastMinute,6}", theme.TextSecondary);
            context.WriteLine("───────────────────────────────────────", theme.TextDim);
            context.WriteLine("  By Level:", theme.TextSecondary);

            if (levelCounts.TryGetValue(LogLevel.Critical, out var critical) && critical > 0)
                context.WriteLine($"    Critical:       {critical,6}", theme.Error);
            if (levelCounts.TryGetValue(LogLevel.Error, out var errorCount) && errorCount > 0)
                context.WriteLine($"    Error:          {errorCount,6}", theme.Error);
            if (levelCounts.TryGetValue(LogLevel.Warning, out var warningCount) && warningCount > 0)
                context.WriteLine($"    Warning:        {warningCount,6}", theme.Warning);
            if (levelCounts.TryGetValue(LogLevel.Information, out var info) && info > 0)
                context.WriteLine($"    Information:    {info,6}", theme.TextPrimary);
            if (levelCounts.TryGetValue(LogLevel.Debug, out var debug) && debug > 0)
                context.WriteLine($"    Debug:          {debug,6}", theme.Info);
            if (levelCounts.TryGetValue(LogLevel.Trace, out var trace) && trace > 0)
                context.WriteLine($"    Trace:          {trace,6}", theme.TextDim);

            context.WriteLine("═══════════════════════════════════════", theme.Info);
        }
        else if (args[0].Equals("export", StringComparison.OrdinalIgnoreCase))
        {
            // Export logs to clipboard
            var useCsv = args.Length > 1 && args[1].Equals("csv", StringComparison.OrdinalIgnoreCase);

            if (useCsv)
            {
                var csv = context.Logs.ExportToCsv();
                var lineCount = csv.Split('\n').Length - 1; // Minus header
                PokeSharp.Engine.UI.Debug.Utilities.ClipboardManager.SetText(csv);
                context.WriteLine($"Exported {lineCount} logs to clipboard (CSV format)", theme.Success);
            }
            else
            {
                context.Logs.CopyToClipboard();
                var (total, filtered, _, _, _, _) = context.Logs.GetStatistics();
                context.WriteLine($"Exported {filtered} logs to clipboard (text format)", theme.Success);
            }
        }
        else
        {
            context.WriteLine($"Unknown log subcommand: '{args[0]}'", theme.Error);
            context.WriteLine(Usage, theme.TextSecondary);
        }

        return Task.CompletedTask;
    }
}
