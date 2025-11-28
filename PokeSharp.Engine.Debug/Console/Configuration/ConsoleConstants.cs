using Microsoft.Xna.Framework.Input;

namespace PokeSharp.Engine.Debug.Console.Configuration;

/// <summary>
/// Constants for the debug console system.
/// Centralizes all magic numbers for maintainability.
/// </summary>
public static class ConsoleConstants
{
    /// <summary>
    /// Rendering constants (colors, sizes, padding).
    /// </summary>
    public static class Rendering
    {
        // Layout
        // NOTE: LineHeight is now defined in UITheme (use ThemeManager.Current.LineHeight)
        // NOTE: Semantic padding constants moved to UITheme (use ThemeManager.Current.PaddingTiny, etc.)
        public const int FontScale = 2;
        public const int Padding = 10;  // Main padding for console edges
        public const int InputAreaTopPadding = 5;
        public const int InputAreaBottomPadding = 5;
        public const int MultiLineIndicatorSpacing = 3;
        public const int GeneralSpacing = 5; // General purpose spacing

        // Parameter hints
        public const int ParameterHintPadding = 10;
        public const int ParameterHintBorderWidth = 2;
        public const int ParameterHintGapFromInput = 10; // Gap between parameter hint and input area

        // Documentation popup
        public const int DocumentationPopupWidthRatio = 70; // Percentage of screen width (0.7)
        public const int DocumentationPopupHeightRatio = 60; // Percentage of screen height (0.6)
        public const int DocumentationPopupPadding = 10;
        public const int DocumentationPopupBorderWidth = 2;
        public const int DocumentationOverlayAlpha = 180; // Semi-transparent overlay

        // Search bar
        public const int SearchBarHeight = 6; // Additional height beyond line height
        public const int SearchBarBottomOffset = 5;

        // Scroll bar
        public const int ScrollBarWidth = 10;
        public const int ScrollBarMinHeight = 20;
        public const int ScrollBarMargin = 20;
        public const int ScrollBarVerticalPadding = 10;
        public const int ScrollBarBottomOffset = 60;
        public const int ScrollIndicatorOffset = 15;
        public const int ScrollIndicatorYOffset = 5;
        public const int ScrollTriangleWidth = 8;
        public const int ScrollTriangleHeight = 6;
        public const int ScrollTriangleGap = 4;  // Gap between triangle and scrollbar

        // Section folding
        public const int SectionFoldBoxSize = 10;

        // Input area
        public const int InputYOffset = 5;  // Top offset for input text within input box
        public const int InputPromptOffset = 20;  // X offset after ">" prompt for single-line input
        public const int InputMultiLineIndicatorGap = 3;  // Gap between input box and multi-line indicator
        public const int SectionFoldBoxSpacing = 5;
        public const int SectionFoldBoxMargin = 15;

        // Auto-complete panel
        public const int SuggestionPanelWidth = 600;
        public const int SuggestionPanelMargin = 20;
        public const int SuggestionDescriptionX = 200;
        public const int SuggestionPanelOffset = 5;
        public const int SuggestionHorizontalPadding = 5;
        public const int SuggestionVerticalPadding = 25;
        public const int LoadingPanelWidth = 200;
        public const int MaxDescriptionWidth = 380; // 580 - SuggestionDescriptionX
        public const int SuggestionDescriptionGap = 80; // Gap between main text and description
        public const int SuggestionRightPadding = 10; // Right edge padding for descriptions
        public const int SuggestionMinTextWidth = 100; // Minimum width for suggestion text
        public const int SuggestionScrollIndicatorRightMargin = 5; // Margin for scroll indicators
        public const int SuggestionHorizontalScrollAmount = 5; // Characters to scroll per key press

        // Input area
        public const int InputAreaHeight = 50;
        public const int InputAreaHeightCalculation = 40; // For positioning
        public const int InputPromptX = 5;
        public const int InputPromptY = 5;
        public const int InputTextX = 20;
        public const int InputTextY = 5;
        public const int InputBackgroundHeight = 30;
        public const int CursorWidth = 2;

