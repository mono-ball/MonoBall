namespace PokeSharp.Engine.Debug.Commands;

/// <summary>
///     Provides console output export operations.
/// </summary>
public interface IConsoleExport
{
    /// <summary>
    ///     Exports console output to a string.
    /// </summary>
    string ExportConsoleOutput();

    /// <summary>
    ///     Copies console output to clipboard.
    /// </summary>
    void CopyConsoleOutputToClipboard();

    /// <summary>
    ///     Gets console output statistics.
    /// </summary>
    (int TotalLines, int FilteredLines) GetConsoleOutputStats();
}
