namespace MonoBallFramework.Game.Engine.UI.Core;

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
        public const float LabelWidth = 130f; // Increased to fit "Entity Pools:" and "Event Types:"
        public const float BarWidth = 150f;
        public const float ValueOffset = 80f; // Offset from label to value text
        public const float RowSpacing = 4f; // Additional spacing per row
    }

    /// <summary>
    ///     Event Inspector panel layout constants.
    /// </summary>
    public static class EventInspector
    {
        // ═══════════════════════════════════════════════════════════════
        // Column Widths (responsive with min/max bounds)
        // ═══════════════════════════════════════════════════════════════

        // Preferred column widths (used when space is available)
        public const float EventNameColumnWidth = 200f; // Matches Profiler.NameColumnWidth
        public const float SubsColumnWidth = 50f;
        public const float CountColumnWidth = 70f;

        public const float TimeColumnWidth = 100f; // Matches Profiler.MsColumnWidth

        // Note: Execution Time bar is DYNAMIC - fills remaining space

        // Minimum column widths (for responsive layout)
        public const float MinEventNameColumnWidth = 120f; // Must fit event names with ellipsis
        public const float MinBarColumnWidth = 80f; // Minimum bar width for readability
        public const float MinSubsColumnWidth = 40f; // Fits "999"
        public const float MinCountColumnWidth = 50f; // Fits "99.9K"
        public const float MinTimeColumnWidth = 75f; // Fits "99.9/99.9"

        // ═══════════════════════════════════════════════════════════════
        // Layout & Spacing
        // ═══════════════════════════════════════════════════════════════

        // Row height (tighter than default PanelRowHeight for more content density)
        public const float RowHeight = 22f;

        // Tree indentation (subscriptions section)
        public const float TreeIndentLevel1 = 24f; // First level indent
        public const float TreeIndentLevel2 = 70f; // Second level indent (after priority)

        // Recent Events section layout
        public const float TimestampColumnWidth = 85f;
        public const float OperationIconWidth = 20f;

        // ═══════════════════════════════════════════════════════════════
        // Responsive Breakpoints
        // ═══════════════════════════════════════════════════════════════

        public const float MinPanelWidth = 450f; // Absolute minimum usable width
        public const float BreakpointHideSubs = 550f; // Hide Subs column below this
        public const float BreakpointHideBar = 650f; // Hide execution bar below this
        public const float BreakpointFullLayout = 800f; // Full layout with comfortable spacing

        // ═══════════════════════════════════════════════════════════════
        // Performance Visualization (values in MILLISECONDS for consistency)
        // ═══════════════════════════════════════════════════════════════

        // Performance bar rendering (use theme.ProfilerBarInset and theme.ProfilerBarMaxScale in code)
        public const float MaxBarTimeMs = 1.0f; // Max time for 100% bar (1ms)

        // Thresholds in milliseconds (matching Profiler's relative approach)
        // Error: >= WarningThresholdMs (event taking >= 1ms is critical)
        // Warning: >= WarningThresholdMs * theme.ProfilerBarWarningThreshold
        // Mild: >= WarningThresholdMs * theme.ProfilerBarMildThreshold
        public const float WarningThresholdMs = 1.0f; // 1ms
    }
}