        // Line numbers (multi-line input)
        public const int LineNumberCharWidth = 8; // Approximate character width in pixels
        public const int LineNumberSuffixChars = 2; // Characters for ": " suffix
        public const int LineNumberSpacing = 4; // Extra spacing after line numbers

        // NOTE: Color constants are managed by UITheme in PokeSharp.Engine.UI.Debug.Core.
        // Use ThemeManager.Current for runtime theme access.
    }

    /// <summary>
    /// Input handling constants (timing, delays).
    /// </summary>
    public static class Input
    {
        // Key repeat timing
        public const float InitialKeyRepeatDelay = 0.5f;    // 500ms before repeat starts
        public const float KeyRepeatInterval = 0.05f;       // 50ms between repeats

        // Auto-complete timing
        public const float AutoCompleteDelay = 0.15f;       // 150ms delay before showing (VS Code-like)

        // Clipboard
        public const int ClipboardPasteLoggingThreshold = 1; // Log if pasting more than this many chars
    }

    /// <summary>
    /// Console system constants (priorities, ordering).
    /// </summary>
    public static class System
    {
        // System execution order
        public const int UpdatePriority = -100;             // Run before game systems (captures input first)
        public const int RenderOrder = 1000;                // Render on top of everything

        // Performance thresholds
        public const int FramesBetweenPerformanceLog = 300; // Log every 5 seconds @ 60fps
    }

    /// <summary>
    /// Animation constants (speed, easing).
    /// </summary>
    public static class Animation
    {
        public const float SlideSpeed = 1200f;              // pixels per second (fast slide animation)
        public const float AnimationThreshold = 0.1f;       // Stop animating when within this many pixels
        public const int LoadingDotsAnimationDivisor = 10;  // Divisor for loading dots animation speed
        public const int LoadingDotsMaxCount = 4;           // Maximum number of animated dots (0-3)
    }

    /// <summary>
    /// Limits and capacity constants.
    /// </summary>
    public static class Limits
    {
        // History
        public const int MaxHistorySize = 100;              // Maximum command history entries

        // Output buffer
        public const int MaxOutputLines = 1000;             // Maximum lines in output buffer

        // Auto-complete
        public const int MaxAutoCompleteSuggestions = 15;   // Maximum suggestions to show
        public const int MaxSuggestionsInLog = 5;           // Maximum suggestions to log

        // Scroll
        public const int MouseWheelUnitsPerNotch = 120;     // Standard mouse wheel units
        public const int ScrollLinesPerNotch = 3;           // Lines to scroll per mouse wheel notch

        // Script arguments
        public const int MaxScriptArguments = 9;            // Max $1-$9 in alias macros

        // Visible lines calculation
        public const int DefaultVisibleLines = 25;          // Default if calculation fails
    }

    /// <summary>
    /// Console size multipliers.
    /// </summary>
    public static class Size
    {
        public const float SmallMultiplier = 0.25f;         // 25% of screen height
        public const float MediumMultiplier = 0.5f;         // 50% of screen height
        public const float FullMultiplier = 1.0f;           // 100% of screen height (fullscreen)
    }

    /// <summary>
    /// File and path constants.
    /// </summary>
    public static class Files
    {
        public const string HistoryFileName = "console_history.json";
        public const string AliasesFileName = "aliases.txt";
        public const string BookmarksFileName = "bookmarks.txt";
        public const string ScriptsDirectoryName = "Scripts";
        public const string ScriptExtension = ".csx";
        public const string StartupScriptName = "startup.csx";
        public const string ExampleScriptName = "example.csx";
        public const string AppDataFolderName = "PokeSharp";
    }

    /// <summary>
    /// Command names and special strings.
    /// </summary>
    public static class Commands
    {
        // Built-in commands
        public const string Clear = "clear";
        public const string Reset = "reset";
        public const string Help = "help";
        public const string Scripts = "scripts";

