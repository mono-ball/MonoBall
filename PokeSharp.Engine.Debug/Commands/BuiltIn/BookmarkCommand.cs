using PokeSharp.Engine.UI.Debug.Core;

namespace PokeSharp.Engine.Debug.Commands.BuiltIn;

/// <summary>
///     Manages bookmarked commands that can be executed via F1-F12 keys.
/// </summary>
[ConsoleCommand("bookmark", "Manage bookmarked commands")]
public class BookmarkCommand : IConsoleCommand
{
    public string Name => "bookmark";
    public string Description => "Manage bookmarked commands";

    public string Usage =>
        @"bookmark                   - List all bookmarks
bookmark <F1-F12> <cmd>    - Bookmark command to F-key
bookmark remove <F1-F12>   - Remove bookmark
bookmark clear             - Clear all bookmarks
bookmark save              - Save bookmarks to disk
bookmark load              - Load bookmarks from disk

Examples:
  bookmark                 (list all)
  bookmark F5 Player.GetMoney()
  bookmark F5 script load quick-test
  bookmark remove F5
  bookmark clear";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        UITheme theme = context.Theme;

        // No args = list bookmarks
        if (args.Length == 0)
        {
            ListBookmarks(context);
            return Task.CompletedTask;
        }

        string subCommand = args[0].ToLower();

        switch (subCommand)
        {
            case "remove":
                if (args.Length < 2)
                {
                    context.WriteLine("Usage: bookmark remove <F1-F12>", theme.Warning);
                    context.WriteLine("Example: bookmark remove F5", theme.TextSecondary);
                    return Task.CompletedTask;
                }

                RemoveBookmark(context, args[1]);
                break;

            case "clear":
                ClearBookmarks(context);
                break;

            case "save":
                SaveBookmarks(context);
                break;

            case "load":
                LoadBookmarks(context);
                break;

            default:
                // Treat as bookmark definition: bookmark <F-key> <command>
                if (args.Length < 2)
                {
                    context.WriteLine("Usage: bookmark <F1-F12> <command>", theme.Warning);
                    context.WriteLine(
                        "Example: bookmark F5 Player.GetMoney()",
                        theme.TextSecondary
                    );
                    return Task.CompletedTask;
                }

                string fkey = args[0];
                string command = string.Join(" ", args.Skip(1));
                AddBookmark(context, fkey, command);
                break;
        }

        return Task.CompletedTask;
    }

    private void ListBookmarks(IConsoleContext context)
    {
        UITheme theme = context.Theme;
        IReadOnlyDictionary<int, string> bookmarks = context.GetAllBookmarks();

        context.WriteLine(
            "══════════════════════════════════════════════════════════════════",
            theme.Success
        );
        context.WriteLine($"  BOOKMARKED COMMANDS ({bookmarks.Count} total)", theme.Success);
        context.WriteLine(
            "══════════════════════════════════════════════════════════════════",
            theme.Success
        );

        if (bookmarks.Count == 0)
        {
            context.WriteLine(
                "  No bookmarks defined. Use 'bookmark <F1-F12> <command>' to create one.",
                theme.TextSecondary
            );
        }
        else
        {
            foreach (KeyValuePair<int, string> bookmark in bookmarks.OrderBy(b => b.Key))
            {
                string fkey = $"F{bookmark.Key}".PadRight(5);
                string command =
                    bookmark.Value.Length > 55
                        ? bookmark.Value.Substring(0, 52) + "..."
                        : bookmark.Value;
                context.WriteLine($"  {fkey} -> {command}", theme.Info);
            }
        }

        context.WriteLine("");
        context.WriteLine("TIP: Press the F-key to execute the bookmarked command", theme.Success);
        context.WriteLine(
            "     Use 'bookmark <F-key> <command>' to create or update",
            theme.Success
        );
    }

    private void AddBookmark(IConsoleContext context, string fkeyStr, string command)
    {
        UITheme theme = context.Theme;

        // Parse F-key (e.g., "F5" -> 5, or "5" -> 5)
        if (!TryParseFKey(fkeyStr, out int fkeyNumber))
        {
            context.WriteLine($"Invalid F-key: '{fkeyStr}'. Must be F1-F12 or 1-12", theme.Error);
            context.WriteLine("Example: bookmark F5 Player.GetMoney()", theme.TextSecondary);
            return;
        }

        if (context.SetBookmark(fkeyNumber, command))
        {
            context.WriteLine($"Bookmarked to F{fkeyNumber}: {command}", theme.Success);
            context.WriteLine("", theme.TextPrimary);
            context.WriteLine($"TIP: Press F{fkeyNumber} to execute this command", theme.Info);
        }
        else
        {
            context.WriteLine($"Failed to bookmark to F{fkeyNumber}", theme.Error);
        }
    }

    private void RemoveBookmark(IConsoleContext context, string fkeyStr)
    {
        UITheme theme = context.Theme;

        if (!TryParseFKey(fkeyStr, out int fkeyNumber))
        {
            context.WriteLine($"Invalid F-key: '{fkeyStr}'. Must be F1-F12 or 1-12", theme.Error);
            return;
        }

        if (context.RemoveBookmark(fkeyNumber))
        {
            context.WriteLine($"Removed bookmark from F{fkeyNumber}", theme.Success);
        }
        else
        {
            context.WriteLine($"No bookmark found at F{fkeyNumber}", theme.Warning);
        }
    }

    private void ClearBookmarks(IConsoleContext context)
    {
        UITheme theme = context.Theme;

        context.ClearAllBookmarks();
        context.WriteLine("All bookmarks cleared", theme.Success);
    }

    private void SaveBookmarks(IConsoleContext context)
    {
        UITheme theme = context.Theme;

        if (context.SaveBookmarks())
        {
            int count = context.GetAllBookmarks().Count;
            context.WriteLine($"Saved {count} bookmark(s) to disk", theme.Success);
        }
        else
        {
            context.WriteLine("Failed to save bookmarks", theme.Error);
        }
    }

    private void LoadBookmarks(IConsoleContext context)
    {
        UITheme theme = context.Theme;

        int count = context.LoadBookmarks();
        if (count > 0)
        {
            context.WriteLine($"Loaded {count} bookmark(s) from disk", theme.Success);
        }
        else
        {
            context.WriteLine("No bookmarks loaded (file may not exist)", theme.Warning);
        }
    }

    private bool TryParseFKey(string fkeyStr, out int fkeyNumber)
    {
        fkeyNumber = 0;

        // Remove "F" prefix if present
        if (fkeyStr.StartsWith("F", StringComparison.OrdinalIgnoreCase))
        {
            fkeyStr = fkeyStr.Substring(1);
        }

        // Parse the number
        if (int.TryParse(fkeyStr, out fkeyNumber))
        {
            // Validate range (1-12)
            return fkeyNumber >= 1 && fkeyNumber <= 12;
        }

        return false;
    }
}
