namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
/// Console size presets for quick toggling.
/// </summary>
public enum ConsoleSize
{
    /// <summary>
    /// Small console (25% of screen height).
    /// </summary>
    Small,

    /// <summary>
    /// Medium console (50% of screen height).
    /// </summary>
    Medium,

    /// <summary>
    /// Large console (75% of screen height).
    /// </summary>
    Large,

    /// <summary>
    /// Full console (100% of screen height).
    /// </summary>
    Full
}

/// <summary>
/// Extension methods for ConsoleSize.
/// </summary>
public static class ConsoleSizeExtensions
{
    /// <summary>
    /// Gets the height multiplier for the given console size.
    /// </summary>
    public static float GetHeightPercent(this ConsoleSize size) => size switch
    {
        ConsoleSize.Small => 0.25f,
        ConsoleSize.Medium => 0.5f,
        ConsoleSize.Large => 0.75f,
        ConsoleSize.Full => 1.0f,
        _ => 0.5f
    };

    /// <summary>
    /// Gets the next larger console size (cycles to Small after Full).
    /// </summary>
    public static ConsoleSize Next(this ConsoleSize size) => size switch
    {
        ConsoleSize.Small => ConsoleSize.Medium,
        ConsoleSize.Medium => ConsoleSize.Large,
        ConsoleSize.Large => ConsoleSize.Full,
        ConsoleSize.Full => ConsoleSize.Small,
        _ => ConsoleSize.Medium
    };

    /// <summary>
    /// Gets the previous smaller console size (cycles to Full before Small).
    /// </summary>
    public static ConsoleSize Previous(this ConsoleSize size) => size switch
    {
        ConsoleSize.Small => ConsoleSize.Full,
        ConsoleSize.Medium => ConsoleSize.Small,
        ConsoleSize.Large => ConsoleSize.Medium,
        ConsoleSize.Full => ConsoleSize.Large,
        _ => ConsoleSize.Medium
    };
}




