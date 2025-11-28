using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Components.Layout;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Input;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
///     Complete console panel combining text output, command input, and auto-completion.
///     This is the new UI framework version of the Quake console.
/// </summary>
public class ConsolePanel : Panel
{
    private readonly TextEditor _commandEditor;
    private readonly DocumentationPopup _documentationPopup;
    private readonly HintBar _hintBar;
    private readonly TextBuffer _outputBuffer;
    private readonly ParameterHintTooltip _parameterHints;
    private readonly SearchBar _searchBar;
    private readonly HintBar _searchHintBar;
    private readonly SuggestionsDropdown _suggestionsDropdown;

    // Console state
    private bool _closeRequested; // Defer close until after rendering
    private ConsoleSize _currentSize = ConsoleSize.Medium; // Default to 50% height

    // Other state flags (different concerns)
    private bool _isCompletingText; // Flag to prevent completion requests during completion

    // Overlay state (mutually exclusive modes)
    private ConsoleOverlayMode _overlayMode = ConsoleOverlayMode.None;
    private string _prompt = NerdFontIcons.Prompt; // Input prompt (changes for multi-line mode)
    private bool _wasVisible; // Track visibility changes for focus management

    /// <summary>
    ///     Creates a ConsolePanel with the specified components.
    ///     Use <see cref="ConsolePanelBuilder" /> to construct instances.
    /// </summary>
    internal ConsolePanel(
        TextBuffer outputBuffer,
        TextEditor commandEditor,
        SuggestionsDropdown suggestionsDropdown,
        HintBar hintBar,
        SearchBar searchBar,
        HintBar searchHintBar,
        ParameterHintTooltip parameterHints,
        DocumentationPopup documentationPopup,
        bool loadHistory
    )
    {
        _outputBuffer = outputBuffer;
        _commandEditor = commandEditor;
        _suggestionsDropdown = suggestionsDropdown;
        _hintBar = hintBar;
        _searchBar = searchBar;
        _searchHintBar = searchHintBar;
        _parameterHints = parameterHints;
        _documentationPopup = documentationPopup;

        Initialize(loadHistory);
    }

    // Layout constraints (from UITheme)
    private static float InputMinHeight => ThemeManager.Current.MinInputHeight; // Minimum height for input (1 line)
    private static float SuggestionsMaxHeight => ThemeManager.Current.MaxSuggestionsHeight;
    private static float Padding => ThemeManager.Current.ComponentGap;

    private static float ComponentSpacing => ThemeManager.Current.ComponentGap; // Semantic name for gaps between components

    private static float TooltipGap => ThemeManager.Current.TooltipGap; // Gap for tooltips above components
    private static float PanelEdgeGap => ThemeManager.Current.PanelEdgeGap; // Gap from panel edges

    // Events
    public Action<string>? OnCommandSubmitted { get; set; }
    public Action<string>? OnCommandChanged { get; set; }
    public Action<string>? OnRequestCompletions { get; set; }
    public Action<string, int>? OnRequestParameterHints { get; set; } // (text, cursorPos)
    public Action<string>? OnRequestDocumentation { get; set; } // (completionText)
    public Action? OnCloseRequested { get; set; }
    public Action<ConsoleSize>? OnSizeChanged { get; set; }

    /// <summary>
    ///     Initializes the console panel with common setup logic.
    /// </summary>
    private void Initialize(bool loadHistory)
    {
        Id = "console_panel";
        // Colors set dynamically in OnRenderContainer for theme switching
        BorderThickness = 1;

        // Add padding to the console panel - this creates the ContentRect for children
        Constraint.Padding = Padding;

        // Load command history from disk if enabled
        if (loadHistory)
        {
            _commandEditor.LoadHistoryFromDisk();
        }

        // Wire up events
        _commandEditor.OnSubmit += HandleCommandSubmit;
        _commandEditor.OnTextChanged += HandleTextChanged;
        _commandEditor.OnRequestCompletions = HandleRequestCompletions;
        _commandEditor.OnEscape = HandleEscape;

        _suggestionsDropdown.OnItemSelected = HandleSuggestionSelected;
        _suggestionsDropdown.OnCancelled = () => _overlayMode = ConsoleOverlayMode.None;

        _searchBar.OnSearchTextChanged = HandleSearchTextChanged;
        _searchBar.OnNextMatch = HandleNextMatch;
        _searchBar.OnPreviousMatch = HandlePreviousMatch;
        _searchBar.OnClose = HandleSearchClose;

        // Add children
        AddChild(_outputBuffer);
        AddChild(_commandEditor);
        AddChild(_suggestionsDropdown);
        AddChild(_hintBar);
        AddChild(_searchBar);
        AddChild(_searchHintBar);
        AddChild(_parameterHints);
        AddChild(_documentationPopup);
    }

