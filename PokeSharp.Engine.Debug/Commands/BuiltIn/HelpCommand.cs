namespace PokeSharp.Engine.Debug.Commands.BuiltIn;

/// <summary>
/// Displays comprehensive help information with support for topics.
/// </summary>
[ConsoleCommand("help", "Display help information (use 'help <topic>' for specific topics)")]
public class HelpCommand : IConsoleCommand
{
    public string Name => "help";
    public string Description => "Display help information";
    public string Usage => "help [topic|command]\n  Topics: keyboard, commands, scripting";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        var topic = args.Length > 0 ? args[0].ToLower() : "overview";

        switch (topic)
        {
            case "overview":
            case "":
                ShowOverview(context);
                break;
            case "keyboard":
            case "shortcuts":
            case "keys":
                ShowKeyboardHelp(context);
                break;
            case "commands":
            case "cmd":
                ShowCommandsHelp(context);
                break;
            case "scripting":
            case "script":
            case "csharp":
                ShowScriptingHelp(context);
                break;
            default:
                // Try to show help for specific command
                if (!ShowCommandHelp(context, topic))
                {
                    context.WriteLine($"Unknown help topic: '{topic}'", context.Theme.Error);
                    context.WriteLine("Available topics: keyboard, commands, scripting", context.Theme.TextSecondary);
                    context.WriteLine("Or use 'help <command>' for command-specific help", context.Theme.TextSecondary);
                }
                break;
        }

