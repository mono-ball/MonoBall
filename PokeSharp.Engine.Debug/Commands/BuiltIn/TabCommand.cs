using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Core;
using Tabs = PokeSharp.Engine.UI.Debug.Core.ConsoleTabs;

namespace PokeSharp.Engine.Debug.Commands.BuiltIn;

/// <summary>
///     Command for switching between console tabs.
/// </summary>
[ConsoleCommand("tab", "Switch between console tabs")]
public class TabCommand : IConsoleCommand
{
    public string Name => "tab";
    public string Description => "Switch between console tabs";

    public string Usage =>
        $@"tab [name|index]

Switches to the specified tab by name or index.

Arguments:
  name    Tab name: {string.Join(", ", Tabs.All.Select(t => t.Name.ToLowerInvariant()))}
  index   Tab index: 0-{Tabs.Count - 1}

Examples:
  tab console     Switch to Console tab
  tab watch       Switch to Watch tab
  tab logs        Switch to Logs tab
  tab variables   Switch to Variables tab
  tab entities    Switch to Entities tab
  tab 0           Switch to Console tab (by index)
  tab             Show current tab and list all tabs

Keyboard Shortcuts:
  Ctrl+1-{Tabs.Count}      Switch tabs directly (Ctrl+1 = Console, etc.)";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        UITheme theme = context.Theme;

        if (args.Length == 0)
        {
            // Show current tab and list all tabs
            int currentTab = context.GetActiveTab();
            context.WriteLine("Console Tabs:", theme.Info);
            context.WriteLine("─────────────────────────────", theme.BorderPrimary);

            foreach (ConsoleTabs.TabDefinition tabDef in Tabs.All)
            {
                bool isActive = tabDef.Index == currentTab;
                string indicator = isActive ? " → " : "   ";
                string status = isActive ? "(active)" : "";
                string shortcut = tabDef.Shortcut.HasValue ? $"[Ctrl+{tabDef.Index + 1}]" : "";
                Color color = isActive ? theme.Success : theme.TextSecondary;
                context.WriteLine(
                    $"{indicator}{tabDef.Index}. {tabDef.Name} {status} {shortcut}",
                    color
                );
            }

            context.WriteLine("", theme.TextPrimary);
            context.WriteLine("Use 'tab <name>' or 'tab <index>' to switch", theme.TextDim);
            return Task.CompletedTask;
        }

        string target = args[0];

        if (Tabs.TryGet(target, out ConsoleTabs.TabDefinition? matchedTab) && matchedTab != null)
        {
            context.SwitchToTab(matchedTab.Index);
            context.WriteLine($"Switched to {matchedTab.Name} tab", theme.Success);
        }
        else
        {
            context.WriteLine($"Unknown tab: '{target}'", theme.Error);
            string validNames = string.Join(", ", Tabs.All.Select(t => t.Name.ToLowerInvariant()));
            context.WriteLine($"Valid tabs: {validNames} (or 0-{Tabs.Count - 1})", theme.TextDim);
        }

        return Task.CompletedTask;
    }
}