    /// <summary>
    ///     Appends a line to the console output.
    /// </summary>
    public void AppendOutput(string text, Color color, string category = "General")
    {
        _outputBuffer.AppendLine(text, color, category);
    }

    /// <summary>
    ///     Clears all output.
    /// </summary>
    public void ClearOutput()
    {
        _outputBuffer.Clear();
    }

    /// <summary>
    ///     Clears command history from memory and disk.
    /// </summary>
    public void ClearHistory()
    {
        _commandEditor.ClearHistory();
    }

    /// <summary>
    ///     Gets the current console size.
    /// </summary>
    public ConsoleSize GetSize()
    {
        return _currentSize;
    }

    /// <summary>
    ///     Sets the console size to a specific preset.
    /// </summary>
    public void SetSize(ConsoleSize size)
    {
        _currentSize = size;
        OnSizeChanged?.Invoke(size);
    }

    /// <summary>
    ///     Sets parameter hints for the current method call.
    /// </summary>
    public void SetParameterHints(ParamHints? hints, int currentParameterIndex = 0)
    {
        _parameterHints.SetHints(hints, currentParameterIndex);
        _overlayMode =
            hints != null && hints.Overloads.Count > 0
                ? ConsoleOverlayMode.ParameterHints
                : ConsoleOverlayMode.None;
    }

    /// <summary>
    ///     Clears parameter hints.
    /// </summary>
    public void ClearParameterHints()
    {
        _parameterHints.Clear();
        if (_overlayMode == ConsoleOverlayMode.ParameterHints)
        {
            _overlayMode = ConsoleOverlayMode.None;
        }
    }

    /// <summary>
    ///     Cycles to the next parameter hint overload.
    /// </summary>
    public void NextParameterHintOverload()
    {
        _parameterHints.NextOverload();
    }

    /// <summary>
    ///     Cycles to the previous parameter hint overload.
    /// </summary>
    public void PreviousParameterHintOverload()
    {
        _parameterHints.PreviousOverload();
    }

    /// <summary>
    ///     Sets documentation for display.
    /// </summary>
    public void SetDocumentation(DocInfo doc)
    {
        _documentationPopup.SetDocumentation(doc);
        _overlayMode = ConsoleOverlayMode.Documentation;
    }

    /// <summary>
    ///     Clears documentation.
    /// </summary>
    public void ClearDocumentation()
    {
        _documentationPopup.Clear();
        if (_overlayMode == ConsoleOverlayMode.Documentation)
        {
            _overlayMode = ConsoleOverlayMode.None;
        }
    }

    /// <summary>
    ///     Cycles to the next larger console size.
    /// </summary>
    public void CycleSizeUp()
    {
        SetSize(_currentSize.Next());
    }

    /// <summary>
    ///     Cycles to the next smaller console size.
    /// </summary>
    public void CycleSizeDown()
    {
        SetSize(_currentSize.Previous());
    }

    /// <summary>
    ///     Gets the number of lines in the output buffer.
    /// </summary>
    public int GetOutputLineCount()
    {
        return _outputBuffer.TotalLines;
    }

    /// <summary>
    ///     Gets the current cursor position in the command editor.
    /// </summary>
    public int GetCursorPosition()
    {
        return _commandEditor.CursorPosition;
    }

    /// <summary>
    ///     Gets the command history.
    /// </summary>
    public IReadOnlyList<string> GetCommandHistory()
    {
        return _commandEditor.GetHistory();
    }

    /// <summary>
    ///     Clears the command history.
    /// </summary>
    public void ClearCommandHistory()
    {
        _commandEditor.ClearHistory();
    }

