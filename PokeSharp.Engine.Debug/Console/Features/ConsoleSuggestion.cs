using Microsoft.CodeAnalysis.Completion;

namespace PokeSharp.Engine.Debug.Console.Features;

/// <summary>
///     Represents a unified suggestion that can be either from API completion or command history.
/// </summary>
public class ConsoleSuggestion
{
    /// <summary>
    ///     Type of suggestion.
    /// </summary>
    public enum SuggestionType
    {
        /// <summary>
        ///     API/code completion from Roslyn (methods, properties, etc.)
        /// </summary>
        ApiCompletion,

        /// <summary>
        ///     Command from history.
        /// </summary>
        History,
    }

    private ConsoleSuggestion()
    {
        DisplayText = string.Empty;
        InsertText = string.Empty;
    }

    /// <summary>
    ///     Gets the type of this suggestion.
    /// </summary>
    public SuggestionType Type { get; init; }

    /// <summary>
    ///     Gets the display text for this suggestion.
    /// </summary>
    public string DisplayText { get; init; }

    /// <summary>
    ///     Gets the text to insert when this suggestion is accepted.
    /// </summary>
    public string InsertText { get; init; }

    /// <summary>
    ///     Gets the description/documentation for this suggestion.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    ///     Gets the underlying CompletionItem for API completions (null for history).
    /// </summary>
    public CompletionItem? CompletionItem { get; init; }

    /// <summary>
    ///     Gets the use count for history suggestions (0 for API).
    /// </summary>
    public int UseCount { get; init; }

    /// <summary>
    ///     Gets the relevance score for sorting (higher = more relevant).
    /// </summary>
    public double RelevanceScore { get; init; }

    /// <summary>
    ///     Creates a suggestion from an API completion item.
    /// </summary>
    public static ConsoleSuggestion FromApiCompletion(CompletionItem item, double score = 100)
    {
        return new ConsoleSuggestion
        {
            Type = SuggestionType.ApiCompletion,
            DisplayText = item.DisplayText,
            InsertText = item.DisplayText,
            Description = item.InlineDescription,
            CompletionItem = item,
            UseCount = 0,
            RelevanceScore = score,
        };
    }

    /// <summary>
    ///     Creates a suggestion from command history.
    /// </summary>
    public static ConsoleSuggestion FromHistory(string command, int useCount, double score)
    {
        return new ConsoleSuggestion
        {
            Type = SuggestionType.History,
            DisplayText = command,
            InsertText = command,
            Description = $"Used {useCount} time{(useCount != 1 ? "s" : "")}",
            CompletionItem = null,
            UseCount = useCount,
            RelevanceScore = score,
        };
    }

    /// <summary>
    ///     Gets a visual prefix for display (icon/indicator).
    ///     Note: Triangles are now rendered programmatically, not as text characters.
    /// </summary>
    public string GetVisualPrefix()
    {
        return Type switch
        {
            SuggestionType.History => "@", // At-sign for history
            _ => string.Empty, // No text prefix (triangles rendered separately if needed)
        };
    }
}
