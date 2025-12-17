using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Engine.UI.Core;
using MonoBallFramework.Game.Engine.UI.Input;
using MonoBallFramework.Game.Engine.UI.Layout;

namespace MonoBallFramework.Game.Engine.UI.Components.Controls;

/// <summary>
///     Reusable sortable table header component for consistent table UX.
/// </summary>
/// <typeparam name="TSort">Enum type representing sort modes.</typeparam>
public class SortableTableHeader<TSort>
    where TSort : struct, Enum
{
    /// <summary>
    ///     Horizontal alignment options.
    /// </summary>
    public enum HorizontalAlignment
    {
        Left,
        Center,
        Right
    }

    private readonly Dictionary<TSort, LayoutRect> _clickRegions = new();
    private readonly List<Column> _columns = new();

    /// <summary>
    ///     Initializes a new sortable table header.
    /// </summary>
    /// <param name="initialSort">Initial sort mode.</param>
    public SortableTableHeader(TSort initialSort)
    {
        CurrentSort = initialSort;
    }

    /// <summary>
    ///     Current sort mode.
    /// </summary>
    public TSort CurrentSort { get; private set; }

    /// <summary>
    ///     Event raised when sort mode changes.
    /// </summary>
    public event Action<TSort>? SortChanged;

    /// <summary>
    ///     Adds a column to the header.
    /// </summary>
    public void AddColumn(Column column)
    {
        _columns.Add(column);
    }

    /// <summary>
    ///     Adds multiple columns to the header.
    /// </summary>
    public void AddColumns(params Column[] columns)
    {
        _columns.AddRange(columns);
    }

    /// <summary>
    ///     Clears all columns.
    /// </summary>
    public void ClearColumns()
    {
        _columns.Clear();
        _clickRegions.Clear();
    }

    /// <summary>
    ///     Sets the current sort mode (without raising the event).
    /// </summary>
    public void SetSort(TSort sortMode)
    {
        CurrentSort = sortMode;
    }

    /// <summary>
    ///     Handles input for column header clicks.
    /// </summary>
    /// <param name="input">Current input state.</param>
    /// <returns>True if sort mode changed.</returns>
    public bool HandleInput(InputState input)
    {
        if (input == null || !input.IsMouseButtonReleased(MouseButton.Left))
        {
            return false;
        }

        Point mousePos = input.MousePosition;
        foreach (KeyValuePair<TSort, LayoutRect> kvp in _clickRegions)
        {
            if (kvp.Value.Contains(mousePos))
            {
                if (!EqualityComparer<TSort>.Default.Equals(CurrentSort, kvp.Key))
                {
                    CurrentSort = kvp.Key;
                    SortChanged?.Invoke(CurrentSort);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Draws the table header.
    /// </summary>
    /// <param name="renderer">UI renderer.</param>
    /// <param name="theme">Current UI theme.</param>
    /// <param name="y">Y position to draw the header.</param>
    /// <param name="lineHeight">Height of a line.</param>
    public void Draw(UIRenderer renderer, UITheme theme, float y, float lineHeight)
    {
        _clickRegions.Clear();

        foreach (Column column in _columns)
        {
            // Build header text with sort indicator
            bool isActiveSort = EqualityComparer<TSort>.Default.Equals(
                CurrentSort,
                column.SortMode
            );
            string headerText = column.Label;

            if (isActiveSort)
            {
                if (column.CustomIcon != null)
                {
                    headerText = $"{column.Label} {column.CustomIcon}";
                }
                else
                {
                    string indicator = column.Ascending
                        ? NerdFontIcons.CaretUp
                        : NerdFontIcons.CaretDown;
                    headerText = $"{column.Label} {indicator}";
                }
            }

            // Measure text
            float headerWidth = renderer.MeasureText(headerText).X;

            // Calculate X position based on alignment
            float textX = column.X;
            if (column.Alignment == HorizontalAlignment.Right && column.MaxWidth.HasValue)
            {
                textX = column.X + column.MaxWidth.Value - headerWidth;
            }
            else if (column.Alignment == HorizontalAlignment.Center && column.MaxWidth.HasValue)
            {
                textX = column.X + (column.MaxWidth.Value / 2) - (headerWidth / 2);
            }

            // Draw header text
            Color color = isActiveSort ? theme.Info : theme.TextSecondary;
            renderer.DrawText(headerText, textX, y, color);

            // Store click region
            float clickWidth = column.MaxWidth ?? headerWidth + theme.InteractiveClickPadding;
            var clickRegion = new LayoutRect(column.X, y, clickWidth, lineHeight);
            _clickRegions[column.SortMode] = clickRegion;
        }
    }

    /// <summary>
    ///     Draws the table header with hover effects.
    /// </summary>
    /// <param name="renderer">UI renderer.</param>
    /// <param name="theme">Current UI theme.</param>
    /// <param name="input">Current input state (for hover detection).</param>
    /// <param name="y">Y position to draw the header.</param>
    /// <param name="lineHeight">Height of a line.</param>
    public void DrawWithHover(
        UIRenderer renderer,
        UITheme theme,
        InputState? input,
        float y,
        float lineHeight
    )
    {
        // Draw hover backgrounds
        if (input != null)
        {
            Point mousePos = input.MousePosition;
            foreach (KeyValuePair<TSort, LayoutRect> kvp in _clickRegions)
            {
                if (kvp.Value.Contains(mousePos))
                {
                    renderer.DrawRectangle(kvp.Value, theme.HoverBackground);
                    break;
                }
            }
        }

        // Draw headers
        Draw(renderer, theme, y, lineHeight);
    }

    /// <summary>
    ///     Gets the click region for a specific sort mode.
    /// </summary>
    public LayoutRect? GetClickRegion(TSort sortMode)
    {
        return _clickRegions.TryGetValue(sortMode, out LayoutRect rect) ? rect : null;
    }

    /// <summary>
    ///     Represents a single column in the table header.
    /// </summary>
    public record Column
    {
        /// <summary>Label text for the column.</summary>
        public required string Label { get; init; }

        /// <summary>Sort mode this column represents.</summary>
        public required TSort SortMode { get; init; }

        /// <summary>X position of the column.</summary>
        public required float X { get; init; }

        /// <summary>Maximum width of the column (for alignment).</summary>
        public float? MaxWidth { get; init; }

        /// <summary>Whether to show ascending (↑) or descending (↓) indicator.</summary>
        public bool Ascending { get; init; } = false;

        /// <summary>Custom sort indicator icon (overrides Ascending).</summary>
        public string? CustomIcon { get; init; }

        /// <summary>Horizontal alignment of the column.</summary>
        public HorizontalAlignment Alignment { get; init; } = HorizontalAlignment.Left;
    }
}
