using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Debug.Breakpoints;

namespace PokeSharp.Engine.Debug.Commands.BuiltIn;

/// <summary>
/// Command to manage breakpoints that pause the game when conditions are met.
/// </summary>
[ConsoleCommand("break", "Manage breakpoints that pause the game")]
public class BreakpointCommand : IConsoleCommand
{
    public string Name => "break";
    public string Description => "Manage breakpoints that pause the game when conditions are met";
    public string Usage => @"break                           - List all breakpoints
break when <expression>         - Pause when C# expression becomes true
break log <level>               - Pause on log level (error, warning, info)
break watch <name>              - Pause when watch alert triggers
break enable <id>               - Enable a breakpoint
break disable <id>              - Disable a breakpoint
break delete <id>               - Delete a breakpoint
break clear                     - Delete all breakpoints
break toggle                    - Toggle breakpoint evaluation on/off

Examples:
  break when Player.GetMoney() < 100     Pause when low on money
  break when CountEntities() > 50        Pause when many entities
  break when GetPlayer() == null         Pause if player missing
  break log error                        Pause on any error log
  break watch playerMoney                Pause when watch alert fires

Available globals: Player, World, Systems, Map, GameState, etc.
Use 'Help()' in console for full API reference.";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        var theme = context.Theme;
        var breakpoints = context.Breakpoints;

        if (breakpoints == null)
        {
            context.WriteLine("⚠ Breakpoints not available.", theme.Warning);
            context.WriteLine("BreakpointManager was not initialized.", theme.TextSecondary);
            return Task.CompletedTask;
        }

        if (args.Length == 0)
        {
            ShowBreakpointList(context, breakpoints);
            return Task.CompletedTask;
        }

        var subCommand = args[0].ToLower();

        switch (subCommand)
        {
            case "when":
                HandleWhen(context, args, breakpoints);
                break;

            case "log":
                HandleLog(context, args, breakpoints);
                break;

            case "watch":
                HandleWatch(context, args, breakpoints);
                break;

            case "enable":
                HandleEnable(context, args, breakpoints);
                break;

            case "disable":
                HandleDisable(context, args, breakpoints);
                break;

            case "delete":
            case "remove":
            case "del":
                HandleDelete(context, args, breakpoints);
                break;

            case "clear":
                HandleClear(context, breakpoints);
                break;

            case "toggle":
                HandleToggle(context, breakpoints);
                break;

            case "list":
                ShowBreakpointList(context, breakpoints);
                break;

            default:
                context.WriteLine($"Unknown breakpoint command: {subCommand}", theme.Error);
                context.WriteLine("Use 'help break' for usage.", theme.TextSecondary);
                break;
        }

