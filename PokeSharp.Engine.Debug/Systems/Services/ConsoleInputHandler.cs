using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Debug.Console.Configuration;
using PokeSharp.Engine.Debug.Console.Features;
using PokeSharp.Engine.Debug.Console.UI;
using TextCopy;
using static PokeSharp.Engine.Debug.Console.Configuration.ConsoleColors;

namespace PokeSharp.Engine.Debug.Systems.Services;

/// <summary>
/// Handles keyboard and mouse input for the console.
/// Extracted from ConsoleSystem to follow Single Responsibility Principle.
/// </summary>
public class ConsoleInputHandler : IConsoleInputHandler
{
    private readonly QuakeConsole _console;
    private readonly ConsoleCommandHistory _history;
    private readonly ConsoleHistoryPersistence _persistence;
    private readonly ILogger _logger;
    private readonly IConsoleAutoCompleteCoordinator? _autoCompleteCoordinator;
    private readonly DocumentationProvider _documentationProvider;
    private readonly ParameterHintProvider? _parameterHintProvider;
    private readonly BookmarkedCommandsManager? _bookmarksManager;

    // Key repeat tracking for smooth editing
    private Keys? _lastHeldKey;
    private float _keyHoldTime;
    private float _lastKeyRepeatTime;
    private const float InitialKeyRepeatDelay = ConsoleConstants.Input.InitialKeyRepeatDelay;
    private const float KeyRepeatInterval = ConsoleConstants.Input.KeyRepeatInterval;

    // Key repeat tracking for suggestion scrolling (separate from text input)
    private Keys? _lastHeldScrollKey;
    private float _scrollKeyHoldTime;
    private float _lastScrollKeyRepeatTime;

    // Key repeat tracking for suggestion navigation (Up/Down)
    private Keys? _lastHeldNavKey;
    private float _navKeyHoldTime;
    private float _lastNavKeyRepeatTime;

    // Key repeat tracking for page scrolling (PageUp/PageDown)
    private Keys? _lastHeldPageKey;
    private float _pageKeyHoldTime;
    private float _lastPageKeyRepeatTime;

    // Key repeat tracking for word navigation (Ctrl+Left/Right)
    private Keys? _lastHeldWordNavKey;
    private float _wordNavKeyHoldTime;
    private float _lastWordNavKeyRepeatTime;

    // Key repeat tracking for word deletion (Ctrl+Backspace/Delete)
    private Keys? _lastHeldDeleteWordKey;
    private float _deleteWordKeyHoldTime;
    private float _lastDeleteWordKeyRepeatTime;

    // Key repeat tracking for search navigation (F3/Shift+F3)
    private Keys? _lastHeldSearchNavKey;
    private float _searchNavKeyHoldTime;
    private float _lastSearchNavKeyRepeatTime;

    // Key repeat tracking for font size changes (Ctrl+Plus/Minus)
    private Keys? _lastHeldFontSizeKey;
    private float _fontSizeKeyHoldTime;
    private float _lastFontSizeKeyRepeatTime;

    // Key repeat tracking for reverse-i-search navigation (Ctrl+R/S)
    private Keys? _lastHeldReverseSearchNavKey;
    private float _reverseSearchNavKeyHoldTime;
    private float _lastReverseSearchNavKeyRepeatTime;

    // Key repeat tracking for parameter hint overload cycling (Ctrl+Shift+Up/Down)
    private Keys? _lastHeldParamHintKey;
    private float _paramHintKeyHoldTime;
    private float _lastParamHintKeyRepeatTime;

    // Key repeat tracking for undo/redo (Ctrl+Z/Y)
    private Keys? _lastHeldUndoRedoKey;
    private float _undoRedoKeyHoldTime;
    private float _lastUndoRedoKeyRepeatTime;

    // Output area text selection state
    private bool _isOutputDragging = false;
    private Point _outputDragStartPosition;
    private Point _outputLastDragPosition;
    private int _outputDragStartLine = -1;
    private int _outputDragStartColumn = -1;

    // Debouncing for filtering to prevent excessive operations
    private string _lastFilteredText = "";
    private const float FilterDebounceDelay = 0.05f; // 50ms debounce

    // Mouse click detection (immediate processing on button press)
    // No need for time window - clicks are processed instantly on button press

    // Mouse drag selection state
    private bool _isDragging = false;
    private Point _dragStartPosition;
    private int _dragStartCharPosition = -1;
    private Point _lastDragPosition;

