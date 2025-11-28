using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Components.Layout;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
///     A quick command palette for executing debug actions.
///     Similar to VS Code's command palette.
/// </summary>
public class QuickCommandPalette : Panel
{
    private InputField? _searchField;

    public QuickCommandPalette()
    {
        // Colors set dynamically in OnRenderContainer for theme switching
        BorderThickness = 2;

        InitializeComponents();
    }

    /// <summary>
    ///     Available commands
    /// </summary>
    public List<DebugCommand> Commands { get; set; } = new();

    /// <summary>
    ///     Search/filter text
    /// </summary>
    public string FilterText { get; set; } = string.Empty;

    /// <summary>
    ///     Callback when command is executed
    /// </summary>
    public Action<DebugCommand>? OnCommandExecuted { get; set; }

    private void InitializeComponents()
    {
        UITheme theme = UITheme.Dark;

        // Title
        var titleLabel = new Label
        {
            Id = Id + "_title",
            Text = "Quick Commands",
            Color = theme.Info,
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.TopLeft,
                Margin = theme.PaddingMedium,
            },
        };
        AddChild(titleLabel);

        // Search field
        _searchField = new InputField
        {
            Id = Id + "_search",
            Placeholder = "Search commands...",
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.TopLeft,
                OffsetY = 30,
                WidthPercent = 0.9f,
                Height = theme.InputHeight,
                MarginLeft = theme.PaddingMedium,
            },
            OnTextChanged = text =>
            {
                FilterText = text;
                UpdateFilteredCommands();
            },
        };
        AddChild(_searchField);

        // Hint text
        var hintLabel = new Label
        {
            Id = Id + "_hint",
            Text = "Type to search, click to execute",
            Color = theme.TextDim,
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.TopLeft,
                OffsetY = 75,
                MarginLeft = theme.PaddingMedium,
            },
        };
        AddChild(hintLabel);
    }

    protected override void OnRenderContainer(UIContext context)
    {
        // Set theme colors dynamically for theme switching
        BackgroundColor = ThemeManager.Current.BackgroundElevated;
        BorderColor = ThemeManager.Current.BorderFocus;

        base.OnRenderContainer(context);

        // Update command list
        UpdateCommandButtons(context);
    }

    private void UpdateCommandButtons(UIContext context)
    {
        UITheme theme = context.Theme;

        // Clear old buttons (skip title, search field, and hint)
        while (Children.Count > 3)
        {
            Children.RemoveAt(Children.Count - 1);
        }

        // Get filtered commands
        List<DebugCommand> filteredCommands = string.IsNullOrWhiteSpace(FilterText)
            ? Commands
            : Commands
                .Where(c =>
                    c.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
                    || (
                        c.Description?.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
                        ?? false
                    )
                )
                .ToList();

        // Create buttons for filtered commands
        float yOffset = 105;
        int maxVisible = 8;

        for (int i = 0; i < Math.Min(filteredCommands.Count, maxVisible); i++)
        {
            DebugCommand command = filteredCommands[i];
            var button = new Button
            {
                Id = $"{Id}_cmd_{i}",
                Text = command.Name,
                Constraint = new LayoutConstraint
                {
                    Anchor = Anchor.TopLeft,
                    OffsetY = yOffset,
                    WidthPercent = 0.9f,
                    Height = theme.ButtonHeight,
                    MarginLeft = theme.PaddingMedium,
                },
                OnClick = () =>
                {
                    command.Execute?.Invoke();
                    OnCommandExecuted?.Invoke(command);
                },
            };

            AddChild(button);

            // Add description label if available
            if (!string.IsNullOrEmpty(command.Description))
            {
                var descLabel = new Label
                {
                    Id = $"{Id}_desc_{i}",
                    Text = command.Description,
                    Color = theme.TextDim,
                    Constraint = new LayoutConstraint
                    {
                        Anchor = Anchor.TopLeft,
                        OffsetY = yOffset + theme.ButtonHeight + 2,
                        MarginLeft = theme.PaddingMedium + 5,
                    },
                };
                AddChild(descLabel);
                yOffset += theme.ButtonHeight + 22;
            }
            else
            {
                yOffset += theme.ButtonHeight + 5;
            }
        }

        // Show count if filtered
        if (filteredCommands.Count > maxVisible)
        {
            var moreLabel = new Label
            {
                Id = Id + "_more",
                Text = $"+ {filteredCommands.Count - maxVisible} more commands...",
                Color = theme.TextSecondary,
                Constraint = new LayoutConstraint
                {
                    Anchor = Anchor.TopLeft,
                    OffsetY = yOffset,
                    MarginLeft = theme.PaddingMedium,
                },
            };
            AddChild(moreLabel);
        }
    }

    private void UpdateFilteredCommands()
    {
        // Filtering happens in UpdateCommandButtons
        // This method exists for future enhancements
    }
}

/// <summary>
///     Represents a debug command that can be executed.
/// </summary>
public class DebugCommand
{
    /// <summary>Command name (shown in list)</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description</summary>
    public string? Description { get; set; }

    /// <summary>Category for organization</summary>
    public string Category { get; set; } = "General";

    /// <summary>Action to execute</summary>
    public Action? Execute { get; set; }

    /// <summary>Optional keyboard shortcut hint</summary>
    public string? Shortcut { get; set; }
}
