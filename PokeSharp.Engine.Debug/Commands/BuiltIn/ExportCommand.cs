using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Utilities;

namespace PokeSharp.Engine.Debug.Commands.BuiltIn;

/// <summary>
///     Exports console output to clipboard.
/// </summary>
[ConsoleCommand("export", "Export console output to clipboard")]
public class ExportCommand : IConsoleCommand
{
    public string Name => "export";
    public string Description => "Export console output to clipboard";

    public string Usage =>
        @"export [target]
  output            Export console output (default)
  logs              Export logs (text format)
  logs csv          Export logs as CSV
  watch             Export watches (text format)
  watch csv         Export watches as CSV";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        UITheme theme = context.Theme;

        // Default to output if no args
        string target = args.Length > 0 ? args[0].ToLowerInvariant() : "output";

        switch (target)
        {
            case "output":
            case "console":
                (int totalLines, int filteredLines) = context.GetConsoleOutputStats();
                context.CopyConsoleOutputToClipboard();
                context.WriteLine($"Exported {filteredLines} lines to clipboard", theme.Success);
                if (totalLines != filteredLines)
                {
                    context.WriteLine(
                        $"({totalLines} total lines, {filteredLines} after filtering)",
                        theme.TextSecondary
                    );
                }

                break;

            case "logs":
                bool useLogCsv =
                    args.Length > 1 && args[1].Equals("csv", StringComparison.OrdinalIgnoreCase);
                if (useLogCsv)
                {
                    string csv = context.Logs.ExportToCsv();
                    int lineCount = csv.Split('\n').Length - 1;
                    ClipboardManager.SetText(csv);
                    context.WriteLine(
                        $"Exported {lineCount} logs to clipboard (CSV format)",
                        theme.Success
                    );
                }
                else
                {
                    context.Logs.CopyToClipboard();
                    (int total, int filtered, _, _, _, _) = context.Logs.GetStatistics();
                    context.WriteLine($"Exported {filtered} logs to clipboard", theme.Success);
                }

                break;

            case "watch":
            case "watches":
                bool useWatchCsv =
                    args.Length > 1 && args[1].Equals("csv", StringComparison.OrdinalIgnoreCase);
                (int watchTotal, int watchPinned, int watchErrors, _, int watchGroups) =
                    context.Watches.GetStatistics();
                context.Watches.CopyToClipboard(useWatchCsv);
                string format = useWatchCsv ? "CSV" : "text";
                context.WriteLine(
                    $"Exported {watchTotal} watches to clipboard ({format} format)",
                    theme.Success
                );
                if (watchPinned > 0 || watchGroups > 0)
                {
                    context.WriteLine(
                        $"({watchPinned} pinned, {watchGroups} groups)",
                        theme.TextSecondary
                    );
                }

                break;

            default:
                context.WriteLine($"Unknown export target: '{target}'", theme.Error);
                context.WriteLine(Usage, theme.TextSecondary);
                break;
        }

        return Task.CompletedTask;
    }
}
