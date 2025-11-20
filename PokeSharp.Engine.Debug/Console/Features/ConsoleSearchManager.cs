using PokeSharp.Engine.Debug.Console.UI;
using System.Collections.Generic;
using System.Linq;

namespace PokeSharp.Engine.Debug.Console.Features;

/// <summary>
/// Manages console search functionality (forward search and reverse-i-search).
/// Extracted from QuakeConsole to follow Single Responsibility Principle.
/// </summary>
public class ConsoleSearchManager
{
    private readonly OutputSearcher _outputSearcher = new();

    // Forward search state
    private bool _isSearchMode = false;
    private string _searchInput = string.Empty;

    // Reverse-i-search state
    private bool _isReverseSearchMode = false;
    private string _reverseSearchInput = string.Empty;
    private List<string> _reverseSearchMatches = new();
    private int _reverseSearchIndex = 0;

    // Section folding state for search
    // Tracks sections that were auto-expanded for search results
    private readonly HashSet<string> _autoExpandedSections = new();
    private int? _lastSearchMatchLineIndex = null;

    /// <summary>
    /// Gets the output searcher instance.
    /// </summary>
    public OutputSearcher OutputSearcher => _outputSearcher;

    /// <summary>
    /// Gets whether forward search mode is active.
    /// </summary>
    public bool IsSearchMode => _isSearchMode;

    /// <summary>
    /// Gets the current forward search input.
    /// </summary>
    public string SearchInput => _searchInput;

    /// <summary>
    /// Gets whether reverse-i-search mode is active.
    /// </summary>
    public bool IsReverseSearchMode => _isReverseSearchMode;

    /// <summary>
    /// Gets the current reverse-i-search input.
    /// </summary>
    public string ReverseSearchInput => _reverseSearchInput;

    /// <summary>
    /// Gets the reverse search matches list.
    /// </summary>
    public List<string> ReverseSearchMatches => _reverseSearchMatches;

    /// <summary>
    /// Gets the current reverse search match index.
    /// </summary>
    public int ReverseSearchIndex => _reverseSearchIndex;

    /// <summary>
    /// Gets the current reverse-i-search match.
    /// </summary>
    public string? CurrentReverseSearchMatch =>
        _reverseSearchMatches.Count > 0 && _reverseSearchIndex < _reverseSearchMatches.Count
            ? _reverseSearchMatches[_reverseSearchIndex]
            : null;

    #region Forward Search

    /// <summary>
    /// Starts forward search mode.
    /// </summary>
    public void StartSearch()
    {
        _isSearchMode = true;
        _searchInput = string.Empty;
    }

    /// <summary>
    /// Exits forward search mode.
    /// </summary>
    public void ExitSearch(ConsoleOutput output)
    {
        _isSearchMode = false;
        _searchInput = string.Empty;
        _outputSearcher.ClearSearch();

        // Restore folding state for auto-expanded sections
        RestoreAutoExpandedSections(output);
    }

    /// <summary>
    /// Updates the forward search query.
    /// </summary>
    public void UpdateSearchQuery(string query, ConsoleOutput output)
    {
        _searchInput = query;
        var outputLines = output.GetAllLines().Select(line => line.Text).ToList();
        _outputSearcher.StartSearch(query, outputLines);

        // Scroll to the current match if found
        if (_outputSearcher.IsSearching)
        {
            var match = _outputSearcher.GetCurrentMatch();
            if (match != null)
            {
                NavigateToMatch(match.LineIndex, output);
            }
        }
    }

    /// <summary>
    /// Navigates to the next search match.
    /// </summary>
    public void NextSearchMatch(ConsoleOutput output)
    {
        if (!_outputSearcher.IsSearching)
            return;

        _outputSearcher.NextMatch();
        var match = _outputSearcher.GetCurrentMatch();
        if (match != null)
        {
            NavigateToMatch(match.LineIndex, output);
        }
    }

    /// <summary>
    /// Navigates to the previous search match.
    /// </summary>
    public void PreviousSearchMatch(ConsoleOutput output)
    {
        if (!_outputSearcher.IsSearching)
            return;

        _outputSearcher.PreviousMatch();
        var match = _outputSearcher.GetCurrentMatch();
        if (match != null)
        {
            NavigateToMatch(match.LineIndex, output);
        }
    }