        // Command prefixes
        public const string SizePrefix = "size ";
        public const string LogPrefix = "log ";
        public const string LoadPrefix = "load ";
        public const string SavePrefix = "save ";
        public const string AliasPrefix = "alias ";
        public const string UnaliasPrefix = "unalias ";
        public const string Aliases = "aliases";

        // Size commands
        public const string SizeSmall = "size small";
        public const string SizeMedium = "size medium";
        public const string SizeFull = "size full";

        // Log commands
        public const string LogOn = "on";
        public const string LogOff = "off";
        public const string LogFilter = "filter";
        public const string LogStatus = "status";

        // Special strings
        public const string ErrorPrefix = "Error";
        public const string AliasIndicator = "[alias]";
    }

    /// <summary>
    /// Regular expression patterns for alias validation.
    /// </summary>
    public static class Patterns
    {
        public const string ValidAliasName = @"^[a-zA-Z_][a-zA-Z0-9_]*$";
        public const string ParameterPlaceholder = @"\$\d";
        public const string MemberAccess = @"(\w+)\.$";
        public const string PartialMemberAccess = @"(\w+)\.(\w*)$";
    }

    /// <summary>
    /// Display text constants.
    /// </summary>
    public static class Text
    {
        // Multi-line indicator format
        public const string MultiLineIndicatorFormat = "({0} lines) [Enter] submit • [Shift+Enter] new line";

        // Symbols (Nerd Font icons when available, ASCII fallback)
        public const string PromptSymbol = ""; // Nerd Font chevron
        // Note: Scroll indicators use programmatic drawing (DrawUpTriangle/DrawDownTriangle)
        // instead of text symbols for pixel-perfect control and consistent scaling
        public const string CursorSymbol = "▌"; // Block cursor

        // Descriptions
        public const string PropertyDescription = "property";
        public const string FieldDescription = "field";
        public const string MethodDescription = "method";
        public const string VariableDescription = "var";
        public const string KeywordDescription = "keyword";
    }

    /// <summary>
    /// Auto-complete filter characters.
    /// </summary>
    public static class AutoComplete
    {
        public static readonly char[] WordSeparators = { ' ', '.', '(', ')', ',', ';', '=', '+', '-', '*', '/' };

        public static readonly char[] SuggestionDismissChars = { ' ', ';', '{', '}' };

        public const char MemberAccessChar = '.';

        // C# Keywords for auto-complete
        public static readonly string[] Keywords =
        {
            "var", "int", "string", "bool", "float", "double",
            "if", "else", "for", "foreach", "while", "return",
            "true", "false", "null", "new"
        };
    }

    /// <summary>
    /// Keyboard shortcut documentation for the console.
    /// These are informational constants describing the key bindings.
    /// </summary>
    public static class KeyboardShortcuts
    {
        // Console toggle
        public const string ToggleConsole = "~ (Tilde)";

        // Navigation
        public const string HistoryUp = "Up Arrow";
        public const string HistoryDown = "Down Arrow";
        public const string CursorLeft = "Left Arrow";
        public const string CursorRight = "Right Arrow";
        public const string CursorToStart = "Home";
        public const string CursorToEnd = "End";

        // Editing
        public const string DeleteChar = "Delete";
        public const string Backspace = "Backspace";
        public const string NewLine = "Shift+Enter";
        public const string Execute = "Enter";

        // Word Navigation
        public const string PreviousWord = "Ctrl/Cmd+Left";
        public const string NextWord = "Ctrl/Cmd+Right";
        public const string DeleteWordBackward = "Ctrl/Cmd+Backspace";
        public const string DeleteWordForward = "Ctrl/Cmd+Delete";

