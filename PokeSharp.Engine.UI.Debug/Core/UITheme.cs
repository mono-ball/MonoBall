using Microsoft.Xna.Framework;

namespace PokeSharp.Engine.UI.Debug.Core;

/// <summary>
///     Centralized theme system for debug UI.
///     Provides a cohesive styling system with multiple theme options.
///     Use ThemeManager.Current for the active theme, or reference specific themes directly.
/// </summary>
public class UITheme
{
    /// <summary>Backward compatibility - references OneDark theme</summary>
    public static UITheme Dark => OneDark;

    /// <summary>OneDark theme (Atom/VS Code dark theme)</summary>
    public static UITheme OneDark { get; } =
        new()
        {
            // ═══════════════════════════════════════════════════════════════
            // ONEDARK COLOR PALETTE REFERENCE
            // ═══════════════════════════════════════════════════════════════
            // Background:    #282c34 (40, 44, 52)
            // Foreground:    #abb2bf (171, 178, 191)
            // Comment:       #5c6370 (92, 99, 112)
            // Red:           #e06c75 (224, 108, 117)
            // Green:         #98c379 (152, 195, 121)
            // Yellow:        #e5c07b (229, 192, 123)
            // Blue:          #61afef (97, 175, 239)
            // Magenta:       #c678dd (198, 120, 221)
            // Cyan:          #56b6c2 (86, 182, 194)
            // Gutter:        #4b5263 (75, 82, 99)
            // Selection:     #3e4451 (62, 68, 81)
            // Cursor:        #528bff (82, 139, 255)
            // ═══════════════════════════════════════════════════════════════

            // Background colors
            BackgroundPrimary = new Color(40, 44, 52, 240), // #282c34
            BackgroundSecondary = new Color(33, 37, 43, 255), // Slightly darker
            BackgroundElevated = new Color(50, 56, 66, 255), // Slightly lighter

            // Text colors
            TextPrimary = new Color(171, 178, 191), // #abb2bf
            TextSecondary = new Color(130, 137, 151), // Dimmer foreground
            TextDim = new Color(92, 99, 112), // #5c6370 (comment color)

            // Interactive element colors
            ButtonNormal = new Color(62, 68, 81), // #3e4451 (selection)
            ButtonHover = new Color(75, 82, 99), // #4b5263 (gutter)
            ButtonPressed = new Color(50, 56, 66),
            ButtonText = new Color(171, 178, 191), // #abb2bf

            // Input colors
            InputBackground = new Color(33, 37, 43),
            InputText = new Color(171, 178, 191), // #abb2bf
            InputCursor = new Color(82, 139, 255), // #528bff
            InputSelection = new Color(62, 68, 81, 150), // #3e4451 with alpha

            // Border colors
            BorderPrimary = new Color(62, 68, 81), // #3e4451
            BorderFocus = new Color(97, 175, 239), // #61afef (blue)

            // Status colors (OneDark semantic colors)
            Success = new Color(152, 195, 121), // #98c379 (green)
            SuccessDim = new Color(120, 160, 95),
            Warning = new Color(229, 192, 123), // #e5c07b (yellow)
            WarningDim = new Color(190, 155, 90),
            WarningMild = new Color(255, 200, 50), // Light orange for mild warnings
            Error = new Color(224, 108, 117), // #e06c75 (red)
            ErrorDim = new Color(180, 85, 95),
            Info = new Color(97, 175, 239), // #61afef (blue)
            InfoDim = new Color(75, 140, 200),

            // Special purpose colors
            Prompt = new Color(97, 175, 239), // #61afef (blue)
            Highlight = new Color(229, 192, 123), // #e5c07b (yellow)
            ScrollbarTrack = new Color(33, 37, 43, 200),
            ScrollbarThumb = new Color(75, 82, 99, 200), // #4b5263
            ScrollbarThumbHover = new Color(92, 99, 112, 255), // #5c6370
            HoverBackground = new Color(50, 56, 66, 100), // BackgroundElevated with alpha
            CursorLineHighlight = new Color(97, 175, 239, 80), // Info with alpha

            // Spacing (pixels)
            PaddingTiny = 2,
            PaddingSmall = 4,
            PaddingMedium = 8,
            PaddingLarge = 12,
            PaddingXLarge = 20,
            MarginTiny = 2,
            MarginSmall = 4,
            MarginMedium = 8,
            MarginLarge = 12,
            MarginXLarge = 20,

            // Sizes
            FontSize = 16,
            LineHeight = 20,
            ScrollbarWidth = 10,
            ScrollbarPadding = 4,
            BorderWidth = 1,

            // Control sizes
            ButtonHeight = 30,
            InputHeight = 30,
            DropdownItemHeight = 25,
            PanelRowHeight = 25,

            // Animation
            AnimationSpeed = 1200f,
            FadeSpeed = 4f,
            CursorBlinkRate = 0.5f,

            // ═══════════════════════════════════════════════════════════════
            // CONSOLE-SPECIFIC COLORS
            // ═══════════════════════════════════════════════════════════════

            // Console semantic colors
            ConsolePrimary = new Color(97, 175, 239), // #61afef (blue)
            ConsolePrimaryDim = new Color(75, 140, 200),
            ConsolePrimaryBright = new Color(120, 190, 255),
            ConsolePrompt = new Color(97, 175, 239), // #61afef (blue)

            // Console output colors
            ConsoleOutputDefault = new Color(171, 178, 191), // #abb2bf
            ConsoleOutputSuccess = new Color(152, 195, 121), // #98c379 (green)
            ConsoleOutputInfo = new Color(86, 182, 194), // #56b6c2 (cyan)
            ConsoleOutputWarning = new Color(229, 192, 123), // #e5c07b (yellow)
            ConsoleOutputError = new Color(224, 108, 117), // #e06c75 (red)
            ConsoleOutputCommand = new Color(97, 175, 239), // #61afef (blue)
            ConsoleOutputAliasExpansion = new Color(92, 99, 112), // #5c6370 (comment)

            // Console component backgrounds
            ConsoleBackground = new Color(40, 44, 52, 240), // #282c34
            ConsoleOutputBackground = new Color(33, 37, 43, 255),
            ConsoleInputBackground = new Color(40, 44, 52, 255), // #282c34
            ConsoleSearchBackground = new Color(50, 56, 66, 255),
            ConsoleHintText = new Color(92, 99, 112, 200), // #5c6370 (comment)

            // Syntax highlighting (OneDark colors)
            SyntaxKeyword = new Color(198, 120, 221), // #c678dd (magenta)
            SyntaxString = new Color(152, 195, 121), // #98c379 (green)
            SyntaxStringInterpolation = new Color(229, 192, 123), // #e5c07b (yellow)
            SyntaxNumber = new Color(209, 154, 102), // #d19a66 (orange)
            SyntaxComment = new Color(92, 99, 112), // #5c6370 (comment)
            SyntaxType = new Color(229, 192, 123), // #e5c07b (yellow)
            SyntaxMethod = new Color(97, 175, 239), // #61afef (blue)
            SyntaxOperator = new Color(171, 178, 191), // #abb2bf
            SyntaxDefault = new Color(171, 178, 191), // #abb2bf

            // Auto-complete
            AutoCompleteSelected = new Color(97, 175, 239), // #61afef (blue)
            AutoCompleteUnselected = new Color(171, 178, 191), // #abb2bf
            AutoCompleteHover = new Color(120, 190, 255),
            AutoCompleteDescription = new Color(92, 99, 112), // #5c6370
            AutoCompleteLoading = new Color(92, 99, 112), // #5c6370

            // Search
            SearchCurrentMatch = new Color(229, 192, 123, 180), // #e5c07b (yellow)
            SearchOtherMatches = new Color(97, 175, 239, 120), // #61afef (blue)
            SearchPrompt = new Color(229, 192, 123), // #e5c07b (yellow)
            SearchSuccess = new Color(152, 195, 121), // #98c379 (green)
            SearchDisabled = new Color(92, 99, 112), // #5c6370
            ReverseSearchPrompt = new Color(229, 192, 123), // #e5c07b (yellow)
            ReverseSearchMatchHighlight = new Color(229, 192, 123), // #e5c07b (yellow)

            // Bracket matching
            BracketMatch = new Color(152, 195, 121, 150), // #98c379 (green)
            BracketMismatch = new Color(224, 108, 117, 150), // #e06c75 (red)

            // Line numbers
            LineNumberDim = new Color(75, 82, 99), // #4b5263 (gutter)
            LineNumberCurrent = new Color(171, 178, 191), // #abb2bf

            // Help/Documentation
            HelpTitle = new Color(229, 192, 123), // #e5c07b (yellow)
            HelpText = new Color(171, 178, 191), // #abb2bf
            HelpSectionHeader = new Color(97, 175, 239), // #61afef (blue)
            HelpExample = new Color(152, 195, 121), // #98c379 (green)
            DocumentationTypeInfo = new Color(229, 192, 123), // #e5c07b (yellow)
            DocumentationLabel = new Color(97, 175, 239), // #61afef (blue)
            DocumentationInstruction = new Color(92, 99, 112), // #5c6370

            // Section headers
            SectionCommand = new Color(97, 175, 239), // #61afef (blue)
            SectionError = new Color(224, 108, 117), // #e06c75 (red)
            SectionCategory = new Color(229, 192, 123), // #e5c07b (yellow)
            SectionManual = new Color(198, 120, 221), // #c678dd (magenta)
            SectionSearch = new Color(229, 192, 123), // #e5c07b (yellow)

            // Section folding
            SectionFoldBackground = new Color(33, 37, 43, 255),
            SectionFoldHover = new Color(62, 68, 81, 255), // #3e4451
            SectionFoldHoverOutline = new Color(75, 82, 99, 255), // #4b5263

            // ═══════════════════════════════════════════════════════════════
            // CONSOLE-SPECIFIC SIZES & SPACING
            // ═══════════════════════════════════════════════════════════════

            // Console component sizes
            MinInputHeight = 35f,
            MaxSuggestionsHeight = 300f,

            // Gaps & offsets
            TooltipGap = 5f,
            ComponentGap = 10f,
            PanelEdgeGap = 20f,
            SuggestionPadding = 20f,

            // Interactive thresholds
            DragThreshold = 5f,
            DoubleClickMaxDistance = 5f,
            DoubleClickThreshold = 0.5f,

            // ═══════════════════════════════════════════════════════════════
            // TAB-SPECIFIC COLORS
            // ═══════════════════════════════════════════════════════════════

            // Tab colors
            TabActive = new Color(40, 44, 52, 255), // #282c34
            TabInactive = new Color(33, 37, 43, 255),
            TabHover = new Color(50, 56, 66, 255),
            TabPressed = new Color(40, 44, 52, 255),
            TabBorder = new Color(62, 68, 81), // #3e4451
            TabActiveIndicator = new Color(97, 175, 239), // #61afef (blue)
            TabBarBackground = new Color(33, 37, 43, 255),
        };

