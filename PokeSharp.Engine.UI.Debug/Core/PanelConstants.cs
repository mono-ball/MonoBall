namespace PokeSharp.Engine.UI.Debug.Core;

/// <summary>
///     Layout constants for debug panels (Profiler, Stats, etc.).
///     Centralizes magic numbers for maintainability.
/// </summary>
public static class PanelConstants
{
    /// <summary>
    ///     Profiler panel layout constants.
    /// </summary>
    public static class Profiler
    {
        public const float BottomPadding = 24f;
        public const float NameColumnWidth = 200f;
        public const float MsColumnWidth = 100f;
    }

    /// <summary>
    ///     Stats panel layout constants.
    /// </summary>
    public static class Stats
    {
        public const float LabelWidth = 100f;
        public const float BarWidth = 150f;
        public const float ValueOffset = 80f; // Offset from label to value text
        public const float RowSpacing = 4f; // Additional spacing per row
    }
}