    #endregion

    #region Reverse-i-search

    /// <summary>
    /// Starts reverse-i-search mode.
    /// </summary>
    public void StartReverseSearch()
    {
        _isReverseSearchMode = true;
        _reverseSearchInput = string.Empty;
        _reverseSearchMatches.Clear();
        _reverseSearchIndex = 0;
    }

    /// <summary>
    /// Exits reverse-i-search mode.
    /// </summary>
    public void ExitReverseSearch()
    {
        _isReverseSearchMode = false;
        _reverseSearchInput = string.Empty;
        _reverseSearchMatches.Clear();
        _reverseSearchIndex = 0;
    }

    /// <summary>
    /// Updates the reverse-i-search query and finds matches.
    /// </summary>
    public void UpdateReverseSearchQuery(string query, IEnumerable<string> historyCommands)
    {
        _reverseSearchInput = query;

        if (string.IsNullOrWhiteSpace(query))
        {
            _reverseSearchMatches.Clear();
            _reverseSearchIndex = 0;
            return;
        }

        // Find all matching commands (most recent first)
        _reverseSearchMatches = historyCommands
            .Where(cmd => cmd.Contains(query, System.StringComparison.OrdinalIgnoreCase))
            .Reverse()
            .ToList();

        _reverseSearchIndex = 0;
    }

    /// <summary>
    /// Moves to the next match in reverse-i-search.
    /// </summary>
    public void ReverseSearchNextMatch()
    {
        if (_reverseSearchMatches.Count == 0)
            return;

        _reverseSearchIndex = (_reverseSearchIndex + 1) % _reverseSearchMatches.Count;
    }

    /// <summary>
    /// Moves to the previous match in reverse-i-search.
    /// </summary>
    public void ReverseSearchPreviousMatch()
    {
        if (_reverseSearchMatches.Count == 0)
            return;

        _reverseSearchIndex = _reverseSearchIndex > 0
            ? _reverseSearchIndex - 1
            : _reverseSearchMatches.Count - 1;
    }

    /// <summary>
    /// Gets the current reverse-i-search match for accepting.
    /// </summary>
    public string? GetCurrentMatch()
    {
        return CurrentReverseSearchMatch;
    }

    #endregion

    #region Section Folding for Search

    /// <summary>
    /// Navigates to a search match, handling section expansion/collapse automatically.
    /// </summary>
    private void NavigateToMatch(int absoluteLineIndex, ConsoleOutput output)
    {
        // If navigating to a different match, collapse the previous auto-expanded section
        if (_lastSearchMatchLineIndex.HasValue && _lastSearchMatchLineIndex != absoluteLineIndex)
        {
            var prevSection = output.GetSectionContainingLine(_lastSearchMatchLineIndex.Value);
            if (prevSection != null && _autoExpandedSections.Contains(prevSection.Id))
            {
                output.CollapseSectionById(prevSection.Id);
                _autoExpandedSections.Remove(prevSection.Id);
            }
        }

        // Expand the section containing the new match if it's collapsed
        if (output.IsLineInCollapsedSection(absoluteLineIndex))
        {
            var expandedSection = output.ExpandSectionContainingLine(absoluteLineIndex);
            if (expandedSection != null)
            {
                _autoExpandedSections.Add(expandedSection.Id);
            }
        }

        // Update the last match line index (absolute)
        _lastSearchMatchLineIndex = absoluteLineIndex;

        // Convert absolute line index to effective line index (accounting for collapsed sections)
        int effectiveLineIndex = output.ConvertAbsoluteToEffectiveIndex(absoluteLineIndex);
        if (effectiveLineIndex >= 0)
        {
            // Scroll to the effective line position
            output.ScrollToLine(effectiveLineIndex);
        }
    }

    /// <summary>
    /// Restores the folding state for all auto-expanded sections.
    /// </summary>
    private void RestoreAutoExpandedSections(ConsoleOutput output)
    {
        foreach (var sectionId in _autoExpandedSections)
        {
            output.CollapseSectionById(sectionId);
        }
        _autoExpandedSections.Clear();
        _lastSearchMatchLineIndex = null;
    }

    #endregion
}