    /// <summary>Monokai theme (Sublime Text classic)</summary>
    public static UITheme Monokai { get; } =
        new()
        {
            // Monokai: #272822 bg, #f8f8f2 fg, #75715e comment
            // #f92672 red, #a6e22e green, #e6db74 yellow, #66d9ef blue, #ae81ff purple
            BackgroundPrimary = new Color(39, 40, 34, 240),
            BackgroundSecondary = new Color(30, 31, 28, 255),
            BackgroundElevated = new Color(55, 56, 48, 255),
            TextPrimary = new Color(248, 248, 242),
            TextSecondary = new Color(200, 200, 190),
            TextDim = new Color(117, 113, 94),
            ButtonNormal = new Color(55, 56, 48),
            ButtonHover = new Color(70, 71, 62),
            ButtonPressed = new Color(45, 46, 40),
            ButtonText = new Color(248, 248, 242),
            InputBackground = new Color(30, 31, 28),
            InputText = new Color(248, 248, 242),
            InputCursor = new Color(248, 248, 242),
            InputSelection = new Color(73, 72, 62, 150),
            BorderPrimary = new Color(73, 72, 62),
            BorderFocus = new Color(102, 217, 239),
            Success = new Color(166, 226, 46),
            SuccessDim = new Color(130, 180, 35),
            Warning = new Color(230, 219, 116),
            WarningDim = new Color(190, 180, 90),
            WarningMild = new Color(253, 235, 110), // Light yellow for mild warnings
            Error = new Color(249, 38, 114),
            ErrorDim = new Color(200, 30, 90),
            Info = new Color(102, 217, 239),
            InfoDim = new Color(80, 175, 195),
            Prompt = new Color(102, 217, 239),
            Highlight = new Color(230, 219, 116),
            ScrollbarTrack = new Color(30, 31, 28, 200),
            ScrollbarThumb = new Color(73, 72, 62, 200),
            ScrollbarThumbHover = new Color(100, 99, 88, 255),
            HoverBackground = new Color(55, 56, 48, 100),
            CursorLineHighlight = new Color(102, 217, 239, 80),
            PaddingTiny = 2,
            PaddingSmall = 4,
            PaddingMedium = 8,
            PaddingLarge = 12,
            PaddingXLarge = 20,
            MarginTiny = 2,
            MarginSmall = 4,
            MarginMedium = 8,
            MarginLarge = 12,
            MarginXLarge = 20,
            FontSize = 16,
            LineHeight = 20,
            ScrollbarWidth = 10,
            ScrollbarPadding = 4,
            BorderWidth = 1,
            ButtonHeight = 30,
            InputHeight = 30,
            DropdownItemHeight = 25,
            PanelRowHeight = 25,
            AnimationSpeed = 1200f,
            FadeSpeed = 4f,
            CursorBlinkRate = 0.5f,
            ConsolePrimary = new Color(102, 217, 239),
            ConsolePrimaryDim = new Color(80, 175, 195),
            ConsolePrimaryBright = new Color(130, 230, 250),
            ConsolePrompt = new Color(102, 217, 239),
            ConsoleOutputDefault = new Color(248, 248, 242),
            ConsoleOutputSuccess = new Color(166, 226, 46),
            ConsoleOutputInfo = new Color(102, 217, 239),
            ConsoleOutputWarning = new Color(230, 219, 116),
            ConsoleOutputError = new Color(249, 38, 114),
            ConsoleOutputCommand = new Color(102, 217, 239),
            ConsoleOutputAliasExpansion = new Color(117, 113, 94),
            ConsoleBackground = new Color(39, 40, 34, 240),
            ConsoleOutputBackground = new Color(30, 31, 28, 255),
            ConsoleInputBackground = new Color(39, 40, 34, 255),
            ConsoleSearchBackground = new Color(55, 56, 48, 255),
            ConsoleHintText = new Color(117, 113, 94, 200),
            SyntaxKeyword = new Color(249, 38, 114),
            SyntaxString = new Color(230, 219, 116),
            SyntaxStringInterpolation = new Color(166, 226, 46),
            SyntaxNumber = new Color(174, 129, 255),
            SyntaxComment = new Color(117, 113, 94),
            SyntaxType = new Color(102, 217, 239),
            SyntaxMethod = new Color(166, 226, 46),
            SyntaxOperator = new Color(248, 248, 242),
            SyntaxDefault = new Color(248, 248, 242),
            AutoCompleteSelected = new Color(102, 217, 239),
            AutoCompleteUnselected = new Color(248, 248, 242),
            AutoCompleteHover = new Color(130, 230, 250),
            AutoCompleteDescription = new Color(117, 113, 94),
            AutoCompleteLoading = new Color(117, 113, 94),
            SearchCurrentMatch = new Color(230, 219, 116, 180),
            SearchOtherMatches = new Color(102, 217, 239, 120),
            SearchPrompt = new Color(230, 219, 116),
            SearchSuccess = new Color(166, 226, 46),
            SearchDisabled = new Color(117, 113, 94),
            ReverseSearchPrompt = new Color(230, 219, 116),
            ReverseSearchMatchHighlight = new Color(230, 219, 116),
            BracketMatch = new Color(166, 226, 46, 150),
            BracketMismatch = new Color(249, 38, 114, 150),
            LineNumberDim = new Color(73, 72, 62),
            LineNumberCurrent = new Color(248, 248, 242),
            HelpTitle = new Color(230, 219, 116),
            HelpText = new Color(248, 248, 242),
            HelpSectionHeader = new Color(102, 217, 239),
            HelpExample = new Color(166, 226, 46),
            DocumentationTypeInfo = new Color(230, 219, 116),
            DocumentationLabel = new Color(102, 217, 239),
            DocumentationInstruction = new Color(117, 113, 94),
            SectionCommand = new Color(102, 217, 239),
            SectionError = new Color(249, 38, 114),
            SectionCategory = new Color(230, 219, 116),
            SectionManual = new Color(174, 129, 255),
            SectionSearch = new Color(230, 219, 116),
            SectionFoldBackground = new Color(30, 31, 28, 255),
            SectionFoldHover = new Color(55, 56, 48, 255),
            SectionFoldHoverOutline = new Color(73, 72, 62, 255),
            MinInputHeight = 35f,
            MaxSuggestionsHeight = 300f,
            TooltipGap = 5f,
            ComponentGap = 10f,
            PanelEdgeGap = 20f,
            SuggestionPadding = 20f,
            DragThreshold = 5f,
            DoubleClickMaxDistance = 5f,
            DoubleClickThreshold = 0.5f,
            TabActive = new Color(39, 40, 34, 255),
            TabInactive = new Color(30, 31, 28, 255),
            TabHover = new Color(55, 56, 48, 255),
            TabPressed = new Color(39, 40, 34, 255),
            TabBorder = new Color(73, 72, 62),
            TabActiveIndicator = new Color(166, 226, 46),
            TabBarBackground = new Color(30, 31, 28, 255),
        };

