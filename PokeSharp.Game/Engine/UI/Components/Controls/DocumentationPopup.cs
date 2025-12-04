using Microsoft.Xna.Framework;
using PokeSharp.Game.Engine.UI.Debug.Components.Base;
using PokeSharp.Game.Engine.UI.Debug.Core;
using PokeSharp.Game.Engine.UI.Debug.Layout;

namespace PokeSharp.Game.Engine.UI.Debug.Components.Controls;

/// <summary>
///     Documentation information for display.
/// </summary>
public class DocInfo
{
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string? ReturnType { get; set; }
    public string? Signature { get; set; }
    public List<ParamDoc> Parameters { get; set; } = new();
    public string? Remarks { get; set; }
    public string? Example { get; set; }
}

/// <summary>
///     Parameter documentation.
/// </summary>
public class ParamDoc
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

/// <summary>
///     Popup that displays detailed documentation for code completions.
///     Activated by pressing F1 on a selected autocomplete item.
/// </summary>
public class DocumentationPopup : UIComponent
{
    // Visual properties - nullable for theme fallback
    private Color? _backgroundColor;
    private Color? _borderColor;
    private DocInfo? _documentation;
    private Color? _exampleColor;
    private Color? _parameterDescColor;
    private Color? _parameterNameColor;
    private Color? _sectionHeaderColor;
    private Color? _signatureColor;
    private Color? _summaryColor;
    private Color? _titleColor;

    public DocumentationPopup(string id)
    {
        Id = id;
    }

    public Color BackgroundColor
    {
        get => _backgroundColor ?? ThemeManager.Current.BackgroundElevated;
        set => _backgroundColor = value;
    }

    public Color BorderColor
    {
        get => _borderColor ?? ThemeManager.Current.BorderPrimary;
        set => _borderColor = value;
    }

    public Color TitleColor
    {
        get => _titleColor ?? ThemeManager.Current.SyntaxMethod;
        set => _titleColor = value;
    }

    public Color SummaryColor
    {
        get => _summaryColor ?? ThemeManager.Current.TextSecondary;
        set => _summaryColor = value;
    }

    public Color SignatureColor
    {
        get => _signatureColor ?? ThemeManager.Current.TextPrimary;
        set => _signatureColor = value;
    }

    public Color SectionHeaderColor
    {
        get => _sectionHeaderColor ?? ThemeManager.Current.Info;
        set => _sectionHeaderColor = value;
    }

    public Color ParameterNameColor
    {
        get => _parameterNameColor ?? ThemeManager.Current.SyntaxType;
        set => _parameterNameColor = value;
    }

    public Color ParameterDescColor
    {
        get => _parameterDescColor ?? ThemeManager.Current.TextSecondary;
        set => _parameterDescColor = value;
    }

    public Color ExampleColor
    {
        get => _exampleColor ?? ThemeManager.Current.SyntaxString;
        set => _exampleColor = value;
    }

    public float Padding { get; set; } = 12f;
    public float LineSpacing { get; set; } = 4f;
    public float SectionSpacing { get; set; } = 8f;
    public float BorderThickness { get; set; } = 1f;
    public float MaxWidth { get; set; } = 500f;

    /// <summary>
    ///     Gets whether the popup has content to display.
    /// </summary>
    public bool HasContent => _documentation != null;

    /// <summary>
    ///     Sets the documentation to display.
    /// </summary>
    public void SetDocumentation(DocInfo? doc)
    {
        _documentation = doc;
    }

    /// <summary>
    ///     Clears the documentation.
    /// </summary>
    public void Clear()
    {
        _documentation = null;
    }

