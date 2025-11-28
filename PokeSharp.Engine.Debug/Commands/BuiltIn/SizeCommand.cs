using PokeSharp.Engine.UI.Debug.Components.Debug;

namespace PokeSharp.Engine.Debug.Commands.BuiltIn;

/// <summary>
///     Command to change the console size/height.
///     Usage: size [small|medium|large|full|25-100]
/// </summary>
[ConsoleCommand("size", "Change the console height")]
public class SizeCommand : IConsoleCommand
{
    public string Name => "size";
    public string Description => "Change the console height";
    public string Usage => "size [small|medium|large|full|25-100]";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        if (args.Length == 0)
        {
            // Show current size and available options
            context.WriteLine("Console Size Options:", context.Theme.Info);
            context.WriteLine("  size small  - 25% height", context.Theme.TextSecondary);
            context.WriteLine("  size medium - 50% height", context.Theme.TextSecondary);
            context.WriteLine("  size large  - 75% height", context.Theme.TextSecondary);
            context.WriteLine("  size full   - 100% height", context.Theme.TextSecondary);
            context.WriteLine("  size <percent> - Custom (25-100)", context.Theme.TextSecondary);
            return Task.CompletedTask;
        }

        string sizeArg = args[0].ToLowerInvariant();
        float? heightPercent = null;

        // Try named presets first
        switch (sizeArg)
        {
            case "small":
            case "s":
                heightPercent = ConsoleSize.Small.GetHeightPercent();
                context.WriteLine("Console size: Small (25%)", context.Theme.Success);
                break;

            case "medium":
            case "med":
            case "m":
                heightPercent = ConsoleSize.Medium.GetHeightPercent();
                context.WriteLine("Console size: Medium (50%)", context.Theme.Success);
                break;

            case "large":
            case "lg":
            case "l":
                heightPercent = ConsoleSize.Large.GetHeightPercent();
                context.WriteLine("Console size: Large (75%)", context.Theme.Success);
                break;

            case "full":
            case "f":
            case "max":
                heightPercent = ConsoleSize.Full.GetHeightPercent();
                context.WriteLine("Console size: Full (100%)", context.Theme.Success);
                break;

            default:
                // Try parsing as a percentage
                if (int.TryParse(sizeArg.TrimEnd('%'), out int percent))
                {
                    if (percent < 25 || percent > 100)
                    {
                        context.WriteLine(
                            "Error: Size must be between 25 and 100",
                            context.Theme.Error
                        );
                        return Task.CompletedTask;
                    }

                    heightPercent = percent / 100f;
                    context.WriteLine($"Console size: {percent}%", context.Theme.Success);
                }
                else
                {
                    context.WriteLine($"Unknown size: {sizeArg}", context.Theme.Error);
                    context.WriteLine(
                        "Use: small, medium, large, full, or a percentage (25-100)",
                        context.Theme.TextSecondary
                    );
                    return Task.CompletedTask;
                }

                break;
        }

        if (heightPercent.HasValue)
        {
            context.SetConsoleHeight(heightPercent.Value);
        }

        return Task.CompletedTask;
    }
}