    /// <summary>
    ///     Saves the command history to disk.
    /// </summary>
    public void SaveCommandHistory()
    {
        _commandEditor.SaveHistoryToDisk();
    }

    /// <summary>
    ///     Loads the command history from disk.
    /// </summary>
    public void LoadCommandHistory()
    {
        _commandEditor.LoadHistoryFromDisk();
    }

    /// <summary>
    ///     Shows the console panel.
    /// </summary>
    public void Show()
    {
        Visible = true;
    }

    /// <summary>
    ///     Hides the console panel.
    /// </summary>
    public void Hide()
    {
        // Defer the close request until after rendering completes
        // to avoid modifying scene collection during Draw
        _closeRequested = true;
        Visible = false;
    }

    /// <summary>
    ///     Checks if a close was requested and triggers the OnCloseRequested event.
    ///     This should be called during Update, not Draw, to avoid scene collection modification during rendering.
    /// </summary>
    public void ProcessDeferredClose()
    {
        if (_closeRequested)
        {
            _closeRequested = false;
            OnCloseRequested?.Invoke();
        }
    }

    /// <summary>
    ///     Sets auto-completion suggestions.
    /// </summary>
    public void SetCompletions(List<string> completions)
    {
        // Don't show suggestions if we're currently completing text
        if (_isCompletingText)
        {
            return;
        }

        if (completions.Count > 0)
        {
            var suggestions = completions.Select(c => new SuggestionItem(c)).ToList();
            _suggestionsDropdown.SetItems(suggestions);
            // Clear any existing filter so all suggestions show
            _suggestionsDropdown.SetFilter(string.Empty);
            _overlayMode = ConsoleOverlayMode.Suggestions;
        }
        else
        {
            _overlayMode = ConsoleOverlayMode.None;
        }
    }

    /// <summary>
    ///     Sets auto-completion suggestions with descriptions.
    /// </summary>
    public void SetCompletions(List<SuggestionItem> suggestions)
    {
        // Don't show suggestions if we're currently completing text
        if (_isCompletingText)
        {
            return;
        }

        if (suggestions.Count > 0)
        {
            _suggestionsDropdown.SetItems(suggestions);
            // Clear any existing filter so all suggestions show
            _suggestionsDropdown.SetFilter(string.Empty);
            _overlayMode = ConsoleOverlayMode.Suggestions;
        }
        else
        {
            _overlayMode = ConsoleOverlayMode.None;
        }
    }

    /// <summary>
    ///     Focuses the command input.
    /// </summary>
    public void FocusInput()
    {
        // Input will auto-focus when panel is shown
    }

    private void HandleCommandSubmit(string command)
    {
        // Echo command to output
        AppendOutput($"{_prompt}{command}", ThemeManager.Current.Prompt);

        // Execute command
        OnCommandSubmitted?.Invoke(command);

        // Hide suggestions and command history search
        if (
            _overlayMode == ConsoleOverlayMode.Suggestions
            || _overlayMode == ConsoleOverlayMode.CommandHistorySearch
        )
        {
            _overlayMode = ConsoleOverlayMode.None;
        }
    }

    /// <summary>
    ///     Sets the input prompt (e.g., "> " for normal, "... " for multi-line mode).
    /// </summary>
    public void SetPrompt(string prompt)
    {
        _prompt = prompt;
    }

    private void HandleTextChanged(string text)
    {
        OnCommandChanged?.Invoke(text);

        // Don't handle text changes if we're currently completing text
        if (_isCompletingText)
        {
            return;
        }

        // Handle command history search mode
        if (_overlayMode == ConsoleOverlayMode.CommandHistorySearch)
        {
            // Re-filter history based on new text
            List<string> history = _commandEditor.GetHistory();
            FilterCommandHistory(history, text);
            return;
        }

        int cursorPos = _commandEditor.CursorPosition;

        // Request parameter hints if we detect a method call
        OnRequestParameterHints?.Invoke(text, cursorPos);

        // If suggestions are visible, filter them based on the current partial word at cursor
        if (_overlayMode == ConsoleOverlayMode.Suggestions && _suggestionsDropdown.HasItems)
        {
            // Extract the partial word at the cursor position for filtering
            string partialWord = ExtractPartialWordAtCursor(text, cursorPos);

            _suggestionsDropdown.SetFilter(partialWord);

            // Hide suggestions if no items match the filter
            if (!_suggestionsDropdown.HasItems)
            {
                _overlayMode = ConsoleOverlayMode.None;
            }
        }
    }

