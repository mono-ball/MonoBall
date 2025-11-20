using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.Debug.Console.Configuration;
using PokeSharp.Engine.Debug.Console.Features;
using System.Collections.Generic;
using static PokeSharp.Engine.Debug.Console.Configuration.ConsoleColors;

namespace PokeSharp.Engine.Debug.Console.UI.Renderers;

/// <summary>
/// Handles rendering of search functionality (forward search and reverse-i-search).
/// Separated from QuakeConsole to follow Single Responsibility Principle.
/// </summary>
public class ConsoleSearchRenderer
{
    private readonly ConsoleFontRenderer _fontRenderer;
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;
    private float _screenWidth;

    public ConsoleSearchRenderer(ConsoleFontRenderer fontRenderer, SpriteBatch spriteBatch, Texture2D pixel, float screenWidth)
    {
        _fontRenderer = fontRenderer;
        _spriteBatch = spriteBatch;
        _pixel = pixel;
        _screenWidth = screenWidth;
    }

    /// <summary>
    /// Updates the screen width when window is resized.
    /// </summary>
    public void UpdateScreenSize(float screenWidth)
    {
        _screenWidth = screenWidth;
    }

    /// <summary>
    /// Draws search highlights for matching text in the output.
    /// </summary>
    public void DrawSearchHighlights(int outputY, int lineHeight, OutputSearcher searcher, int scrollOffset, IReadOnlyList<ConsoleLine> allLines, UI.ConsoleOutput output)
    {
        var currentMatchColor = Search_CurrentMatch; // Bright yellow/orange
        var matchColor = Search_OtherMatches; // Light blue
        var currentMatch = searcher.GetCurrentMatch();

        foreach (var match in searcher.Matches)
        {
            if (match.LineIndex >= allLines.Count)
                continue;

            // Convert absolute line index to effective line index (accounting for collapsed sections)
            int effectiveLineIndex = output.ConvertAbsoluteToEffectiveIndex(match.LineIndex);
            if (effectiveLineIndex < 0)
                continue; // Line is hidden in a collapsed section

            // Check if match is in visible area (using effective indices)
            int relativeLineIndex = effectiveLineIndex - scrollOffset;
            if (relativeLineIndex < 0 || relativeLineIndex >= ConsoleConstants.Limits.DefaultVisibleLines)
                continue;

            var lineText = allLines[match.LineIndex].Text;
            if (match.StartColumn + match.Length > lineText.Length)
                continue;

            // Measure text position
            var textBeforeMatch = lineText.Substring(0, match.StartColumn);
            var matchText = lineText.Substring(match.StartColumn, match.Length);
            var offsetSize = _fontRenderer.MeasureString(textBeforeMatch);
            var matchSize = _fontRenderer.MeasureString(matchText);

            int highlightX = ConsoleConstants.Rendering.Padding + (int)offsetSize.X;
            int highlightY = outputY + (relativeLineIndex * lineHeight);

            bool isCurrentMatch = currentMatch != null &&
                                 currentMatch.LineIndex == match.LineIndex &&
                                 currentMatch.StartColumn == match.StartColumn;

            var color = isCurrentMatch ? currentMatchColor : matchColor;
            DrawRectangle(highlightX, highlightY, (int)matchSize.X, lineHeight, color);
        }
    }

