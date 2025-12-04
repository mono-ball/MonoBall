using PokeSharp.Game.Engine.UI.Debug.Core;
using PokeSharp.Game.Engine.UI.Debug.Layout;

namespace PokeSharp.Game.Engine.UI.Debug.Components.Controls;

/// <summary>
///     Standardized empty state display for consistent UX across all components.
/// </summary>
public static class EmptyStateComponent
{
    /// <summary>
    ///     Draws a centered empty state message with optional icon.
    /// </summary>
    /// <param name="renderer">UI renderer.</param>
    /// <param name="theme">Current UI theme.</param>
    /// <param name="area">Rectangle to center the empty state within.</param>
    /// <param name="title">Main title text.</param>
    /// <param name="description">Optional description text.</param>
    /// <param name="icon">Optional Nerd Font icon.</param>
    public static void DrawCentered(
        UIRenderer renderer,
        UITheme theme,
        LayoutRect area,
        string title,
        string? description = null,
        string? icon = null
    )
    {
        int lineHeight = renderer.GetLineHeight();
        float centerY = area.Y + (area.Height / 2);

        // Calculate total height needed
        int lineCount = 1; // Title
        if (icon != null)
        {
            lineCount++;
        }

        if (description != null)
        {
            lineCount++;
        }

        float totalHeight = (lineCount * lineHeight) + ((lineCount - 1) * theme.SpacingTight);
        float startY = centerY - (totalHeight / 2);
        float y = startY;

        // Draw icon if provided
        if (icon != null)
        {
            float iconWidth = renderer.MeasureText(icon).X;
            float iconX = area.X + (area.Width / 2) - (iconWidth / 2);
            renderer.DrawText(icon, iconX, y, theme.TextSecondary * 0.5f);
            y += lineHeight + theme.SpacingTight;
        }

        // Draw title (centered)
        float titleWidth = renderer.MeasureText(title).X;
        float titleX = area.X + (area.Width / 2) - (titleWidth / 2);
        renderer.DrawText(title, titleX, y, theme.TextSecondary);
        y += lineHeight;

        // Draw description if provided (centered)
        if (description != null)
        {
            y += theme.SpacingTight;
            float descWidth = renderer.MeasureText(description).X;
            float descX = area.X + (area.Width / 2) - (descWidth / 2);
            renderer.DrawText(description, descX, y, theme.TextDim);
        }
    }

    /// <summary>
    ///     Draws a left-aligned empty state message.
    /// </summary>
    /// <param name="renderer">UI renderer.</param>
    /// <param name="theme">Current UI theme.</param>
    /// <param name="x">X position.</param>
    /// <param name="y">Y position.</param>
    /// <param name="title">Main title text.</param>
    /// <param name="description">Optional description text.</param>
    /// <returns>The Y position after the empty state (for layout purposes).</returns>
    public static float DrawLeftAligned(
        UIRenderer renderer,
        UITheme theme,
        float x,
        float y,
        string title,
        string? description = null
    )
    {
        int lineHeight = renderer.GetLineHeight();

        // Draw title
        renderer.DrawText(title, x, y, theme.TextDim);
        y += lineHeight;

        // Draw description if provided
        if (description != null)
        {
            renderer.DrawText(description, x, y, theme.TextDim);
            y += lineHeight;
        }

        return y;
    }

    /// <summary>
    ///     Draws an empty state with a loading indicator.
    /// </summary>
    /// <param name="renderer">UI renderer.</param>
    /// <param name="theme">Current UI theme.</param>
    /// <param name="area">Rectangle to center the empty state within.</param>
    /// <param name="title">Main title text.</param>
    /// <param name="description">Optional description text.</param>
    public static void DrawLoading(
        UIRenderer renderer,
        UITheme theme,
        LayoutRect area,
        string title,
        string? description = null
    )
    {
        DrawCentered(renderer, theme, area, title, description, NerdFontIcons.Spinner);
    }

    /// <summary>
    ///     Draws an empty state for "no data" scenarios.
    /// </summary>
    /// <param name="renderer">UI renderer.</param>
    /// <param name="theme">Current UI theme.</param>
    /// <param name="area">Rectangle to center the empty state within.</param>
    /// <param name="title">Main title text.</param>
    /// <param name="description">Optional description text.</param>
    public static void DrawNoData(
        UIRenderer renderer,
        UITheme theme,
        LayoutRect area,
        string title,
        string? description = null
    )
    {
        DrawCentered(renderer, theme, area, title, description, NerdFontIcons.FileAlert);
    }

    /// <summary>
    ///     Draws an empty state for error scenarios.
    /// </summary>
    /// <param name="renderer">UI renderer.</param>
    /// <param name="theme">Current UI theme.</param>
    /// <param name="area">Rectangle to center the empty state within.</param>
    /// <param name="title">Main title text.</param>
    /// <param name="description">Optional description text.</param>
    public static void DrawError(
        UIRenderer renderer,
        UITheme theme,
        LayoutRect area,
        string title,
        string? description = null
    )
    {
        int lineHeight = renderer.GetLineHeight();
        float centerY = area.Y + (area.Height / 2);

        // Calculate total height needed
        int lineCount = 2; // Icon + title
        if (description != null)
        {
            lineCount++;
        }

        float totalHeight = (lineCount * lineHeight) + ((lineCount - 1) * theme.SpacingTight);
        float startY = centerY - (totalHeight / 2);
        float y = startY;

        // Draw error icon
        string icon = NerdFontIcons.AlertCircle;
        float iconWidth = renderer.MeasureText(icon).X;
        float iconX = area.X + (area.Width / 2) - (iconWidth / 2);
        renderer.DrawText(icon, iconX, y, theme.Error);
        y += lineHeight + theme.SpacingTight;

        // Draw title (centered)
        float titleWidth = renderer.MeasureText(title).X;
        float titleX = area.X + (area.Width / 2) - (titleWidth / 2);
        renderer.DrawText(title, titleX, y, theme.TextSecondary);
        y += lineHeight;

        // Draw description if provided (centered)
        if (description != null)
        {
            y += theme.SpacingTight;
            float descWidth = renderer.MeasureText(description).X;
            float descX = area.X + (area.Width / 2) - (descWidth / 2);
            renderer.DrawText(description, descX, y, theme.TextDim);
        }
    }
}