    /// <summary>
    ///     Extracts the partial word at the cursor position for autocomplete filtering.
    ///     Walks backward from cursor to find the word boundary.
    /// </summary>
    private string ExtractPartialWordAtCursor(string text, int cursorPos)
    {
        if (string.IsNullOrEmpty(text) || cursorPos == 0)
        {
            return string.Empty;
        }

        // Find word start by walking backward from cursor
        int wordStart = cursorPos;
        while (wordStart > 0)
        {
            char c = text[wordStart - 1];
            // Stop at word boundaries
            if (
                char.IsWhiteSpace(c)
                || c == '('
                || c == ')'
                || c == '['
                || c == ']'
                || c == '{'
                || c == '}'
                || c == ','
                || c == ';'
                || c == '='
                || c == '.'
            )
            {
                break;
            }

            wordStart--;
        }

        // Extract the partial word from word start to cursor
        return text.Substring(wordStart, cursorPos - wordStart);
    }

    private void HandleRequestCompletions(string text)
    {
        OnRequestCompletions?.Invoke(text);
    }

    private void HandleEscape()
    {
        // Priority: Search > Documentation > Suggestions > (scene handles close)
        if (_overlayMode == ConsoleOverlayMode.Search)
        {
            _overlayMode = ConsoleOverlayMode.None;
            _outputBuffer.ClearSearch();
            _searchBar.Clear();
        }
        else if (_overlayMode == ConsoleOverlayMode.Documentation)
        {
            ClearDocumentation();
        }
        else if (
            _overlayMode == ConsoleOverlayMode.Suggestions
            || _overlayMode == ConsoleOverlayMode.CommandHistorySearch
        )
        {
            _overlayMode = ConsoleOverlayMode.None;
            _suggestionsDropdown.Clear();
        }
        else
        {
            // No overlay to dismiss - fire close request
            // The scene will decide if we should actually close (e.g., only if on Console tab)
            OnCloseRequested?.Invoke();
        }
    }

    private void ToggleSearch()
    {
        if (_overlayMode == ConsoleOverlayMode.Search)
        {
            _overlayMode = ConsoleOverlayMode.None;
        }
        else
        {
            _overlayMode = ConsoleOverlayMode.Search;
        }

        if (_overlayMode == ConsoleOverlayMode.Search)
        {
            // Focus search bar
            Context?.SetFocus(_searchBar.Id);
        }
        else
        {
            _searchBar.Clear();
        }
    }

    private void HandleSearchTextChanged(string searchText)
    {
        // If search text is empty or whitespace, clear the search
        if (string.IsNullOrWhiteSpace(searchText))
        {
            _outputBuffer.ClearSearch();
            _searchBar.TotalMatches = 0;
            _searchBar.CurrentMatchIndex = 0;
        }
        else
        {
            int matchCount = _outputBuffer.Search(searchText);
            _searchBar.TotalMatches = matchCount;
            _searchBar.CurrentMatchIndex = _outputBuffer.GetCurrentSearchMatchIndex();
        }
    }

    private void HandleNextMatch()
    {
        _outputBuffer.FindNext();
        _searchBar.CurrentMatchIndex = _outputBuffer.GetCurrentSearchMatchIndex();
    }

    private void HandlePreviousMatch()
    {
        _outputBuffer.FindPrevious();
        _searchBar.CurrentMatchIndex = _outputBuffer.GetCurrentSearchMatchIndex();
    }

    private void HandleSearchClose()
    {
        if (_overlayMode == ConsoleOverlayMode.Search)
        {
            _overlayMode = ConsoleOverlayMode.None;
        }

        _outputBuffer.ClearSearch();
        _searchBar.Clear();
    }

