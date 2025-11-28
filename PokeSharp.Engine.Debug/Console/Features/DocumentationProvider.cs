using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;

namespace PokeSharp.Engine.Debug.Console.Features;

/// <summary>
///     Provides detailed documentation for autocomplete items.
/// </summary>
public class DocumentationProvider
{
    /// <summary>
    ///     Gets documentation for a completion item.
    /// </summary>
    public async Task<Documentation> GetDocumentationAsync(
        CompletionItem item,
        CompletionService? completionService = null,
        Document? document = null
    )
    {
        var doc = new Documentation { DisplayText = item.DisplayText, Kind = GetItemKind(item) };

        // Try to get description from the item itself
        if (!string.IsNullOrEmpty(item.InlineDescription))
        {
            doc = doc with { Summary = item.InlineDescription };
        }

        // If we have completion service and document, get extended documentation
        if (completionService != null && document != null)
        {
            try
            {
                CompletionDescription? description = await completionService.GetDescriptionAsync(
                    document,
                    item
                );
                if (description != null)
                {
                    doc = ParseDescription(doc, description);
                }
            }
            catch
            {
                // Fallback to basic info if extended documentation fails
            }
        }

        // Generate signature
        doc = doc with
        {
            Signature = GenerateSignature(item, doc),
        };

        return doc;
    }

    /// <summary>
    ///     Gets the kind/type of the completion item.
    /// </summary>
    private string GetItemKind(CompletionItem item)
    {
        // Check tags to determine item kind
        if (item.Tags.Contains("Method"))
        {
            return "Method";
        }

        if (item.Tags.Contains("Property"))
        {
            return "Property";
        }

        if (item.Tags.Contains("Field"))
        {
            return "Field";
        }

        if (item.Tags.Contains("Class"))
        {
            return "Class";
        }

        if (item.Tags.Contains("Interface"))
        {
            return "Interface";
        }

        if (item.Tags.Contains("Enum"))
        {
            return "Enum";
        }

        if (item.Tags.Contains("Struct"))
        {
            return "Struct";
        }

        if (item.Tags.Contains("Namespace"))
        {
            return "Namespace";
        }

        if (item.Tags.Contains("Variable"))
        {
            return "Variable";
        }

        if (item.Tags.Contains("Keyword"))
        {
            return "Keyword";
        }

        return "Symbol";
    }

    /// <summary>
    ///     Parses CompletionDescription to extract detailed documentation.
    /// </summary>
    private Documentation ParseDescription(Documentation doc, CompletionDescription description)
    {
        var summary = new StringBuilder();
        var parameters = new List<ParameterInfo>();
        string? returns = null;

        foreach (TaggedText part in description.TaggedParts)
        {
            // Handle different XML doc sections
            if (part.Tag == "Summary")
            {
                summary.Append(part.Text);
            }
            else if (part.Tag == "Returns")
            {
                returns = part.Text;
            }
            else if (part.Tag == "Param")
            {
                // Parameter documentation (format: "paramName: description")
                string paramText = part.Text;
                int colonIndex = paramText.IndexOf(':');
                if (colonIndex > 0)
                {
                    string paramName = paramText.Substring(0, colonIndex).Trim();
                    string paramDesc = paramText.Substring(colonIndex + 1).Trim();
                    parameters.Add(new ParameterInfo(paramName, "unknown", paramDesc));
                }
            }
            else if (part.Tag != "LineBreak")
            {
                // For signature parts, add to summary if not already categorized
                if (summary.Length > 0 && part.Text.Trim().Length > 0)
                {
                    summary.Append(' ');
                }

                summary.Append(part.Text);
            }
        }

        string summaryText = summary.ToString().Trim();
        if (summaryText.Length > 0)
        {
            doc = doc with { Summary = summaryText };
        }

        if (parameters.Count > 0)
        {
            doc = doc with { Parameters = parameters };
        }

        if (returns != null)
        {
            doc = doc with { Returns = returns };
        }

        return doc;
    }

