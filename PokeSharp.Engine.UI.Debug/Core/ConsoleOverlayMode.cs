namespace PokeSharp.Engine.UI.Debug.Core;

/// <summary>
///     Defines the active overlay mode for the console.
///     These modes are mutually exclusive - only one can be active at a time.
/// </summary>
public enum ConsoleOverlayMode
{
    /// <summary>No overlay is currently showing.</summary>
    None,

    /// <summary>Auto-complete suggestions are showing.</summary>
    Suggestions,

    /// <summary>Search bar is active (searches console output).</summary>
    Search,

    /// <summary>Command history search is active (searches command history).</summary>
    CommandHistorySearch,

    /// <summary>Parameter hints tooltip is showing.</summary>
    ParameterHints,

    /// <summary>Documentation popup is showing.</summary>
    Documentation,
}