        // Autocomplete
        public const string NextSuggestion = "Down Arrow (when suggestions visible)";
        public const string PrevSuggestion = "Up Arrow (when suggestions visible)";
        public const string AcceptSuggestion = "Tab or Enter";
        public const string CancelSuggestions = "Escape";
        public const string ShowDocumentation = "F1 (when suggestion selected)";

        // Text Selection
        public const string SelectText = "Shift+Arrows";
        public const string SelectToStartEnd = "Shift+Home/End";
        public const string SelectWords = "Shift+Ctrl/Cmd+Arrows";
        public const string SelectAll = "Ctrl/Cmd+A";

        // Clipboard
        public const string Copy = "Ctrl/Cmd+C";
        public const string Cut = "Ctrl/Cmd+X";
        public const string Paste = "Ctrl/Cmd+V";

        // Font Size
        public const string IncreaseFontSize = "Ctrl/Cmd+Plus";
        public const string DecreaseFontSize = "Ctrl/Cmd+Minus";
        public const string ResetFontSize = "Ctrl/Cmd+0";

        // Undo/Redo
        public const string Undo = "Ctrl/Cmd+Z";
        public const string Redo = "Ctrl/Cmd+Y or Ctrl/Cmd+Shift+Z";

        // Search
        public const string OpenSearch = "Ctrl/Cmd+F";
        public const string NextMatch = "F3 or Enter (in search)";
        public const string PreviousMatch = "Shift+F3";
        public const string ExitSearch = "Escape (in search)";
    }

    /// <summary>
    /// Console tab definitions.
    /// Re-exported from PokeSharp.Engine.UI.Debug.Core.ConsoleTabs for convenience.
    /// The canonical definition is in the UI.Debug assembly to avoid circular dependencies.
    /// </summary>
    public static class Tabs
    {
        // Re-export from UI.Debug for backward compatibility
        public static PokeSharp.Engine.UI.Debug.Core.ConsoleTabs.TabDefinition Console
            => PokeSharp.Engine.UI.Debug.Core.ConsoleTabs.Console;
        public static PokeSharp.Engine.UI.Debug.Core.ConsoleTabs.TabDefinition Watch
            => PokeSharp.Engine.UI.Debug.Core.ConsoleTabs.Watch;
        public static PokeSharp.Engine.UI.Debug.Core.ConsoleTabs.TabDefinition Logs
            => PokeSharp.Engine.UI.Debug.Core.ConsoleTabs.Logs;
        public static PokeSharp.Engine.UI.Debug.Core.ConsoleTabs.TabDefinition Variables
            => PokeSharp.Engine.UI.Debug.Core.ConsoleTabs.Variables;
        public static PokeSharp.Engine.UI.Debug.Core.ConsoleTabs.TabDefinition Entities
            => PokeSharp.Engine.UI.Debug.Core.ConsoleTabs.Entities;

        public static IReadOnlyList<PokeSharp.Engine.UI.Debug.Core.ConsoleTabs.TabDefinition> All
            => PokeSharp.Engine.UI.Debug.Core.ConsoleTabs.All;
        public static int Count => PokeSharp.Engine.UI.Debug.Core.ConsoleTabs.Count;

        public static PokeSharp.Engine.UI.Debug.Core.ConsoleTabs.TabDefinition? GetByIndex(int index)
            => PokeSharp.Engine.UI.Debug.Core.ConsoleTabs.GetByIndex(index);

        public static bool TryGet(string input, out PokeSharp.Engine.UI.Debug.Core.ConsoleTabs.TabDefinition? tab)
            => PokeSharp.Engine.UI.Debug.Core.ConsoleTabs.TryGet(input, out tab);

        public static PokeSharp.Engine.UI.Debug.Core.ConsoleTabs.TabDefinition? GetByShortcut(Keys key)
            => PokeSharp.Engine.UI.Debug.Core.ConsoleTabs.GetByShortcut(key);

        public static IEnumerable<string> GetAllAliases()
            => PokeSharp.Engine.UI.Debug.Core.ConsoleTabs.GetAllAliases();
    }
}