    /// <summary>Dracula theme (popular dark theme)</summary>
    public static UITheme Dracula { get; } =
        new()
        {
            // Dracula: #282a36 bg, #f8f8f2 fg, #6272a4 comment
            // #ff5555 red, #50fa7b green, #f1fa8c yellow, #8be9fd cyan, #bd93f9 purple, #ff79c6 pink
            BackgroundPrimary = new Color(40, 42, 54, 240),
            BackgroundSecondary = new Color(33, 34, 44, 255),
            BackgroundElevated = new Color(68, 71, 90, 255),
            TextPrimary = new Color(248, 248, 242),
            TextSecondary = new Color(200, 200, 200),
            TextDim = new Color(98, 114, 164),
            ButtonNormal = new Color(68, 71, 90),
            ButtonHover = new Color(85, 88, 110),
            ButtonPressed = new Color(55, 57, 72),
            ButtonText = new Color(248, 248, 242),
            InputBackground = new Color(33, 34, 44),
            InputText = new Color(248, 248, 242),
            InputCursor = new Color(248, 248, 242),
            InputSelection = new Color(68, 71, 90, 150),
            BorderPrimary = new Color(68, 71, 90),
            BorderFocus = new Color(139, 233, 253),
            Success = new Color(80, 250, 123),
            SuccessDim = new Color(60, 200, 95),
            Warning = new Color(241, 250, 140),
            WarningDim = new Color(200, 205, 110),
            WarningMild = new Color(255, 230, 100), // Light yellow for mild warnings
            Error = new Color(255, 85, 85),
            ErrorDim = new Color(210, 70, 70),
            Info = new Color(139, 233, 253),
            InfoDim = new Color(110, 190, 210),
            Prompt = new Color(139, 233, 253),
            Highlight = new Color(241, 250, 140),
            ScrollbarTrack = new Color(33, 34, 44, 200),
            ScrollbarThumb = new Color(68, 71, 90, 200),
            ScrollbarThumbHover = new Color(98, 114, 164, 255),
            HoverBackground = new Color(68, 71, 90, 100),
            CursorLineHighlight = new Color(139, 233, 253, 80),
            PaddingTiny = 2,
            PaddingSmall = 4,
            PaddingMedium = 8,
            PaddingLarge = 12,
            PaddingXLarge = 20,
            MarginTiny = 2,
            MarginSmall = 4,
            MarginMedium = 8,
            MarginLarge = 12,
            MarginXLarge = 20,
            FontSize = 16,
            LineHeight = 20,
            ScrollbarWidth = 10,
            ScrollbarPadding = 4,
            BorderWidth = 1,
            ButtonHeight = 30,
            InputHeight = 30,
            DropdownItemHeight = 25,
            PanelRowHeight = 25,
            AnimationSpeed = 1200f,
            FadeSpeed = 4f,
            CursorBlinkRate = 0.5f,
            ConsolePrimary = new Color(139, 233, 253),
            ConsolePrimaryDim = new Color(110, 190, 210),
            ConsolePrimaryBright = new Color(160, 245, 255),
            ConsolePrompt = new Color(139, 233, 253),
            ConsoleOutputDefault = new Color(248, 248, 242),
            ConsoleOutputSuccess = new Color(80, 250, 123),
            ConsoleOutputInfo = new Color(139, 233, 253),
            ConsoleOutputWarning = new Color(241, 250, 140),
            ConsoleOutputError = new Color(255, 85, 85),
            ConsoleOutputCommand = new Color(139, 233, 253),
            ConsoleOutputAliasExpansion = new Color(98, 114, 164),
            ConsoleBackground = new Color(40, 42, 54, 240),
            ConsoleOutputBackground = new Color(33, 34, 44, 255),
            ConsoleInputBackground = new Color(40, 42, 54, 255),
            ConsoleSearchBackground = new Color(68, 71, 90, 255),
            ConsoleHintText = new Color(98, 114, 164, 200),
            SyntaxKeyword = new Color(255, 121, 198),
            SyntaxString = new Color(241, 250, 140),
            SyntaxStringInterpolation = new Color(80, 250, 123),
            SyntaxNumber = new Color(189, 147, 249),
            SyntaxComment = new Color(98, 114, 164),
            SyntaxType = new Color(139, 233, 253),
            SyntaxMethod = new Color(80, 250, 123),
            SyntaxOperator = new Color(248, 248, 242),
            SyntaxDefault = new Color(248, 248, 242),
            AutoCompleteSelected = new Color(189, 147, 249),
            AutoCompleteUnselected = new Color(248, 248, 242),
            AutoCompleteHover = new Color(210, 170, 255),
            AutoCompleteDescription = new Color(98, 114, 164),
            AutoCompleteLoading = new Color(98, 114, 164),
            SearchCurrentMatch = new Color(241, 250, 140, 180),
            SearchOtherMatches = new Color(189, 147, 249, 120),
            SearchPrompt = new Color(241, 250, 140),
            SearchSuccess = new Color(80, 250, 123),
            SearchDisabled = new Color(98, 114, 164),
            ReverseSearchPrompt = new Color(241, 250, 140),
            ReverseSearchMatchHighlight = new Color(241, 250, 140),
            BracketMatch = new Color(80, 250, 123, 150),
            BracketMismatch = new Color(255, 85, 85, 150),
            LineNumberDim = new Color(68, 71, 90),
            LineNumberCurrent = new Color(248, 248, 242),
            HelpTitle = new Color(241, 250, 140),
            HelpText = new Color(248, 248, 242),
            HelpSectionHeader = new Color(139, 233, 253),
            HelpExample = new Color(80, 250, 123),
            DocumentationTypeInfo = new Color(241, 250, 140),
            DocumentationLabel = new Color(139, 233, 253),
            DocumentationInstruction = new Color(98, 114, 164),
            SectionCommand = new Color(139, 233, 253),
            SectionError = new Color(255, 85, 85),
            SectionCategory = new Color(241, 250, 140),
            SectionManual = new Color(189, 147, 249),
            SectionSearch = new Color(241, 250, 140),
            SectionFoldBackground = new Color(33, 34, 44, 255),
            SectionFoldHover = new Color(68, 71, 90, 255),
            SectionFoldHoverOutline = new Color(98, 114, 164, 255),
            MinInputHeight = 35f,
            MaxSuggestionsHeight = 300f,
            TooltipGap = 5f,
            ComponentGap = 10f,
            PanelEdgeGap = 20f,
            SuggestionPadding = 20f,
            DragThreshold = 5f,
            DoubleClickMaxDistance = 5f,
            DoubleClickThreshold = 0.5f,
            TabActive = new Color(40, 42, 54, 255),
            TabInactive = new Color(33, 34, 44, 255),
            TabHover = new Color(68, 71, 90, 255),
            TabPressed = new Color(40, 42, 54, 255),
            TabBorder = new Color(68, 71, 90),
            TabActiveIndicator = new Color(189, 147, 249),
            TabBarBackground = new Color(33, 34, 44, 255),
        };

