using Microsoft.CodeAnalysis.Completion;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Debug.Console.Features;
using PokeSharp.Engine.Debug.Console.Scripting;
using PokeSharp.Engine.UI.Debug.Components.Controls;

namespace PokeSharp.Engine.Debug.Services;

/// <summary>
///     Provides auto-completion suggestions for console input.
/// </summary>
public class ConsoleCompletionProvider
{
    private readonly ILogger _logger;
    private ConsoleGlobals? _globals;

    public ConsoleCompletionProvider(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Sets the console globals for completion lookup.
    /// </summary>
    public void SetGlobals(ConsoleGlobals globals)
    {
        _globals = globals;
    }

    /// <summary>
    ///     Gets auto-completion suggestions for the given partial command.
    /// </summary>
    public async Task<List<SuggestionItem>> GetCompletionsAsync(
        string partialCommand,
        int cursorPosition
    )
    {
        try
        {
            if (_globals == null)
            {
                _logger.LogWarning("Console globals not initialized, cannot provide completions");
                return new List<SuggestionItem>();
            }

            // Use ConsoleAutoComplete for Roslyn-based completions
            var autoComplete = new ConsoleAutoComplete(_logger);
            autoComplete.SetGlobals(_globals);
            autoComplete.SetReferences(
                ConsoleScriptEvaluator.GetDefaultReferences(),
                ConsoleScriptEvaluator.GetDefaultImports()
            );

            List<CompletionItem> completionItems = await autoComplete.GetCompletionsAsync(
                partialCommand,
                cursorPosition
            );

            // Convert CompletionItem objects to SuggestionItem for the UI
            var suggestions = completionItems
                .Select(item => new SuggestionItem(
                    item.DisplayText,
                    item.InlineDescription,
                    item.Tags.Contains("history") ? "History" : "API",
                    item.Tags.Contains("history") ? Color.LightGreen : Color.LightBlue
                ))
                .ToList();

            return suggestions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting completions");
            return new List<SuggestionItem>();
        }
    }
}