    /// <summary>
    ///     Generates a signature string for the item.
    /// </summary>
    private string GenerateSignature(CompletionItem item, Documentation doc)
    {
        var sb = new StringBuilder();

        // Add kind prefix
        sb.Append($"({doc.Kind.ToLower()}) ");

        // Add display text
        sb.Append(item.DisplayText);

        // Add parameters if it's a method
        if (doc.Kind == "Method" && doc.Parameters.Count > 0)
        {
            sb.Append('(');
            for (int i = 0; i < doc.Parameters.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                ParameterInfo param = doc.Parameters[i];
                sb.Append(param.Type != "unknown" ? $"{param.Type} {param.Name}" : param.Name);
            }

            sb.Append(')');
        }
        else if (doc.Kind == "Method")
        {
            sb.Append("()");
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Formats documentation for display.
    /// </summary>
    public string FormatForDisplay(Documentation doc, int maxWidth = 60)
    {
        var sb = new StringBuilder();
        string separator = new('═', maxWidth);
        string topBorder = "╔" + separator + "╗";
        string bottomBorder = "╚" + separator + "╝";

        sb.AppendLine(topBorder);
        sb.AppendLine($"║ {CenterText("Documentation", maxWidth)} ║");
        sb.AppendLine(bottomBorder);
        sb.AppendLine();

        // Signature
        if (!string.IsNullOrEmpty(doc.Signature))
        {
            sb.AppendLine($"  {doc.Signature}");
            sb.AppendLine();
        }

        // Summary
        if (!string.IsNullOrEmpty(doc.Summary))
        {
            sb.AppendLine("  Summary:");
            WrapText(sb, doc.Summary, maxWidth - 4, "    ");
            sb.AppendLine();
        }

        // Parameters
        if (doc.Parameters.Count > 0)
        {
            sb.AppendLine("  Parameters:");
            foreach (ParameterInfo param in doc.Parameters)
            {
                sb.Append($"    {param.Name}");
                if (!string.IsNullOrEmpty(param.Description))
                {
                    sb.Append($" - {param.Description}");
                }

                sb.AppendLine();
            }

            sb.AppendLine();
        }

        // Returns
        if (!string.IsNullOrEmpty(doc.Returns))
        {
            sb.AppendLine("  Returns:");
            WrapText(sb, doc.Returns, maxWidth - 4, "    ");
            sb.AppendLine();
        }

        // If no detailed info, show helpful message
        if (!doc.HasDetailedInfo)
        {
            sb.AppendLine("  No additional documentation available.");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Centers text within a given width.
    /// </summary>
    private string CenterText(string text, int width)
    {
        if (text.Length >= width)
        {
            return text.Substring(0, width);
        }

        int padding = (width - text.Length) / 2;
        return new string(' ', padding) + text + new string(' ', width - text.Length - padding);
    }

    /// <summary>
    ///     Wraps text to fit within a maximum width.
    /// </summary>
    private void WrapText(StringBuilder sb, string text, int maxWidth, string indent)
    {
        string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var currentLine = new StringBuilder(indent);

        foreach (string word in words)
        {
            if (currentLine.Length + word.Length + 1 > maxWidth)
            {
                sb.AppendLine(currentLine.ToString());
                currentLine.Clear();
                currentLine.Append(indent);
            }

            if (currentLine.Length > indent.Length)
            {
                currentLine.Append(' ');
            }

            currentLine.Append(word);
        }

        if (currentLine.Length > indent.Length)
        {
            sb.AppendLine(currentLine.ToString());
        }
    }

    /// <summary>
    ///     Represents detailed documentation for a completion item.
    /// </summary>
    public record Documentation
    {
        public string DisplayText { get; init; } = string.Empty;
        public string? Signature { get; init; }
        public string? Summary { get; init; }
        public List<ParameterInfo> Parameters { get; init; } = new();
        public string? Returns { get; init; }
        public string? Example { get; init; }
        public string? Remarks { get; init; }
        public string Kind { get; init; } = "Unknown";

        public bool HasDetailedInfo =>
            !string.IsNullOrEmpty(Summary)
            || Parameters.Count > 0
            || !string.IsNullOrEmpty(Returns);
    }

    /// <summary>
    ///     Represents parameter information.
    /// </summary>
    public record ParameterInfo(string Name, string Type, string? Description);
}