    /// <summary>Gruvbox Dark theme (retro, warm colors)</summary>
    public static UITheme GruvboxDark { get; } =
        new()
        {
            // Gruvbox: #282828 bg, #ebdbb2 fg, #928374 comment
            // #fb4934 red, #b8bb26 green, #fabd2f yellow, #83a598 blue, #d3869b purple, #8ec07c aqua
            BackgroundPrimary = new Color(40, 40, 40, 240),
            BackgroundSecondary = new Color(29, 32, 33, 255),
            BackgroundElevated = new Color(60, 56, 54, 255),
            TextPrimary = new Color(235, 219, 178),
            TextSecondary = new Color(189, 174, 147),
            TextDim = new Color(146, 131, 116),
            ButtonNormal = new Color(60, 56, 54),
            ButtonHover = new Color(80, 73, 69),
            ButtonPressed = new Color(50, 48, 47),
            ButtonText = new Color(235, 219, 178),
            InputBackground = new Color(29, 32, 33),
            InputText = new Color(235, 219, 178),
            InputCursor = new Color(235, 219, 178),
            InputSelection = new Color(69, 65, 59, 150),
            BorderPrimary = new Color(80, 73, 69),
            BorderFocus = new Color(131, 165, 152),
            Success = new Color(184, 187, 38),
            SuccessDim = new Color(142, 192, 124),
            Warning = new Color(250, 189, 47),
            WarningDim = new Color(215, 153, 33),
            WarningMild = new Color(254, 215, 90), // Light orange for mild warnings
            Error = new Color(251, 73, 52),
            ErrorDim = new Color(204, 36, 29),
            Info = new Color(131, 165, 152),
            InfoDim = new Color(104, 157, 106),
            Prompt = new Color(131, 165, 152),
            Highlight = new Color(250, 189, 47),
            ScrollbarTrack = new Color(29, 32, 33, 200),
            ScrollbarThumb = new Color(80, 73, 69, 200),
            ScrollbarThumbHover = new Color(102, 92, 84, 255),
            HoverBackground = new Color(60, 56, 54, 100),
            CursorLineHighlight = new Color(131, 165, 152, 80),
            PaddingTiny = 2,
            PaddingSmall = 4,
            PaddingMedium = 8,
            PaddingLarge = 12,
            PaddingXLarge = 20,
            MarginTiny = 2,
            MarginSmall = 4,
            MarginMedium = 8,
            MarginLarge = 12,
            MarginXLarge = 20,
            FontSize = 16,
            LineHeight = 20,
            ScrollbarWidth = 10,
            ScrollbarPadding = 4,
            BorderWidth = 1,
            ButtonHeight = 30,
            InputHeight = 30,
            DropdownItemHeight = 25,
            PanelRowHeight = 25,
            AnimationSpeed = 1200f,
            FadeSpeed = 4f,
            CursorBlinkRate = 0.5f,
            ConsolePrimary = new Color(131, 165, 152),
            ConsolePrimaryDim = new Color(104, 157, 106),
            ConsolePrimaryBright = new Color(142, 192, 124),
            ConsolePrompt = new Color(131, 165, 152),
            ConsoleOutputDefault = new Color(235, 219, 178),
            ConsoleOutputSuccess = new Color(184, 187, 38),
            ConsoleOutputInfo = new Color(142, 192, 124),
            ConsoleOutputWarning = new Color(250, 189, 47),
            ConsoleOutputError = new Color(251, 73, 52),
            ConsoleOutputCommand = new Color(131, 165, 152),
            ConsoleOutputAliasExpansion = new Color(146, 131, 116),
            ConsoleBackground = new Color(40, 40, 40, 240),
            ConsoleOutputBackground = new Color(29, 32, 33, 255),
            ConsoleInputBackground = new Color(40, 40, 40, 255),
            ConsoleSearchBackground = new Color(60, 56, 54, 255),
            ConsoleHintText = new Color(146, 131, 116, 200),
            SyntaxKeyword = new Color(251, 73, 52),
            SyntaxString = new Color(184, 187, 38),
            SyntaxStringInterpolation = new Color(250, 189, 47),
            SyntaxNumber = new Color(211, 134, 155),
            SyntaxComment = new Color(146, 131, 116),
            SyntaxType = new Color(250, 189, 47),
            SyntaxMethod = new Color(131, 165, 152),
            SyntaxOperator = new Color(235, 219, 178),
            SyntaxDefault = new Color(235, 219, 178),
            AutoCompleteSelected = new Color(131, 165, 152),
            AutoCompleteUnselected = new Color(235, 219, 178),
            AutoCompleteHover = new Color(142, 192, 124),
            AutoCompleteDescription = new Color(146, 131, 116),
            AutoCompleteLoading = new Color(146, 131, 116),
            SearchCurrentMatch = new Color(250, 189, 47, 180),
            SearchOtherMatches = new Color(131, 165, 152, 120),
            SearchPrompt = new Color(250, 189, 47),
            SearchSuccess = new Color(184, 187, 38),
            SearchDisabled = new Color(146, 131, 116),
            ReverseSearchPrompt = new Color(250, 189, 47),
            ReverseSearchMatchHighlight = new Color(250, 189, 47),
            BracketMatch = new Color(184, 187, 38, 150),
            BracketMismatch = new Color(251, 73, 52, 150),
            LineNumberDim = new Color(80, 73, 69),
            LineNumberCurrent = new Color(235, 219, 178),
            HelpTitle = new Color(250, 189, 47),
            HelpText = new Color(235, 219, 178),
            HelpSectionHeader = new Color(131, 165, 152),
            HelpExample = new Color(184, 187, 38),
            DocumentationTypeInfo = new Color(250, 189, 47),
            DocumentationLabel = new Color(131, 165, 152),
            DocumentationInstruction = new Color(146, 131, 116),
            SectionCommand = new Color(131, 165, 152),
            SectionError = new Color(251, 73, 52),
            SectionCategory = new Color(250, 189, 47),
            SectionManual = new Color(211, 134, 155),
            SectionSearch = new Color(250, 189, 47),
            SectionFoldBackground = new Color(29, 32, 33, 255),
            SectionFoldHover = new Color(60, 56, 54, 255),
            SectionFoldHoverOutline = new Color(80, 73, 69, 255),
            MinInputHeight = 35f,
            MaxSuggestionsHeight = 300f,
            TooltipGap = 5f,
            ComponentGap = 10f,
            PanelEdgeGap = 20f,
            SuggestionPadding = 20f,
            DragThreshold = 5f,
            DoubleClickMaxDistance = 5f,
            DoubleClickThreshold = 0.5f,
            TabActive = new Color(40, 40, 40, 255),
            TabInactive = new Color(29, 32, 33, 255),
            TabHover = new Color(60, 56, 54, 255),
            TabPressed = new Color(40, 40, 40, 255),
            TabBorder = new Color(80, 73, 69),
            TabActiveIndicator = new Color(215, 153, 33),
            TabBarBackground = new Color(29, 32, 33, 255),
        };