    protected override void OnRender(UIContext context)
    {
        if (!HasContent || Rect.Height <= 0)
        {
            return;
        }

        UIRenderer renderer = Renderer;
        LayoutRect resolvedRect = Rect;
        int lineHeight = renderer.GetLineHeight();

        // Draw background
        renderer.DrawRectangle(resolvedRect, BackgroundColor);

        // Draw border
        renderer.DrawRectangleOutline(resolvedRect, BorderColor, (int)BorderThickness);

        float yPos = resolvedRect.Y + Padding;
        float xPos = resolvedRect.X + Padding;
        float contentWidth = resolvedRect.Width - (Padding * 2);

        // Title
        if (!string.IsNullOrEmpty(_documentation!.Title))
        {
            renderer.DrawText(_documentation.Title, new Vector2(xPos, yPos), TitleColor);
            yPos += lineHeight + LineSpacing;
        }

        // Signature
        if (!string.IsNullOrEmpty(_documentation.Signature))
        {
            List<string> wrappedSignature = WrapText(
                _documentation.Signature,
                contentWidth,
                renderer
            );
            foreach (string line in wrappedSignature)
            {
                renderer.DrawText(line, new Vector2(xPos, yPos), SignatureColor);
                yPos += lineHeight;
            }

            yPos += SectionSpacing;
        }

        // Summary
        if (!string.IsNullOrEmpty(_documentation.Summary))
        {
            List<string> wrappedSummary = WrapText(_documentation.Summary, contentWidth, renderer);
            foreach (string line in wrappedSummary)
            {
                renderer.DrawText(line, new Vector2(xPos, yPos), SummaryColor);
                yPos += lineHeight;
            }

            yPos += SectionSpacing;
        }

        // Return type
        if (!string.IsNullOrEmpty(_documentation.ReturnType))
        {
            renderer.DrawText("Returns:", new Vector2(xPos, yPos), SectionHeaderColor);
            yPos += lineHeight + LineSpacing;

            renderer.DrawText(
                $"  {_documentation.ReturnType}",
                new Vector2(xPos, yPos),
                SummaryColor
            );
            yPos += lineHeight + SectionSpacing;
        }

        // Parameters
        if (_documentation.Parameters.Count > 0)
        {
            renderer.DrawText("Parameters:", new Vector2(xPos, yPos), SectionHeaderColor);
            yPos += lineHeight + LineSpacing;

            foreach (ParamDoc param in _documentation.Parameters)
            {
                // Parameter name
                renderer.DrawText($"  {param.Name}", new Vector2(xPos, yPos), ParameterNameColor);
                yPos += lineHeight;

                // Parameter description
                if (!string.IsNullOrEmpty(param.Description))
                {
                    List<string> wrappedDesc = WrapText(
                        param.Description,
                        contentWidth - 20,
                        renderer
                    );
                    foreach (string line in wrappedDesc)
                    {
                        renderer.DrawText(
                            $"    {line}",
                            new Vector2(xPos, yPos),
                            ParameterDescColor
                        );
                        yPos += lineHeight;
                    }
                }
            }

            yPos += SectionSpacing;
        }

        // Remarks
        if (!string.IsNullOrEmpty(_documentation.Remarks))
        {
            renderer.DrawText("Remarks:", new Vector2(xPos, yPos), SectionHeaderColor);
            yPos += lineHeight + LineSpacing;

            List<string> wrappedRemarks = WrapText(_documentation.Remarks, contentWidth, renderer);
            foreach (string line in wrappedRemarks)
            {
                renderer.DrawText($"  {line}", new Vector2(xPos, yPos), SummaryColor);
                yPos += lineHeight;
            }

            yPos += SectionSpacing;
        }

        // Example
        if (!string.IsNullOrEmpty(_documentation.Example))
        {
            renderer.DrawText("Example:", new Vector2(xPos, yPos), SectionHeaderColor);
            yPos += lineHeight + LineSpacing;

            string[] exampleLines = _documentation.Example.Split('\n');
            foreach (string line in exampleLines)
            {
                renderer.DrawText($"  {line}", new Vector2(xPos, yPos), ExampleColor);
                yPos += lineHeight;
            }
        }
    }

    /// <summary>
    ///     Wraps text to fit within a specified width.
    /// </summary>
    private List<string> WrapText(string text, float maxWidth, UIRenderer renderer)
    {
        var result = new List<string>();
        string[] words = text.Split(' ');
        string currentLine = "";

        foreach (string word in words)
        {
            string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
            float testWidth = renderer.MeasureText(testLine).X;

            if (testWidth > maxWidth && !string.IsNullOrEmpty(currentLine))
            {
                result.Add(currentLine);
                currentLine = word;
            }
            else
            {
                currentLine = testLine;
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
        {
            result.Add(currentLine);
        }

        return result;
    }

    /// <summary>
    ///     Calculates the desired height for the popup based on content.
    /// </summary>
    public float GetDesiredHeight(UIRenderer? renderer = null)
    {
        if (!HasContent)
        {
            return 0;
        }

        float lineHeight = 20f; // Default fallback

        if (renderer != null)
        {
            lineHeight = renderer.GetLineHeight();
        }
        else
        {
            try
            {
                if (Renderer != null)
                {
                    lineHeight = Renderer.GetLineHeight();
                }
            }
            catch
            {
                // No context available, use default
            }
        }

        // Calculate approximate height based on content
        float height = Padding * 2; // Top and bottom padding

        // Title
        if (!string.IsNullOrEmpty(_documentation!.Title))
        {
            height += lineHeight + LineSpacing;
        }

        // Signature (estimate 2 lines max)
        if (!string.IsNullOrEmpty(_documentation.Signature))
        {
            height += (lineHeight * 2) + SectionSpacing;
        }

        // Summary (estimate based on length)
        if (!string.IsNullOrEmpty(_documentation.Summary))
        {
            int estimatedLines = Math.Max(1, _documentation.Summary.Length / 60);
            height += (lineHeight * estimatedLines) + SectionSpacing;
        }

        // Return type
        if (!string.IsNullOrEmpty(_documentation.ReturnType))
        {
            height += (lineHeight * 2) + SectionSpacing;
        }

        // Parameters
        if (_documentation.Parameters.Count > 0)
        {
            height += lineHeight + LineSpacing; // Header
            foreach (ParamDoc param in _documentation.Parameters)
            {
                height += lineHeight; // Param name
                if (!string.IsNullOrEmpty(param.Description))
                {
                    int estimatedLines = Math.Max(1, param.Description.Length / 50);
                    height += lineHeight * estimatedLines;
                }
            }

            height += SectionSpacing;
        }

        // Remarks
        if (!string.IsNullOrEmpty(_documentation.Remarks))
        {
            int estimatedLines = Math.Max(1, _documentation.Remarks.Length / 60);
            height += lineHeight + LineSpacing + (lineHeight * estimatedLines) + SectionSpacing;
        }

        // Example
        if (!string.IsNullOrEmpty(_documentation.Example))
        {
            int exampleLines = _documentation.Example.Split('\n').Length;
            height += lineHeight + LineSpacing + (lineHeight * exampleLines);
        }

        // Cap at a reasonable max height
        return Math.Min(height, 600f);
    }
}
