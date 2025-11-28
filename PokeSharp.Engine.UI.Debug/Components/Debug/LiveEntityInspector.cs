using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Components.Layout;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Layout;
using PokeSharp.Engine.UI.Debug.Models;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
///     Live entity inspector with entity selection and refresh.
///     Enhanced version that supports real-time updates.
/// </summary>
public class LiveEntityInspector : Panel
{
    private Dropdown? _entityDropdown;
    private ScrollView? _propertiesScrollView;
    private Label? _refreshLabel;

    private float _timeSinceLastRefresh;

    public LiveEntityInspector()
    {
        // Colors set dynamically in OnRenderContainer for theme switching
        BorderThickness = 1;

        InitializeComponents();
    }

    /// <summary>
    ///     Currently selected entity
    /// </summary>
    public EntityInfo? SelectedEntity { get; set; }

    /// <summary>
    ///     List of available entities to inspect
    /// </summary>
    public List<EntityInfo> AvailableEntities { get; set; } = new();

    /// <summary>
    ///     Auto-refresh interval in seconds (0 = manual only)
    /// </summary>
    public float AutoRefreshInterval { get; set; } = 1.0f;

    /// <summary>
    ///     Callback to get fresh entity data
    /// </summary>
    public Func<EntityInfo, EntityInfo>? OnRefreshEntity { get; set; }

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

        // Entity selector dropdown
        _entityDropdown = new Dropdown
        {
            Id = Id + "_selector",
            Options = new List<string> { "No entities" },
            SelectedIndex = -1,
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.TopLeft,
                OffsetY = 28,
                WidthPercent = 0.9f,
                Height = 30,
                MarginLeft = theme.PaddingMedium,
            },
            OnSelectionChanged = index =>
            {
                if (index >= 0 && index < AvailableEntities.Count)
                {
                    SelectEntity(AvailableEntities[index]);
                }
            },
        };
        AddChild(_entityDropdown);

        // Refresh button and info
        var refreshButton = new Button
        {
            Id = Id + "_refresh",
            Text = "Refresh",
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.TopLeft,
                OffsetY = 68,
                Width = 100,
                Height = 25,
                MarginLeft = theme.PaddingMedium,
            },
            OnClick = () => RefreshEntityData(),
        };
        AddChild(refreshButton);

        _refreshLabel = new Label
        {
            Id = Id + "_refresh_label",
            Text = "Auto: 1.0s",
            Color = theme.TextDim,
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.TopLeft,
                OffsetY = 73,
                OffsetX = 110,
                MarginLeft = theme.PaddingMedium,
            },
        };
        AddChild(_refreshLabel);

        // Properties scroll view
        _propertiesScrollView = new ScrollView
        {
            Id = Id + "_properties",
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.TopLeft,
                OffsetY = 105,
                WidthPercent = 0.9f,
                HeightPercent = 0.65f,
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

        // Update dropdown options
        if (_entityDropdown != null)
        {
            _entityDropdown.Options =
                AvailableEntities.Count > 0
                    ? AvailableEntities.Select(e => $"[{e.Id}] {e.Name}").ToList()
                    : new List<string> { "No entities" };
        }

        // Update refresh label
        if (_refreshLabel != null)
        {
            if (AutoRefreshInterval > 0)
            {
                _timeSinceLastRefresh += 0.016f; // Approximate frame time
                _refreshLabel.Text =
                    $"Auto: {AutoRefreshInterval:F1}s (next in {Math.Max(0, AutoRefreshInterval - _timeSinceLastRefresh):F1}s)";

                // Auto-refresh if interval elapsed
                if (_timeSinceLastRefresh >= AutoRefreshInterval)
                {
                    RefreshEntityData();
                    _timeSinceLastRefresh = 0;
                }
            }
            else
            {
                _refreshLabel.Text = "Auto: Off";
            }
        }

        // Update properties display
        UpdatePropertiesDisplay(context);
    }

    private void UpdatePropertiesDisplay(UIContext context)
    {
        if (_propertiesScrollView == null)
        {
            return;
        }

        _propertiesScrollView.ClearChildren();

        if (SelectedEntity == null)
        {
            var noSelectionLabel = new Label
            {
                Id = Id + "_no_selection",
                Text = "No entity selected",
                Color = context.Theme.TextDim,
                Constraint = new LayoutConstraint { Anchor = Anchor.TopLeft },
            };
            _propertiesScrollView.AddChild(noSelectionLabel);
            return;
        }

        float yOffset = 0;
        float lineHeight = ThemeManager.Current.PanelRowHeight;

        // Entity header
        var headerLabel = new Label
        {
            Id = Id + "_header",
            Text = $"Entity: {SelectedEntity.Name} (ID: {SelectedEntity.Id})",
            Color = context.Theme.Info,
            Constraint = new LayoutConstraint
            {
                Anchor = Anchor.TopLeft,
                OffsetY = yOffset,
                Height = lineHeight,
            },
        };
        _propertiesScrollView.AddChild(headerLabel);
        yOffset += lineHeight + 5;

        // Properties section
        if (SelectedEntity.Properties.Count > 0)
        {
            var propsHeaderLabel = new Label
            {
                Id = Id + "_props_header",
                Text = "Properties:",
                Color = context.Theme.Success,
                Constraint = new LayoutConstraint
                {
                    Anchor = Anchor.TopLeft,
                    OffsetY = yOffset,
                    Height = lineHeight,
                },
            };
            _propertiesScrollView.AddChild(propsHeaderLabel);
            yOffset += lineHeight;

            foreach (KeyValuePair<string, string> property in SelectedEntity.Properties)
            {
                var propLabel = new Label
                {
                    Id = $"{Id}_prop_{property.Key}",
                    Text = $"  {property.Key}: {property.Value}",
                    Color = context.Theme.TextPrimary,
                    Constraint = new LayoutConstraint
                    {
                        Anchor = Anchor.TopLeft,
                        OffsetY = yOffset,
                        Height = lineHeight,
                    },
                };
                _propertiesScrollView.AddChild(propLabel);
                yOffset += lineHeight;
            }
        }

        yOffset += 5;

        // Components section
        if (SelectedEntity.Components.Count > 0)
        {
            var compsHeaderLabel = new Label
            {
                Id = Id + "_comps_header",
                Text = "Components:",
                Color = context.Theme.Warning,
                Constraint = new LayoutConstraint
                {
                    Anchor = Anchor.TopLeft,
                    OffsetY = yOffset,
                    Height = lineHeight,
                },
            };
            _propertiesScrollView.AddChild(compsHeaderLabel);
            yOffset += lineHeight;

            foreach (string component in SelectedEntity.Components)
            {
                var compLabel = new Label
                {
                    Id = $"{Id}_comp_{component}",
                    Text = $"  - {component}",
                    Color = context.Theme.Success,
                    Constraint = new LayoutConstraint
                    {
                        Anchor = Anchor.TopLeft,
                        OffsetY = yOffset,
                        OffsetX = 10,
                        Height = lineHeight,
                    },
                };
                _propertiesScrollView.AddChild(compLabel);
                yOffset += lineHeight;
            }
        }
    }

    private void SelectEntity(EntityInfo entity)
    {
        SelectedEntity = entity;
        _timeSinceLastRefresh = 0;
        RefreshEntityData();
    }

    private void RefreshEntityData()
    {
        if (SelectedEntity != null && OnRefreshEntity != null)
        {
            SelectedEntity = OnRefreshEntity(SelectedEntity);
        }
    }
}
