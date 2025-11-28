using System.Globalization;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Core.Services;
using PokeSharp.Engine.UI.Debug.Core;

namespace PokeSharp.Engine.Debug.Commands.BuiltIn;

/// <summary>
///     Controls game time - pause, resume, step, and time scale.
/// </summary>
[ConsoleCommand("time", "Control game time (pause, resume, step, speed)")]
public class TimeCommand : IConsoleCommand
{
    public string Name => "time";
    public string Description => "Control game time (pause, resume, step, speed)";

    public string Usage =>
        @"time                    Show current time state
time pause              Pause the game (timescale = 0)
time resume             Resume the game (timescale = 1)
time step [frames]      Step forward N frames when paused (default: 1)
time scale <value>      Set time scale (0.5 = half speed, 2 = double)
time slowmo <percent>   Set slow motion (25 = 25% speed)

Aliases:
  pause                 Same as 'time pause'
  resume                Same as 'time resume'
  step [frames]         Same as 'time step [frames]'

Examples:
  time pause            Pause the game
  time step 10          Advance 10 frames while paused
  time scale 0.5        Run at half speed
  time slowmo 25        Run at 25% speed";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        UITheme theme = context.Theme;
        ITimeControl? timeControl = context.TimeControl;

        // Check if time control is available
        if (timeControl == null)
        {
            context.WriteLine("⚠ Time control is not available.", theme.Warning);
            context.WriteLine("", theme.TextPrimary);
            context.WriteLine("This usually means:", theme.TextSecondary);
            context.WriteLine(
                "  • ITimeControl was not found in loaded assemblies",
                theme.TextSecondary
            );
            context.WriteLine(
                "  • ITimeControl is not registered in the DI container",
                theme.TextSecondary
            );
            context.WriteLine("", theme.TextPrimary);
            context.WriteLine(
                "Check the console logs (enable with 'logging on') for details.",
                theme.TextSecondary
            );
            return Task.CompletedTask;
        }

        // No args = show current state
        if (args.Length == 0)
        {
            ShowTimeState(context, timeControl);
            return Task.CompletedTask;
        }

        string subCommand = args[0].ToLowerInvariant();

        switch (subCommand)
        {
            case "pause":
            case "p":
                timeControl.Pause();
                context.WriteLine("Game paused", theme.Warning);
                ShowTimeState(context, timeControl);
                break;

            case "resume":
            case "r":
                timeControl.Resume();
                context.WriteLine("Game resumed", theme.Success);
                ShowTimeState(context, timeControl);
                break;

            case "step":
            case "s":
                if (!timeControl.IsPaused)
                {
                    context.WriteLine(
                        "Game is not paused. Use 'pause' first, then 'step'.",
                        theme.Warning
                    );
                    break;
                }

                int frames = 1;
                if (args.Length > 1 && int.TryParse(args[1], out int parsedFrames))
                {
                    frames = Math.Max(1, parsedFrames);
                }

                timeControl.Step(frames);
                context.WriteLine($"Stepping {frames} frame{(frames == 1 ? "" : "s")}", theme.Info);
                break;

            case "scale":
            case "speed":
                if (args.Length < 2)
                {
                    context.WriteLine(
                        $"Current time scale: {timeControl.TimeScale:F2}x",
                        theme.Info
                    );
                    context.WriteLine(
                        "Usage: time scale <value>  (e.g., time scale 0.5)",
                        theme.TextSecondary
                    );
                    break;
                }

                // Use InvariantCulture to ensure "0.5" parses correctly regardless of locale
                if (
                    float.TryParse(
                        args[1],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float scale
                    )
                )
                {
                    scale = Math.Clamp(scale, 0f, 10f);
                    timeControl.TimeScale = scale;

                    string message = scale switch
                    {
                        0f => "Game paused (time scale = 0)",
                        1f => "Normal speed (time scale = 1)",
                        < 1f => $"Slow motion: {scale * 100:F0}% speed",
                        _ => $"Fast forward: {scale:F1}x speed",
                    };
                    context.WriteLine(message, scale == 0 ? theme.Warning : theme.Success);
                }
                else
                {
                    context.WriteLine($"Invalid time scale: {args[1]}", theme.Error);
                    context.WriteLine(
                        "Usage: time scale <number>  (e.g., 0.5, 1, 2)",
                        theme.TextSecondary
                    );
                }

                break;

            case "slowmo":
            case "slow":
                if (args.Length < 2)
                {
                    float currentPercent = timeControl.TimeScale * 100;
                    context.WriteLine($"Current speed: {currentPercent:F0}%", theme.Info);
                    context.WriteLine(
                        "Usage: time slowmo <percent>  (e.g., time slowmo 25)",
                        theme.TextSecondary
                    );
                    break;
                }

                // Use InvariantCulture for consistent decimal parsing
                if (
                    float.TryParse(
                        args[1],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float percent
                    )
                )
                {
                    // Convert percentage to scale (25% = 0.25)
                    float slowScale = Math.Clamp(percent / 100f, 0f, 10f);
                    timeControl.TimeScale = slowScale;
                    context.WriteLine(
                        $"Speed set to {percent:F0}% (scale = {slowScale:F2})",
                        theme.Success
                    );
                }
                else
                {
                    context.WriteLine($"Invalid percentage: {args[1]}", theme.Error);
                    context.WriteLine(
                        "Usage: time slowmo <percent>  (e.g., 25 for 25% speed)",
                        theme.TextSecondary
                    );
                }

                break;

            default:
                context.WriteLine($"Unknown time command: {subCommand}", theme.Error);
                context.WriteLine("", theme.TextPrimary);
                context.WriteLine("Available commands:", theme.Info);
                context.WriteLine("  time pause              Pause the game", theme.TextSecondary);
                context.WriteLine("  time resume             Resume the game", theme.TextSecondary);
                context.WriteLine(
                    "  time step [frames]      Step N frames when paused",
                    theme.TextSecondary
                );
                context.WriteLine(
                    "  time scale <value>      Set time scale (0.5, 1, 2, etc.)",
                    theme.TextSecondary
                );
                context.WriteLine(
                    "  time slowmo <percent>   Set speed percentage (25, 50, etc.)",
                    theme.TextSecondary
                );
                break;
        }