        return Task.CompletedTask;
    }

    private static void HandleWhen(IConsoleContext context, string[] args, IBreakpointOperations breakpoints)
    {
        var theme = context.Theme;

        if (args.Length < 2)
        {
            context.WriteLine("Usage: break when <expression>", theme.Warning);
            context.WriteLine("Examples:", theme.TextSecondary);
            context.WriteLine("  break when Player.GetMoney() < 100", theme.TextDim);
            context.WriteLine("  break when CountEntities() > 50", theme.TextDim);
            context.WriteLine("  break when GetPlayer() == null", theme.TextDim);
            return;
        }

        // Join all args after "when" as the expression
        var expression = string.Join(" ", args.Skip(1));

        var id = breakpoints.AddExpressionBreakpoint(expression);
        context.WriteLine($"Breakpoint #{id} added: when {expression}", theme.Success);
        context.WriteLine("Game will pause when this expression becomes true.", theme.TextSecondary);
    }

    private static void HandleLog(IConsoleContext context, string[] args, IBreakpointOperations breakpoints)
    {
        var theme = context.Theme;

        if (args.Length < 2)
        {
            context.WriteLine("Usage: break log <level>", theme.Warning);
            context.WriteLine("Levels: trace, debug, info, warning, error, critical", theme.TextSecondary);
            return;
        }

        var levelStr = args[1].ToLower();
        LogLevel? level = levelStr switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "info" or "information" => LogLevel.Information,
            "warn" or "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "critical" or "fatal" => LogLevel.Critical,
            _ => null
        };

        if (level == null)
        {
            context.WriteLine($"Unknown log level: {levelStr}", theme.Error);
            context.WriteLine("Valid levels: trace, debug, info, warning, error, critical", theme.TextSecondary);
            return;
        }

        var id = breakpoints.AddLogLevelBreakpoint(level.Value);
        context.WriteLine($"Breakpoint #{id} added: on log {level.Value}+", theme.Success);
        context.WriteLine($"Game will pause when a {level.Value} or higher log is written.", theme.TextSecondary);
    }

    private static void HandleWatch(IConsoleContext context, string[] args, IBreakpointOperations breakpoints)
    {
        var theme = context.Theme;

        if (args.Length < 2)
        {
            context.WriteLine("Usage: break watch <name>", theme.Warning);
            context.WriteLine("Use * for any watch alert", theme.TextSecondary);
            return;
        }

        var watchName = args[1];

        // Create a callback that checks if the watch has an alert
        Func<bool> alertChecker = () =>
        {
            try
            {
                return context.Watches.IsAlertActive(watchName);
            }
            catch
            {
                return false;
            }
        };

        var id = breakpoints.AddWatchAlertBreakpoint(watchName, alertChecker);
        context.WriteLine($"Breakpoint #{id} added: on watch alert '{watchName}'", theme.Success);
        context.WriteLine("Game will pause when this watch's alert condition triggers.", theme.TextSecondary);
    }

    private static void HandleEnable(IConsoleContext context, string[] args, IBreakpointOperations breakpoints)
    {
        var theme = context.Theme;

        if (args.Length < 2 || !int.TryParse(args[1], out var id))
        {
            context.WriteLine("Usage: break enable <id>", theme.Warning);
            return;
        }

        if (breakpoints.EnableBreakpoint(id))
        {
            context.WriteLine($"Breakpoint #{id} enabled", theme.Success);
        }
        else
        {
            context.WriteLine($"Breakpoint #{id} not found", theme.Error);
        }
    }

    private static void HandleDisable(IConsoleContext context, string[] args, IBreakpointOperations breakpoints)
    {
        var theme = context.Theme;

        if (args.Length < 2 || !int.TryParse(args[1], out var id))
        {
            context.WriteLine("Usage: break disable <id>", theme.Warning);
            return;
        }

        if (breakpoints.DisableBreakpoint(id))
        {
            context.WriteLine($"Breakpoint #{id} disabled", theme.Success);
        }
        else
        {
            context.WriteLine($"Breakpoint #{id} not found", theme.Error);
        }
    }

    private static void HandleDelete(IConsoleContext context, string[] args, IBreakpointOperations breakpoints)
    {
        var theme = context.Theme;

        if (args.Length < 2 || !int.TryParse(args[1], out var id))
        {
            context.WriteLine("Usage: break delete <id>", theme.Warning);
            return;
        }

        if (breakpoints.RemoveBreakpoint(id))
        {
            context.WriteLine($"Breakpoint #{id} deleted", theme.Success);
        }
        else
        {
            context.WriteLine($"Breakpoint #{id} not found", theme.Error);
        }
    }

    private static void HandleClear(IConsoleContext context, IBreakpointOperations breakpoints)
    {
        var theme = context.Theme;
        var count = breakpoints.Breakpoints.Count;

        if (count == 0)
        {
            context.WriteLine("No breakpoints to clear.", theme.TextSecondary);
            return;
        }

        breakpoints.ClearAllBreakpoints();
        context.WriteLine($"Cleared {count} breakpoint(s)", theme.Success);
    }

    private static void HandleToggle(IConsoleContext context, IBreakpointOperations breakpoints)
    {
        var theme = context.Theme;
        breakpoints.IsEnabled = !breakpoints.IsEnabled;

        if (breakpoints.IsEnabled)
        {
            context.WriteLine("Breakpoint evaluation ENABLED", theme.Success);
        }
        else
        {
            context.WriteLine("Breakpoint evaluation DISABLED", theme.Warning);
            context.WriteLine("Breakpoints will not trigger until re-enabled.", theme.TextSecondary);
        }
    }

    private static void ShowBreakpointList(IConsoleContext context, IBreakpointOperations breakpoints)
    {
        var theme = context.Theme;
        var allBreakpoints = breakpoints.Breakpoints;

        context.WriteLine("═══ Breakpoints ═══", theme.Info);

        if (allBreakpoints.Count == 0)
        {
            context.WriteLine("  No breakpoints set.", theme.TextSecondary);
            context.WriteLine("", theme.TextPrimary);
            context.WriteLine("Use 'break when <expr>' to add an expression breakpoint", theme.TextDim);
            context.WriteLine("Use 'break log error' to pause on errors", theme.TextDim);
            return;
        }

        var (total, enabled, disabled, totalHits) = breakpoints.GetStatistics();

        foreach (var bp in allBreakpoints)
        {
            var status = bp.IsEnabled ? "●" : "○";
            var statusColor = bp.IsEnabled ? theme.Success : theme.TextSecondary;
            var hitInfo = bp.HitCount > 0 ? $" (hits: {bp.HitCount})" : "";

            context.WriteLine($"  {status} #{bp.Id} [{bp.Type}] {bp.Description}{hitInfo}", statusColor);
        }

        context.WriteLine("", theme.TextPrimary);
        context.WriteLine($"Total: {total} ({enabled} enabled, {disabled} disabled)", theme.TextSecondary);

        if (!breakpoints.IsEnabled)
        {
            context.WriteLine("⚠ Breakpoint evaluation is DISABLED", theme.Warning);
        }

        context.WriteLine("", theme.TextPrimary);
        context.WriteLine("Commands: enable/disable/delete <id>, clear, toggle", theme.TextDim);
    }
}