    /// <summary>Nord theme (arctic, cool colors)</summary>
    public static UITheme Nord { get; } =
        new()
        {
            // Nord: #2e3440 bg, #eceff4 fg, #4c566a comment
            // #bf616a red, #a3be8c green, #ebcb8b yellow, #81a1c1 blue, #b48ead purple, #88c0d0 cyan
            BackgroundPrimary = new Color(46, 52, 64, 240),
            BackgroundSecondary = new Color(59, 66, 82, 255),
            BackgroundElevated = new Color(67, 76, 94, 255),
            TextPrimary = new Color(236, 239, 244),
            TextSecondary = new Color(216, 222, 233),
            TextDim = new Color(76, 86, 106),
            ButtonNormal = new Color(67, 76, 94),
            ButtonHover = new Color(76, 86, 106),
            ButtonPressed = new Color(59, 66, 82),
            ButtonText = new Color(236, 239, 244),
            InputBackground = new Color(59, 66, 82),
            InputText = new Color(236, 239, 244),
            InputCursor = new Color(236, 239, 244),
            InputSelection = new Color(67, 76, 94, 150),
            BorderPrimary = new Color(67, 76, 94),
            BorderFocus = new Color(136, 192, 208),
            Success = new Color(163, 190, 140),
            SuccessDim = new Color(143, 188, 187),
            Warning = new Color(235, 203, 139),
            WarningDim = new Color(208, 135, 112),
            WarningMild = new Color(245, 220, 160), // Light warm for mild warnings
            Error = new Color(191, 97, 106),
            ErrorDim = new Color(180, 142, 173),
            Info = new Color(129, 161, 193),
            InfoDim = new Color(94, 129, 172),
            Prompt = new Color(136, 192, 208),
            Highlight = new Color(235, 203, 139),
            ScrollbarTrack = new Color(59, 66, 82, 200),
            ScrollbarThumb = new Color(76, 86, 106, 200),
            ScrollbarThumbHover = new Color(94, 129, 172, 255),
            HoverBackground = new Color(67, 76, 94, 100),
            CursorLineHighlight = new Color(129, 161, 193, 80),
            PaddingTiny = 2,
            PaddingSmall = 4,
            PaddingMedium = 8,
            PaddingLarge = 12,
            PaddingXLarge = 20,
            MarginTiny = 2,
            MarginSmall = 4,
            MarginMedium = 8,
            MarginLarge = 12,
            MarginXLarge = 20,
            FontSize = 16,
            LineHeight = 20,
            ScrollbarWidth = 10,
            ScrollbarPadding = 4,
            BorderWidth = 1,
            ButtonHeight = 30,
            InputHeight = 30,
            DropdownItemHeight = 25,
            PanelRowHeight = 25,
            AnimationSpeed = 1200f,
            FadeSpeed = 4f,
            CursorBlinkRate = 0.5f,
            ConsolePrimary = new Color(136, 192, 208),
            ConsolePrimaryDim = new Color(129, 161, 193),
            ConsolePrimaryBright = new Color(143, 188, 187),
            ConsolePrompt = new Color(136, 192, 208),
            ConsoleOutputDefault = new Color(236, 239, 244),
            ConsoleOutputSuccess = new Color(163, 190, 140),
            ConsoleOutputInfo = new Color(136, 192, 208),
            ConsoleOutputWarning = new Color(235, 203, 139),
            ConsoleOutputError = new Color(191, 97, 106),
            ConsoleOutputCommand = new Color(129, 161, 193),
            ConsoleOutputAliasExpansion = new Color(76, 86, 106),
            ConsoleBackground = new Color(46, 52, 64, 240),
            ConsoleOutputBackground = new Color(59, 66, 82, 255),
            ConsoleInputBackground = new Color(46, 52, 64, 255),
            ConsoleSearchBackground = new Color(67, 76, 94, 255),
            ConsoleHintText = new Color(76, 86, 106, 200),
            SyntaxKeyword = new Color(129, 161, 193),
            SyntaxString = new Color(163, 190, 140),
            SyntaxStringInterpolation = new Color(235, 203, 139),
            SyntaxNumber = new Color(180, 142, 173),
            SyntaxComment = new Color(76, 86, 106),
            SyntaxType = new Color(136, 192, 208),
            SyntaxMethod = new Color(136, 192, 208),
            SyntaxOperator = new Color(236, 239, 244),
            SyntaxDefault = new Color(236, 239, 244),
            AutoCompleteSelected = new Color(136, 192, 208),
            AutoCompleteUnselected = new Color(236, 239, 244),
            AutoCompleteHover = new Color(143, 188, 187),
            AutoCompleteDescription = new Color(76, 86, 106),
            AutoCompleteLoading = new Color(76, 86, 106),
            SearchCurrentMatch = new Color(235, 203, 139, 180),
            SearchOtherMatches = new Color(136, 192, 208, 120),
            SearchPrompt = new Color(235, 203, 139),
            SearchSuccess = new Color(163, 190, 140),
            SearchDisabled = new Color(76, 86, 106),
            ReverseSearchPrompt = new Color(235, 203, 139),
            ReverseSearchMatchHighlight = new Color(235, 203, 139),
            BracketMatch = new Color(163, 190, 140, 150),
            BracketMismatch = new Color(191, 97, 106, 150),
            LineNumberDim = new Color(67, 76, 94),
            LineNumberCurrent = new Color(236, 239, 244),
            HelpTitle = new Color(235, 203, 139),
            HelpText = new Color(236, 239, 244),
            HelpSectionHeader = new Color(136, 192, 208),
            HelpExample = new Color(163, 190, 140),
            DocumentationTypeInfo = new Color(235, 203, 139),
            DocumentationLabel = new Color(136, 192, 208),
            DocumentationInstruction = new Color(76, 86, 106),
            SectionCommand = new Color(136, 192, 208),
            SectionError = new Color(191, 97, 106),
            SectionCategory = new Color(235, 203, 139),
            SectionManual = new Color(180, 142, 173),
            SectionSearch = new Color(235, 203, 139),
            SectionFoldBackground = new Color(59, 66, 82, 255),
            SectionFoldHover = new Color(67, 76, 94, 255),
            SectionFoldHoverOutline = new Color(76, 86, 106, 255),
            MinInputHeight = 35f,
            MaxSuggestionsHeight = 300f,
            TooltipGap = 5f,
            ComponentGap = 10f,
            PanelEdgeGap = 20f,
            SuggestionPadding = 20f,
            DragThreshold = 5f,
            DoubleClickMaxDistance = 5f,
            DoubleClickThreshold = 0.5f,
            TabActive = new Color(46, 52, 64, 255),
            TabInactive = new Color(59, 66, 82, 255),
            TabHover = new Color(67, 76, 94, 255),
            TabPressed = new Color(46, 52, 64, 255),
            TabBorder = new Color(67, 76, 94),
            TabActiveIndicator = new Color(136, 192, 208),
            TabBarBackground = new Color(59, 66, 82, 255),
        };

    /// <summary>Solarized Dark theme (Ethan Schoonover's classic)</summary>
    public static UITheme SolarizedDark { get; } =
        new()
        {
            // Solarized: #002b36 bg, #839496 fg, #586e75 comment
            // #dc322f red, #859900 green, #b58900 yellow, #268bd2 blue, #6c71c4 violet, #2aa198 cyan
            BackgroundPrimary = new Color(0, 43, 54, 240),
            BackgroundSecondary = new Color(7, 54, 66, 255),
            BackgroundElevated = new Color(88, 110, 117, 50),
            TextPrimary = new Color(131, 148, 150),
            TextSecondary = new Color(147, 161, 161),
            TextDim = new Color(88, 110, 117),
            ButtonNormal = new Color(7, 54, 66),
            ButtonHover = new Color(88, 110, 117, 100),
            ButtonPressed = new Color(0, 43, 54),
            ButtonText = new Color(131, 148, 150),
            InputBackground = new Color(7, 54, 66),
            InputText = new Color(131, 148, 150),
            InputCursor = new Color(131, 148, 150),
            InputSelection = new Color(88, 110, 117, 100),
            BorderPrimary = new Color(88, 110, 117),
            BorderFocus = new Color(38, 139, 210),
            Success = new Color(133, 153, 0),
            SuccessDim = new Color(42, 161, 152),
            Warning = new Color(181, 137, 0),
            WarningDim = new Color(203, 75, 22),
            WarningMild = new Color(220, 180, 50), // Light orange for mild warnings
            Error = new Color(220, 50, 47),
            ErrorDim = new Color(211, 54, 130),
            Info = new Color(38, 139, 210),
            InfoDim = new Color(108, 113, 196),
            Prompt = new Color(38, 139, 210),
            Highlight = new Color(181, 137, 0),
            ScrollbarTrack = new Color(7, 54, 66, 200),
            ScrollbarThumb = new Color(88, 110, 117, 150),
            ScrollbarThumbHover = new Color(101, 123, 131, 200),
            HoverBackground = new Color(88, 110, 117, 100),
            CursorLineHighlight = new Color(38, 139, 210, 80),
            PaddingTiny = 2,
            PaddingSmall = 4,
            PaddingMedium = 8,
            PaddingLarge = 12,
            PaddingXLarge = 20,
            MarginTiny = 2,
            MarginSmall = 4,
            MarginMedium = 8,
            MarginLarge = 12,
            MarginXLarge = 20,
            FontSize = 16,
            LineHeight = 20,
            ScrollbarWidth = 10,
            ScrollbarPadding = 4,
            BorderWidth = 1,
            ButtonHeight = 30,
            InputHeight = 30,
            DropdownItemHeight = 25,
            PanelRowHeight = 25,
            AnimationSpeed = 1200f,
            FadeSpeed = 4f,
            CursorBlinkRate = 0.5f,
            ConsolePrimary = new Color(38, 139, 210),
            ConsolePrimaryDim = new Color(108, 113, 196),
            ConsolePrimaryBright = new Color(42, 161, 152),
            ConsolePrompt = new Color(38, 139, 210),
            ConsoleOutputDefault = new Color(131, 148, 150),
            ConsoleOutputSuccess = new Color(133, 153, 0),
            ConsoleOutputInfo = new Color(42, 161, 152),
            ConsoleOutputWarning = new Color(181, 137, 0),
            ConsoleOutputError = new Color(220, 50, 47),
            ConsoleOutputCommand = new Color(38, 139, 210),
            ConsoleOutputAliasExpansion = new Color(88, 110, 117),
            ConsoleBackground = new Color(0, 43, 54, 240),
            ConsoleOutputBackground = new Color(7, 54, 66, 255),
            ConsoleInputBackground = new Color(0, 43, 54, 255),
            ConsoleSearchBackground = new Color(7, 54, 66, 255),
            ConsoleHintText = new Color(88, 110, 117, 200),
            SyntaxKeyword = new Color(133, 153, 0),
            SyntaxString = new Color(42, 161, 152),
            SyntaxStringInterpolation = new Color(181, 137, 0),
            SyntaxNumber = new Color(211, 54, 130),
            SyntaxComment = new Color(88, 110, 117),
            SyntaxType = new Color(181, 137, 0),
            SyntaxMethod = new Color(38, 139, 210),
            SyntaxOperator = new Color(131, 148, 150),
            SyntaxDefault = new Color(131, 148, 150),
            AutoCompleteSelected = new Color(38, 139, 210),
            AutoCompleteUnselected = new Color(131, 148, 150),
            AutoCompleteHover = new Color(42, 161, 152),
            AutoCompleteDescription = new Color(88, 110, 117),
            AutoCompleteLoading = new Color(88, 110, 117),
            SearchCurrentMatch = new Color(181, 137, 0, 180),
            SearchOtherMatches = new Color(38, 139, 210, 120),
            SearchPrompt = new Color(181, 137, 0),
            SearchSuccess = new Color(133, 153, 0),
            SearchDisabled = new Color(88, 110, 117),
            ReverseSearchPrompt = new Color(181, 137, 0),
            ReverseSearchMatchHighlight = new Color(181, 137, 0),
            BracketMatch = new Color(133, 153, 0, 150),
            BracketMismatch = new Color(220, 50, 47, 150),
            LineNumberDim = new Color(88, 110, 117),
            LineNumberCurrent = new Color(131, 148, 150),
            HelpTitle = new Color(181, 137, 0),
            HelpText = new Color(131, 148, 150),
            HelpSectionHeader = new Color(38, 139, 210),
            HelpExample = new Color(133, 153, 0),
            DocumentationTypeInfo = new Color(181, 137, 0),
            DocumentationLabel = new Color(38, 139, 210),
            DocumentationInstruction = new Color(88, 110, 117),
            SectionCommand = new Color(38, 139, 210),
            SectionError = new Color(220, 50, 47),
            SectionCategory = new Color(181, 137, 0),
            SectionManual = new Color(108, 113, 196),
            SectionSearch = new Color(181, 137, 0),
            SectionFoldBackground = new Color(7, 54, 66, 255),
            SectionFoldHover = new Color(88, 110, 117, 100),
            SectionFoldHoverOutline = new Color(88, 110, 117, 255),
            MinInputHeight = 35f,
            MaxSuggestionsHeight = 300f,
            TooltipGap = 5f,
            ComponentGap = 10f,
            PanelEdgeGap = 20f,
            SuggestionPadding = 20f,
            DragThreshold = 5f,
            DoubleClickMaxDistance = 5f,
            DoubleClickThreshold = 0.5f,
            TabActive = new Color(0, 43, 54, 255),
            TabInactive = new Color(7, 54, 66, 255),
            TabHover = new Color(88, 110, 117, 100),
            TabPressed = new Color(0, 43, 54, 255),
            TabBorder = new Color(88, 110, 117),
            TabActiveIndicator = new Color(38, 139, 210),
            TabBarBackground = new Color(7, 54, 66, 255),
        };

