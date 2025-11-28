using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Core;

namespace PokeSharp.Engine.Debug.Commands.BuiltIn;

[ConsoleCommand("watch", "Manage watch expressions for real-time monitoring")]
public class WatchCommand : IConsoleCommand
{
    public string Name => "watch";
    public string Description => "Manage watch expressions for real-time monitoring";

    public string Usage =>
        @"watch [list | add | remove | clear | toggle | pin | unpin | interval | group | alert | compare | preset]
  list: List all watches
  add <name> <expression> [--group <name>] [--when <condition>]: Add watch
  remove <name>: Remove a watch
  clear: Clear all watches
  toggle: Toggle auto-update on/off
  pin <name>: Pin watch to top
  unpin <name>: Unpin watch
  interval <ms>: Set update interval (100-60000ms)
  group list: List all groups
  group collapse <name>: Collapse group
  group expand <name>: Expand group
  group toggle <name>: Toggle group
  alert list: List all watches with alerts
  alert set <name> <type> <threshold>: Set alert (types: above, below, equals, changes)
  alert remove <name>: Remove alert
  alert clear <name>: Clear triggered alert status
  compare list: List all watches with comparisons
  compare set <watch1> <watch2> [label]: Compare watch1 to watch2
  compare remove <name>: Remove comparison
  preset list: List all available presets
  preset save <name> <description>: Save current configuration as preset
  preset load <name>: Load preset configuration
  preset delete <name>: Delete a preset
  preset builtin: Create built-in presets

Use 'tab watch' to switch to the Watch tab.

Examples:
  watch add money Player.GetMoney() --group player
  watch add hp Player.GetHP() --group player --when ""Game.InBattle()""
  watch alert set hp below 20
  watch alert set money changes
  watch group collapse player
  watch compare set actual_hp expected_hp Expected
  watch compare set player_pos target_pos Target
  watch preset save my_config My custom watch configuration
  watch preset load performance";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        UITheme theme = context.Theme;

