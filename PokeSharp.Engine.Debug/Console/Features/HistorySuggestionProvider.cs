namespace PokeSharp.Engine.Debug.Console.Features;

/// <summary>
///     Provides command suggestions from history based on current input.
///     Implements smart ranking based on frequency and recency.
/// </summary>
public class HistorySuggestionProvider
{
    private const int MaxSuggestions = 5;
    private readonly ConsoleCommandHistory _history;

    public HistorySuggestionProvider(ConsoleCommandHistory history)
    {
        _history = history;
    }

    /// <summary>
    ///     Gets command suggestions from history that match the current input.
    /// </summary>
    /// <param name="currentInput">The current input text.</param>
    /// <param name="maxResults">Maximum number of suggestions to return.</param>
    /// <returns>List of matching history suggestions, ranked by relevance.</returns>
    public List<HistorySuggestion> GetSuggestions(
        string currentInput,
        int maxResults = MaxSuggestions
    )
    {
        if (string.IsNullOrWhiteSpace(currentInput))
        {
            return new List<HistorySuggestion>();
        }

        IReadOnlyList<string> allHistory = _history.GetAll();
        if (allHistory.Count == 0)
        {
            return new List<HistorySuggestion>();
        }

        // Filter and rank commands
        var matches = new List<(string command, double score, int useCount)>();
        Dictionary<string, int> commandFrequency = CountCommandFrequency(allHistory.ToList());

        foreach (string command in allHistory.Distinct())
        {
            // Skip exact matches (already typed)
            if (command.Equals(currentInput, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Check if command matches
            double matchScore = CalculateMatchScore(command, currentInput);
            if (matchScore > 0)
            {
                int useCount = commandFrequency.GetValueOrDefault(command, 1);
                double recencyBonus = CalculateRecencyBonus(allHistory.ToList(), command);
                double finalScore = matchScore + recencyBonus + (useCount * 0.1);

                matches.Add((command, finalScore, useCount));
            }
        }

        // Sort by score and take top results
        return matches
            .OrderByDescending(m => m.score)
            .Take(maxResults)
            .Select(m => new HistorySuggestion(m.command, m.useCount, 0))
            .ToList();
    }

    /// <summary>
    ///     Calculates how well a command matches the input.
    /// </summary>
    private double CalculateMatchScore(string command, string input)
    {
        // Case-insensitive matching
        string lowerCommand = command.ToLowerInvariant();
        string lowerInput = input.ToLowerInvariant();

        // Exact prefix match (highest score)
        if (lowerCommand.StartsWith(lowerInput))
        {
            return 100.0;
        }

        // Contains match (medium score)
        if (lowerCommand.Contains(lowerInput))
        {
            // Bonus if it's at word boundary
            if (IsAtWordBoundary(lowerCommand, lowerInput))
            {
                return 75.0;
            }

            return 50.0;
        }

        // Fuzzy match (lower score)
        if (IsFuzzyMatch(lowerCommand, lowerInput))
        {
            return 25.0;
        }

        return 0;
    }

    /// <summary>
    ///     Checks if the input appears at a word boundary in the command.
    /// </summary>
    private bool IsAtWordBoundary(string command, string input)
    {
        int index = command.IndexOf(input, StringComparison.Ordinal);
        if (index <= 0)
        {
            return true;
        }

        // Check if previous character is a word boundary (space, dot, parenthesis, etc.)
        char prevChar = command[index - 1];
        return prevChar == ' ' || prevChar == '.' || prevChar == '(' || prevChar == ')';
    }

    /// <summary>
    ///     Checks if the input characters appear in order in the command (fuzzy matching).
    /// </summary>
    private bool IsFuzzyMatch(string command, string input)
    {
        int inputIndex = 0;
        for (int i = 0; i < command.Length && inputIndex < input.Length; i++)
        {
            if (char.ToLowerInvariant(command[i]) == char.ToLowerInvariant(input[inputIndex]))
            {
                inputIndex++;
            }
        }

        return inputIndex == input.Length;
    }

    /// <summary>
    ///     Calculates a recency bonus for commands used recently.
    /// </summary>
    private double CalculateRecencyBonus(List<string> allHistory, string command)
    {
        // Find the most recent occurrence
        for (int i = allHistory.Count - 1; i >= 0; i--)
        {
            if (allHistory[i].Equals(command, StringComparison.OrdinalIgnoreCase))
            {
                // More recent = higher bonus
                int positionFromEnd = allHistory.Count - i;
                if (positionFromEnd <= 5)
                {
                    return 20.0; // Very recent
                }

                if (positionFromEnd <= 20)
                {
                    return 10.0; // Recent
                }

                if (positionFromEnd <= 50)
                {
                    return 5.0; // Somewhat recent
                }

                return 0;
            }
        }

        return 0;
    }

    /// <summary>
    ///     Counts how many times each command appears in history.
    /// </summary>
    private Dictionary<string, int> CountCommandFrequency(List<string> history)
    {
        var frequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (string command in history)
        {
            if (frequency.ContainsKey(command))
            {
                frequency[command]++;
            }
            else
            {
                frequency[command] = 1;
            }
        }

        return frequency;
    }

    /// <summary>
    ///     Gets the most frequently used commands.
    /// </summary>
    public List<HistorySuggestion> GetMostFrequentCommands(int maxResults = 10)
    {
        IReadOnlyList<string> allHistory = _history.GetAll();
        if (allHistory.Count == 0)
        {
            return new List<HistorySuggestion>();
        }

        Dictionary<string, int> frequency = CountCommandFrequency(allHistory.ToList());

        return frequency
            .OrderByDescending(kvp => kvp.Value)
            .Take(maxResults)
            .Select(kvp => new HistorySuggestion(kvp.Key, kvp.Value, 0))
            .ToList();
    }

    /// <summary>
    ///     Represents a history-based suggestion.
    /// </summary>
    public record HistorySuggestion(string Command, int UseCount, int DaysAgo);
}