        return Task.CompletedTask;
    }

    private static void ShowTimeState(IConsoleContext context, ITimeControl timeControl)
    {
        UITheme theme = context.Theme;
        float scale = timeControl.TimeScale;
        bool isPaused = timeControl.IsPaused;

        context.WriteLine("", theme.TextPrimary);
        context.WriteLine(
            "═══════════════════════════════════════════════════════════════════",
            theme.Info
        );
        context.WriteLine("  TIME CONTROL", theme.Info);
        context.WriteLine(
            "═══════════════════════════════════════════════════════════════════",
            theme.Info
        );

        // Status
        string status = isPaused ? "PAUSED" : "RUNNING";
        Color statusColor = isPaused ? theme.Warning : theme.Success;
        context.WriteLine($"  Status:     {status}", statusColor);

        // Time scale
        string scaleDescription = scale switch
        {
            0f => "Paused",
            < 0.25f => "Very slow",
            < 0.5f => "Slow",
            < 1f => "Slow motion",
            1f => "Normal",
            < 2f => "Fast",
            _ => "Very fast",
        };
        context.WriteLine($"  Time Scale: {scale:F2}x ({scaleDescription})", theme.TextPrimary);
        context.WriteLine($"  Speed:      {scale * 100:F0}%", theme.TextPrimary);

        context.WriteLine("", theme.TextPrimary);
        context.WriteLine(
            "Quick commands: pause | resume | step | scale <n> | slowmo <n>",
            theme.TextDim
        );
    }
}

/// <summary>
///     Shortcut command: pause (alias for 'time pause')
/// </summary>
[ConsoleCommand("pause", "Pause the game")]
public class PauseCommand : IConsoleCommand
{
    public string Name => "pause";
    public string Description => "Pause the game";
    public string Usage => "pause";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        ITimeControl? timeControl = context.TimeControl;
        if (timeControl == null)
        {
            context.WriteLine(
                "⚠ Time control is not available. Run 'time' for details.",
                context.Theme.Warning
            );
            return Task.CompletedTask;
        }

        timeControl.Pause();
        context.WriteLine("Game paused", context.Theme.Warning);
        context.WriteLine(
            "Use 'resume' to continue, 'step' to advance frames",
            context.Theme.TextSecondary
        );
        return Task.CompletedTask;
    }
}

/// <summary>
///     Shortcut command: resume (alias for 'time resume')
/// </summary>
[ConsoleCommand("resume", "Resume the game")]
public class ResumeCommand : IConsoleCommand
{
    public string Name => "resume";
    public string Description => "Resume the game";
    public string Usage => "resume";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        ITimeControl? timeControl = context.TimeControl;
        if (timeControl == null)
        {
            context.WriteLine(
                "⚠ Time control is not available. Run 'time' for details.",
                context.Theme.Warning
            );
            return Task.CompletedTask;
        }

        timeControl.Resume();
        context.WriteLine("Game resumed", context.Theme.Success);
        return Task.CompletedTask;
    }
}

/// <summary>
///     Shortcut command: step (alias for 'time step')
/// </summary>
[ConsoleCommand("step", "Step forward frames when paused")]
public class StepCommand : IConsoleCommand
{
    public string Name => "step";
    public string Description => "Step forward frames when paused";

    public string Usage =>
        @"step [frames]    Step forward N frames (default: 1)

Examples:
  step           Advance 1 frame
  step 10        Advance 10 frames
  step 60        Advance ~1 second at 60fps";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        UITheme theme = context.Theme;
        ITimeControl? timeControl = context.TimeControl;

        if (timeControl == null)
        {
            context.WriteLine(
                "⚠ Time control is not available. Run 'time' for details.",
                theme.Warning
            );
            return Task.CompletedTask;
        }

        int frames = 1;
        if (args.Length > 0 && int.TryParse(args[0], out int parsedFrames))
        {
            frames = Math.Max(1, parsedFrames);
        }

        if (!timeControl.IsPaused)
        {
            context.WriteLine("Game is not paused. Use 'pause' first, then 'step'.", theme.Warning);
            return Task.CompletedTask;
        }

        timeControl.Step(frames);
        context.WriteLine($"Stepping {frames} frame{(frames == 1 ? "" : "s")}...", theme.Info);
        return Task.CompletedTask;
    }
}
