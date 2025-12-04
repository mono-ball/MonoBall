namespace MonoBallFramework.Game.Engine.UI.Core;

/// <summary>
///     Text utilities for UI rendering operations.
/// </summary>
public static class TextUtils
{
    /// <summary>
    ///     Truncates text to fit within a maximum width, appending an ellipsis if truncated.
    /// </summary>
    /// <param name="renderer">The UI renderer used for text measurement.</param>
    /// <param name="text">The text to truncate.</param>
    /// <param name="maxWidth">The maximum width in pixels.</param>
    /// <returns>The truncated text with ellipsis if needed, or the original text if it fits.</returns>
    public static string TruncateWithEllipsis(this UIRenderer renderer, string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        // Check if text fits without truncation
        if (renderer.MeasureText(text).X <= maxWidth)
        {
            return text;
        }

        string ellipsis = NerdFontIcons.Ellipsis;
        float ellipsisWidth = renderer.MeasureText(ellipsis).X;
        float targetWidth = maxWidth - ellipsisWidth;

        // If even the ellipsis doesn't fit, return just the ellipsis
        if (targetWidth <= 0)
        {
            return ellipsis;
        }

        // Binary search for optimal length (more efficient than linear)
        int left = 0;
        int right = text.Length;
        int bestLen = 0;

        while (left <= right)
        {
            int mid = (left + right) / 2;
            string truncated = text[..mid];
            float width = renderer.MeasureText(truncated).X;

            if (width <= targetWidth)
            {
                bestLen = mid;
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }

        return bestLen > 0 ? text[..bestLen] + ellipsis : ellipsis;
    }

    /// <summary>
    ///     Truncates text to fit within a maximum width, appending a custom suffix if truncated.
    /// </summary>
    /// <param name="renderer">The UI renderer used for text measurement.</param>
    /// <param name="text">The text to truncate.</param>
    /// <param name="maxWidth">The maximum width in pixels.</param>
    /// <param name="suffix">The suffix to append when truncated (e.g., "...", "…").</param>
    /// <returns>The truncated text with suffix if needed, or the original text if it fits.</returns>
    public static string TruncateWithSuffix(
        this UIRenderer renderer,
        string text,
        float maxWidth,
        string suffix
    )
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        if (renderer.MeasureText(text).X <= maxWidth)
        {
            return text;
        }

        float suffixWidth = renderer.MeasureText(suffix).X;
        float targetWidth = maxWidth - suffixWidth;

        if (targetWidth <= 0)
        {
            return suffix;
        }

        // Binary search for optimal length
        int left = 0;
        int right = text.Length;
        int bestLen = 0;

        while (left <= right)
        {
            int mid = (left + right) / 2;
            string truncated = text[..mid];
            float width = renderer.MeasureText(truncated).X;

            if (width <= targetWidth)
            {
                bestLen = mid;
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }

        return bestLen > 0 ? text[..bestLen] + suffix : suffix;
    }
}
