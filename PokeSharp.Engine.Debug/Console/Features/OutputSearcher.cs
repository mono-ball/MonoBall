using System.Text.RegularExpressions;

namespace PokeSharp.Engine.Debug.Console.Features;

/// <summary>
///     Handles searching through console output with navigation and highlighting.
/// </summary>
public class OutputSearcher
{
    private readonly List<SearchMatch> _matches = new();

    /// <summary>
    ///     Gets whether a search is currently active.
    /// </summary>
    public bool IsSearching => !string.IsNullOrEmpty(SearchQuery) && _matches.Count > 0;

    /// <summary>
    ///     Gets the current search query.
    /// </summary>
    public string SearchQuery { get; private set; } = string.Empty;

    /// <summary>
    ///     Gets all matches.
    /// </summary>
    public IReadOnlyList<SearchMatch> Matches => _matches;

    /// <summary>
    ///     Gets the index of the currently selected match.
    /// </summary>
    public int CurrentMatchIndex { get; private set; } = -1;

    /// <summary>
    ///     Gets the total number of matches.
    /// </summary>
    public int MatchCount => _matches.Count;

    /// <summary>
    ///     Gets whether case-sensitive search is enabled.
    /// </summary>
    public bool IsCaseSensitive { get; private set; }

    /// <summary>
    ///     Gets whether regex search is enabled.
    /// </summary>
    public bool IsUsingRegex { get; private set; }

    /// <summary>
    ///     Starts a new search with the given query.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="outputLines">The console output lines to search through.</param>
    /// <param name="caseSensitive">Whether the search should be case-sensitive.</param>
    /// <param name="useRegex">Whether to treat the query as a regular expression.</param>
    public void StartSearch(
        string query,
        IReadOnlyList<string> outputLines,
        bool caseSensitive = false,
        bool useRegex = false
    )
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            ClearSearch();
            return;
        }

        SearchQuery = query;
        IsCaseSensitive = caseSensitive;
        IsUsingRegex = useRegex;
        _matches.Clear();
        CurrentMatchIndex = -1;

        // Perform the search
        SearchInOutput(outputLines);

        // Select first match if any
        if (_matches.Count > 0)
        {
            CurrentMatchIndex = 0;
        }
    }

    /// <summary>
    ///     Moves to the next match.
    /// </summary>
    /// <returns>True if moved to next match, false if no more matches.</returns>
    public bool NextMatch()
    {
        if (_matches.Count == 0)
        {
            return false;
        }

        CurrentMatchIndex = (CurrentMatchIndex + 1) % _matches.Count;
        return true;
    }

    /// <summary>
    ///     Moves to the previous match.
    /// </summary>
    /// <returns>True if moved to previous match, false if no more matches.</returns>
    public bool PreviousMatch()
    {
        if (_matches.Count == 0)
        {
            return false;
        }

        CurrentMatchIndex = CurrentMatchIndex <= 0 ? _matches.Count - 1 : CurrentMatchIndex - 1;
        return true;
    }

    /// <summary>
    ///     Gets the current match.
    /// </summary>
    public SearchMatch? GetCurrentMatch()
    {
        if (CurrentMatchIndex < 0 || CurrentMatchIndex >= _matches.Count)
        {
            return null;
        }

        return _matches[CurrentMatchIndex];
    }

    /// <summary>
    ///     Clears the current search.
    /// </summary>
    public void ClearSearch()
    {
        SearchQuery = string.Empty;
        _matches.Clear();
        CurrentMatchIndex = -1;
        IsCaseSensitive = false;
        IsUsingRegex = false;
    }

    /// <summary>
    ///     Performs the actual search through output lines.
    /// </summary>
    private void SearchInOutput(IReadOnlyList<string> outputLines)
    {
        for (int lineIndex = 0; lineIndex < outputLines.Count; lineIndex++)
        {
            string line = outputLines[lineIndex];

            if (IsUsingRegex)
            {
                SearchWithRegex(line, lineIndex);
            }
            else
            {
                SearchWithPlainText(line, lineIndex);
            }
        }
    }

    /// <summary>
    ///     Searches a line using plain text matching.
    /// </summary>
    private void SearchWithPlainText(string line, int lineIndex)
    {
        StringComparison comparison = IsCaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        int startIndex = 0;

        while (startIndex < line.Length)
        {
            int index = line.IndexOf(SearchQuery, startIndex, comparison);
            if (index == -1)
            {
                break;
            }

            _matches.Add(new SearchMatch(lineIndex, index, SearchQuery.Length));
            startIndex = index + 1; // Move past this match to find overlapping matches
        }
    }

    /// <summary>
    ///     Searches a line using regular expression matching.
    /// </summary>
    private void SearchWithRegex(string line, int lineIndex)
    {
        try
        {
            RegexOptions options = IsCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            var regex = new Regex(SearchQuery, options);
            MatchCollection matches = regex.Matches(line);

            foreach (Match match in matches)
            {
                _matches.Add(new SearchMatch(lineIndex, match.Index, match.Length));
            }
        }
        catch (ArgumentException)
        {
            // Invalid regex pattern - fall back to plain text search
            SearchWithPlainText(line, lineIndex);
        }
    }

    /// <summary>
    ///     Updates the search with new output lines (e.g., when output changes).
    /// </summary>
    public void UpdateSearch(IReadOnlyList<string> outputLines)
    {
        if (string.IsNullOrEmpty(SearchQuery))
        {
            return;
        }

        int previousMatchCount = _matches.Count;
        int previousMatchLineIndex = GetCurrentMatch()?.LineIndex ?? -1;

        // Re-run the search
        _matches.Clear();
        SearchInOutput(outputLines);

        // Try to maintain the current match position
        if (CurrentMatchIndex >= 0 && previousMatchLineIndex >= 0)
        {
            // Find a match on the same line or close to it
            for (int i = 0; i < _matches.Count; i++)
            {
                if (_matches[i].LineIndex >= previousMatchLineIndex)
                {
                    CurrentMatchIndex = i;
                    return;
                }
            }
        }

        // If we couldn't maintain position, select first match
        CurrentMatchIndex = _matches.Count > 0 ? 0 : -1;
    }

    /// <summary>
    ///     Represents a match in the console output.
    /// </summary>
    public record SearchMatch(int LineIndex, int StartColumn, int Length);
}