    /// <summary>
    /// Initializes a new instance of the ConsoleInputHandler.
    /// </summary>
    public ConsoleInputHandler(
        QuakeConsole console,
        ConsoleCommandHistory history,
        ConsoleHistoryPersistence persistence,
        ILogger logger,
        IConsoleAutoCompleteCoordinator? autoCompleteCoordinator = null,
        ParameterHintProvider? parameterHintProvider = null,
        BookmarkedCommandsManager? bookmarksManager = null)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _autoCompleteCoordinator = autoCompleteCoordinator;
        _parameterHintProvider = parameterHintProvider;
        _bookmarksManager = bookmarksManager;
        _documentationProvider = new DocumentationProvider();
    }

    /// <summary>
    /// Handles input for the current frame.
    /// </summary>
    public InputHandlingResult HandleInput(
        float deltaTime,
        KeyboardState keyboardState,
        KeyboardState previousKeyboardState,
        MouseState mouseState,
        MouseState previousMouseState)
    {
        var isShiftPressed = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);

        // Note: Backtick toggle is handled by ConsoleScene directly
        // Don't handle it here to avoid double-processing

        // Only process console input if visible
        if (!_console.IsVisible)
        {
            return InputHandlingResult.None;
        }

        // Update mouse position for hover effects
        _console.UpdateMousePosition(new Point(mouseState.X, mouseState.Y));

        // Handle mouse clicks on console output (for section toggling)
        var mouseResult = HandleMouseInput(mouseState, previousMouseState, deltaTime);
        if (mouseResult != InputHandlingResult.None)
            return mouseResult;

        // Handle Enter key - accept reverse-i-search OR suggestion OR execute command (not with Shift for multi-line)
        if (WasKeyJustPressed(Keys.Enter, keyboardState, previousKeyboardState) && !isShiftPressed)
        {
            // Check if reverse-i-search is active
            if (_console.IsReverseSearchMode)
            {
                _console.AcceptReverseSearchMatch();
                _logger.LogInformation("Accepted reverse-i-search match");
                // Re-evaluate parameter hints for the accepted command
                UpdateParameterHints();
                return InputHandlingResult.Consumed;
            }

            // Check if auto-complete suggestions are visible
            if (_console.HasSuggestions())
            {
                // Accept the selected suggestion
                var suggestion = _console.GetSelectedSuggestion();
                if (suggestion != null)
                {
                    _logger.LogInformation("Enter pressed - accepting auto-complete suggestion: {Suggestion}", suggestion);
                    InsertCompletion(suggestion);
                }
                _console.ClearAutoCompleteSuggestions();
                _lastFilteredText = ""; // Reset debounce tracking after accepting
                return InputHandlingResult.Consumed;
            }

            // No suggestions, execute command normally
            // Save history before executing
            if (_console.Config.PersistHistory)
            {
                _persistence.SaveHistory(_history.GetAll());
            }

            var command = _console.GetInputText();
            _console.ClearAutoCompleteSuggestions();
            _console.ClearParameterHints(); // Clear parameter hints when executing command
            return InputHandlingResult.Execute(command);
        }

        // Handle Tab - accept auto-complete suggestion OR trigger if none shown
        if (WasKeyJustPressed(Keys.Tab, keyboardState, previousKeyboardState) && _console.Config.AutoCompleteEnabled)
        {
            _logger.LogInformation("Tab key pressed! Auto-complete enabled: {Enabled}", _console.Config.AutoCompleteEnabled);

            var suggestion = _console.GetSelectedSuggestion();
            _logger.LogInformation("Current selected suggestion: {Suggestion}", suggestion ?? "null");

            if (suggestion != null)
            {
                // Accept selected suggestion - insert it intelligently
                _logger.LogInformation("Accepting suggestion: {Suggestion}", suggestion);
                InsertCompletion(suggestion);
                _console.ClearAutoCompleteSuggestions();
                _lastFilteredText = ""; // Reset debounce tracking after accepting
            }
            else
            {
                // No suggestions shown, trigger auto-complete
                _logger.LogInformation("No suggestions, triggering auto-complete...");
                return InputHandlingResult.TriggerAutoComplete();
            }
            return InputHandlingResult.Consumed;
        }

        // Handle Ctrl/Cmd key combinations
        bool isCtrlPressed = keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);
        bool isCmdPressed = keyboardState.IsKeyDown(Keys.LeftWindows) || keyboardState.IsKeyDown(Keys.RightWindows);

        // Handle Ctrl/Cmd+A - select all
        if (WasKeyJustPressed(Keys.A, keyboardState, previousKeyboardState) && (isCtrlPressed || isCmdPressed))
        {
            _console.Input.SelectAll();
            _logger.LogInformation("Select all triggered");
            return InputHandlingResult.Consumed;
        }

        // Handle Ctrl/Cmd+C - copy (from whichever area has a selection)
        if (WasKeyJustPressed(Keys.C, keyboardState, previousKeyboardState) && (isCtrlPressed || isCmdPressed))
        {
            // Try input field first, then output (only one can be active)
            if (_console.Input.HasSelection)
            {
                try
                {
                    var selectedText = _console.Input.SelectedText;
                    ClipboardService.SetText(selectedText);
                    _logger.LogInformation("Copied {Length} characters to clipboard", selectedText.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to copy to clipboard");
                    _console.AppendOutput("Failed to copy to clipboard", Output_Warning);
                }
            }
            else if (_console.Output.HasOutputSelection)
            {
                try
                {
                    var selectedText = _console.Output.GetSelectedOutputText();
                    if (!string.IsNullOrEmpty(selectedText))
                    {
                        ClipboardService.SetText(selectedText);
                        _logger.LogInformation("Copied {Length} characters to clipboard", selectedText.Length);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to copy to clipboard");
                    _console.AppendOutput("Failed to copy to clipboard", Output_Warning);
                }
            }
            return InputHandlingResult.Consumed;
        }

        // Handle Ctrl/Cmd+X - cut
        if (WasKeyJustPressed(Keys.X, keyboardState, previousKeyboardState) && (isCtrlPressed || isCmdPressed))
        {
            if (_console.Input.HasSelection)
            {
                try
                {
                    var selectedText = _console.Input.SelectedText;
                    ClipboardService.SetText(selectedText);
                    _console.Input.DeleteSelection();
                    _logger.LogInformation("Cut {Length} characters to clipboard", selectedText.Length);

                    // Clear suggestions after cut
                    if (_console.HasSuggestions())
                    {
                        _console.ClearAutoCompleteSuggestions();
                        _lastFilteredText = "";
                    }

                    // Re-evaluate parameter hints after cut
                    UpdateParameterHints();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cut to clipboard");
                    _console.AppendOutput("Failed to cut to clipboard", Output_Warning);
                }
            }
            return InputHandlingResult.Consumed;
        }

        // Handle Ctrl/Cmd+V - paste from clipboard
        if (WasKeyJustPressed(Keys.V, keyboardState, previousKeyboardState) && (isCtrlPressed || isCmdPressed))
        {
            try
            {
                var clipboardText = ClipboardService.GetText();
                if (!string.IsNullOrEmpty(clipboardText))
                {
                    _logger.LogInformation("Pasting {Length} characters from clipboard", clipboardText.Length);
                    _console.Input.InsertText(clipboardText);

                    // Clear suggestions after paste
                    if (_console.HasSuggestions())
                    {
                        _console.ClearAutoCompleteSuggestions();
                        _lastFilteredText = ""; // Reset debounce tracking
                    }

                    // Re-evaluate parameter hints after paste
                    UpdateParameterHints();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to paste from clipboard");
                _console.AppendOutput("Failed to paste from clipboard", Output_Warning);
            }
            return InputHandlingResult.Consumed;
        }

        // Handle Ctrl+Space - explicitly trigger auto-complete
        if (WasKeyJustPressed(Keys.Space, keyboardState, previousKeyboardState) &&
            (keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl)) &&
            _console.Config.AutoCompleteEnabled)
        {
            return InputHandlingResult.TriggerAutoComplete();
        }

        // Handle font size changes with key repeat
        var fontSizeResult = HandleFontSizeChanges(keyboardState, previousKeyboardState, deltaTime, isShiftPressed, isCtrlPressed, isCmdPressed);
        if (fontSizeResult != InputHandlingResult.None)
            return fontSizeResult;

        // Handle Ctrl/Cmd+Z/Y with key repeat - undo/redo
        var undoRedoResult = HandleUndoRedo(keyboardState, previousKeyboardState, deltaTime, isCtrlPressed, isCmdPressed, isShiftPressed);
        if (undoRedoResult != InputHandlingResult.None)
            return undoRedoResult;

        // Handle Ctrl/Cmd+F - open search mode
        if (WasKeyJustPressed(Keys.F, keyboardState, previousKeyboardState) &&
            (isCtrlPressed || isCmdPressed))
        {
            _console.StartSearch();
            _logger.LogInformation("Search mode activated");
            return InputHandlingResult.Consumed;
        }

        // Handle Ctrl/Cmd+R - open reverse-i-search mode
        if (WasKeyJustPressed(Keys.R, keyboardState, previousKeyboardState) &&
            (isCtrlPressed || isCmdPressed) && !_console.IsReverseSearchMode)
        {
            _console.StartReverseSearch();
            _logger.LogInformation("Reverse-i-search mode activated");
            return InputHandlingResult.Consumed;
        }

        // Handle Ctrl/Cmd+R/S in reverse-i-search with key repeat - cycle through matches
        var reverseSearchNavResult = HandleReverseSearchNavigation(keyboardState, previousKeyboardState, deltaTime, isCtrlPressed, isCmdPressed);
        if (reverseSearchNavResult != InputHandlingResult.None)
            return reverseSearchNavResult;

        // Handle function keys (F1-F12) with key repeat for F3
        var functionKeyResult = HandleFunctionKeys(keyboardState, previousKeyboardState, deltaTime, isShiftPressed);
        if (functionKeyResult != InputHandlingResult.None)
            return functionKeyResult;

        // Handle Escape - close documentation OR suggestions OR exit search modes OR close console
        // Handle Ctrl+Shift+Up/Down - cycle through parameter hint overloads
        bool isCtrlShiftPressed = (keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl)) &&
                                   (keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift));

        // Handle Ctrl+Shift+Up/Down with key repeat - cycle parameter hint overloads
        var paramHintResult = HandleParameterHintNavigation(keyboardState, previousKeyboardState, deltaTime, isCtrlShiftPressed);
        if (paramHintResult != InputHandlingResult.None)
            return paramHintResult;

        if (WasKeyJustPressed(Keys.Escape, keyboardState, previousKeyboardState))
        {
            // Check if parameter hints are showing
            if (_console.HasParameterHints())
            {
                _console.ClearParameterHints();
                _logger.LogDebug("Closed parameter hints");
                return InputHandlingResult.Consumed;
            }

            // Check if documentation is showing
            if (_console.IsShowingDocumentation)
            {
                _console.HideDocumentation();
                _logger.LogDebug("Closed documentation popup");
                return InputHandlingResult.Consumed;
            }

            // Check if reverse-i-search mode is active
            if (_console.IsReverseSearchMode)
            {
                _console.ExitReverseSearch();
                _logger.LogInformation("Escape pressed - exiting reverse-i-search mode");
                return InputHandlingResult.Consumed;
            }

            // Check if search mode is active
            if (_console.IsSearchMode)
            {
                _console.ExitSearch();
                _logger.LogInformation("Escape pressed - exiting search mode");
                return InputHandlingResult.Consumed;
            }

            // Check if auto-complete suggestions are visible
            if (_console.HasSuggestions())
            {
                // Just close the suggestions, don't close the console
                _logger.LogInformation("Escape pressed - closing auto-complete suggestions");
                _console.ClearAutoCompleteSuggestions();
                _lastFilteredText = ""; // Reset debounce tracking
                return InputHandlingResult.Consumed;
            }

            // No suggestions, close the console (scene will handle popping itself)
            // Save history when closing
            if (_console.Config.PersistHistory)
            {
                _persistence.SaveHistory(_history.GetAll());
            }

            return InputHandlingResult.CloseConsole();
        }

        // Handle PageUp/PageDown with key repeat - scroll suggestions OR scroll output
        var pageScrollResult = HandlePageScrolling(keyboardState, previousKeyboardState, deltaTime);
        if (pageScrollResult != InputHandlingResult.None)
            return pageScrollResult;

        // Handle Ctrl+Home - scroll to top
        if (WasKeyJustPressed(Keys.Home, keyboardState, previousKeyboardState) &&
            (keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl)))
        {
            _console.Output.ScrollToTop();
            return InputHandlingResult.Consumed;
        }

        // Handle Ctrl+End - scroll to bottom
        if (WasKeyJustPressed(Keys.End, keyboardState, previousKeyboardState) &&
            (keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl)))
        {
            _console.Output.ScrollToBottom();
            return InputHandlingResult.Consumed;
        }

        // Handle Ctrl/Cmd+Left/Right with key repeat - word navigation
        var wordNavResult = HandleWordNavigation(keyboardState, previousKeyboardState, deltaTime, isCtrlPressed, isCmdPressed, isShiftPressed);
        if (wordNavResult != InputHandlingResult.None)
            return wordNavResult;

        // Handle Ctrl+Backspace/Delete with key repeat - delete words
        var deleteWordResult = HandleWordDeletion(keyboardState, previousKeyboardState, deltaTime, isCtrlPressed, isCmdPressed);
        if (deleteWordResult != InputHandlingResult.None)
            return deleteWordResult;

        // Handle Alt+[ and Alt+] - Collapse/Expand all sections
        var isAltPressed = keyboardState.IsKeyDown(Keys.LeftAlt) || keyboardState.IsKeyDown(Keys.RightAlt);
        if (isAltPressed)
        {
            // Alt+[ - Collapse all sections
            if (WasKeyJustPressed(Keys.OemOpenBrackets, keyboardState, previousKeyboardState))
            {
                _console.Output.CollapseAllSections();
                _logger.LogDebug("Collapsed all sections");
                return InputHandlingResult.Consumed;
            }

            // Alt+] - Expand all sections
            if (WasKeyJustPressed(Keys.OemCloseBrackets, keyboardState, previousKeyboardState))
            {
                _console.Output.ExpandAllSections();
                _logger.LogDebug("Expanded all sections");
                return InputHandlingResult.Consumed;
            }
        }

        // Handle Mouse Wheel - scroll output
        int scrollDelta = mouseState.ScrollWheelValue - previousMouseState.ScrollWheelValue;
        if (scrollDelta != 0)
        {
            int scrollLines = scrollDelta / ConsoleConstants.Limits.MouseWheelUnitsPerNotch * ConsoleConstants.Limits.ScrollLinesPerNotch;
            if (scrollLines > 0)
            {
                for (int i = 0; i < scrollLines; i++)
                    _console.Output.ScrollUp();
            }
            else if (scrollLines < 0)
            {
                for (int i = 0; i < -scrollLines; i++)
                    _console.Output.ScrollDown();
            }
            return InputHandlingResult.Consumed;
        }

        // Handle Up/Down arrow with key repeat for suggestion navigation OR command history
        // This consumes the input if suggestions are visible
        var navResult = HandleSuggestionNavigation(keyboardState, previousKeyboardState, deltaTime);
        if (navResult == InputHandlingResult.Consumed)
        {
            return InputHandlingResult.Consumed;
        }

        // Handle Left/Right arrow with key repeat for suggestion scrolling
        // This consumes the input if suggestions are visible
        var scrollResult = HandleSuggestionScrolling(keyboardState, previousKeyboardState, deltaTime);
        if (scrollResult == InputHandlingResult.Consumed)
        {
            return InputHandlingResult.Consumed;
        }

        // Handle text input with key repeat
        HandleTextInput(keyboardState, previousKeyboardState, deltaTime);

        return InputHandlingResult.None;
    }

    /// <summary>
    /// Handles text input with key repeat support.
    /// </summary>
    private void HandleTextInput(KeyboardState keyboardState, KeyboardState previousKeyboardState, float deltaTime)
    {
        var pressedKeys = keyboardState.GetPressedKeys();

        foreach (var key in pressedKeys)
        {
            if (!ShouldProcessKey(key, previousKeyboardState, deltaTime))
                continue;

            bool isShiftPressed = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
            char? character = KeyToCharConverter.ToChar(key, isShiftPressed);

            ProcessKeyPress(key, character, isShiftPressed);
        }

        // Reset key repeat if no keys are pressed
        if (pressedKeys.Length == 0)
        {
            ResetKeyRepeatState();
        }
    }

    /// <summary>
    /// Determines if a key should be processed based on key repeat timing.
    /// </summary>
    private bool ShouldProcessKey(Keys key, KeyboardState previousKeyboardState, float deltaTime)
    {
        // Check if key was just pressed
        if (!previousKeyboardState.IsKeyDown(key))
        {
            _lastHeldKey = key;
            _keyHoldTime = 0;
            _lastKeyRepeatTime = 0;
            return true;
        }

        // Check for key repeat
        if (_lastHeldKey != key)
            return false;

        _keyHoldTime += deltaTime;

        // Start repeating after initial delay
        if (_keyHoldTime < InitialKeyRepeatDelay)
            return false;

        _lastKeyRepeatTime += deltaTime;

        // Repeat at interval
        if (_lastKeyRepeatTime < KeyRepeatInterval)
            return false;

        _lastKeyRepeatTime = 0;
        return true;
    }

    /// <summary>
    /// Processes a single key press and routes to the appropriate handler.
    /// </summary>
    private void ProcessKeyPress(Keys key, char? character, bool isShiftPressed)
    {
        // Handle reverse-i-search mode input
        if (_console.IsReverseSearchMode)
        {
            HandleReverseSearchModeInput(key, character);
            return;
        }

        // Handle search mode input
        if (_console.IsSearchMode)
        {
            HandleSearchModeInput(key, character);
            return;
        }

        // Normal input handling
        HandleNormalModeInput(key, character, isShiftPressed);
    }

    /// <summary>
    /// Handles input in normal mode (not search or reverse-i-search).
    /// </summary>
    private void HandleNormalModeInput(Keys key, char? character, bool isShiftPressed)
    {
        _console.Input.HandleKeyPress(key, character, isShiftPressed);

        // Notify auto-complete coordinator that user is typing
        if (character.HasValue && !char.IsControl(character.Value))
        {
            _autoCompleteCoordinator?.NotifyTyping();
        }

        UpdateParameterHintsForKeyPress(key, character);
        UpdateAutoCompleteSuggestionsForKeyPress(key, character);
    }

    /// <summary>
    /// Updates parameter hints based on the key press.
    /// </summary>
    private void UpdateParameterHintsForKeyPress(Keys key, char? character)
    {
        if (!character.HasValue)
        {
            if (key == Keys.Back || key == Keys.Delete)
                UpdateParameterHints();
            return;
        }

        // Update parameter hints when typing '(' or ','
        if (character.Value == '(' || character.Value == ',')
        {
            UpdateParameterHints();
        }
        // Clear parameter hints when typing ')'
        else if (character.Value == ')')
        {
            _console.ClearParameterHints();
        }
    }

    /// <summary>
    /// Updates auto-complete suggestions based on the key press.
    /// </summary>
    private void UpdateAutoCompleteSuggestionsForKeyPress(Keys key, char? character)
    {
        if (!_console.HasSuggestions())
            return;

        // Clear suggestions on certain keys
        if (ShouldClearSuggestions(key))
        {
            _console.ClearAutoCompleteSuggestions();
            _lastFilteredText = "";
            return;
        }

        // Filter suggestions as user types
        if (character.HasValue && !char.IsControl(character.Value))
        {
            var currentText = _console.GetInputText();

            // Only filter if text has actually changed (debounce check)
            if (currentText != _lastFilteredText)
            {
                _console.FilterSuggestions(currentText);
                _lastFilteredText = currentText;
            }
        }
    }

    /// <summary>
    /// Resets the key repeat tracking state.
    /// </summary>
    private void ResetKeyRepeatState()
    {
        _lastHeldKey = null;
        _keyHoldTime = 0;
        _lastKeyRepeatTime = 0;
    }

    /// <summary>
    /// Handles left/right arrow scrolling in autocomplete suggestions with key repeat support.
    /// </summary>
    /// <returns>InputHandlingResult indicating if the input was consumed.</returns>
    /// <summary>
    /// Handles Up/Down arrow navigation through suggestions with key repeat support.
    /// Also handles command history navigation when no suggestions are visible.
    /// </summary>
    private InputHandlingResult HandleSuggestionNavigation(KeyboardState keyboardState, KeyboardState previousKeyboardState, float deltaTime)
    {
        // Don't handle navigation in search modes - let them handle their own input
        if (_console.IsReverseSearchMode || _console.IsSearchMode)
        {
            return InputHandlingResult.None;
        }

        // Don't handle if in multi-line mode - let arrow keys navigate within the text
        if (_console.Input.IsMultiLine)
        {
            return InputHandlingResult.None;
        }

        Keys? currentNavKey = null;

        // Determine which navigation key is pressed
        if (keyboardState.IsKeyDown(Keys.Up))
            currentNavKey = Keys.Up;
        else if (keyboardState.IsKeyDown(Keys.Down))
            currentNavKey = Keys.Down;

        if (currentNavKey.HasValue)
        {
            bool shouldNavigate = false;

            // Check if key was just pressed
            if (!previousKeyboardState.IsKeyDown(currentNavKey.Value))
            {
                _lastHeldNavKey = currentNavKey.Value;
                _navKeyHoldTime = 0;
                _lastNavKeyRepeatTime = 0;
                shouldNavigate = true;
            }
            // Check for key repeat
            else if (_lastHeldNavKey == currentNavKey.Value)
            {
                _navKeyHoldTime += deltaTime;

                // Start repeating after initial delay
                if (_navKeyHoldTime >= InitialKeyRepeatDelay)
                {
                    _lastNavKeyRepeatTime += deltaTime;

                    // Repeat at interval
                    if (_lastNavKeyRepeatTime >= KeyRepeatInterval)
                    {
                        shouldNavigate = true;
                        _lastNavKeyRepeatTime = 0;
                    }
                }
            }

            if (shouldNavigate)
            {
                if (_console.HasSuggestions())
                {
                    // Navigate auto-complete suggestions
                    if (currentNavKey.Value == Keys.Up)
                    {
                        _console.NavigateSuggestions(up: true);
                    }
                    else if (currentNavKey.Value == Keys.Down)
                    {
                        _console.NavigateSuggestions(up: false);
                    }
                }
                else
                {
                    // Navigate command history
                    if (currentNavKey.Value == Keys.Up)
                    {
                        var prevCommand = _history.NavigateUp();
                        if (prevCommand != null)
                        {
                            _console.Input.SetText(prevCommand);
                            // Re-evaluate parameter hints for the new command
                            UpdateParameterHints();
                        }
                    }
                    else if (currentNavKey.Value == Keys.Down)
                    {
                        var nextCommand = _history.NavigateDown();
                        if (nextCommand != null)
                        {
                            _console.Input.SetText(nextCommand);
                            // Re-evaluate parameter hints for the new command
                            UpdateParameterHints();
                        }
                    }
                }
            }

            // Consume the input to prevent other actions
            return InputHandlingResult.Consumed;
        }
        else
        {
            // Reset navigation key repeat if no nav keys are pressed
            _lastHeldNavKey = null;
            _navKeyHoldTime = 0;
            _lastNavKeyRepeatTime = 0;
            return InputHandlingResult.None;
        }
    }

    private InputHandlingResult HandleSuggestionScrolling(KeyboardState keyboardState, KeyboardState previousKeyboardState, float deltaTime)
    {
        // Don't handle scrolling in search modes - let them handle their own input
        if (_console.IsReverseSearchMode || _console.IsSearchMode)
        {
            return InputHandlingResult.None;
        }

        // Only handle if suggestions are visible
        if (!_console.HasSuggestions())
        {
            // Reset scroll key repeat state when no suggestions
            _lastHeldScrollKey = null;
            _scrollKeyHoldTime = 0;
            _lastScrollKeyRepeatTime = 0;
            return InputHandlingResult.None;
        }

        Keys? currentScrollKey = null;

        // Determine which scroll key is pressed
        if (keyboardState.IsKeyDown(Keys.Left))
            currentScrollKey = Keys.Left;
        else if (keyboardState.IsKeyDown(Keys.Right))
            currentScrollKey = Keys.Right;

        if (currentScrollKey.HasValue)
        {
            bool shouldScroll = false;

            // Check if key was just pressed
            if (!previousKeyboardState.IsKeyDown(currentScrollKey.Value))
            {
                _lastHeldScrollKey = currentScrollKey.Value;
                _scrollKeyHoldTime = 0;
                _lastScrollKeyRepeatTime = 0;
                shouldScroll = true;
            }
            // Check for key repeat
            else if (_lastHeldScrollKey == currentScrollKey.Value)
            {
                _scrollKeyHoldTime += deltaTime;

                // Start repeating after initial delay
                if (_scrollKeyHoldTime >= InitialKeyRepeatDelay)
                {
                    _lastScrollKeyRepeatTime += deltaTime;

                    // Repeat at interval
                    if (_lastScrollKeyRepeatTime >= KeyRepeatInterval)
                    {
                        shouldScroll = true;
                        _lastScrollKeyRepeatTime = 0;
                    }
                }
            }

            if (shouldScroll)
            {
                if (currentScrollKey.Value == Keys.Left)
                {
                    _console.ScrollSuggestionRight(); // Scroll right (show more of beginning)
                }
                else if (currentScrollKey.Value == Keys.Right)
                {
                    _console.ScrollSuggestionLeft(); // Scroll left (show more of end)
                }
            }

            // Consume the input to prevent cursor movement in input field
            return InputHandlingResult.Consumed;
        }
        else
        {
            // Reset scroll key repeat if no scroll keys are pressed
            _lastHeldScrollKey = null;
            _scrollKeyHoldTime = 0;
            _lastScrollKeyRepeatTime = 0;
            return InputHandlingResult.None;
        }
    }

    /// <summary>
    /// Handles PageUp/PageDown with key repeat support for scrolling output or suggestions.
    /// </summary>
    private InputHandlingResult HandlePageScrolling(KeyboardState keyboardState, KeyboardState previousKeyboardState, float deltaTime)
    {
        Keys? currentPageKey = null;

        // Determine which page key is pressed
        if (keyboardState.IsKeyDown(Keys.PageUp))
            currentPageKey = Keys.PageUp;
        else if (keyboardState.IsKeyDown(Keys.PageDown))
            currentPageKey = Keys.PageDown;

        if (currentPageKey.HasValue)
        {
            bool shouldScroll = false;

            // Check if key was just pressed
            if (!previousKeyboardState.IsKeyDown(currentPageKey.Value))
            {
                _lastHeldPageKey = currentPageKey.Value;
                _pageKeyHoldTime = 0;
                _lastPageKeyRepeatTime = 0;
                shouldScroll = true;
            }
            // Check for key repeat
            else if (_lastHeldPageKey == currentPageKey.Value)
            {
                _pageKeyHoldTime += deltaTime;

                if (_pageKeyHoldTime >= InitialKeyRepeatDelay)
                {
                    _lastPageKeyRepeatTime += deltaTime;

                    if (_lastPageKeyRepeatTime >= KeyRepeatInterval)
                    {
                        shouldScroll = true;
                        _lastPageKeyRepeatTime = 0;
                    }
                }
            }

            if (shouldScroll)
            {
                if (_console.HasSuggestions())
                {
                    // Scroll suggestions
                    _console.ScrollSuggestions(currentPageKey.Value == Keys.PageUp ? -5 : 5);
                }
                else
                {
                    // Scroll output
                    if (currentPageKey.Value == Keys.PageUp)
                        _console.Output.PageUp();
                    else
                        _console.Output.PageDown();
                }
            }

            return InputHandlingResult.Consumed;
        }
        else
        {
            // Reset state
            _lastHeldPageKey = null;
            _pageKeyHoldTime = 0;
            _lastPageKeyRepeatTime = 0;
            return InputHandlingResult.None;
        }
    }

    /// <summary>
    /// Handles Ctrl+Left/Right with key repeat support for word navigation.
    /// </summary>
    private InputHandlingResult HandleWordNavigation(KeyboardState keyboardState, KeyboardState previousKeyboardState, 
                                                     float deltaTime, bool isCtrlPressed, bool isCmdPressed, bool isShiftPressed)
    {
        if (!isCtrlPressed && !isCmdPressed)
            return InputHandlingResult.None;

        Keys? currentWordNavKey = null;

        if (keyboardState.IsKeyDown(Keys.Left))
            currentWordNavKey = Keys.Left;
        else if (keyboardState.IsKeyDown(Keys.Right))
            currentWordNavKey = Keys.Right;

        if (currentWordNavKey.HasValue)
        {
            bool shouldNavigate = false;

            if (!previousKeyboardState.IsKeyDown(currentWordNavKey.Value))
            {
                _lastHeldWordNavKey = currentWordNavKey.Value;
                _wordNavKeyHoldTime = 0;
                _lastWordNavKeyRepeatTime = 0;
                shouldNavigate = true;
            }
            else if (_lastHeldWordNavKey == currentWordNavKey.Value)
            {
                _wordNavKeyHoldTime += deltaTime;

                if (_wordNavKeyHoldTime >= InitialKeyRepeatDelay)
                {
                    _lastWordNavKeyRepeatTime += deltaTime;

                    if (_lastWordNavKeyRepeatTime >= KeyRepeatInterval)
                    {
                        shouldNavigate = true;
                        _lastWordNavKeyRepeatTime = 0;
                    }
                }
            }

            if (shouldNavigate)
            {
                if (currentWordNavKey.Value == Keys.Left)
                    _console.Input.MoveToPreviousWord(extendSelection: isShiftPressed);
                else
                    _console.Input.MoveToNextWord(extendSelection: isShiftPressed);
            }

            return InputHandlingResult.Consumed;
        }
        else
        {
            _lastHeldWordNavKey = null;
            _wordNavKeyHoldTime = 0;
            _lastWordNavKeyRepeatTime = 0;
            return InputHandlingResult.None;
        }
    }

    /// <summary>
    /// Handles Ctrl+Backspace/Delete with key repeat support for word deletion.
    /// </summary>
    private InputHandlingResult HandleWordDeletion(KeyboardState keyboardState, KeyboardState previousKeyboardState,
                                                   float deltaTime, bool isCtrlPressed, bool isCmdPressed)
    {
        if (!isCtrlPressed && !isCmdPressed)
            return InputHandlingResult.None;

        Keys? currentDeleteWordKey = null;

        if (keyboardState.IsKeyDown(Keys.Back))
            currentDeleteWordKey = Keys.Back;
        else if (keyboardState.IsKeyDown(Keys.Delete))
            currentDeleteWordKey = Keys.Delete;

        if (currentDeleteWordKey.HasValue)
        {
            bool shouldDelete = false;

            if (!previousKeyboardState.IsKeyDown(currentDeleteWordKey.Value))
            {
                _lastHeldDeleteWordKey = currentDeleteWordKey.Value;
                _deleteWordKeyHoldTime = 0;
                _lastDeleteWordKeyRepeatTime = 0;
                shouldDelete = true;
            }
            else if (_lastHeldDeleteWordKey == currentDeleteWordKey.Value)
            {
                _deleteWordKeyHoldTime += deltaTime;

                if (_deleteWordKeyHoldTime >= InitialKeyRepeatDelay)
                {
                    _lastDeleteWordKeyRepeatTime += deltaTime;

                    if (_lastDeleteWordKeyRepeatTime >= KeyRepeatInterval)
                    {
                        shouldDelete = true;
                        _lastDeleteWordKeyRepeatTime = 0;
                    }
                }
            }

            if (shouldDelete)
            {
                if (currentDeleteWordKey.Value == Keys.Back)
                    _console.Input.DeleteWordBackward();
                else
                    _console.Input.DeleteWordForward();

                // Clear suggestions if present
                if (_console.HasSuggestions())
                {
                    _console.ClearAutoCompleteSuggestions();
                    _lastFilteredText = "";
                }

                // Re-evaluate parameter hints after deletion
                UpdateParameterHints();
            }

            return InputHandlingResult.Consumed;
        }
        else
        {
            _lastHeldDeleteWordKey = null;
            _deleteWordKeyHoldTime = 0;
            _lastDeleteWordKeyRepeatTime = 0;
            return InputHandlingResult.None;
        }
    }

    /// <summary>
    /// Handles Ctrl+R/S in reverse-i-search with key repeat support.
    /// </summary>
    private InputHandlingResult HandleReverseSearchNavigation(KeyboardState keyboardState, KeyboardState previousKeyboardState,
                                                              float deltaTime, bool isCtrlPressed, bool isCmdPressed)
    {
        if (!_console.IsReverseSearchMode || (!isCtrlPressed && !isCmdPressed))
            return InputHandlingResult.None;

        Keys? currentReverseSearchNavKey = null;

        if (keyboardState.IsKeyDown(Keys.R))
            currentReverseSearchNavKey = Keys.R;
        else if (keyboardState.IsKeyDown(Keys.S))
            currentReverseSearchNavKey = Keys.S;

        if (currentReverseSearchNavKey.HasValue)
        {
            bool shouldNavigate = false;

            if (!previousKeyboardState.IsKeyDown(currentReverseSearchNavKey.Value))
            {
                _lastHeldReverseSearchNavKey = currentReverseSearchNavKey.Value;
                _reverseSearchNavKeyHoldTime = 0;
                _lastReverseSearchNavKeyRepeatTime = 0;
                shouldNavigate = true;
            }
            else if (_lastHeldReverseSearchNavKey == currentReverseSearchNavKey.Value)
            {
                _reverseSearchNavKeyHoldTime += deltaTime;

                if (_reverseSearchNavKeyHoldTime >= InitialKeyRepeatDelay)
                {
                    _lastReverseSearchNavKeyRepeatTime += deltaTime;

                    if (_lastReverseSearchNavKeyRepeatTime >= KeyRepeatInterval)
                    {
                        shouldNavigate = true;
                        _lastReverseSearchNavKeyRepeatTime = 0;
                    }
                }
            }

            if (shouldNavigate)
            {
                if (currentReverseSearchNavKey.Value == Keys.R)
                {
                    _console.ReverseSearchNextMatch();
                    _logger.LogDebug("Reverse-i-search next match");
                }
                else
                {
                    _console.ReverseSearchPreviousMatch();
                    _logger.LogDebug("Reverse-i-search previous match");
                }
            }

            return InputHandlingResult.Consumed;
        }
        else
        {
            _lastHeldReverseSearchNavKey = null;
            _reverseSearchNavKeyHoldTime = 0;
            _lastReverseSearchNavKeyRepeatTime = 0;
            return InputHandlingResult.None;
        }
    }

    /// <summary>
    /// Handles Ctrl+Shift+Up/Down with key repeat support for parameter hint overload cycling.
    /// </summary>
    private InputHandlingResult HandleParameterHintNavigation(KeyboardState keyboardState, KeyboardState previousKeyboardState,
                                                              float deltaTime, bool isCtrlShiftPressed)
    {
        if (!isCtrlShiftPressed || !_console.HasParameterHints())
            return InputHandlingResult.None;

        Keys? currentParamHintKey = null;

        if (keyboardState.IsKeyDown(Keys.Up))
            currentParamHintKey = Keys.Up;
        else if (keyboardState.IsKeyDown(Keys.Down))
            currentParamHintKey = Keys.Down;

        if (currentParamHintKey.HasValue)
        {
            bool shouldNavigate = false;

            if (!previousKeyboardState.IsKeyDown(currentParamHintKey.Value))
            {
                _lastHeldParamHintKey = currentParamHintKey.Value;
                _paramHintKeyHoldTime = 0;
                _lastParamHintKeyRepeatTime = 0;
                shouldNavigate = true;
            }
            else if (_lastHeldParamHintKey == currentParamHintKey.Value)
            {
                _paramHintKeyHoldTime += deltaTime;

                if (_paramHintKeyHoldTime >= InitialKeyRepeatDelay)
                {
                    _lastParamHintKeyRepeatTime += deltaTime;

                    if (_lastParamHintKeyRepeatTime >= KeyRepeatInterval)
                    {
                        shouldNavigate = true;
                        _lastParamHintKeyRepeatTime = 0;
                    }
                }
            }

            if (shouldNavigate)
            {
                if (currentParamHintKey.Value == Keys.Up)
                {
                    _console.PreviousParameterHintOverload();
                    _logger.LogDebug("Cycled to previous parameter hint overload");
                }
                else
                {
                    _console.NextParameterHintOverload();
                    _logger.LogDebug("Cycled to next parameter hint overload");
                }
            }

            return InputHandlingResult.Consumed;
        }
        else
        {
            _lastHeldParamHintKey = null;
            _paramHintKeyHoldTime = 0;
            _lastParamHintKeyRepeatTime = 0;
            return InputHandlingResult.None;
        }
    }

    /// <summary>
    /// Handles Ctrl+Z/Y with key repeat support for undo/redo operations.
    /// </summary>
    private InputHandlingResult HandleUndoRedo(KeyboardState keyboardState, KeyboardState previousKeyboardState,
                                               float deltaTime, bool isCtrlPressed, bool isCmdPressed, bool isShiftPressed)
    {
        if (!isCtrlPressed && !isCmdPressed)
            return InputHandlingResult.None;

        Keys? currentUndoRedoKey = null;
        bool isUndo = false;
        bool isRedo = false;

        // Ctrl+Z (without Shift) = Undo
        if (keyboardState.IsKeyDown(Keys.Z) && !isShiftPressed)
        {
            currentUndoRedoKey = Keys.Z;
            isUndo = true;
        }
        // Ctrl+Y OR Ctrl+Shift+Z = Redo
        else if (keyboardState.IsKeyDown(Keys.Y) || 
                 (keyboardState.IsKeyDown(Keys.Z) && isShiftPressed))
        {
            currentUndoRedoKey = keyboardState.IsKeyDown(Keys.Y) ? Keys.Y : Keys.Z;
            isRedo = true;
        }

        if (currentUndoRedoKey.HasValue)
        {
            bool shouldProcess = false;

            // Check if key was just pressed
            if (!previousKeyboardState.IsKeyDown(currentUndoRedoKey.Value))
            {
                _lastHeldUndoRedoKey = currentUndoRedoKey.Value;
                _undoRedoKeyHoldTime = 0;
                _lastUndoRedoKeyRepeatTime = 0;
                shouldProcess = true;
            }
            // Check for key repeat
            else if (_lastHeldUndoRedoKey == currentUndoRedoKey.Value)
            {
                _undoRedoKeyHoldTime += deltaTime;

                if (_undoRedoKeyHoldTime >= InitialKeyRepeatDelay)
                {
                    _lastUndoRedoKeyRepeatTime += deltaTime;

                    if (_lastUndoRedoKeyRepeatTime >= KeyRepeatInterval)
                    {
                        shouldProcess = true;
                        _lastUndoRedoKeyRepeatTime = 0;
                    }
                }
            }

            if (shouldProcess)
            {
                if (isUndo)
                {
                    if (_console.Input.Undo())
                    {
                        _logger.LogDebug("Undo performed");
                        // Re-evaluate parameter hints after undo
                        UpdateParameterHints();
                    }
                }
                else if (isRedo)
                {
                    if (_console.Input.Redo())
                    {
                        _logger.LogDebug("Redo performed");
                        // Re-evaluate parameter hints after redo
                        UpdateParameterHints();
                    }
                }
            }

            return InputHandlingResult.Consumed;
        }
        else
        {
            _lastHeldUndoRedoKey = null;
            _undoRedoKeyHoldTime = 0;
            _lastUndoRedoKeyRepeatTime = 0;
            return InputHandlingResult.None;
        }
    }

    /// <summary>
    /// Checks if a key was just pressed (down now, up before).
    /// </summary>
    private static bool WasKeyJustPressed(Keys key, KeyboardState current, KeyboardState previous)
    {
        return current.IsKeyDown(key) && !previous.IsKeyDown(key);
    }

    /// <summary>
    /// Updates parameter hints based on the current input.
    /// </summary>
    private void UpdateParameterHints()
    {
        if (_parameterHintProvider == null)
            return;

        var inputText = _console.GetInputText();
        var cursorPos = _console.Input.CursorPosition;

        // Get parameter hints from provider
        var hints = _parameterHintProvider.GetParameterHints(inputText, cursorPos);

        if (hints != null)
        {
            // Count commas from the opening parenthesis to determine current parameter
            int currentParamIndex = CountParameterIndex(inputText, cursorPos);
            _console.SetParameterHints(hints, currentParamIndex);
        }
        else
        {
            _console.ClearParameterHints();
        }
    }

    /// <summary>
    /// Counts which parameter the cursor is currently on by counting commas.
    /// </summary>
    private int CountParameterIndex(string text, int cursorPos)
    {
        if (string.IsNullOrEmpty(text) || cursorPos <= 0)
            return 0;

        int openParenPos = FindOpeningParenthesis(text, cursorPos);
        if (openParenPos == -1)
            return 0;

        return CountCommasInParameterList(text, openParenPos, cursorPos);
    }

    /// <summary>
    /// Finds the last opening parenthesis before the cursor position.
    /// </summary>
    private int FindOpeningParenthesis(string text, int cursorPos)
    {
        int parenDepth = 0;

        for (int i = cursorPos - 1; i >= 0; i--)
        {
            char c = text[i];

            if (c == ')')
            {
                parenDepth++;
            }
            else if (c == '(')
            {
                if (parenDepth == 0)
                    return i;

                parenDepth--;
            }
        }

        return -1;
    }

    /// <summary>
    /// Counts commas between opening parenthesis and cursor at the same nesting level.
    /// </summary>
    private int CountCommasInParameterList(string text, int openParenPos, int cursorPos)
    {
        int paramIndex = 0;
        int parenDepth = 0;
        bool inString = false;
        char stringChar = '\0';

        for (int i = openParenPos + 1; i < cursorPos; i++)
        {
            char c = text[i];

            if (UpdateStringState(c, text, i, ref inString, ref stringChar))
                continue;

            if (inString)
                continue;

            UpdateParameterTracking(c, ref parenDepth, ref paramIndex);
        }

        return paramIndex;
    }

    /// <summary>
    /// Updates the string literal tracking state.
    /// </summary>
    private bool UpdateStringState(char c, string text, int index, ref bool inString, ref char stringChar)
    {
        if (c != '"' && c != '\'')
            return false;

        if (!inString)
        {
            inString = true;
            stringChar = c;
            return true;
        }

        // Check if this is the closing quote (not escaped)
        if (c == stringChar && (index == 0 || text[index - 1] != '\\'))
        {
            inString = false;
        }

        return true;
    }

    /// <summary>
    /// Updates parenthesis depth and parameter count.
    /// </summary>
    private void UpdateParameterTracking(char c, ref int parenDepth, ref int paramIndex)
    {
        if (c == '(')
        {
            parenDepth++;
        }
        else if (c == ')')
        {
            parenDepth--;
        }
        else if (c == ',' && parenDepth == 0)
        {
            paramIndex++;
        }
    }

    /// <summary>
    /// Inserts an auto-complete suggestion at the cursor position.
    /// Handles complex C# syntax including partial identifiers after operators.
    /// </summary>
    private void InsertCompletion(string completion)
    {
        // Validation: ensure completion is not null or empty
        if (string.IsNullOrEmpty(completion))
        {
            _logger.LogWarning("Attempted to insert null or empty completion");
            return;
        }

        // Check if this is a history suggestion (starts with @ prefix)
        const string historyPrefix = "@ ";
        if (completion.StartsWith(historyPrefix))
        {
            // History suggestion - replace entire input with the command
            var historicalCommand = completion.Substring(historyPrefix.Length);
            _console.Input.SetText(historicalCommand);
            _console.Input.SetCursorPosition(historicalCommand.Length);
            _logger.LogInformation("Inserted historical command: {Command}", historicalCommand);
            // Re-evaluate parameter hints for the historical command
            UpdateParameterHints();
            return;
        }

        var currentText = _console.Input.Text;
        var cursorPos = _console.Input.CursorPosition;

        // Validation: ensure cursor position is within valid bounds
        if (cursorPos < 0 || cursorPos > currentText.Length)
        {
            _logger.LogWarning("Invalid cursor position {CursorPos} for text length {Length}", cursorPos, currentText.Length);
            cursorPos = Math.Clamp(cursorPos, 0, currentText.Length);
        }

        // Find the start of the current word being typed
        // Handle edge case: cursor at start of text
        if (cursorPos == 0)
        {
            _console.Input.SetText(completion + currentText);
            _console.Input.SetCursorPosition(completion.Length);
            _logger.LogInformation("Inserted completion at start: {Completion}", completion);
            // Re-evaluate parameter hints after inserting completion
            UpdateParameterHints();
            return;
        }

        int wordStart = cursorPos - 1;

        // Walk backwards to find word start, handling C# syntax:
        // - Stop at word separators (space, dot, comma, etc.)
        // - But continue through valid identifier characters
        while (wordStart >= 0)
        {
            char c = currentText[wordStart];

            // Stop at word separator (don't include it)
            if (IsWordSeparator(c))
        {
                wordStart++;
                break;
            }

            // Stop at invalid identifier characters
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                wordStart++;
                break;
            }

            wordStart--;
        }

        // Adjust if we went all the way to start
        if (wordStart < 0)
            wordStart = 0;

        // Replace from word start to cursor with the completion
        var before = currentText.Substring(0, wordStart);
        var after = currentText.Substring(cursorPos);
        var newText = before + completion + after;
        var newCursorPos = wordStart + completion.Length;

        _console.Input.SetText(newText);
        // Directly set cursor position to end of inserted completion
        _console.Input.SetCursorPosition(newCursorPos);

        _logger.LogInformation("Inserted completion: '{Completion}' replacing '{Partial}' at position {Position}",
            completion,
            currentText.Substring(wordStart, cursorPos - wordStart),
            wordStart);

        // Re-evaluate parameter hints after inserting completion
        UpdateParameterHints();
    }

    /// <summary>
    /// Checks if a character is a word separator for auto-completion.
    /// </summary>
    private static bool IsWordSeparator(char c)
    {
        return Array.IndexOf(ConsoleConstants.AutoComplete.WordSeparators, c) >= 0;
    }

    /// <summary>
    /// Checks if typing this key should clear auto-complete suggestions.
    /// Clears on structural characters that typically indicate end of an identifier.
    /// </summary>
    private static bool ShouldClearSuggestions(Keys key)
    {
        // Clear on space, semicolon, braces, brackets, comma
        return key == Keys.Space
            || key == Keys.OemSemicolon      // ;
            || key == Keys.OemOpenBrackets   // [
            || key == Keys.OemCloseBrackets  // ]
            || key == Keys.OemComma          // ,
            || key == Keys.Enter;            // New line without Shift
    }

    /// <summary>
    /// Handles keyboard input in search mode.
    /// </summary>
    private void HandleSearchModeInput(Keys key, char? character)
    {
        string searchInput = _console.SearchInput;

        if (key == Keys.Back && searchInput.Length > 0)
        {
            // Delete last character
            searchInput = searchInput.Substring(0, searchInput.Length - 1);
            _console.UpdateSearchQuery(searchInput);
        }
        else if (key == Keys.Enter)
        {
            // Enter navigates to next match (like F3)
            _console.NextSearchMatch();
        }
        else if (character.HasValue && !char.IsControl(character.Value))
        {
            // Add character to search
            searchInput += character.Value;
            _console.UpdateSearchQuery(searchInput);
        }
    }

    /// <summary>
    /// Handles keyboard input in reverse-i-search mode.
    /// </summary>
    private void HandleReverseSearchModeInput(Keys key, char? character)
    {
        string searchInput = _console.ReverseSearchInput;

        if (key == Keys.Back && searchInput.Length > 0)
        {
            // Delete last character
            searchInput = searchInput.Substring(0, searchInput.Length - 1);
            _console.UpdateReverseSearchQuery(searchInput, _history.GetAll());
        }
        else if (character.HasValue && !char.IsControl(character.Value))
        {
            // Add character to search
            searchInput += character.Value;
            _console.UpdateReverseSearchQuery(searchInput, _history.GetAll());
        }
    }

    /// <summary>
    /// Shows documentation for the currently selected autocomplete suggestion.
    /// </summary>
    private void ShowDocumentationForSelectedSuggestion()
    {
        try
        {
            // Get the selected suggestion from the coordinator
            if (_autoCompleteCoordinator == null)
                return;

            var selectedItem = _autoCompleteCoordinator.GetSelectedSuggestion();
            if (selectedItem == null)
            {
                _logger.LogDebug("No suggestion selected");
                return;
            }

            // Get documentation synchronously (without extended Roslyn info)
            // We do this to keep the UI responsive - advanced docs can be added later if needed
            var documentation = _documentationProvider.GetDocumentationAsync(selectedItem).GetAwaiter().GetResult();

            // Format and display
            var formattedText = _documentationProvider.FormatForDisplay(documentation);
            _console.ShowDocumentation(formattedText);

            _logger.LogInformation("Displayed documentation for: {DisplayText}", selectedItem.DisplayText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show documentation");
        }
    }

    /// <summary>
    /// Handles mouse input for the console.
    /// Uses time-based click detection window for consistent behavior across frame rates.
    /// </summary>
    private InputHandlingResult HandleMouseInput(MouseState mouseState, MouseState previousMouseState, float deltaTime)
    {
        // Handle mouse wheel scrolling
        int scrollDelta = mouseState.ScrollWheelValue - previousMouseState.ScrollWheelValue;
        if (scrollDelta != 0)
        {
            // Standard mouse wheel delta is 120 per notch
            // Positive delta = scroll up (backward in output), Negative = scroll down (forward)
            int lines = scrollDelta / 120;

            Point mousePosition = new Point(mouseState.X, mouseState.Y);

            // Check if mouse is over auto-complete window
            if (_console.IsMouseOverAutoComplete(mousePosition))
            {
                // Scroll auto-complete (invert direction for natural scrolling)
                _console.ScrollAutoComplete(-lines);
                _logger.LogTrace("Scrolled auto-complete by {Lines} items", -lines);
            }
            else
            {
                // Scroll console output (invert direction for natural scrolling)
                _console.ScrollOutput(-lines);
                _logger.LogTrace("Scrolled console output by {Lines} lines", -lines);
            }

            return InputHandlingResult.Consumed;
        }

        // Handle mouse button press (start of click or drag)
        if (mouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed &&
            previousMouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Released)
        {
            Point clickPosition = new Point(mouseState.X, mouseState.Y);

            // Check for auto-complete item click (higher priority)
            int autoCompleteItemIndex = _console.GetAutoCompleteItemAt(clickPosition);

            if (autoCompleteItemIndex >= 0)
            {
                // Clear all selections
                _console.Input.ClearSelection();
                _console.Output.ClearOutputSelection();

                // Get the suggestion BEFORE we change anything
                _console.SelectAutoCompleteItem(autoCompleteItemIndex);
                var suggestion = _console.GetSelectedSuggestion();

                // Clear autocomplete BEFORE inserting (prevents re-triggering)
                _console.ClearAutoCompleteSuggestions();
                _lastFilteredText = ""; // Reset debounce tracking

                // Now insert the completion
                if (suggestion != null)
                {
                    InsertCompletion(suggestion);
                }

                return InputHandlingResult.Consumed;
            }

            // Check for section header click
            bool sectionClicked = _console.HandleOutputClick(clickPosition);

            if (sectionClicked)
            {
                // Clear all selections
                _console.Input.ClearSelection();
                _console.Output.ClearOutputSelection();

                return InputHandlingResult.Consumed;
            }

            // Check for output area click - this might start a text selection drag
            if (_console.IsMouseOverOutputArea(clickPosition))
            {
                var (line, column) = _console.GetOutputPositionAtMouse(clickPosition);
                if (line >= 0 && column >= 0)
                {
                    // Clear input selection (only one selection active at a time)
                    _console.Input.ClearSelection();

                    // Start output text selection drag
                    _isOutputDragging = true;
                    _outputDragStartPosition = clickPosition;
                    _outputLastDragPosition = clickPosition;
                    _outputDragStartLine = line;
                    _outputDragStartColumn = column;

                    // Start selection at this position
                    _console.Output.StartOutputSelection(line, column);

                    return InputHandlingResult.Consumed;
                }
                else
                {
                    // Clicked in output area but not on valid text - clear all selections
                    _console.Input.ClearSelection();
                    _console.Output.ClearOutputSelection();
                    return InputHandlingResult.Consumed;
                }
            }

            // Check for input field click - this might start a drag selection
            if (_console.IsMouseOverInputField(clickPosition))
            {
                int charPosition = _console.GetCharacterPositionAtMouse(clickPosition);
                if (charPosition >= 0)
                {
                    // Clear output selection (only one selection active at a time)
                    _console.Output.ClearOutputSelection();

                    // Start potential drag operation
                    _isDragging = true;
                    _dragStartPosition = clickPosition;
                    _lastDragPosition = clickPosition;
                    _dragStartCharPosition = charPosition;

                    // Position cursor at click point
                    _console.Input.SetCursorPosition(charPosition);
                    _console.Input.ClearSelection();

                    return InputHandlingResult.Consumed;
                }
            }

            // Click anywhere else - clear all selections
            _console.Input.ClearSelection();
            _console.Output.ClearOutputSelection();
        }

        // Handle mouse dragging for output area text selection
        if (_isOutputDragging && mouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
        {
            Point currentPosition = new Point(mouseState.X, mouseState.Y);

            // Only process if mouse has moved since last check (performance optimization)
            if (currentPosition.X != _outputLastDragPosition.X || currentPosition.Y != _outputLastDragPosition.Y)
            {
                // Check if we've moved enough to start selection (avoid accidental selection on click)
                int dragDistance = Math.Abs(currentPosition.X - _outputDragStartPosition.X) +
                                 Math.Abs(currentPosition.Y - _outputDragStartPosition.Y);

                if (dragDistance > 5) // 5 pixel threshold
                {
                    var (line, column) = _console.GetOutputPositionAtMouse(currentPosition);
                    if (line >= 0 && column >= 0)
                    {
                        // Update selection range
                        _console.Output.ExtendOutputSelection(line, column);
                    }
                }

                _outputLastDragPosition = currentPosition;
            }

            return InputHandlingResult.Consumed;
        }

        // Handle mouse dragging for input field text selection
        if (_isDragging && mouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
        {
            Point currentPosition = new Point(mouseState.X, mouseState.Y);

            // Only process if mouse has moved since last check (performance optimization)
            if (currentPosition.X != _lastDragPosition.X || currentPosition.Y != _lastDragPosition.Y)
            {
                // Check if we've moved enough to start selection (avoid accidental selection on click)
                int dragDistance = Math.Abs(currentPosition.X - _dragStartPosition.X) +
                                 Math.Abs(currentPosition.Y - _dragStartPosition.Y);

                if (dragDistance > 5) // 5 pixel threshold
                {
                    int currentCharPosition = _console.GetCharacterPositionAtMouse(currentPosition);
                    if (currentCharPosition >= 0 && _dragStartCharPosition >= 0)
                    {
                        // Update selection range
                        _console.Input.SetSelection(_dragStartCharPosition, currentCharPosition);
                    }
                }

                _lastDragPosition = currentPosition;
            }

            return InputHandlingResult.Consumed;
        }

        // Handle mouse button release (end of output drag)
        if (_isOutputDragging && mouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Released)
        {
            _isOutputDragging = false;
            _outputDragStartLine = -1;
            _outputDragStartColumn = -1;
            return InputHandlingResult.Consumed;
        }

        // Handle mouse button release (end of input drag)
        if (_isDragging && mouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Released)
        {
            _isDragging = false;
            _dragStartCharPosition = -1;
            return InputHandlingResult.Consumed;
        }

        return InputHandlingResult.None;
    }

    /// <summary>
    /// Handles font size changes with Ctrl/Cmd key combinations with key repeat support.
    /// </summary>
    private InputHandlingResult HandleFontSizeChanges(
        KeyboardState keyboardState,
        KeyboardState previousKeyboardState,
        float deltaTime,
        bool isShiftPressed,
        bool isCtrlPressed,
        bool isCmdPressed)
    {
        bool isModifierPressed = isCtrlPressed || isCmdPressed;

        if (!isModifierPressed)
            return InputHandlingResult.None;

        // Handle Ctrl/Cmd+0 (without Shift) - reset font size (no repeat)
        if (WasKeyJustPressed(Keys.D0, keyboardState, previousKeyboardState) &&
            !isShiftPressed && isModifierPressed)
        {
            int newSize = _console.ResetFontSize();
            _console.AppendOutput($"Font size reset to {newSize}pt (default)", Success_Bright);
            _logger.LogInformation("Font size reset to {Size}pt", newSize);
            return InputHandlingResult.Consumed;
        }

        // Determine which font size key is pressed (with repeat support)
        Keys? currentFontSizeKey = null;
        
        // Plus key or Shift+0 for increase
        if (keyboardState.IsKeyDown(Keys.OemPlus) || 
            (keyboardState.IsKeyDown(Keys.D0) && isShiftPressed))
        {
            currentFontSizeKey = Keys.OemPlus; // Use OemPlus as identifier for increase
        }
        // Minus key for decrease
        else if (keyboardState.IsKeyDown(Keys.OemMinus))
        {
            currentFontSizeKey = Keys.OemMinus;
        }

        if (currentFontSizeKey.HasValue)
        {
            bool shouldChange = false;

            if (!previousKeyboardState.IsKeyDown(currentFontSizeKey.Value) &&
                !previousKeyboardState.IsKeyDown(Keys.D0)) // Check both for Shift+0 case
            {
                _lastHeldFontSizeKey = currentFontSizeKey.Value;
                _fontSizeKeyHoldTime = 0;
                _lastFontSizeKeyRepeatTime = 0;
                shouldChange = true;
            }
            else if (_lastHeldFontSizeKey == currentFontSizeKey.Value)
            {
                _fontSizeKeyHoldTime += deltaTime;

                if (_fontSizeKeyHoldTime >= InitialKeyRepeatDelay)
                {
                    _lastFontSizeKeyRepeatTime += deltaTime;

                    if (_lastFontSizeKeyRepeatTime >= KeyRepeatInterval)
                    {
                        shouldChange = true;
                        _lastFontSizeKeyRepeatTime = 0;
                    }
                }
            }

            if (shouldChange)
            {
                int newSize;
                if (currentFontSizeKey.Value == Keys.OemPlus)
                {
                    newSize = _console.IncreaseFontSize();
                    _logger.LogInformation("Font size increased to {Size}pt", newSize);
                }
                else
                {
                    newSize = _console.DecreaseFontSize();
                    _logger.LogInformation("Font size decreased to {Size}pt", newSize);
                }
                // Only output on first press, not every repeat
                if (_fontSizeKeyHoldTime == 0)
                {
                    _console.AppendOutput($"Font size: {newSize}pt", Success_Bright);
                }
            }

            return InputHandlingResult.Consumed;
        }
        else
        {
            _lastHeldFontSizeKey = null;
            _fontSizeKeyHoldTime = 0;
            _lastFontSizeKeyRepeatTime = 0;
            return InputHandlingResult.None;
        }
    }

    /// <summary>
    /// Handles function keys (F1-F12) for bookmarks, search navigation, and documentation.
    /// F3 has key repeat support for fast search navigation.
    /// </summary>
    private InputHandlingResult HandleFunctionKeys(
        KeyboardState keyboardState,
        KeyboardState previousKeyboardState,
        float deltaTime,
        bool isShiftPressed)
    {
        // Handle F1-F12 - execute bookmarked commands (no repeat - single fire)
        var fKeyNumber = GetFKeyNumber(keyboardState, previousKeyboardState);
        if (fKeyNumber.HasValue && _bookmarksManager != null)
        {
            var command = _bookmarksManager.GetBookmark(fKeyNumber.Value);
            if (command != null)
            {
                _logger.LogInformation("Executing bookmarked command from F{FKey}: {Command}", fKeyNumber.Value, command);
                _console.AppendOutput($"> F{fKeyNumber.Value}: {command}", Warning);
                return InputHandlingResult.Execute(command);
            }
            // If no bookmark for this F-key, fall through to other handlers
        }

        // Handle F3 / Shift+F3 with key repeat - navigate search results
        if (_console.IsSearchMode && _console.OutputSearcher.IsSearching && keyboardState.IsKeyDown(Keys.F3))
        {
            bool shouldNavigate = false;

            if (!previousKeyboardState.IsKeyDown(Keys.F3))
            {
                _lastHeldSearchNavKey = Keys.F3;
                _searchNavKeyHoldTime = 0;
                _lastSearchNavKeyRepeatTime = 0;
                shouldNavigate = true;
            }
            else if (_lastHeldSearchNavKey == Keys.F3)
            {
                _searchNavKeyHoldTime += deltaTime;

                if (_searchNavKeyHoldTime >= InitialKeyRepeatDelay)
                {
                    _lastSearchNavKeyRepeatTime += deltaTime;

                    if (_lastSearchNavKeyRepeatTime >= KeyRepeatInterval)
                    {
                        shouldNavigate = true;
                        _lastSearchNavKeyRepeatTime = 0;
                    }
                }
            }

            if (shouldNavigate)
            {
                if (isShiftPressed)
                {
                    _console.PreviousSearchMatch();
                    _logger.LogDebug("Moved to previous search match");
                }
                else
                {
                    _console.NextSearchMatch();
                    _logger.LogDebug("Moved to next search match");
                }
            }

            return InputHandlingResult.Consumed;
        }
        else if (!keyboardState.IsKeyDown(Keys.F3))
        {
            // Reset F3 repeat state when not pressed
            _lastHeldSearchNavKey = null;
            _searchNavKeyHoldTime = 0;
            _lastSearchNavKeyRepeatTime = 0;
        }

        // Handle F1 - show/hide documentation for selected autocomplete item (if no bookmark)
        if (WasKeyJustPressed(Keys.F1, keyboardState, previousKeyboardState))
        {
            // If documentation is showing, hide it
            if (_console.IsShowingDocumentation)
            {
                _console.HideDocumentation();
                _logger.LogDebug("Closed documentation popup");
                return InputHandlingResult.Consumed;
            }

            // If autocomplete suggestions are visible, show docs for selected item
            if (_console.HasSuggestions())
            {
                ShowDocumentationForSelectedSuggestion();
                _logger.LogInformation("Showing documentation for selected suggestion");
                return InputHandlingResult.Consumed;
            }
        }

        return InputHandlingResult.None;
    }

    /// <summary>
    /// Gets the F-key number (1-12) if an F-key was just pressed.
    /// </summary>
    private static int? GetFKeyNumber(KeyboardState keyboardState, KeyboardState previousKeyboardState)
    {
        if (WasKeyJustPressed(Keys.F1, keyboardState, previousKeyboardState)) return 1;
        if (WasKeyJustPressed(Keys.F2, keyboardState, previousKeyboardState)) return 2;
        if (WasKeyJustPressed(Keys.F3, keyboardState, previousKeyboardState)) return 3;
        if (WasKeyJustPressed(Keys.F4, keyboardState, previousKeyboardState)) return 4;
        if (WasKeyJustPressed(Keys.F5, keyboardState, previousKeyboardState)) return 5;
        if (WasKeyJustPressed(Keys.F6, keyboardState, previousKeyboardState)) return 6;
        if (WasKeyJustPressed(Keys.F7, keyboardState, previousKeyboardState)) return 7;
        if (WasKeyJustPressed(Keys.F8, keyboardState, previousKeyboardState)) return 8;
        if (WasKeyJustPressed(Keys.F9, keyboardState, previousKeyboardState)) return 9;
        if (WasKeyJustPressed(Keys.F10, keyboardState, previousKeyboardState)) return 10;
        if (WasKeyJustPressed(Keys.F11, keyboardState, previousKeyboardState)) return 11;
        if (WasKeyJustPressed(Keys.F12, keyboardState, previousKeyboardState)) return 12;
        return null;
    }
}