    /// <summary>
    /// Draws the forward search bar UI.
    /// </summary>
    public void DrawSearchBar(int consoleHeight, int lineHeight, string searchInput, OutputSearcher searcher)
    {
        // Calculate total height needed: help line + search bar
        int totalHeight = lineHeight * 2 + 10; // Two lines plus padding
        int searchBarY = consoleHeight - totalHeight - ConsoleConstants.Rendering.SearchBarBottomOffset;

        // Draw help text line at the top
        int helpY = searchBarY;
        DrawRectangle(0, helpY, (int)_screenWidth, lineHeight + 4, Background_Secondary);
        
        string helpText = "F3: Next  •  Shift+F3: Prev  •  Esc: Exit";
        var helpSize = _fontRenderer.MeasureString(helpText);
        int helpX = ((int)_screenWidth - (int)helpSize.X) / 2; // Center the help text
        _fontRenderer.DrawString(helpText, helpX, helpY + 2, Text_Tertiary);

        // Draw main search bar
        int mainBarY = helpY + lineHeight + 4;
        DrawRectangle(0, mainBarY, (int)_screenWidth, lineHeight + 6, Background_Elevated);

        int textY = mainBarY + 3;

        // Draw search prompt and input
        string prompt = "Search: ";
        _fontRenderer.DrawString(prompt, ConsoleConstants.Rendering.Padding, textY, Search_Prompt);
        var promptSize = _fontRenderer.MeasureString(prompt);
        int inputX = ConsoleConstants.Rendering.Padding + (int)promptSize.X;

        // Draw match count on the right
        string matchInfo = BuildSearchMatchInfo(searcher, searchInput);
        var matchInfoSize = _fontRenderer.MeasureString(matchInfo);
        int matchInfoX = (int)_screenWidth - ConsoleConstants.Rendering.Padding - (int)matchInfoSize.X;

        var matchColor = searcher.IsSearching ? Search_Success : Search_Disabled;
        _fontRenderer.DrawString(matchInfo, matchInfoX, textY, matchColor);

        // Calculate max width available for input (leave spacing between input and match info)
        int maxInputWidth = matchInfoX - inputX - 20; // 20 pixel spacing buffer
        string displayInput = searchInput + "_";

        // Truncate input if it's too long (show most recent characters)
        var inputSize = _fontRenderer.MeasureString(displayInput);
        if (inputSize.X > maxInputWidth)
        {
            string ellipsis = "...";
            
            // Start from the end and find how much we can fit
            for (int i = searchInput.Length - 1; i >= 0; i--)
            {
                string truncated = ellipsis + searchInput.Substring(i) + "_";
                if (_fontRenderer.MeasureString(truncated).X <= maxInputWidth)
                {
                    displayInput = truncated;
                    break;
                }
            }
        }

        _fontRenderer.DrawString(displayInput, inputX, textY, Text_Primary);
    }

    /// <summary>
    /// Draws the reverse-i-search bar UI.
    /// </summary>
    public void DrawReverseSearchBar(int consoleHeight, int lineHeight, string reverseSearchInput,
                                    List<string> matches, int matchIndex, string currentMatch)
    {
        // Calculate total height needed: help line + search bar + preview (if there's a match)
        int totalHeight = lineHeight * 2 + 10; // Two lines plus padding
        if (currentMatch != null)
        {
            totalHeight += lineHeight + 6; // Add preview height
        }

        int searchBarY = consoleHeight - totalHeight - ConsoleConstants.Rendering.SearchBarBottomOffset;

        // Draw help text line at the top
        int helpY = searchBarY;
        DrawRectangle(0, helpY, (int)_screenWidth, lineHeight + 4, Background_Secondary);
        
        string helpText = "Ctrl+R: Next  •  Ctrl+S: Prev  •  Enter: Accept  •  Esc: Cancel";
        var helpSize = _fontRenderer.MeasureString(helpText);
        int helpX = ((int)_screenWidth - (int)helpSize.X) / 2; // Center the help text
        _fontRenderer.DrawString(helpText, helpX, helpY + 2, Text_Tertiary);

        // Draw main search bar
        int mainBarY = helpY + lineHeight + 4;
        DrawRectangle(0, mainBarY, (int)_screenWidth, lineHeight + 6, Background_Elevated);

        int textY = mainBarY + 3;

        // Draw reverse-i-search prompt and input
        string prompt = "(reverse-i-search)` ";
        _fontRenderer.DrawString(prompt, ConsoleConstants.Rendering.Padding, textY, ReverseSearch_Prompt);
        var promptSize = _fontRenderer.MeasureString(prompt);
        int inputX = ConsoleConstants.Rendering.Padding + (int)promptSize.X;

        // Draw match count on the right
        string matchInfo = BuildReverseSearchMatchInfo(matches, matchIndex, reverseSearchInput);
        var matchInfoSize = _fontRenderer.MeasureString(matchInfo);
        int matchInfoX = (int)_screenWidth - ConsoleConstants.Rendering.Padding - (int)matchInfoSize.X;

        var matchColor = matches.Count > 0 ? Search_Success : Search_Disabled;
        _fontRenderer.DrawString(matchInfo, matchInfoX, textY, matchColor);

        // Calculate max width available for input (leave spacing between input and match info)
        int maxInputWidth = matchInfoX - inputX - 20; // 20 pixel spacing buffer
        string displayInput = reverseSearchInput + "_";

        // Truncate input if it's too long (show most recent characters)
        var inputSize = _fontRenderer.MeasureString(displayInput);
        if (inputSize.X > maxInputWidth)
        {
            string ellipsis = "...";
            
            // Start from the end and find how much we can fit
            for (int i = reverseSearchInput.Length - 1; i >= 0; i--)
            {
                string truncated = ellipsis + reverseSearchInput.Substring(i) + "_";
                if (_fontRenderer.MeasureString(truncated).X <= maxInputWidth)
                {
                    displayInput = truncated;
                    break;
                }
            }
        }

        _fontRenderer.DrawString(displayInput, inputX, textY, Text_Primary);

        // Show matched command preview below the search bar
        if (currentMatch != null)
        {
            int previewY = mainBarY + lineHeight + 6;
            DrawReverseSearchPreview(previewY, lineHeight, currentMatch, reverseSearchInput);
        }
    }