    private void ToggleCommandHistorySearch()
    {
        if (_overlayMode == ConsoleOverlayMode.CommandHistorySearch)
        {
            // Exit command history search mode
            _overlayMode = ConsoleOverlayMode.None;
            _suggestionsDropdown.Clear();
        }
        else
        {
            // Enter command history search mode
            _overlayMode = ConsoleOverlayMode.CommandHistorySearch;

            // Get all history and populate suggestions
            List<string> history = _commandEditor.GetHistory();
            FilterCommandHistory(history, _commandEditor.Text);

            // Focus stays on command editor
            Context?.SetFocus(_commandEditor.Id);
        }
    }

    private void FilterCommandHistory(List<string> history, string filterText)
    {
        // Filter by substring match (case-insensitive)
        var filtered = history
            .Where(cmd =>
                string.IsNullOrWhiteSpace(filterText)
                || cmd.Contains(filterText, StringComparison.OrdinalIgnoreCase)
            )
            .Reverse() // Most recent first
            .Select(cmd => new SuggestionItem(cmd, "from history", "Command"))
            .ToList();

        _suggestionsDropdown.SetItems(filtered);

        if (filtered.Count > 0)
        {
            _suggestionsDropdown.SelectedIndex = 0;
        }
    }

    private void HandleSuggestionSelected(SuggestionItem item)
    {
        // Handle command history search mode
        if (_overlayMode == ConsoleOverlayMode.CommandHistorySearch)
        {
            // Insert selected command into input
            _commandEditor.SetText(item.Text);
            _commandEditor.MoveCursorToEnd();

            // Exit search mode
            _overlayMode = ConsoleOverlayMode.None;
            _suggestionsDropdown.Clear();
            return;
        }

        // CRITICAL: Set flags to prevent re-requesting completions
        _overlayMode = ConsoleOverlayMode.None;
        _isCompletingText = true;

        try
        {
            // Clear the suggestions dropdown completely
            _suggestionsDropdown.Clear();

            // Complete the text
            _commandEditor.CompleteText(item.Text);
        }
        finally
        {
            // Always reset the flag, even if an exception occurs
            _isCompletingText = false;
        }
    }