        return Task.CompletedTask;
    }

    private void ShowOverview(IConsoleContext context)
    {
        var theme = context.Theme;

        // Title with bright colors
        context.WriteLine("╔══════════════════════════════════════════════════════════════════╗", theme.BorderFocus);
        context.WriteLine("║                  PokeSharp Debug Console                         ║", theme.Success);
        context.WriteLine("║                   Press Ctrl+~ to toggle                         ║", theme.Info);
        context.WriteLine("╚══════════════════════════════════════════════════════════════════╝", theme.BorderFocus);
        context.WriteLine("Welcome to the PokeSharp debug console!", theme.Success);
        context.WriteLine("This console provides:", theme.TextPrimary);
        context.WriteLine("  •  Real-time C# scripting with game API access", theme.Info);
        context.WriteLine("  •  Command execution and history search", theme.Info);
        context.WriteLine("  •  Output search and filtering", theme.Info);
        context.WriteLine("  •  Auto-completion and documentation", theme.Info);
        context.WriteLine("");
        // Section divider with bright color
        context.WriteLine("══════════════════════════════════════════════════════════════════", theme.Success);
        context.WriteLine("  QUICK START", theme.Success);
        context.WriteLine("══════════════════════════════════════════════════════════════════", theme.Success);
        context.WriteLine("  GET HELP:", theme.BorderFocus);
        context.WriteLine($"    {"help keyboard",-18} Show all keyboard shortcuts", theme.TextPrimary);
        context.WriteLine($"    {"help commands",-18} List all available commands", theme.TextPrimary);
        context.WriteLine($"    {"help scripting",-18} C# scripting guide", theme.TextPrimary);
        context.WriteLine("  COMMON ACTIONS:", theme.BorderFocus);
        context.WriteLine($"    {"clear",-18} Clear console output", theme.TextSecondary);
        context.WriteLine($"    {"Ctrl+R",-18} Search command history", theme.Info);
        context.WriteLine($"    {"Ctrl+F",-18} Search console output", theme.Info);
        context.WriteLine($"    {"Tab",-18} Auto-complete code", theme.Info);
        context.WriteLine("  TRY THIS:", theme.BorderFocus);
        context.WriteLine("    Player.GetMoney()", theme.SyntaxString);
        context.WriteLine("    Player.AddMoney(1000)", theme.SyntaxString);
        context.WriteLine("");
        // Help topics with color coding
        context.WriteLine("══════════════════════════════════════════════════════════════════", theme.Success);
        context.WriteLine("  HELP TOPICS", theme.Success);
        context.WriteLine("══════════════════════════════════════════════════════════════════", theme.Success);
        context.WriteLine($"  {"help keyboard",-20} {"->",-3} All keyboard shortcuts", theme.Info);
        context.WriteLine($"  {"help commands",-20} {"->",-3} All console commands", theme.Info);
        context.WriteLine($"  {"help scripting",-20} {"->",-3} C# scripting guide", theme.Info);
        context.WriteLine("");
        context.WriteLine("TIP: Type any help topic above to learn more!", theme.Success);
    }

    private void ShowKeyboardHelp(IConsoleContext context)
    {
        var theme = context.Theme;

        context.WriteLine("══════════════════════════════════════════════════════════════════", theme.Success);
        context.WriteLine("  KEYBOARD SHORTCUTS", theme.Success);
        context.WriteLine("══════════════════════════════════════════════════════════════════", theme.Success);
        context.WriteLine("  TEXT INPUT:", theme.BorderFocus);
        context.WriteLine($"    {"Enter",-18} Submit command (single-line)", theme.Info);
        context.WriteLine($"    {"Ctrl+Enter",-18} Submit command (multi-line)", theme.Info);
        context.WriteLine($"    {"Shift+Enter",-18} New line (multi-line input)", theme.Info);
        context.WriteLine($"    {"Escape",-18} Cancel current operation", theme.Warning);
        context.WriteLine("  NAVIGATION & EDITING:", theme.BorderFocus);
        context.WriteLine($"    {"Left/Right",-18} Move cursor", theme.TextSecondary);
        context.WriteLine($"    {"Home/End",-18} Jump to start/end of line", theme.TextSecondary);
        context.WriteLine($"    {"Ctrl+Left/Right",-18} Jump by word", theme.TextSecondary);
        context.WriteLine($"    {"Ctrl+A",-18} Select all text", theme.Info);
        context.WriteLine($"    {"Ctrl+X/C/V",-18} Cut/Copy/Paste", theme.Info);
        context.WriteLine("  HISTORY & SEARCH:", theme.BorderFocus);
        context.WriteLine($"    {"Up/Down",-18} Navigate command history", theme.Info);
        context.WriteLine($"    {"Ctrl+R",-18} Search command history *", theme.Success);
        context.WriteLine($"    {"",18}   Type to filter, Enter to select", theme.TextSecondary);
        context.WriteLine($"    {"Ctrl+F",-18} Search console output", theme.Success);
        context.WriteLine($"    {"",18}   F3/Shift+F3 to navigate results", theme.TextSecondary);
        context.WriteLine("  AUTO-COMPLETION:", theme.BorderFocus);
        context.WriteLine($"    {"Tab",-18} Show/accept completions", theme.Info);
        context.WriteLine($"    {"Up/Down",-18} Navigate completion list", theme.TextSecondary);
        context.WriteLine($"    {"Esc",-18} Cancel completions", theme.Warning);
        context.WriteLine($"    {"F1",-18} Show documentation", theme.Success);
        context.WriteLine("  CONSOLE CONTROL:", theme.BorderFocus);
        context.WriteLine($"    {"Ctrl+~ or `",-18} Toggle console on/off", theme.Info);
        context.WriteLine($"    {"Ctrl+Up/Down",-18} Resize console", theme.Info);
        context.WriteLine($"    {"Ctrl+L",-18} Clear output", theme.Info);
        context.WriteLine("");
        context.WriteLine("TIP: Watch the hint bar at the bottom for context-specific shortcuts!", theme.Success);
    }

    private void ShowCommandsHelp(IConsoleContext context)
    {
        var theme = context.Theme;

        context.WriteLine("══════════════════════════════════════════════════════════════════", theme.Success);
        context.WriteLine("  CONSOLE COMMANDS", theme.Success);
        context.WriteLine("══════════════════════════════════════════════════════════════════", theme.Success);
        context.WriteLine("Console commands help you control the debug environment.", theme.TextPrimary);
        context.WriteLine("Commands can be executed with or without the '/' prefix.", theme.TextSecondary);
        context.WriteLine("");
        context.WriteLine("  BUILT-IN COMMANDS:", theme.BorderFocus);
        // Hardcoded common commands for better formatting
        var commonCommands = new[]
        {
            ("help", "Display this help information"),
            ("clear", "Clear all console output"),
            ("exit", "Close the console (same as quit)"),
            ("quit", "Close the console"),
            ("log", "Manage logging (on/off, level, filter, search, clear)"),
            ("history", "Manage command history (list, clear, save, load)"),
            ("alias", "Manage aliases (list, create, remove)"),
            ("script", "Manage scripts (list, load, save, reset)"),
            ("bookmark", "Manage bookmarks (F-key shortcuts)"),
            ("watch", "Manage watch expressions (real-time monitoring)")
        };

        foreach (var (name, desc) in commonCommands)
        {
            context.WriteLine($"    {name,-15} -> {desc}", theme.Info);
        }
        context.WriteLine("");
        context.WriteLine("  GET DETAILED HELP:", theme.BorderFocus);
        context.WriteLine("    help <command>        Example: help logging", theme.Success);
        context.WriteLine("");
        context.WriteLine("TIP: Type a command name and press Tab for auto-completion!", theme.Success);
    }

    private void ShowScriptingHelp(IConsoleContext context)
    {
        var theme = context.Theme;

        context.WriteLine("══════════════════════════════════════════════════════════════════", theme.Success);
        context.WriteLine("  C# SCRIPTING GUIDE", theme.Success);
        context.WriteLine("══════════════════════════════════════════════════════════════════", theme.Success);
        context.WriteLine("The console supports full C# scripting with real-time evaluation.", theme.Success);
        context.WriteLine("You have direct access to the entire game API!", theme.TextPrimary);
        context.WriteLine("");
        context.WriteLine("  BASIC USAGE:", theme.BorderFocus);
        context.WriteLine("    2 + 2                    Simple expressions", theme.TextSecondary);
        context.WriteLine("    Math.Sqrt(16)            Math operations", theme.TextSecondary);
        context.WriteLine("    Player.GetMoney()        Method calls", theme.Info);
        context.WriteLine("    Player.Name              Property access", theme.Info);
        context.WriteLine("    var x = 42               Variable assignment", theme.Info);
        context.WriteLine("  MULTI-LINE SCRIPTS:", theme.BorderFocus);
        context.WriteLine("    Press Shift+Enter to write multi-line code:", theme.TextSecondary);
        context.WriteLine("    var money = Player.GetMoney();", theme.SyntaxKeyword);
        context.WriteLine("    Player.AddMoney(1000);", theme.SyntaxKeyword);
        context.WriteLine("    return $\"Old: {money}, New: {Player.GetMoney()}\";", theme.SyntaxString);
        context.WriteLine("  AVAILABLE APIs:", theme.BorderFocus);
        context.WriteLine("    Player           Inventory, stats, money, position", theme.Info);
        context.WriteLine("    Game             Current scene, time, game state", theme.Info);
        context.WriteLine("    Entities         ECS entity queries and modifications", theme.Info);
        context.WriteLine("    Scenes           Scene loading and management", theme.Info);
        context.WriteLine("  AUTO-COMPLETION:", theme.BorderFocus);
        context.WriteLine($"    {"Tab",-15} -> Show context-aware completions", theme.Success);
        context.WriteLine($"    {"Type '.'",-15} -> Show methods/properties", theme.Success);
        context.WriteLine($"    {"Type '('",-15} -> Show parameter hints", theme.Success);
        context.WriteLine($"    {"F1",-15} -> View documentation", theme.Success);
        context.WriteLine("  TRY THESE EXAMPLES:", theme.BorderFocus);
        context.WriteLine("    Player.GetMoney()                  Check current money", theme.SyntaxString);
        context.WriteLine("    Player.AddMoney(1000)              Add 1000 to wallet", theme.SyntaxString);
        context.WriteLine("    Math.Max(10, 20)                   Math functions", theme.SyntaxString);
        context.WriteLine("    DateTime.Now                       System types", theme.SyntaxString);
        context.WriteLine("    Enumerable.Range(1,10).Sum()       LINQ queries", theme.SyntaxString);
        context.WriteLine("  PRO TIPS:", theme.BorderFocus);
        context.WriteLine("    *  Use semicolons to separate statements", theme.TextSecondary);
        context.WriteLine("    *  Return values are automatically printed", theme.TextSecondary);
        context.WriteLine("    *  Variables persist across evaluations", theme.TextSecondary);
        context.WriteLine("    *  Import namespaces with 'using' statements", theme.TextSecondary);
        context.WriteLine("");
        context.WriteLine("TIP: Have fun experimenting! The console is your playground.", theme.Success);
    }

    private bool ShowCommandHelp(IConsoleContext context, string commandName)
    {
        var command = context.GetCommand(commandName);
        if (command == null)
            return false;

        var theme = context.Theme;

        context.WriteLine("══════════════════════════════════════════════════════════════════", theme.Info);
        context.WriteLine($"  COMMAND: {command.Name.ToUpper()}", theme.Info);
        context.WriteLine("══════════════════════════════════════════════════════════════════", theme.Info);
        context.WriteLine("");

        context.WriteLine("  Description:", theme.Success);
        context.WriteLine($"    {command.Description}", theme.TextPrimary);
        context.WriteLine("");

        if (!string.IsNullOrEmpty(command.Usage))
        {
            context.WriteLine("  Usage:", theme.Success);
            var usageLines = command.Usage.Split('\n');
            foreach (var line in usageLines)
            {
                context.WriteLine($"    {line.Trim()}", theme.TextPrimary);
            }
            context.WriteLine("");
        }

        context.WriteLine($"Type 'help commands' to see all available commands.", theme.TextSecondary);

        return true;
    }
}

