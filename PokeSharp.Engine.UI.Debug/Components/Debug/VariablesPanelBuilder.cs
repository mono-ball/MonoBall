using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
///     Builder for creating VariablesPanel with customizable components.
/// </summary>
public class VariablesPanelBuilder
{
    private int _maxLines = 1000;
    private TextBuffer? _variablesBuffer;

    public static VariablesPanelBuilder Create()
    {
        return new VariablesPanelBuilder();
    }

    public VariablesPanelBuilder WithVariablesBuffer(TextBuffer buffer)
    {
        _variablesBuffer = buffer;
        return this;
    }

    public VariablesPanelBuilder WithMaxLines(int maxLines)
    {
        _maxLines = maxLines;
        return this;
    }

    public VariablesPanel Build()
    {
        return new VariablesPanel(
            _variablesBuffer ?? CreateDefaultVariablesBuffer(),
            CreateDefaultStatusBar()
        );
    }

    private TextBuffer CreateDefaultVariablesBuffer()
    {
        return new TextBuffer("variables_buffer")
        {
            // BackgroundColor uses theme fallback - don't set explicitly
            AutoScroll = false,
            MaxLines = _maxLines,
            Constraint = new LayoutConstraint { Anchor = Anchor.StretchTop },
        };
    }

    private static StatusBar CreateDefaultStatusBar()
    {
        return new StatusBar("variables_status")
        {
            Constraint = new LayoutConstraint { Anchor = Anchor.StretchBottom },
        };
    }
}