        if (args.Length == 0 || args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            // Show watch count and summary
            int watchCount = context.Watches.Count;
            var groups = context.Watches.GetGroups().ToList();
            (int total, int pinned, int errors, int alertsTriggered, int groupCount) =
                context.Watches.GetStatistics();

            context.WriteLine($"Watches: {watchCount} total", theme.Info);
            if (pinned > 0)
            {
                context.WriteLine($"  Pinned: {pinned}", theme.Success);
            }

            if (errors > 0)
            {
                context.WriteLine($"  Errors: {errors}", theme.Error);
            }

            if (alertsTriggered > 0)
            {
                context.WriteLine($"  Alerts triggered: {alertsTriggered}", theme.Warning);
            }

            if (groupCount > 0)
            {
                context.WriteLine($"  Groups: {groupCount}", theme.TextSecondary);
            }

            context.WriteLine("", theme.TextPrimary);
            context.WriteLine("Use 'tab watch' to view the Watch panel", theme.TextDim);
        }
        else if (args[0].Equals("add", StringComparison.OrdinalIgnoreCase))
        {
            // Parse watch add command
            if (args.Length < 3)
            {
                context.WriteLine(
                    "Usage: watch add <name> <expression> [--group <name>] [--when <condition>]",
                    theme.Warning
                );
                context.WriteLine(
                    "Example: watch add hp Player.GetHP() --group player --when \"Game.InBattle()\"",
                    theme.TextSecondary
                );
                return Task.CompletedTask;
            }

            string name = args[1];

            // Find flags
            int groupIndex = Array.IndexOf(args, "--group");
            int whenIndex = Array.IndexOf(args, "--when");

            // Determine expression end index
            int expressionEndIndex = args.Length;
            if (groupIndex > 0)
            {
                expressionEndIndex = Math.Min(expressionEndIndex, groupIndex);
            }

            if (whenIndex > 0)
            {
                expressionEndIndex = Math.Min(expressionEndIndex, whenIndex);
            }

            // Extract expression
            string expression = string.Join(" ", args.Skip(2).Take(expressionEndIndex - 2));

            // Extract group
            string? group = null;
            if (groupIndex > 0 && groupIndex + 1 < args.Length)
            {
                int groupEndIndex = whenIndex > groupIndex ? whenIndex : args.Length;
                group = string.Join(
                    " ",
                    args.Skip(groupIndex + 1).Take(groupEndIndex - (groupIndex + 1))
                );
            }

            // Extract condition
            string? condition = null;
            if (whenIndex > 0 && whenIndex + 1 < args.Length)
            {
                int condEndIndex = groupIndex > whenIndex ? groupIndex : args.Length;
                condition = string.Join(
                    " ",
                    args.Skip(whenIndex + 1).Take(condEndIndex - (whenIndex + 1))
                );
                // Remove quotes if present
                condition = condition.Trim('"', '\'');
            }

            if (context.AddWatch(name, expression, group, condition))
            {
                string groupInfo = !string.IsNullOrEmpty(group) ? $" [group: {group}]" : "";
                string condInfo = !string.IsNullOrEmpty(condition) ? " [conditional]" : "";
                context.WriteLine(
                    $"Watch '{name}' added: {expression}{groupInfo}{condInfo}",
                    theme.Success
                );
                context.WriteLine(
                    "Switch to Watch tab (Ctrl+2) to view results",
                    theme.TextSecondary
                );
            }
            else
            {
                context.WriteLine($"Failed to add watch '{name}'", theme.Error);
                context.WriteLine(
                    "Watch limit reached (50 max) or invalid expression",
                    theme.Error
                );
            }
        }
        else if (args[0].Equals("remove", StringComparison.OrdinalIgnoreCase))
        {
            // Remove watch
            if (args.Length < 2)
            {
                context.WriteLine("Usage: watch remove <name>", theme.Warning);
                return Task.CompletedTask;
            }

            string name = args[1];
            if (context.Watches.Remove(name))
            {
                context.WriteLine($"Watch '{name}' removed", theme.Success);
            }
            else
            {
                context.WriteLine($"Watch '{name}' not found", theme.Error);
            }
        }
        else if (args[0].Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            context.Watches.Clear();
            context.WriteLine("All watches cleared", theme.Success);
        }
        else if (args[0].Equals("toggle", StringComparison.OrdinalIgnoreCase))
        {
            bool autoUpdate = context.Watches.AutoUpdate = !context.Watches.AutoUpdate;
            string status = autoUpdate ? "enabled" : "disabled";
            context.WriteLine($"Watch auto-update {status}", theme.Success);
        }
        else if (args[0].Equals("pin", StringComparison.OrdinalIgnoreCase))
        {
            // Pin watch
            if (args.Length < 2)
            {
                context.WriteLine("Usage: watch pin <name>", theme.Warning);
                return Task.CompletedTask;
            }

            string name = args[1];
            if (context.Watches.Pin(name))
            {
                context.WriteLine($"Watch '{name}' pinned to top", theme.Success);
            }
            else
            {
                context.WriteLine($"Watch '{name}' not found", theme.Error);
            }
        }
        else if (args[0].Equals("unpin", StringComparison.OrdinalIgnoreCase))
        {
            // Unpin watch
            if (args.Length < 2)
            {
                context.WriteLine("Usage: watch unpin <name>", theme.Warning);
                return Task.CompletedTask;
            }

            string name = args[1];
            if (context.Watches.Unpin(name))
            {
                context.WriteLine($"Watch '{name}' unpinned", theme.Success);
            }
            else
            {
                context.WriteLine($"Watch '{name}' not found", theme.Error);
            }
        }
        else if (args[0].Equals("interval", StringComparison.OrdinalIgnoreCase))
        {
            // Set update interval
            if (args.Length < 2)
            {
                context.WriteLine("Usage: watch interval <milliseconds>", theme.Warning);
                context.WriteLine("Valid range: 100ms - 60000ms (1 minute)", theme.TextSecondary);
                return Task.CompletedTask;
            }

            if (double.TryParse(args[1], out double milliseconds))
            {
                double seconds = milliseconds / 1000.0;
                if (seconds >= 0.1 && seconds <= 60.0)
                {
                    context.Watches.UpdateInterval = seconds;
                    string display = seconds < 1.0 ? $"{milliseconds:F0}ms" : $"{seconds:F1}s";
                    context.WriteLine($"Watch update interval set to {display}", theme.Success);
                }
                else
                {
                    context.WriteLine(
                        "Invalid interval. Must be between 100ms and 60000ms",
                        theme.Error
                    );
                }
            }
            else
            {
                context.WriteLine($"Invalid number: '{args[1]}'", theme.Error);
            }
        }
        else if (args[0].Equals("group", StringComparison.OrdinalIgnoreCase))
        {
            // Group management
            if (args.Length < 2)
            {
                context.WriteLine(
                    "Usage: watch group [list | collapse <name> | expand <name> | toggle <name>]",
                    theme.Warning
                );
                return Task.CompletedTask;
            }

            string subCommand = args[1].ToLower();
            if (subCommand == "list")
            {
                var groups = context.Watches.GetGroups().ToList();
                context.WriteLine(
                    "══════════════════════════════════════════════════════════════════",
                    theme.Success
                );
                context.WriteLine($"  WATCH GROUPS ({groups.Count} total)", theme.Success);
                context.WriteLine(
                    "══════════════════════════════════════════════════════════════════",
                    theme.Success
                );

                if (groups.Count == 0)
                {
                    context.WriteLine("  No groups defined.", theme.TextSecondary);
                }
                else
                {
                    foreach (string group in groups)
                    {
                        context.WriteLine($"  - {group}", theme.TextPrimary);
                    }
                }

                context.WriteLine("", theme.TextSecondary);
                context.WriteLine(
                    "TIP: Use 'watch group collapse <name>' to collapse a group",
                    theme.Success
                );
            }
            else if (subCommand == "collapse" && args.Length > 2)
            {
                string groupName = args[2];
                if (context.Watches.CollapseGroup(groupName))
                {
                    context.WriteLine($"Group '{groupName}' collapsed", theme.Success);
                }
                else
                {
                    context.WriteLine($"Group '{groupName}' not found", theme.Error);
                }
            }
            else if (subCommand == "expand" && args.Length > 2)
            {
                string groupName = args[2];
                if (context.Watches.ExpandGroup(groupName))
                {
                    context.WriteLine($"Group '{groupName}' expanded", theme.Success);
                }
                else
                {
                    context.WriteLine($"Group '{groupName}' not found", theme.Error);
                }
            }
            else if (subCommand == "toggle" && args.Length > 2)
            {
                string groupName = args[2];
                if (context.Watches.ToggleGroup(groupName))
                {
                    context.WriteLine($"Group '{groupName}' toggled", theme.Success);
                }
                else
                {
                    context.WriteLine($"Group '{groupName}' not found", theme.Error);
                }
            }
            else
            {
                context.WriteLine(
                    "Usage: watch group [list | collapse <name> | expand <name> | toggle <name>]",
                    theme.Warning
                );
            }
        }
        else if (args[0].Equals("alert", StringComparison.OrdinalIgnoreCase))
        {
            // Alert management
            if (args.Length < 2)
            {
                context.WriteLine(
                    "Usage: watch alert [list | set <name> <type> <threshold> | remove <name> | clear <name>]",
                    theme.Warning
                );
                return Task.CompletedTask;
            }

            string subCommand = args[1].ToLower();
            if (subCommand == "list")
            {
                var alertWatches = context.Watches.GetWatchesWithAlerts().ToList();
                context.WriteLine(
                    "══════════════════════════════════════════════════════════════════",
                    theme.Success
                );
                context.WriteLine($"  WATCH ALERTS ({alertWatches.Count} total)", theme.Success);
                context.WriteLine(
                    "══════════════════════════════════════════════════════════════════",
                    theme.Success
                );

                if (alertWatches.Count == 0)
                {
                    context.WriteLine("  No alerts configured.", theme.TextSecondary);
                }
                else
                {
                    foreach ((string name, string alertType, bool triggered) in alertWatches)
                    {
                        string status = triggered ? "[TRIGGERED]" : "[watching]";
                        Color statusColor = triggered ? theme.Error : theme.TextDim;
                        context.WriteLine($"  - {name}: {alertType} {status}", statusColor);
                    }
                }

                context.WriteLine("", theme.TextSecondary);
                context.WriteLine(
                    "TIP: Use 'watch alert clear <name>' to clear triggered status",
                    theme.Success
                );
            }
            else if (subCommand == "set" && args.Length >= 4)
            {
                string name = args[2];
                string alertType = args[3].ToLower();

                // Validate alert type
                if (
                    alertType != "above"
                    && alertType != "below"
                    && alertType != "equals"
                    && alertType != "changes"
                )
                {
                    context.WriteLine($"Invalid alert type: '{alertType}'", theme.Error);
                    context.WriteLine(
                        "Valid types: above, below, equals, changes",
                        theme.TextSecondary
                    );
                    return Task.CompletedTask;
                }

                object? threshold = null;

                // For above/below/equals, we need a threshold
                if (alertType != "changes")
                {
                    if (args.Length < 5)
                    {
                        context.WriteLine(
                            $"Alert type '{alertType}' requires a threshold value",
                            theme.Warning
                        );
                        context.WriteLine(
                            "Usage: watch alert set <name> <type> <threshold>",
                            theme.TextSecondary
                        );
                        return Task.CompletedTask;
                    }

                    // Try to parse threshold as number, otherwise use as string
                    string thresholdStr = args[4];
                    if (double.TryParse(thresholdStr, out double numThreshold))
                    {
                        threshold = numThreshold;
                    }
                    else
                    {
                        threshold = thresholdStr;
                    }
                }

                if (context.Watches.SetAlert(name, alertType, threshold))
                {
                    string thresholdInfo = threshold != null ? $" (threshold: {threshold})" : "";
                    context.WriteLine(
                        $"Alert set on '{name}': {alertType}{thresholdInfo}",
                        theme.Success
                    );
                }
                else
                {
                    context.WriteLine($"Watch '{name}' not found", theme.Error);
                }
            }
            else if (subCommand == "remove" && args.Length > 2)
            {
                string name = args[2];
                if (context.Watches.RemoveAlert(name))
                {
                    context.WriteLine($"Alert removed from '{name}'", theme.Success);
                }
                else
                {
                    context.WriteLine($"Watch '{name}' not found", theme.Error);
                }
            }
            else if (subCommand == "clear" && args.Length > 2)
            {
                string name = args[2];
                if (context.Watches.ClearAlertStatus(name))
                {
                    context.WriteLine($"Alert status cleared for '{name}'", theme.Success);
                }
                else
                {
                    context.WriteLine($"Watch '{name}' not found", theme.Error);
                }
            }
            else
            {
                context.WriteLine(
                    "Usage: watch alert [list | set <name> <type> <threshold> | remove <name> | clear <name>]",
                    theme.Warning
                );
            }
        }
        else if (args[0].Equals("compare", StringComparison.OrdinalIgnoreCase))
        {
            // Comparison management
            if (args.Length < 2)
            {
                context.WriteLine(
                    "Usage: watch compare [list | set <watch1> <watch2> [label] | remove <name>]",
                    theme.Warning
                );
                return Task.CompletedTask;
            }

            string subCommand = args[1].ToLower();
            if (subCommand == "list")
            {
                var comparisons = context.Watches.GetWatchesWithComparisons().ToList();
                context.WriteLine(
                    "══════════════════════════════════════════════════════════════════",
                    theme.Success
                );
                context.WriteLine(
                    $"  WATCH COMPARISONS ({comparisons.Count} total)",
                    theme.Success
                );
                context.WriteLine(
                    "══════════════════════════════════════════════════════════════════",
                    theme.Success
                );

                if (comparisons.Count == 0)
                {
                    context.WriteLine("  No comparisons configured.", theme.TextSecondary);
                }
                else
                {
                    foreach ((string name, string comparedWith) in comparisons)
                    {
                        context.WriteLine(
                            $"  - {name} compared to {comparedWith}",
                            theme.TextPrimary
                        );
                    }
                }

                context.WriteLine("", theme.TextSecondary);
                context.WriteLine(
                    "TIP: Switch to Watch tab to see comparison details",
                    theme.Success
                );
            }
            else if (subCommand == "set" && args.Length >= 4)
            {
                string watch1 = args[2];
                string watch2 = args[3];
                string label = args.Length > 4 ? args[4] : "Expected";

                if (context.Watches.SetComparison(watch1, watch2, label))
                {
                    context.WriteLine(
                        $"Comparison set: '{watch1}' compared to '{watch2}' ({label})",
                        theme.Success
                    );
                    context.WriteLine(
                        "Switch to Watch tab to see the difference calculation",
                        theme.TextSecondary
                    );
                }
                else
                {
                    context.WriteLine(
                        "Failed to set comparison. Make sure both watches exist.",
                        theme.Error
                    );
                }
            }
            else if (subCommand == "remove" && args.Length > 2)
            {
                string name = args[2];
                if (context.Watches.RemoveComparison(name))
                {
                    context.WriteLine($"Comparison removed from '{name}'", theme.Success);
                }
                else
                {
                    context.WriteLine($"Watch '{name}' not found", theme.Error);
                }
            }
            else
            {
                context.WriteLine(
                    "Usage: watch compare [list | set <watch1> <watch2> [label] | remove <name>]",
                    theme.Warning
                );
            }
        }
        else if (args[0].Equals("preset", StringComparison.OrdinalIgnoreCase))
        {
            // Preset management
            if (args.Length < 2)
            {
                context.WriteLine(
                    "Usage: watch preset [list | save <name> <description> | load <name> | delete <name> | builtin]",
                    theme.Warning
                );
                return Task.CompletedTask;
            }

            string subCommand = args[1].ToLower();
            if (subCommand == "list")
            {
                var presets = context.ListWatchPresets().ToList();
                context.WriteLine(
                    "══════════════════════════════════════════════════════════════════",
                    theme.Success
                );
                context.WriteLine($"  WATCH PRESETS ({presets.Count} total)", theme.Success);
                context.WriteLine(
                    "══════════════════════════════════════════════════════════════════",
                    theme.Success
                );

                if (presets.Count == 0)
                {
                    context.WriteLine("  No presets available.", theme.TextSecondary);
                    context.WriteLine(
                        "  Use 'watch preset save <name> <description>' to save current configuration",
                        theme.TextSecondary
                    );
                    context.WriteLine(
                        "  Use 'watch preset builtin' to create built-in presets",
                        theme.TextSecondary
                    );
                }
                else
                {
                    foreach (
                        (
                            string name,
                            string description,
                            int watchCount,
                            DateTime createdAt
                        ) in presets
                    )
                    {
                        string date = createdAt.ToString("yyyy-MM-dd HH:mm");
                        context.WriteLine(
                            $"  - {name.PadRight(20)} ({watchCount} watches) - {description}",
                            theme.TextPrimary
                        );
                        context.WriteLine($"    Created: {date}", theme.TextDim);
                    }
                }

                context.WriteLine("", theme.TextSecondary);
                context.WriteLine(
                    "TIP: Use 'watch preset load <name>' to load a preset",
                    theme.Success
                );
            }
            else if (subCommand == "save" && args.Length >= 3)
            {
                string name = args[2];
                string description =
                    args.Length > 3 ? string.Join(" ", args.Skip(3)) : "Custom watch configuration";

                if (context.SaveWatchPreset(name, description))
                {
                    int watchCount = context.Watches.Count;
                    context.WriteLine(
                        $"Preset '{name}' saved ({watchCount} watches)",
                        theme.Success
                    );
                    context.WriteLine(
                        $"Load it later with: watch preset load {name}",
                        theme.TextSecondary
                    );
                }
                else
                {
                    context.WriteLine($"Failed to save preset '{name}'", theme.Error);
                }
            }
            else if (subCommand == "load" && args.Length > 2)
            {
                string name = args[2];

                if (context.LoadWatchPreset(name))
                {
                    context.WriteLine($"Preset '{name}' loaded successfully", theme.Success);
                    context.WriteLine("Use 'tab watch' to view", theme.TextDim);
                }
                else
                {
                    context.WriteLine($"Failed to load preset '{name}'", theme.Error);
                }
            }
            else if (subCommand == "delete" && args.Length > 2)
            {
                string name = args[2];

                if (context.DeleteWatchPreset(name))
                {
                    context.WriteLine($"Preset '{name}' deleted", theme.Success);
                }
                else
                {
                    context.WriteLine($"Preset '{name}' not found", theme.Error);
                }
            }
            else if (subCommand == "builtin")
            {
                context.CreateBuiltInWatchPresets();
                context.WriteLine("Built-in presets created:", theme.Success);
                context.WriteLine(
                    "  - performance: Monitor FPS, frame time, memory",
                    theme.TextPrimary
                );
                context.WriteLine("  - combat: Monitor battle system values", theme.TextPrimary);
                context.WriteLine(
                    "  - player_stats: Monitor player position, money, map",
                    theme.TextPrimary
                );
                context.WriteLine("  - memory: Monitor GC and memory metrics", theme.TextPrimary);
                context.WriteLine("", theme.TextSecondary);
                context.WriteLine("Use 'watch preset load <name>' to load a preset", theme.Success);
            }
            else
            {
                context.WriteLine(
                    "Usage: watch preset [list | save <name> <description> | load <name> | delete <name> | builtin]",
                    theme.Warning
                );
            }
        }
        else
        {
            context.WriteLine($"Unknown watch subcommand: '{args[0]}'", theme.Error);
            context.WriteLine(Usage, theme.TextSecondary);
        }

        return Task.CompletedTask;
    }
}