    /// <summary>Pokéball theme - Dark with red/yellow accents inspired by Pokémon</summary>
    public static UITheme Pokeball { get; } =
        new()
        {
            // ═══════════════════════════════════════════════════════════════
            // POKÉBALL COLOR PALETTE
            // ═══════════════════════════════════════════════════════════════
            // Background:    #1a1a1d (26, 26, 29) - Inside the Pokéball (dark)
            // Foreground:    #e8e8e8 (232, 232, 232) - Clean white
            // Red:           #ee1515 (238, 21, 21) - Pokéball top
            // Yellow:        #ffcb05 (255, 203, 5) - Pikachu electric
            // Blue:          #3d7dca (61, 125, 202) - Great Ball
            // Green:         #78c850 (120, 200, 80) - Grass type
            // Orange:        #f08030 (240, 128, 48) - Fire type
            // Purple:        #705898 (112, 88, 152) - Ghost type
            // Cyan:          #6890f0 (104, 144, 240) - Water type
            // ═══════════════════════════════════════════════════════════════

            // Background colors - Dark like inside a Pokéball
            BackgroundPrimary = new Color(26, 26, 29, 240),
            BackgroundSecondary = new Color(20, 20, 23, 255),
            BackgroundElevated = new Color(38, 38, 42, 255),

            // Text colors - Clean whites
            TextPrimary = new Color(232, 232, 232),
            TextSecondary = new Color(180, 180, 185),
            TextDim = new Color(120, 120, 128),

            // Interactive element colors - Pokéball red accents
            ButtonNormal = new Color(45, 45, 50),
            ButtonHover = new Color(238, 21, 21, 200), // Pokéball red on hover
            ButtonPressed = new Color(200, 15, 15),
            ButtonText = new Color(232, 232, 232),

            // Input colors
            InputBackground = new Color(20, 20, 23),
            InputText = new Color(232, 232, 232),
            InputCursor = new Color(255, 203, 5), // Pikachu yellow cursor!
            InputSelection = new Color(238, 21, 21, 100), // Red selection

            // Border colors
            BorderPrimary = new Color(60, 60, 65),
            BorderFocus = new Color(238, 21, 21), // Pokéball red focus

            // Status colors - Pokémon type inspired
            Success = new Color(120, 200, 80), // Grass type green
            SuccessDim = new Color(90, 160, 60),
            Warning = new Color(255, 203, 5), // Pikachu yellow
            WarningDim = new Color(200, 160, 5),
            WarningMild = new Color(255, 220, 80), // Light electric yellow
            Error = new Color(238, 21, 21), // Pokéball red
            ErrorDim = new Color(180, 15, 15),
            Info = new Color(104, 144, 240), // Water type blue
            InfoDim = new Color(80, 115, 200),

            // Special purpose colors
            Prompt = new Color(255, 203, 5), // Pikachu yellow prompt
            Highlight = new Color(255, 203, 5),
            ScrollbarTrack = new Color(20, 20, 23, 200),
            ScrollbarThumb = new Color(238, 21, 21, 150), // Red scrollbar thumb
            ScrollbarThumbHover = new Color(238, 21, 21, 220),
            HoverBackground = new Color(38, 38, 42, 100),
            CursorLineHighlight = new Color(104, 144, 240, 80),

            // Spacing
            PaddingTiny = 2,
            PaddingSmall = 4,
            PaddingMedium = 8,
            PaddingLarge = 12,
            PaddingXLarge = 20,
            MarginTiny = 2,
            MarginSmall = 4,
            MarginMedium = 8,
            MarginLarge = 12,
            MarginXLarge = 20,
            FontSize = 16,
            LineHeight = 20,
            ScrollbarWidth = 10,
            ScrollbarPadding = 4,
            BorderWidth = 1,
            ButtonHeight = 30,
            InputHeight = 30,
            DropdownItemHeight = 25,
            PanelRowHeight = 25,
            AnimationSpeed = 1200f,
            FadeSpeed = 4f,
            CursorBlinkRate = 0.5f,

            // Console colors
            ConsolePrimary = new Color(255, 203, 5), // Pikachu yellow
            ConsolePrimaryDim = new Color(200, 160, 5),
            ConsolePrimaryBright = new Color(255, 220, 50),
            ConsolePrompt = new Color(255, 203, 5),
            ConsoleOutputDefault = new Color(232, 232, 232),
            ConsoleOutputSuccess = new Color(120, 200, 80), // Grass type
            ConsoleOutputInfo = new Color(104, 144, 240), // Water type
            ConsoleOutputWarning = new Color(240, 128, 48), // Fire type
            ConsoleOutputError = new Color(238, 21, 21), // Pokéball red
            ConsoleOutputCommand = new Color(255, 203, 5),
            ConsoleOutputAliasExpansion = new Color(120, 120, 128),
            ConsoleBackground = new Color(26, 26, 29, 240),
            ConsoleOutputBackground = new Color(20, 20, 23, 255),
            ConsoleInputBackground = new Color(26, 26, 29, 255),
            ConsoleSearchBackground = new Color(38, 38, 42, 255),
            ConsoleHintText = new Color(120, 120, 128, 200),

            // Syntax highlighting - Type-based colors!
            SyntaxKeyword = new Color(238, 21, 21), // Pokéball red
            SyntaxString = new Color(120, 200, 80), // Grass type green
            SyntaxStringInterpolation = new Color(255, 203, 5),
            SyntaxNumber = new Color(240, 128, 48), // Fire type orange
            SyntaxComment = new Color(120, 120, 128),
            SyntaxType = new Color(104, 144, 240), // Water type blue
            SyntaxMethod = new Color(255, 203, 5), // Pikachu yellow
            SyntaxOperator = new Color(232, 232, 232),
            SyntaxDefault = new Color(232, 232, 232),

            // Autocomplete
            AutoCompleteSelected = new Color(238, 21, 21),
            AutoCompleteUnselected = new Color(232, 232, 232),
            AutoCompleteHover = new Color(255, 203, 5),
            AutoCompleteDescription = new Color(120, 120, 128),
            AutoCompleteLoading = new Color(120, 120, 128),

            // Search
            SearchCurrentMatch = new Color(255, 203, 5, 180),
            SearchOtherMatches = new Color(238, 21, 21, 120),
            SearchPrompt = new Color(255, 203, 5),
            SearchSuccess = new Color(120, 200, 80),
            SearchDisabled = new Color(120, 120, 128),
            ReverseSearchPrompt = new Color(255, 203, 5),
            ReverseSearchMatchHighlight = new Color(255, 203, 5),

            // Brackets
            BracketMatch = new Color(120, 200, 80, 150),
            BracketMismatch = new Color(238, 21, 21, 150),

            // Line numbers
            LineNumberDim = new Color(120, 120, 128),
            LineNumberCurrent = new Color(255, 203, 5),

            // Help/Documentation
            HelpTitle = new Color(255, 203, 5),
            HelpText = new Color(232, 232, 232),
            HelpSectionHeader = new Color(238, 21, 21),
            HelpExample = new Color(120, 200, 80),
            DocumentationTypeInfo = new Color(104, 144, 240),
            DocumentationLabel = new Color(255, 203, 5),
            DocumentationInstruction = new Color(120, 120, 128),

            // Sections
            SectionCommand = new Color(255, 203, 5),
            SectionError = new Color(238, 21, 21),
            SectionCategory = new Color(240, 128, 48),
            SectionManual = new Color(112, 88, 152), // Ghost type purple
            SectionSearch = new Color(255, 203, 5),
            SectionFoldBackground = new Color(20, 20, 23, 255),
            SectionFoldHover = new Color(238, 21, 21, 100),
            SectionFoldHoverOutline = new Color(238, 21, 21, 255),

            // Layout
            MinInputHeight = 35f,
            MaxSuggestionsHeight = 300f,
            TooltipGap = 5f,
            ComponentGap = 10f,
            PanelEdgeGap = 20f,
            SuggestionPadding = 20f,
            DragThreshold = 5f,
            DoubleClickMaxDistance = 5f,
            DoubleClickThreshold = 0.5f,

            // Tabs - Pokéball inspired
            TabActive = new Color(26, 26, 29, 255),
            TabInactive = new Color(20, 20, 23, 255),
            TabHover = new Color(238, 21, 21, 100),
            TabPressed = new Color(200, 15, 15, 255),
            TabBorder = new Color(60, 60, 65),
            TabActiveIndicator = new Color(255, 203, 5), // Pikachu yellow indicator
            TabBarBackground = new Color(20, 20, 23, 255),
        };

