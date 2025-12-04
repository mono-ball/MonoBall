using Microsoft.Xna.Framework;
using PokeSharp.Game.Engine.UI.Debug.Core;

namespace PokeSharp.Game.Engine.Debug.Commands.BuiltIn;

/// <summary>
///     Command to list and switch UI themes.
///     Usage: theme [name]
/// </summary>
[ConsoleCommand("theme", "List or switch UI themes")]
public class ThemeCommand : IConsoleCommand
{
    public string Name => "theme";
    public string Description => "List or switch UI themes";

    public string Usage =>
        "theme [onedark|monokai|dracula|gruvbox|nord|solarized|solarized-light|pokeball]";

    public Task ExecuteAsync(IConsoleContext context, string[] args)
    {
        if (args.Length == 0)
        {
            // List available themes
            string currentTheme = ThemeManager.GetCurrentThemeName();

            context.WriteLine("Available Themes:", context.Theme.Info);
            context.WriteLine("", context.Theme.TextPrimary);

            foreach (string themeName in ThemeManager.AvailableThemes)
            {
                string marker = themeName.Equals(currentTheme, StringComparison.OrdinalIgnoreCase)
                    ? "► "
                    : "  ";
                Color color = themeName.Equals(currentTheme, StringComparison.OrdinalIgnoreCase)
                    ? context.Theme.Success
                    : context.Theme.TextSecondary;

                context.WriteLine($"{marker}{themeName}", color);
            }

            context.WriteLine("", context.Theme.TextPrimary);
            context.WriteLine($"Current: {currentTheme}", context.Theme.TextDim);
            context.WriteLine("Use 'theme <name>' to switch", context.Theme.TextDim);
            return Task.CompletedTask;
        }

        string requestedTheme = args[0].ToLowerInvariant();

        if (ThemeManager.SetTheme(requestedTheme))
        {
            context.WriteLine($"Theme changed to: {requestedTheme}", context.Theme.Success);
        }
        else
        {
            context.WriteLine($"Unknown theme: {requestedTheme}", context.Theme.Error);
            context.WriteLine(
                "Available: " + string.Join(", ", ThemeManager.AvailableThemes),
                context.Theme.TextDim
            );
        }

        return Task.CompletedTask;
    }
}
