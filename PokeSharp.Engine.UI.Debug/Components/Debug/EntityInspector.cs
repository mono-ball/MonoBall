using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Components.Layout;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Layout;
using PokeSharp.Engine.UI.Debug.Models;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
///     A panel that displays entity properties and components.
///     Example debug component for inspecting game entities.
/// </summary>
public class EntityInspector : Panel
{
    private ScrollView? _propertiesScrollView;

    public EntityInspector()
    {
        // Colors set dynamically in OnRenderContainer for theme switching
        BorderThickness = 1;

        InitializeComponents();
    }

    /// <summary>
    ///     Entity to inspect (would be an actual entity in the game)
    /// </summary>
    public EntityInfo? SelectedEntity { get; set; }

    private void InitializeComponents()
    {
        UITheme theme = UITheme.Dark;

        // Title
        var titleLabel = new Label
        {
            Id = Id + "_title",
            Text = "Entity Inspector",
            Color = theme.Info,
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.TopLeft,
                Margin = theme.PaddingMedium,
            },
        };
        AddChild(titleLabel);

        // Entity ID label
        var entityIdLabel = new Label
        {
            Id = Id + "_entity_id",
            Text = "Entity: <none>",
            Color = theme.TextSecondary,
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.TopLeft,
                OffsetY = 25,
                MarginLeft = theme.PaddingMedium,
            },
        };
        AddChild(entityIdLabel);

        // Properties scroll view
        _propertiesScrollView = new ScrollView
        {
            Id = Id + "_properties",
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.TopLeft,
                OffsetY = 55,
                WidthPercent = 0.9f,
                HeightPercent = 0.7f,
                Margin = theme.PaddingMedium,
            },
        };
        AddChild(_propertiesScrollView);
    }

    protected override void OnRenderContainer(UIContext context)
    {
        // Set theme colors dynamically for theme switching
        BackgroundColor = ThemeManager.Current.BackgroundSecondary;
        BorderColor = ThemeManager.Current.BorderPrimary;

        base.OnRenderContainer(context);

        if (_propertiesScrollView == null)
        {
            return;
        }

        // Clear previous properties
        _propertiesScrollView.ClearChildren();

        if (SelectedEntity != null)
        {
            // Add property labels
            float yOffset = 0;
            float lineHeight = ThemeManager.Current.PanelRowHeight;

            foreach (KeyValuePair<string, string> property in SelectedEntity.Properties)
            {
                var propertyLabel = new Label
                {
                    Id = $"{Id}_prop_{property.Key}",
                    Text = $"{property.Key}: {property.Value}",
                    Color = context.Theme.TextPrimary,
                    Constraint = new LayoutConstraint
                    {
                        Anchor = Anchor.TopLeft,
                        OffsetY = yOffset,
                        Height = lineHeight,
                    },
                };

                _propertiesScrollView.AddChild(propertyLabel);
                yOffset += lineHeight;
            }

            // Add components section
            yOffset += 10;
            var componentsLabel = new Label
            {
                Id = Id + "_components_header",
                Text = "Components:",
                Color = context.Theme.Info,
                Constraint = new LayoutConstraint
                {
                    Anchor = Anchor.TopLeft,
                    OffsetY = yOffset,
                    Height = lineHeight,
                },
            };
            _propertiesScrollView.AddChild(componentsLabel);
            yOffset += lineHeight;

            foreach (string component in SelectedEntity.Components)
            {
                var componentLabel = new Label
                {
                    Id = $"{Id}_comp_{component}",
                    Text = $"- {component}",
                    Color = context.Theme.Success,
                    Constraint = new LayoutConstraint
                    {
                        Anchor = Anchor.TopLeft,
                        OffsetY = yOffset,
                        OffsetX = 10,
                        Height = lineHeight,
                    },
                };

                _propertiesScrollView.AddChild(componentLabel);
                yOffset += lineHeight;
            }
        }
        else
        {
            // No entity selected
            var noSelectionLabel = new Label
            {
                Id = Id + "_no_selection",
                Text = "No entity selected",
                Color = context.Theme.TextDim,
                Constraint = new LayoutConstraint { Anchor = Anchor.TopLeft },
            };
            _propertiesScrollView.AddChild(noSelectionLabel);
        }
    }
}
