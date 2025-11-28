using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
/// Builder for creating ConsolePanel with customizable components.
/// Allows dependency injection for testing and custom configurations.
/// </summary>
public class ConsolePanelBuilder
{
    private TextBuffer? _outputBuffer;
    private TextEditor? _commandEditor;
    private SuggestionsDropdown? _suggestionsDropdown;
    private HintBar? _hintBar;
    private SearchBar? _searchBar;
    private HintBar? _searchHintBar;
    private ParameterHintTooltip? _parameterHints;
    private DocumentationPopup? _documentationPopup;

    private int _maxOutputLines = 5000;
    private int _maxVisibleSuggestions = 8;
    private bool _loadHistoryFromDisk = true;

    /// <summary>
    /// Creates a new ConsolePanelBuilder with default settings.
    /// </summary>
    public static ConsolePanelBuilder Create() => new();

    /// <summary>
    /// Sets a custom output buffer component.
    /// </summary>
    public ConsolePanelBuilder WithOutputBuffer(TextBuffer outputBuffer)
    {
        _outputBuffer = outputBuffer;
        return this;
    }

    /// <summary>
    /// Sets a custom command editor component.
    /// </summary>
    public ConsolePanelBuilder WithCommandEditor(TextEditor commandEditor)
    {
        _commandEditor = commandEditor;
        return this;
    }

    /// <summary>
    /// Sets a custom suggestions dropdown component.
    /// </summary>
    public ConsolePanelBuilder WithSuggestionsDropdown(SuggestionsDropdown dropdown)
    {
        _suggestionsDropdown = dropdown;
        return this;
    }

    /// <summary>
    /// Sets a custom hint bar component.
    /// </summary>
    public ConsolePanelBuilder WithHintBar(HintBar hintBar)
    {
        _hintBar = hintBar;
        return this;
    }

    /// <summary>
    /// Sets a custom search bar component.
    /// </summary>
    public ConsolePanelBuilder WithSearchBar(SearchBar searchBar)
    {
        _searchBar = searchBar;
        return this;
    }

    /// <summary>
    /// Sets a custom search hint bar component.
    /// </summary>
    public ConsolePanelBuilder WithSearchHintBar(HintBar searchHintBar)
    {
        _searchHintBar = searchHintBar;
        return this;
    }

    /// <summary>
    /// Sets a custom parameter hints component.
    /// </summary>
    public ConsolePanelBuilder WithParameterHints(ParameterHintTooltip parameterHints)
    {
        _parameterHints = parameterHints;
        return this;
    }

    /// <summary>
    /// Sets a custom documentation popup component.
    /// </summary>
    public ConsolePanelBuilder WithDocumentationPopup(DocumentationPopup popup)
    {
        _documentationPopup = popup;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of output lines to retain.
    /// </summary>
    public ConsolePanelBuilder WithMaxOutputLines(int maxLines)
    {
        _maxOutputLines = maxLines;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of visible suggestions in the dropdown.
    /// </summary>
    public ConsolePanelBuilder WithMaxVisibleSuggestions(int maxVisible)
    {
        _maxVisibleSuggestions = maxVisible;
        return this;
    }

    /// <summary>
    /// Sets whether to load command history from disk on startup.
    /// </summary>
    public ConsolePanelBuilder WithHistoryPersistence(bool enabled)
    {
        _loadHistoryFromDisk = enabled;
        return this;
    }

    /// <summary>
    /// Builds the ConsolePanel with the configured components.
    /// </summary>
    public ConsolePanel Build()
    {
        return new ConsolePanel(
            _outputBuffer ?? CreateDefaultOutputBuffer(),
            _commandEditor ?? CreateDefaultCommandEditor(),
            _suggestionsDropdown ?? CreateDefaultSuggestionsDropdown(),
            _hintBar ?? CreateDefaultHintBar(),
            _searchBar ?? CreateDefaultSearchBar(),
            _searchHintBar ?? CreateDefaultSearchHintBar(),
            _parameterHints ?? CreateDefaultParameterHints(),
            _documentationPopup ?? CreateDefaultDocumentationPopup(),
            _loadHistoryFromDisk
        );
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Default Component Factories
    // ═══════════════════════════════════════════════════════════════════════

    private TextBuffer CreateDefaultOutputBuffer()
    {
        return new TextBuffer("console_output")
        {
            // BackgroundColor uses theme fallback - don't set explicitly
            AutoScroll = true,
            MaxLines = _maxOutputLines,
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.StretchTop,
                Height = 400
            }
        };
    }

    private TextEditor CreateDefaultCommandEditor()
    {
        return new TextEditor("console_input")
        {
            Prompt = Core.NerdFontIcons.Prompt,
            // BackgroundColor uses theme fallback - don't set explicitly
            MinVisibleLines = 1,
            MaxVisibleLines = 10,
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.StretchBottom,
                OffsetY = 0,
                Height = 30 // Fixed height
            }
        };
    }

    private SuggestionsDropdown CreateDefaultSuggestionsDropdown()
    {
        return new SuggestionsDropdown("console_suggestions")
        {
            MaxVisibleItems = _maxVisibleSuggestions,
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.BottomLeft,
                OffsetX = ThemeManager.Current.ComponentGap,
                OffsetY = -(ThemeManager.Current.MinInputHeight + ThemeManager.Current.ComponentGap),
                WidthPercent = 0.5f,
                MinWidth = 400f,
                MaxWidth = 800f,
                Height = 0
            }
        };
    }

    private HintBar CreateDefaultHintBar()
    {
        return new HintBar("console_hints")
        {
            // TextColor uses theme fallback - don't set explicitly
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.StretchBottom,
                OffsetY = 0,
                Height = 0
            }
        };
    }

    private SearchBar CreateDefaultSearchBar()
    {
        return new SearchBar("console_search")
        {
            // BackgroundColor uses theme fallback - don't set explicitly
            BorderThickness = 0,
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.StretchBottom,
                OffsetY = 0,
                Height = 30 // Fixed height
            }
        };
    }

    private HintBar CreateDefaultSearchHintBar()
    {
        return new HintBar("search_hints")
        {
            // TextColor uses theme fallback - don't set explicitly
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.StretchBottom,
                OffsetY = 0,
                Height = 0
            }
        };
    }

    private ParameterHintTooltip CreateDefaultParameterHints()
    {
        return new ParameterHintTooltip("parameter_hints")
        {
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.BottomLeft,
                OffsetY = 0,
                Height = 0,
                Width = 0,
                MaxWidth = 800f,
                MaxHeight = 300f
            }
        };
    }

    private DocumentationPopup CreateDefaultDocumentationPopup()
    {
        return new DocumentationPopup("documentation_popup")
        {
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.TopRight,
                OffsetX = -ThemeManager.Current.PanelEdgeGap,
                OffsetY = ThemeManager.Current.PanelEdgeGap,
                WidthPercent = 0.35f,
                MinWidth = 400f,
                MaxWidth = 600f,
                HeightPercent = 0.7f,
                Height = 0
            }
        };
    }
}