    /// <summary>Solarized Light theme (Ethan Schoonover's classic - light variant)</summary>
    public static UITheme SolarizedLight { get; } =
        new()
        {
            // ═══════════════════════════════════════════════════════════════
            // SOLARIZED LIGHT COLOR PALETTE
            // ═══════════════════════════════════════════════════════════════
            // Background:    #fdf6e3 (253, 246, 227) - base3
            // Foreground:    #657b83 (101, 123, 131) - base00
            // Comment:       #93a1a1 (147, 161, 161) - base1
            // Accent colors same as dark variant
            // ═══════════════════════════════════════════════════════════════

            // Background colors - Light cream/paper tones
            BackgroundPrimary = new Color(253, 246, 227, 250), // #fdf6e3 (base3)
            BackgroundSecondary = new Color(238, 232, 213, 255), // #eee8d5 (base2)
            BackgroundElevated = new Color(253, 246, 227, 255), // Slightly elevated

            // Text colors - Dark on light
            TextPrimary = new Color(101, 123, 131), // #657b83 (base00)
            TextSecondary = new Color(88, 110, 117), // #586e75 (base01)
            TextDim = new Color(147, 161, 161), // #93a1a1 (base1)

            // Interactive elements
            ButtonNormal = new Color(238, 232, 213), // #eee8d5 (base2)
            ButtonHover = new Color(147, 161, 161, 100),
            ButtonPressed = new Color(238, 232, 213),
            ButtonText = new Color(101, 123, 131), // #657b83

            // Input colors
            InputBackground = new Color(253, 246, 227), // #fdf6e3
            InputText = new Color(101, 123, 131), // #657b83
            InputCursor = new Color(38, 139, 210), // #268bd2 (blue)
            InputSelection = new Color(38, 139, 210, 80),

            // Border colors
            BorderPrimary = new Color(147, 161, 161), // #93a1a1 (base1)
            BorderFocus = new Color(38, 139, 210), // #268bd2 (blue)

            // Status colors (same accent colors as dark)
            Success = new Color(133, 153, 0), // #859900 (green)
            SuccessDim = new Color(42, 161, 152), // #2aa198 (cyan)
            Warning = new Color(181, 137, 0), // #b58900 (yellow)
            WarningDim = new Color(203, 75, 22), // #cb4b16 (orange)
            WarningMild = new Color(220, 180, 50), // Light orange for mild warnings
            Error = new Color(220, 50, 47), // #dc322f (red)
            ErrorDim = new Color(211, 54, 130), // #d33682 (magenta)
            Info = new Color(38, 139, 210), // #268bd2 (blue)
            InfoDim = new Color(108, 113, 196), // #6c71c4 (violet)

            // Special purpose colors
            Prompt = new Color(38, 139, 210), // #268bd2 (blue)
            Highlight = new Color(181, 137, 0), // #b58900 (yellow)
            ScrollbarTrack = new Color(238, 232, 213, 200), // #eee8d5
            ScrollbarThumb = new Color(147, 161, 161, 180), // #93a1a1
            ScrollbarThumbHover = new Color(101, 123, 131, 220), // #657b83
            HoverBackground = new Color(238, 232, 213, 150), // base2 with alpha
            CursorLineHighlight = new Color(38, 139, 210, 50), // blue with alpha

            // Spacing
            PaddingTiny = 2,
            PaddingSmall = 4,
            PaddingMedium = 8,
            PaddingLarge = 12,
            PaddingXLarge = 20,
            MarginTiny = 2,
            MarginSmall = 4,
            MarginMedium = 8,
            MarginLarge = 12,
            MarginXLarge = 20,
            FontSize = 16,
            LineHeight = 20,
            ScrollbarWidth = 10,
            ScrollbarPadding = 4,
            BorderWidth = 1,
            ButtonHeight = 30,
            InputHeight = 30,
            DropdownItemHeight = 25,
            PanelRowHeight = 25,
            AnimationSpeed = 1200f,
            FadeSpeed = 4f,
            CursorBlinkRate = 0.5f,

            // Console colors
            ConsolePrimary = new Color(38, 139, 210), // #268bd2 (blue)
            ConsolePrimaryDim = new Color(108, 113, 196), // #6c71c4 (violet)
            ConsolePrimaryBright = new Color(42, 161, 152), // #2aa198 (cyan)
            ConsolePrompt = new Color(38, 139, 210),
            ConsoleOutputDefault = new Color(101, 123, 131), // #657b83
            ConsoleOutputSuccess = new Color(133, 153, 0), // #859900
            ConsoleOutputInfo = new Color(42, 161, 152), // #2aa198 (cyan)
            ConsoleOutputWarning = new Color(181, 137, 0), // #b58900
            ConsoleOutputError = new Color(220, 50, 47), // #dc322f
            ConsoleOutputCommand = new Color(38, 139, 210), // #268bd2
            ConsoleOutputAliasExpansion = new Color(147, 161, 161), // #93a1a1
            ConsoleBackground = new Color(253, 246, 227, 250), // #fdf6e3
            ConsoleOutputBackground = new Color(238, 232, 213, 255), // #eee8d5
            ConsoleInputBackground = new Color(253, 246, 227, 255),
            ConsoleSearchBackground = new Color(238, 232, 213, 255),
            ConsoleHintText = new Color(147, 161, 161, 200), // #93a1a1

            // Syntax highlighting
            SyntaxKeyword = new Color(133, 153, 0), // #859900 (green)
            SyntaxString = new Color(42, 161, 152), // #2aa198 (cyan)
            SyntaxStringInterpolation = new Color(181, 137, 0), // #b58900 (yellow)
            SyntaxNumber = new Color(211, 54, 130), // #d33682 (magenta)
            SyntaxComment = new Color(147, 161, 161), // #93a1a1 (base1)
            SyntaxType = new Color(181, 137, 0), // #b58900 (yellow)
            SyntaxMethod = new Color(38, 139, 210), // #268bd2 (blue)
            SyntaxOperator = new Color(101, 123, 131), // #657b83
            SyntaxDefault = new Color(101, 123, 131), // #657b83

            // Autocomplete
            AutoCompleteSelected = new Color(38, 139, 210), // #268bd2
            AutoCompleteUnselected = new Color(101, 123, 131), // #657b83
            AutoCompleteHover = new Color(42, 161, 152), // #2aa198
            AutoCompleteDescription = new Color(147, 161, 161), // #93a1a1
            AutoCompleteLoading = new Color(147, 161, 161),

            // Search
            SearchCurrentMatch = new Color(181, 137, 0, 150), // Yellow with alpha
            SearchOtherMatches = new Color(38, 139, 210, 80), // Blue with alpha
            SearchPrompt = new Color(181, 137, 0), // #b58900
            SearchSuccess = new Color(133, 153, 0), // #859900
            SearchDisabled = new Color(147, 161, 161), // #93a1a1
            ReverseSearchPrompt = new Color(181, 137, 0),
            ReverseSearchMatchHighlight = new Color(181, 137, 0),

            // Brackets
            BracketMatch = new Color(133, 153, 0, 120), // Green with alpha
            BracketMismatch = new Color(220, 50, 47, 120), // Red with alpha

            // Line numbers
            LineNumberDim = new Color(147, 161, 161), // #93a1a1
            LineNumberCurrent = new Color(101, 123, 131), // #657b83

            // Help/Documentation
            HelpTitle = new Color(181, 137, 0), // #b58900
            HelpText = new Color(101, 123, 131), // #657b83
            HelpSectionHeader = new Color(38, 139, 210), // #268bd2
            HelpExample = new Color(133, 153, 0), // #859900
            DocumentationTypeInfo = new Color(181, 137, 0), // #b58900
            DocumentationLabel = new Color(38, 139, 210), // #268bd2
            DocumentationInstruction = new Color(147, 161, 161), // #93a1a1

            // Sections
            SectionCommand = new Color(38, 139, 210), // #268bd2
            SectionError = new Color(220, 50, 47), // #dc322f
            SectionCategory = new Color(181, 137, 0), // #b58900
            SectionManual = new Color(108, 113, 196), // #6c71c4
            SectionSearch = new Color(181, 137, 0), // #b58900
            SectionFoldBackground = new Color(238, 232, 213, 255),
            SectionFoldHover = new Color(147, 161, 161, 80),
            SectionFoldHoverOutline = new Color(147, 161, 161, 200),

            // Layout
            MinInputHeight = 35f,
            MaxSuggestionsHeight = 300f,
            TooltipGap = 5f,
            ComponentGap = 10f,
            PanelEdgeGap = 20f,
            SuggestionPadding = 20f,
            DragThreshold = 5f,
            DoubleClickMaxDistance = 5f,
            DoubleClickThreshold = 0.5f,

            // Tabs
            TabActive = new Color(253, 246, 227, 255), // #fdf6e3
            TabInactive = new Color(238, 232, 213, 255), // #eee8d5
            TabHover = new Color(147, 161, 161, 80),
            TabPressed = new Color(253, 246, 227, 255),
            TabBorder = new Color(147, 161, 161), // #93a1a1
            TabActiveIndicator = new Color(38, 139, 210), // #268bd2
            TabBarBackground = new Color(238, 232, 213, 255), // #eee8d5
        };

