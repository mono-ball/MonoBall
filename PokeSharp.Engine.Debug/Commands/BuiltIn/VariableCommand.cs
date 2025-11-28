using PokeSharp.Engine.UI.Debug.Core;

namespace PokeSharp.Engine.Debug.Commands.BuiltIn;

/// <summary>
///     Command for managing script variables in the Variables tab.
///     Named 'variable' to avoid conflict with C# 'var' keyword.
/// </summary>
[ConsoleCommand("variable", "Manage script variables")]
public class VariableCommand : IConsoleCommand
{
    public string Name => "variable";
    public string Description => "Manage script variables";

    public string Usage =>
        @"variable [subcommand]
  (no args)         Show variable count and stats
  list              List all variable names
  search <text>     Search variables by name/type/value
  clear-search      Clear the search filter
  expand <name>     Expand a variable to show properties
  collapse <name>   Collapse an expanded variable
  expand-all        Expand all expandable variables
  collapse-all      Collapse all expanded variables
  pin <name>        Pin a variable to the top
  unpin <name>      Unpin a variable
  clear             Clear all user-defined variables

Use 'tab variables' to switch to the Variables tab.

Examples:
  variable search Player          Search for variables containing 'Player'
  variable expand player          Expand the 'player' variable
  variable pin health             Pin the 'health' variable to top";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        UITheme theme = context.Theme;

        if (args.Length == 0)
        {
            // Show stats
            (int variables, int globals, int pinned, int expanded) =
                context.Variables.GetStatistics();

            context.WriteLine($"Variables: {variables} defined", theme.Info);
            context.WriteLine($"  Globals: {globals}", theme.TextSecondary);
            if (pinned > 0)
            {
                context.WriteLine($"  Pinned: {pinned}", theme.Warning);
            }

            if (expanded > 0)
            {
                context.WriteLine($"  Expanded: {expanded}", theme.TextSecondary);
            }

            context.WriteLine("", theme.TextPrimary);
            context.WriteLine("Use 'tab variables' to view the Variables panel", theme.TextDim);
            return Task.CompletedTask;
        }

        string subCommand = args[0].ToLowerInvariant();

        switch (subCommand)
        {
            case "list":
                ListVariables(context);
                break;

            case "search":
                if (args.Length < 2)
                {
                    context.WriteLine("Usage: var search <text>", theme.Warning);
                    return Task.CompletedTask;
                }

                string searchText = string.Join(" ", args.Skip(1));
                context.Variables.SetSearchFilter(searchText);
                context.WriteLine($"Filtering variables by: \"{searchText}\"", theme.Success);
                break;

            case "clear-search":
                context.Variables.ClearSearchFilter();
                context.WriteLine("Search filter cleared", theme.Success);
                break;

            case "expand":
                if (args.Length < 2)
                {
                    context.WriteLine("Usage: var expand <name>", theme.Warning);
                    return Task.CompletedTask;
                }

                context.Variables.Expand(args[1]);
                context.WriteLine($"Expanded: {args[1]}", theme.Success);
                break;

            case "collapse":
                if (args.Length < 2)
                {
                    context.WriteLine("Usage: var collapse <name>", theme.Warning);
                    return Task.CompletedTask;
                }

                context.Variables.Collapse(args[1]);
                context.WriteLine($"Collapsed: {args[1]}", theme.Success);
                break;

            case "expand-all":
                context.Variables.ExpandAll();
                context.WriteLine("All variables expanded", theme.Success);
                break;

            case "collapse-all":
                context.Variables.CollapseAll();
                context.WriteLine("All variables collapsed", theme.Success);
                break;

            case "pin":
                if (args.Length < 2)
                {
                    context.WriteLine("Usage: var pin <name>", theme.Warning);
                    return Task.CompletedTask;
                }

                context.Variables.Pin(args[1]);
                context.WriteLine($"Pinned: {args[1]}", theme.Success);
                break;

            case "unpin":
                if (args.Length < 2)
                {
                    context.WriteLine("Usage: var unpin <name>", theme.Warning);
                    return Task.CompletedTask;
                }

                context.Variables.Unpin(args[1]);
                context.WriteLine($"Unpinned: {args[1]}", theme.Success);
                break;

            case "clear":
                context.Variables.Clear();
                context.WriteLine("All user-defined variables cleared", theme.Success);
                break;

            default:
                context.WriteLine($"Unknown subcommand: {subCommand}", theme.Error);
                context.WriteLine("Use 'help var' for available subcommands", theme.TextDim);
                break;
        }

        return Task.CompletedTask;
    }

    private void ListVariables(IConsoleContext context)
    {
        UITheme theme = context.Theme;
        var names = context.Variables.GetNames().ToList();

        if (names.Count == 0)
        {
            context.WriteLine("No variables defined.", theme.TextDim);
            context.WriteLine(
                "Use expressions like 'var x = 42;' to create variables.",
                theme.TextDim
            );
            return;
        }

        context.WriteLine($"Variables ({names.Count}):", theme.Info);
        foreach (string name in names.OrderBy(n => n))
        {
            object? value = context.Variables.GetValue(name);
            string valueStr = value?.ToString() ?? "null";
            if (valueStr.Length > 40)
            {
                valueStr = valueStr.Substring(0, 37) + "...";
            }

            context.WriteLine($"  {name}: {valueStr}", theme.TextPrimary);
        }
    }
}