    private void DrawReverseSearchPreview(int previewY, int lineHeight, string match, string searchInput)
    {
        DrawRectangle(0, previewY, (int)_screenWidth, lineHeight + 6, Background_Secondary);

        int matchIndex = match.IndexOf(searchInput, System.StringComparison.OrdinalIgnoreCase);
        if (matchIndex >= 0)
        {
            // Highlight the matched portion
            var beforeMatch = match.Substring(0, matchIndex);
            var matchText = match.Substring(matchIndex, searchInput.Length);
            var afterMatch = match.Substring(matchIndex + searchInput.Length);

            int previewTextY = previewY + 3;
            int currentX = ConsoleConstants.Rendering.Padding;

            if (beforeMatch.Length > 0)
            {
                _fontRenderer.DrawString(beforeMatch, currentX, previewTextY, Text_Secondary);
                currentX += (int)_fontRenderer.MeasureString(beforeMatch).X;
            }

            _fontRenderer.DrawString(matchText, currentX, previewTextY, ReverseSearch_MatchHighlight);
            currentX += (int)_fontRenderer.MeasureString(matchText).X;

            if (afterMatch.Length > 0)
            {
                _fontRenderer.DrawString(afterMatch, currentX, previewTextY, Text_Secondary);
            }
        }
        else
        {
            _fontRenderer.DrawString(match, ConsoleConstants.Rendering.Padding, previewY + 3, Text_Secondary);
        }
    }

    private string BuildReverseSearchMatchInfo(List<string> matches, int matchIndex, string searchInput)
    {
        if (matches.Count > 0)
        {
            return $"[{matchIndex + 1}/{matches.Count}]";
        }
        else if (!string.IsNullOrEmpty(searchInput))
        {
            return "[No matches]";
        }
        else
        {
            return "[Type to search]";
        }
    }

    private string BuildSearchMatchInfo(OutputSearcher searcher, string searchInput)
    {
        if (searcher.IsSearching)
        {
            return $"[{searcher.CurrentMatchIndex + 1}/{searcher.MatchCount}]";
        }
        else if (!string.IsNullOrEmpty(searchInput))
        {
            return "[No matches]";
        }
        else
        {
            return "[Type to search]";
        }
    }

    private void DrawRectangle(int x, int y, int width, int height, Color color)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(x, y, width, height), color);
    }
}