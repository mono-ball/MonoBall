using PokeSharp.Engine.UI.Debug.Core;

namespace PokeSharp.Engine.Debug.Commands.BuiltIn;

/// <summary>
///     Manages command history (view, clear, save, load).
/// </summary>
[ConsoleCommand("history", "Manage command history")]
public class HistoryCommand : IConsoleCommand
{
    public string Name => "history";
    public string Description => "Manage command history";

    public string Usage =>
        @"history [subcommand]
  history        - Show command history
  history clear  - Clear history
  history save   - Save history to disk
  history load   - Load history from disk";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        // No arguments - show history
        if (args.Length == 0)
        {
            ShowHistory(context);
            return Task.CompletedTask;
        }

        string subcommand = args[0].ToLower();

        switch (subcommand)
        {
            case "clear":
                ClearHistory(context);
                break;

            case "save":
                SaveHistory(context);
                break;

            case "load":
                LoadHistory(context);
                break;

            default:
                context.WriteLine($"Unknown subcommand: '{subcommand}'", context.Theme.Error);
                context.WriteLine(
                    "Use 'help history' for usage information",
                    context.Theme.TextSecondary
                );
                break;
        }

        return Task.CompletedTask;
    }

    private void ShowHistory(IConsoleContext context)
    {
        UITheme theme = context.Theme;
        IReadOnlyList<string> history = context.GetCommandHistory();

        if (history.Count == 0)
        {
            context.WriteLine("No commands in history", theme.TextSecondary);
            return;
        }

        context.WriteLine(
            "══════════════════════════════════════════════════════════════════",
            theme.Success
        );
        context.WriteLine($"  COMMAND HISTORY ({history.Count} commands)", theme.Success);
        context.WriteLine(
            "══════════════════════════════════════════════════════════════════",
            theme.Success
        );

        // Show most recent first (reverse order)
        for (int i = history.Count - 1; i >= 0; i--)
        {
            int index = i + 1;
            string command = history[i];

            // Truncate long commands
            string displayCommand =
                command.Length > 80 ? command.Substring(0, 77) + "..." : command;

            context.WriteLine($"  {index, 3}. {displayCommand}", theme.TextPrimary);
        }

        context.WriteLine("");
        context.WriteLine(
            "TIP: Use Up/Down arrows to navigate history or Ctrl+R to search",
            theme.Success
        );
    }

    private void ClearHistory(IConsoleContext context)
    {
        UITheme theme = context.Theme;
        int countBefore = context.GetCommandHistory().Count;

        context.ClearCommandHistory();

        context.WriteLine($"Cleared {countBefore} command(s) from history", theme.Success);
    }

    private void SaveHistory(IConsoleContext context)
    {
        UITheme theme = context.Theme;
        int count = context.GetCommandHistory().Count;

        context.SaveCommandHistory();

        context.WriteLine($"Saved {count} command(s) to disk", theme.Success);
    }

    private void LoadHistory(IConsoleContext context)
    {
        UITheme theme = context.Theme;

        context.LoadCommandHistory();

        int count = context.GetCommandHistory().Count;
        context.WriteLine($"Loaded {count} command(s) from disk", theme.Success);
    }
}
