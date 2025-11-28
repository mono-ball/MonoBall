using PokeSharp.Engine.UI.Debug.Core;

namespace PokeSharp.Engine.Debug.Commands.BuiltIn;

/// <summary>
///     Manages command aliases and macros.
/// </summary>
[ConsoleCommand("alias", "Manage command aliases")]
public class AliasCommand : IConsoleCommand
{
    public string Name => "alias";
    public string Description => "Manage command aliases";

    public string Usage =>
        @"alias                      - List all aliases
alias <name>=<command>     - Create new alias
alias remove <name>        - Remove an alias

Examples:
  alias                    (list all)
  alias gm=Player.GiveMoney($1)
  alias remove gm";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        UITheme theme = context.Theme;

        // No args = list aliases
        if (args.Length == 0)
        {
            ListAliases(context);
            return Task.CompletedTask;
        }

        // Check for "remove" subcommand
        if (args[0].Equals("remove", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 2)
            {
                context.WriteLine("Usage: alias remove <name>", theme.Warning);
                context.WriteLine("Example: alias remove gm", theme.TextSecondary);
                return Task.CompletedTask;
            }

            string name = args[1].Trim();
            if (context.RemoveAlias(name))
            {
                context.WriteLine($"Alias '{name}' removed", theme.Success);
            }
            else
            {
                context.WriteLine($"Alias '{name}' not found.", theme.Error);
            }

            return Task.CompletedTask;
        }

        // Otherwise, treat as alias definition
        string fullAlias = string.Join(" ", args);
        string[] parts = fullAlias.Split('=', 2);

        if (parts.Length != 2)
        {
            context.WriteLine("Invalid alias format. Use: alias <name>=<command>", theme.Error);
            context.WriteLine("", theme.TextPrimary);
            context.WriteLine(Usage, theme.TextSecondary);
            return Task.CompletedTask;
        }

        string aliasName = parts[0].Trim();
        string command = parts[1].Trim();

        if (context.DefineAlias(aliasName, command))
        {
            context.WriteLine($"Alias defined: {aliasName} = {command}", theme.Success);
        }
        else
        {
            context.WriteLine(
                $"Failed to define alias '{aliasName}'. Invalid name or command.",
                theme.Error
            );
        }

        return Task.CompletedTask;
    }

    private void ListAliases(IConsoleContext context)
    {
        UITheme theme = context.Theme;
        IReadOnlyDictionary<string, string> aliases = context.GetAllAliases();

        context.WriteLine(
            "══════════════════════════════════════════════════════════════════",
            theme.Success
        );
        context.WriteLine($"  DEFINED ALIASES ({aliases.Count} total)", theme.Success);
        context.WriteLine(
            "══════════════════════════════════════════════════════════════════",
            theme.Success
        );

        if (aliases.Count == 0)
        {
            context.WriteLine(
                "  No aliases defined. Use 'alias <name>=<command>' to create one.",
                theme.TextSecondary
            );
        }
        else
        {
            foreach (KeyValuePair<string, string> alias in aliases.OrderBy(a => a.Key))
            {
                string aliasName = alias.Key.PadRight(15);
                bool isMacro = alias.Value.Contains("$");
                string macroHint = isMacro ? " (macro)" : "";
                context.WriteLine(
                    $"  {aliasName} = {alias.Value}{macroHint}",
                    isMacro ? theme.SyntaxString : theme.TextPrimary
                );
            }
        }

        context.WriteLine("");
        context.WriteLine(
            "TIP: Use 'alias <name>=<command>' to create, 'alias remove <name>' to delete",
            theme.Success
        );
    }
}
