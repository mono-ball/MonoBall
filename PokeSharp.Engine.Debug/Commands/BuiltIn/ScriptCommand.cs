using PokeSharp.Engine.UI.Debug.Core;

namespace PokeSharp.Engine.Debug.Commands.BuiltIn;

/// <summary>
///     Manages script files and script evaluator state.
/// </summary>
[ConsoleCommand("script", "Manage scripts")]
public class ScriptCommand : IConsoleCommand
{
    public string Name => "script";
    public string Description => "Manage scripts";

    public string Usage =>
        @"script                     - List all scripts
script list                - List all scripts
script load <filename>     - Load and execute script
script save <name> <code>  - Save script to file
script reset               - Reset script state

Examples:
  script                   (list all)
  script load example
  script save test Print('Hello')
  script reset";

    public async Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        UITheme theme = context.Theme;

        // No args = list scripts
        if (args.Length == 0)
        {
            ListScripts(context);
            return;
        }

        string subCommand = args[0].ToLower();

        switch (subCommand)
        {
            case "list":
                ListScripts(context);
                break;

            case "load":
                await LoadScript(context, args.Skip(1).ToArray());
                break;

            case "save":
                SaveScript(context, args.Skip(1).ToArray());
                break;

            case "reset":
                ResetScript(context);
                break;

            default:
                context.WriteLine($"Unknown script subcommand: '{subCommand}'", theme.Error);
                context.WriteLine("", theme.TextPrimary);
                context.WriteLine(Usage, theme.TextSecondary);
                break;
        }
    }

    private void ListScripts(IConsoleContext context)
    {
        UITheme theme = context.Theme;
        List<string> scripts = context.ListScripts();

        if (scripts.Count == 0)
        {
            context.WriteLine("No scripts found in Scripts directory", theme.TextSecondary);
            context.WriteLine("", theme.TextPrimary);
            context.WriteLine("Scripts are stored in:", theme.Info);
            context.WriteLine($"  {context.GetScriptsDirectory()}", theme.TextSecondary);
            context.WriteLine("", theme.TextPrimary);
            context.WriteLine(
                "Create a script with: script save <filename> <code>",
                theme.TextSecondary
            );
            return;
        }

        context.WriteLine(
            "══════════════════════════════════════════════════════════════════",
            theme.Success
        );
        context.WriteLine($"  AVAILABLE SCRIPTS ({scripts.Count} total)", theme.Success);
        context.WriteLine(
            "══════════════════════════════════════════════════════════════════",
            theme.Success
        );
        context.WriteLine("Location:", theme.Info);
        context.WriteLine($"  {context.GetScriptsDirectory()}", theme.TextSecondary);
        context.WriteLine("");

        // Group scripts by type for better organization
        var exampleScripts = scripts
            .Where(s => s.StartsWith("example", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var userScripts = scripts.Except(exampleScripts).ToList();

        if (exampleScripts.Any())
        {
            context.WriteLine("  EXAMPLE SCRIPTS:", theme.BorderFocus);
            foreach (string script in exampleScripts)
            {
                string name = Path.GetFileNameWithoutExtension(script);
                context.WriteLine($"    {script, -30} script load {name}", theme.Info);
            }

            context.WriteLine("");
        }

        if (userScripts.Any())
        {
            context.WriteLine("  USER SCRIPTS:", theme.BorderFocus);
            foreach (string script in userScripts)
            {
                string name = Path.GetFileNameWithoutExtension(script);
                context.WriteLine($"    {script, -30} script load {name}", theme.TextPrimary);
            }

            context.WriteLine("");
        }

        context.WriteLine("TIP: Use 'script load <filename>' to execute a script", theme.Success);
    }

    private async Task LoadScript(IConsoleContext context, string[] args)
    {
        UITheme theme = context.Theme;

        if (args.Length == 0)
        {
            context.WriteLine("Usage: script load <filename>", theme.Warning);
            context.WriteLine("Example: script load example", theme.TextSecondary);
            context.WriteLine("", theme.TextPrimary);
            context.WriteLine("Use 'script' to see all available scripts", theme.TextSecondary);
            return;
        }

        string filename = args[0];

        // Load the script content
        string? scriptContent = context.LoadScript(filename);
        if (scriptContent == null)
        {
            context.WriteLine($"Failed to load script: {filename}", theme.Error);
            context.WriteLine("", theme.TextPrimary);
            context.WriteLine("Use 'script' to see all available scripts", theme.TextSecondary);
            return;
        }

        // Show what we're executing
        context.WriteLine($"Executing script: {filename}.csx", theme.Info);
        context.WriteLine("", theme.TextPrimary);

        // Execute the script using the evaluator
        await context.ExecuteScriptAsync(scriptContent);
    }

    private void SaveScript(IConsoleContext context, string[] args)
    {
        UITheme theme = context.Theme;

        if (args.Length < 2)
        {
            context.WriteLine("Usage: script save <filename> <content>", theme.Warning);
            context.WriteLine("Example: script save test Player.GetMoney()", theme.TextSecondary);
            context.WriteLine("", theme.TextPrimary);
            context.WriteLine("Note: For complex multi-line scripts, it's better to:", theme.Info);
            context.WriteLine(
                "  1. Create a .csx file in the Scripts directory",
                theme.TextSecondary
            );
            context.WriteLine("  2. Edit it in your favorite editor", theme.TextSecondary);
            context.WriteLine("  3. Load it with: script load <filename>", theme.TextSecondary);
            return;
        }

        string filename = args[0];
        string content = string.Join(" ", args.Skip(1));

        // Save the script
        if (context.SaveScript(filename, content))
        {
            context.WriteLine($"Script saved: {filename}.csx", theme.Success);
            context.WriteLine("", theme.TextPrimary);
            context.WriteLine($"Load it with: script load {filename}", theme.Info);
        }
        else
        {
            context.WriteLine($"Failed to save script: {filename}", theme.Error);
        }
    }

    private void ResetScript(IConsoleContext context)
    {
        UITheme theme = context.Theme;

        context.ResetScriptState();
        context.WriteLine("Script state reset - all variables cleared", theme.Success);
        context.WriteLine("", theme.TextPrimary);
        context.WriteLine(
            "Note: Built-in globals (Player, Api, World, etc.) remain available",
            theme.Info
        );
    }
}