    // Background colors
    public Color BackgroundPrimary { get; init; }
    public Color BackgroundSecondary { get; init; }
    public Color BackgroundElevated { get; init; }

    // Text colors
    public Color TextPrimary { get; init; }
    public Color TextSecondary { get; init; }
    public Color TextDim { get; init; }

    // Interactive element colors
    public Color ButtonNormal { get; init; }
    public Color ButtonHover { get; init; }
    public Color ButtonPressed { get; init; }
    public Color ButtonText { get; init; }

    // Input colors
    public Color InputBackground { get; init; }
    public Color InputText { get; init; }
    public Color InputCursor { get; init; }
    public Color InputSelection { get; init; }

    // Border colors
    public Color BorderPrimary { get; init; }
    public Color BorderFocus { get; init; }

    // Status colors
    public Color Success { get; init; }
    public Color SuccessDim { get; init; }
    public Color Warning { get; init; }
    public Color WarningDim { get; init; }
    public Color WarningMild { get; init; }
    public Color Error { get; init; }
    public Color ErrorDim { get; init; }
    public Color Info { get; init; }
    public Color InfoDim { get; init; }

    // Special purpose colors
    public Color Prompt { get; init; }
    public Color Highlight { get; init; }
    public Color ScrollbarTrack { get; init; }
    public Color ScrollbarThumb { get; init; }
    public Color ScrollbarThumbHover { get; init; }

    // Pre-computed faded colors (for performance - avoid allocating on every access)
    public Color HoverBackground { get; init; }
    public Color CursorLineHighlight { get; init; }

    // Spacing
    public float PaddingTiny { get; init; }
    public float PaddingSmall { get; init; }
    public float PaddingMedium { get; init; }
    public float PaddingLarge { get; init; }
    public float PaddingXLarge { get; init; }

    public float MarginTiny { get; init; }
    public float MarginSmall { get; init; }
    public float MarginMedium { get; init; }
    public float MarginLarge { get; init; }
    public float MarginXLarge { get; init; }

    // Sizes
    public int FontSize { get; init; }
    public int LineHeight { get; init; }
    public int ScrollbarWidth { get; init; }
    public int ScrollbarPadding { get; init; }
    public int BorderWidth { get; init; }

    // Control sizes
    public int ButtonHeight { get; init; }
    public int InputHeight { get; init; }
    public int DropdownItemHeight { get; init; }
    public int PanelRowHeight { get; init; }

    // Animation
    public float AnimationSpeed { get; init; }
    public float FadeSpeed { get; init; }
    public float CursorBlinkRate { get; init; }

    // ═══════════════════════════════════════════════════════════════
    // CONSOLE-SPECIFIC COLORS
    // ═══════════════════════════════════════════════════════════════

    // Console semantic colors
    public Color ConsolePrimary { get; init; }
    public Color ConsolePrimaryDim { get; init; }
    public Color ConsolePrimaryBright { get; init; }
    public Color ConsolePrompt { get; init; }

    // Console output colors
    public Color ConsoleOutputDefault { get; init; }
    public Color ConsoleOutputSuccess { get; init; }
    public Color ConsoleOutputInfo { get; init; }
    public Color ConsoleOutputWarning { get; init; }
    public Color ConsoleOutputError { get; init; }
    public Color ConsoleOutputCommand { get; init; }
    public Color ConsoleOutputAliasExpansion { get; init; }

    // Console component backgrounds
    public Color ConsoleBackground { get; init; }
    public Color ConsoleOutputBackground { get; init; }
    public Color ConsoleInputBackground { get; init; }
    public Color ConsoleSearchBackground { get; init; }
    public Color ConsoleHintText { get; init; }

    // Syntax highlighting
    public Color SyntaxKeyword { get; init; }
    public Color SyntaxString { get; init; }
    public Color SyntaxStringInterpolation { get; init; }
    public Color SyntaxNumber { get; init; }
    public Color SyntaxComment { get; init; }
    public Color SyntaxType { get; init; }
    public Color SyntaxMethod { get; init; }
    public Color SyntaxOperator { get; init; }
    public Color SyntaxDefault { get; init; }

    // Auto-complete
    public Color AutoCompleteSelected { get; init; }
    public Color AutoCompleteUnselected { get; init; }
    public Color AutoCompleteHover { get; init; }
    public Color AutoCompleteDescription { get; init; }
    public Color AutoCompleteLoading { get; init; }

    // Search
    public Color SearchCurrentMatch { get; init; }
    public Color SearchOtherMatches { get; init; }
    public Color SearchPrompt { get; init; }
    public Color SearchSuccess { get; init; }
    public Color SearchDisabled { get; init; }
    public Color ReverseSearchPrompt { get; init; }
    public Color ReverseSearchMatchHighlight { get; init; }

    // Bracket matching
    public Color BracketMatch { get; init; }
    public Color BracketMismatch { get; init; }

    // Line numbers
    public Color LineNumberDim { get; init; }
    public Color LineNumberCurrent { get; init; }

    // Help/Documentation
    public Color HelpTitle { get; init; }
    public Color HelpText { get; init; }
    public Color HelpSectionHeader { get; init; }
    public Color HelpExample { get; init; }
    public Color DocumentationTypeInfo { get; init; }
    public Color DocumentationLabel { get; init; }
    public Color DocumentationInstruction { get; init; }

    // Section headers
    public Color SectionCommand { get; init; }
    public Color SectionError { get; init; }
    public Color SectionCategory { get; init; }
    public Color SectionManual { get; init; }
    public Color SectionSearch { get; init; }

    // Section folding
    public Color SectionFoldBackground { get; init; }
    public Color SectionFoldHover { get; init; }
    public Color SectionFoldHoverOutline { get; init; }

    // ═══════════════════════════════════════════════════════════════
    // CONSOLE-SPECIFIC SIZES & SPACING
    // ═══════════════════════════════════════════════════════════════

    // Console component sizes
    public float MinInputHeight { get; init; }
    public float MaxSuggestionsHeight { get; init; }

    // Gaps & offsets
    public float TooltipGap { get; init; }
    public float ComponentGap { get; init; }
    public float PanelEdgeGap { get; init; }
    public float SuggestionPadding { get; init; }

    // Interactive thresholds
    public float DragThreshold { get; init; }
    public float DoubleClickMaxDistance { get; init; }
    public float DoubleClickThreshold { get; init; }

    // ═══════════════════════════════════════════════════════════════
    // TAB-SPECIFIC COLORS
    // ═══════════════════════════════════════════════════════════════

    // Tab colors
    public Color TabActive { get; init; }
    public Color TabInactive { get; init; }
    public Color TabHover { get; init; }
    public Color TabPressed { get; init; }
    public Color TabBorder { get; init; }
    public Color TabActiveIndicator { get; init; }
    public Color TabBarBackground { get; init; }
}