    protected override void OnRenderContainer(UIContext context)
    {
        // Update colors from current theme for dynamic theme switching
        BackgroundColor = ThemeManager.Current.ConsoleBackground;
        BorderColor = ThemeManager.Current.BorderPrimary;

        float inputHeight,
            hintHeight;

        if (_overlayMode == ConsoleOverlayMode.Search)
        {
            // Search mode: Hide input/hint, show search/search-hint
            _commandEditor.Constraint.Height = 0; // Hide input
            _hintBar.Constraint.Height = 0; // Hide input hint
            _commandEditor.Visible = false;
            _hintBar.Visible = false;

            // Update search hint bar
            string searchHintText = "[Enter]/[F3] next | [Shift+F3] previous | [Esc] close";
            _searchHintBar.SetText(searchHintText);

            // Calculate search heights
            inputHeight = InputMinHeight; // Search bar uses fixed height
            hintHeight = _searchHintBar.GetDesiredHeight(context.Renderer);

            // Position search hint bar at absolute bottom
            _searchHintBar.Constraint.Height = hintHeight;
            _searchHintBar.Constraint.OffsetY = 0;
            _searchHintBar.Visible = true;

            // Position search bar above hint bar
            _searchBar.Constraint.Height = inputHeight;
            _searchBar.Constraint.OffsetY = -hintHeight;
            _searchBar.Visible = true;
        }
        else
        {
            // Normal mode: Show input/hint, hide search/search-hint
            _searchBar.Constraint.Height = 0; // Hide search
            _searchHintBar.Constraint.Height = 0; // Hide search hint
            _searchBar.Visible = false;
            _searchHintBar.Visible = false;
            _commandEditor.Visible = true;
            _hintBar.Visible = true;

            // Update hint bar based on mode and editor state
            if (_overlayMode == ConsoleOverlayMode.CommandHistorySearch)
            {
                _hintBar.SetText(ConsoleShortcuts.GetHistorySearchModeHints());
            }
            else if (_overlayMode == ConsoleOverlayMode.Suggestions)
            {
                _hintBar.SetText(ConsoleShortcuts.GetSuggestionsModeHints());
            }
            else if (_commandEditor.IsMultiLine)
            {
                _hintBar.SetText(ConsoleShortcuts.GetMultiLineModeHints(_commandEditor.LineCount));
            }
            else
            {
                _hintBar.SetText(ConsoleShortcuts.GetNormalModeHints());
            }

            // Calculate dynamic heights
            inputHeight = _commandEditor.GetDesiredHeight(context.Renderer);
            hintHeight = _hintBar.GetDesiredHeight(context.Renderer);

            // Position hint bar at absolute bottom
            _hintBar.Constraint.Height = hintHeight;
            _hintBar.Constraint.OffsetY = 0; // At the very bottom

            // Position input editor above hint bar
            _commandEditor.Constraint.Height = inputHeight;
            _commandEditor.Constraint.OffsetY = -hintHeight; // Moves up by hint height
        }

        // Update suggestions dropdown offset to stay above the input and hints
        _suggestionsDropdown.Constraint.OffsetY = -(inputHeight + hintHeight + ComponentSpacing);

        // Update parameter hints tooltip
        if (_overlayMode == ConsoleOverlayMode.ParameterHints && _parameterHints.HasContent)
        {
            float hintTooltipHeight = _parameterHints.GetDesiredHeight(context.Renderer);
            float hintTooltipWidth = _parameterHints.GetDesiredWidth(context.Renderer);

            _parameterHints.Constraint.Height = hintTooltipHeight;
            _parameterHints.Constraint.Width = hintTooltipWidth;
            // Position bottom of tooltip above the input bar with proper gap
            _parameterHints.Constraint.OffsetY = -(inputHeight + hintHeight + TooltipGap);
            _parameterHints.Constraint.OffsetX = Padding; // Align with left edge of content
        }
        else
        {
            _parameterHints.Constraint.Height = 0; // Hide when not active
        }

        // Update documentation popup
        if (_overlayMode == ConsoleOverlayMode.Documentation && _documentationPopup.HasContent)
        {
            float docHeight = _documentationPopup.GetDesiredHeight(context.Renderer);
            _documentationPopup.Constraint.Height = docHeight;
        }
        else
        {
            _documentationPopup.Constraint.Height = 0; // Hide when not active
        }

        // Calculate layout heights based on the actual constraint padding
        float paddingTop = Constraint.GetPaddingTop();
        float paddingBottom = Constraint.GetPaddingBottom();
        float contentHeight = Rect.Height - paddingTop - paddingBottom; // Use actual constraint padding, not static Padding property
        float outputHeight = contentHeight - inputHeight - hintHeight - ComponentSpacing; // Leave space for input + hints + gap

        // Update output buffer height
        _outputBuffer.Constraint.Height = outputHeight;

        // Handle input for suggestions navigation BEFORE rendering
        InputState? input = context.Input;
        if (input != null)
        {
            // Handle Ctrl+F - Open output search
            if (input.IsCtrlDown() && input.IsKeyPressed(Keys.F))
            {
                ToggleSearch();
                input.ConsumeKey(Keys.F);
            }

            // Handle Ctrl+R - Toggle command history search
            if (input.IsCtrlDown() && input.IsKeyPressed(Keys.R))
            {
                ToggleCommandHistorySearch();
                input.ConsumeKey(Keys.R);
            }

            // Handle F1 - Request documentation for selected suggestion
            if (input.IsKeyPressed(Keys.F1) && _overlayMode == ConsoleOverlayMode.Suggestions)
            {
                SuggestionItem? selectedItem = _suggestionsDropdown.SelectedItem;
                if (selectedItem != null)
                {
                    OnRequestDocumentation?.Invoke(selectedItem.Text);
                    input.ConsumeKey(Keys.F1);
                }
            }

            // Handle Ctrl+Up/Down - Cycle console size OR parameter hint overloads
            if (input.IsCtrlDown() && input.IsKeyPressed(Keys.Up))
            {
                // If parameter hints are showing, cycle through overloads
                if (_overlayMode == ConsoleOverlayMode.ParameterHints && _parameterHints.HasContent)
                {
                    PreviousParameterHintOverload();
                    input.ConsumeKey(Keys.Up);
                }
                else
                {
                    CycleSizeUp();
                    input.ConsumeKey(Keys.Up);
                }
            }
            else if (input.IsCtrlDown() && input.IsKeyPressed(Keys.Down))
            {
                // If parameter hints are showing, cycle through overloads
                if (_overlayMode == ConsoleOverlayMode.ParameterHints && _parameterHints.HasContent)
                {
                    NextParameterHintOverload();
                    input.ConsumeKey(Keys.Down);
                }
                else
                {
                    CycleSizeDown();
                    input.ConsumeKey(Keys.Down);
                }
            }

            // Handle special keyboard shortcuts
            if (input.IsKeyPressed(Keys.Escape))
            {
                HandleEscape();
                input.ConsumeKey(Keys.Escape); // Consume so child components don't also handle it
            }

            // Handle suggestions keyboard navigation when visible
            if (
                _overlayMode == ConsoleOverlayMode.Suggestions
                || _overlayMode == ConsoleOverlayMode.CommandHistorySearch
            )
            {
                // Up/Down with repeat for smooth navigation
                if (input.IsKeyPressedWithRepeat(Keys.Down))
                {
                    _suggestionsDropdown.SelectNext();
                    input.ConsumeKey(Keys.Down); // Consume to prevent command history navigation
                }
                else if (input.IsKeyPressedWithRepeat(Keys.Up))
                {
                    _suggestionsDropdown.SelectPrevious();
                    input.ConsumeKey(Keys.Up); // Consume to prevent command history navigation
                }
                // Enter - accept suggestion if one is selected
                else if (input.IsKeyPressed(Keys.Enter))
                {
                    // Only consume Enter if we have a valid suggestion to accept
                    if (_suggestionsDropdown.SelectedItem != null)
                    {
                        _suggestionsDropdown.AcceptSelected();
                        input.ConsumeKey(Keys.Enter); // Consume to prevent command submission
                    }
                    // Don't consume - let CommandInput handle it normally
                }
            }
        }

        // Draw panel background with Y offset applied
        // NOTE: For now, we draw without offset since we can't easily transform children
        // Animation is functional but visual transform needs SpriteBatch.Begin with Matrix transform
        // which would require UIRenderer enhancement
        base.OnRenderContainer(context);

        // Auto-focus management - only set focus on visibility change (not every frame)
        if (Visible && !_wasVisible)
        {
            // Console just became visible - set initial focus
            if (_overlayMode == ConsoleOverlayMode.Search)
            {
                context.SetFocus(_searchBar.Id);
            }
            else
            {
                context.SetFocus(_commandEditor.Id);
            }
        }

        _wasVisible = Visible;

        // Don't render suggestions dropdown unless visible
        if (
            _overlayMode != ConsoleOverlayMode.Suggestions
            && _overlayMode != ConsoleOverlayMode.CommandHistorySearch
        )
        {
            // Remove suggestions from rendering
            _suggestionsDropdown.Constraint.Height = 0;
        }
        else
        {
            // Calculate suggestions height using the dropdown's item height
            int itemCount = Math.Min(
                _suggestionsDropdown.MaxVisibleItems,
                _suggestionsDropdown.ItemCount
            );
            int calculatedHeight = (itemCount * (int)_suggestionsDropdown.ItemHeight) + 20; // Item height + padding
            _suggestionsDropdown.Constraint.Height = calculatedHeight;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Export Methods
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Exports all console output to a string.
    /// </summary>
    public string ExportOutputToString()
    {
        return _outputBuffer.ExportToString();
    }

    /// <summary>
    ///     Copies all console output to clipboard.
    /// </summary>
    public void CopyOutputToClipboard()
    {
        _outputBuffer.CopyAllToClipboard();
    }

    /// <summary>
    ///     Copies current selection (if any) or all output to clipboard.
    /// </summary>
    public void CopyToClipboard()
    {
        if (_outputBuffer.HasSelection)
        {
            _outputBuffer.CopySelectionToClipboard();
        }
        else
        {
            _outputBuffer.CopyAllToClipboard();
        }
    }

    /// <summary>
    ///     Gets output statistics.
    /// </summary>
    public (int TotalLines, int FilteredLines) GetOutputStats()
    {
        return (_outputBuffer.TotalLineCount, _outputBuffer.FilteredLineCount);
    }
}
