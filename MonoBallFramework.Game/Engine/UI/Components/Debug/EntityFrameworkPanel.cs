using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoBallFramework.Game.Engine.Core.Modding.CustomTypes;
using MonoBallFramework.Game.Engine.UI.Components.Base;
using MonoBallFramework.Game.Engine.UI.Components.Controls;
using MonoBallFramework.Game.Engine.UI.Components.Layout;
using MonoBallFramework.Game.Engine.UI.Core;
using MonoBallFramework.Game.Engine.UI.Input;
using MonoBallFramework.Game.Engine.UI.Layout;
using MonoBallFramework.Game.GameData;
using MonoBallFramework.Game.Scripting.Api;

namespace MonoBallFramework.Game.Engine.UI.Components.Debug;

/// <summary>
///     Dual-pane panel for exploring Entity Framework data from GameDataContext.
///     Left pane shows DbSets and entities in a tree view.
///     Right pane shows details of the selected item.
///     Implements a design similar to EntitiesPanelDualPane.
/// </summary>
public class EntityFrameworkPanel : DebugPanelBase
{
    private const float DefaultSplitRatio = 0.4f; // 40% for list pane
    private const int MaxItemsPerSet = 1000; // Show more items since we're listing everything
    private const int MaxListLines = 50000;
    private const int MaxDetailLines = 10000;

    private readonly SplitPanel _splitPanel;
    private readonly TextBuffer _listBuffer;
    private readonly TextBuffer _detailBuffer;
    private readonly EntityFilterBar _filterBar;

    private readonly Dictionary<int, string> _lineToEntity = new(); // Line number -> Entity path
    private IDbContextFactory<GameDataContext>? _contextFactory;
    private ICustomTypesApi? _customTypesApi;
    private string _dbSetFilter = "";
    private string _searchFilter = "";
    private string? _selectedEntityPath;
    private object? _selectedEntity;

    /// <summary>
    ///     Creates an EntityFrameworkPanel with the specified components.
    ///     Use <see cref="EntityFrameworkPanelBuilder" /> to construct instances.
    /// </summary>
    internal EntityFrameworkPanel(StatusBar statusBar)
        : base(statusBar)
    {
        Id = "entityframework_panel";

        // Create text buffers for each pane
        _listBuffer = new TextBuffer("ef_list_buffer")
        {
            AutoScroll = false,
            MaxLines = MaxListLines,
        };

        _detailBuffer = new TextBuffer("ef_detail_buffer")
        {
            AutoScroll = false,
            MaxLines = MaxDetailLines,
        };

        // Create split panel with horizontal layout
        _splitPanel = new SplitPanel
        {
            Id = "ef_split_panel",
            Orientation = SplitOrientation.Horizontal,
            SplitRatio = DefaultSplitRatio,
            MinFirstPaneSize = 150,
            MinSecondPaneSize = 200,
            SplitterSize = 4,
            ShowSplitter = true,
        };

        // Add buffers to split panel
        _splitPanel.SetFirstPane(_listBuffer);
        _splitPanel.SetSecondPane(_detailBuffer);

        // Split panel fills space above StatusBar
        _splitPanel.Constraint.Anchor = Anchor.StretchTop;

        // Create filter bar
        _filterBar = new EntityFilterBar
        {
            Id = "ef_filter_bar",
            ShowComponentDropdown = false, // Hide component dropdown for EF panel
        };
        _filterBar.Constraint.Anchor = Anchor.StretchTop;
        _filterBar.Constraint.Height = _filterBar.PreferredHeight;
        _filterBar.OnFilterChanged += HandleFilterBarChanged;

        // Position split panel below the filter bar
        _splitPanel.Constraint.OffsetY = _filterBar.PreferredHeight;

        AddChild(_filterBar);
        AddChild(_splitPanel);
    }

    private void HandleFilterBarChanged(string tag, string component, string search)
    {
        _dbSetFilter = tag; // Use tag dropdown for DbSet filter
        _searchFilter = search;
        UpdateDisplay();
    }

    /// <summary>
    ///     Gets or sets the split ratio (0-1, ratio for left pane).
    /// </summary>
    public float SplitRatio
    {
        get => _splitPanel.SplitRatio;
        set => _splitPanel.SplitRatio = Math.Clamp(value, 0.2f, 0.8f);
    }

    /// <summary>
    ///     Sets the DbContext factory for accessing Entity Framework data.
    /// </summary>
    public void SetContextFactory(IDbContextFactory<GameDataContext>? factory)
    {
        _contextFactory = factory;
        UpdateDisplay();
    }

    /// <summary>
    ///     Sets the custom types API for accessing mod-defined content types.
    /// </summary>
    public void SetCustomTypesApi(ICustomTypesApi? customTypesApi)
    {
        _customTypesApi = customTypesApi;
        UpdateDisplay();
    }

    /// <summary>
    ///     Sets the DbSet filter.
    /// </summary>
    public void SetDbSetFilter(string dbSetName)
    {
        _dbSetFilter = dbSetName ?? "";
        _filterBar.SelectedTag = _dbSetFilter;
        UpdateDisplay();
    }

    /// <summary>
    ///     Sets the search filter.
    /// </summary>
    public void SetSearchFilter(string filter)
    {
        _searchFilter = filter ?? "";
        _filterBar.SearchText = _searchFilter;
        UpdateDisplay();
    }

    /// <summary>
    ///     Clears all filters.
    /// </summary>
    public void ClearFilters()
    {
        _dbSetFilter = "";
        _searchFilter = "";
        _filterBar.ClearFilters();
        UpdateDisplay();
    }


    /// <summary>
    ///     Forces an immediate update of the display.
    /// </summary>
    public void UpdateDisplay()
    {
        // Preserve scroll position during redraw
        int prevListScroll = _listBuffer.ScrollOffset;
        int prevDetailScroll = _detailBuffer.ScrollOffset;
        string? prevSelectedEntityPath = _selectedEntityPath;

        _listBuffer.Clear();
        _detailBuffer.ClearPreservingScroll(); // Preserve detail scroll position
        _lineToEntity.Clear();

        if (_contextFactory == null && _customTypesApi == null)
        {
            _listBuffer.AppendLine(
                "  No data sources configured.",
                ThemeManager.Current.TextDim
            );
            UpdateStatusBar();
            return;
        }

        try
        {
            // Collect all filter tags (DbSets + Custom Type categories)
            var allFilterTags = new List<string>();

            // Get DbSet properties if context factory is available
            PropertyInfo[] dbSetProperties = Array.Empty<PropertyInfo>();
            GameDataContext? context = null;

            if (_contextFactory != null)
            {
                context = _contextFactory.CreateDbContext();
                dbSetProperties = GetDbSetProperties(context);
                allFilterTags.AddRange(dbSetProperties.Select(p => p.Name));
            }

            // Get custom type categories
            IReadOnlyCollection<string> customTypeCategories = _customTypesApi?.GetCategories() ?? Array.Empty<string>();
            foreach (string category in customTypeCategories)
            {
                // Prefix custom types with icon to distinguish from DbSets
                allFilterTags.Add($"⚡{category}");
            }

            if (dbSetProperties.Length == 0 && customTypeCategories.Count == 0)
            {
                _listBuffer.AppendLine(
                    "  No DbSets or custom types found.",
                    ThemeManager.Current.TextDim
                );
                context?.Dispose();
                UpdateStatusBar();
                return;
            }

            // Update filter bar with all available sources
            _filterBar.SetTags(allFilterTags.OrderBy(n => n).ToList());
            _filterBar.SetComponents(new List<string>()); // No component filter for EF

            // Collect all entities from all sources into a flat list
            var allEntities = new List<(string SourceName, int Index, object Entity, Type? EntityType, bool IsCustomType)>();

            // Collect from DbSets
            if (context != null)
            {
                foreach (PropertyInfo prop in dbSetProperties)
                {
                    string dbSetName = prop.Name;

                    // Apply filter (check without prefix for DbSets)
                    if (!string.IsNullOrEmpty(_dbSetFilter) && _dbSetFilter != dbSetName && !_dbSetFilter.StartsWith("⚡"))
                    {
                        continue;
                    }
                    // Skip DbSets if filtering by custom type
                    if (!string.IsNullOrEmpty(_dbSetFilter) && _dbSetFilter.StartsWith("⚡"))
                    {
                        continue;
                    }

                    try
                    {
                        object? dbSetValue = prop.GetValue(context);
                        if (dbSetValue == null) continue;

                        Type? entityType = GetEntityType(prop.PropertyType);
                        IEnumerable<object>? items = null;

                        if (dbSetValue is IQueryable<object> queryable)
                        {
                            items = queryable.Take(MaxItemsPerSet).ToList();
                        }
                        else if (dbSetValue is IEnumerable<object> enumerable)
                        {
                            items = enumerable.Take(MaxItemsPerSet).ToList();
                        }

                        if (items != null)
                        {
                            int index = 0;
                            foreach (object item in items)
                            {
                                allEntities.Add((dbSetName, index, item, entityType, false));
                                index++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _listBuffer.AppendLine(
                            $"  [Error loading {dbSetName}: {ex.Message}]",
                            ThemeManager.Current.Error
                        );
                    }
                }

                // Dispose context using 'using' pattern for proper cleanup
                if (context != null)
                {
                    context.Dispose();
                }
            }

            // Collect from custom types
            if (_customTypesApi != null)
            {
                foreach (string category in customTypeCategories)
                {
                    string prefixedCategory = $"⚡{category}";

                    // Apply filter (check with prefix for custom types)
                    // Skip categories that don't match the current filter
                    if (!string.IsNullOrEmpty(_dbSetFilter) && _dbSetFilter != prefixedCategory)
                    {
                        // If filter is set to a DbSet name (no ⚡ prefix), skip all custom types
                        if (!_dbSetFilter.StartsWith("⚡"))
                        {
                            continue;
                        }
                        // If filtering for a specific custom type category, skip non-matching custom types
                        if (_dbSetFilter.StartsWith("⚡") && prefixedCategory.StartsWith("⚡"))
                        {
                            continue;
                        }
                    }

                    try
                    {
                        var definitions = _customTypesApi.GetAllDefinitions(category).ToList();
                        int index = 0;
                        foreach (ICustomTypeDefinition def in definitions)
                        {
                            allEntities.Add((prefixedCategory, index, def, typeof(ICustomTypeDefinition), true));
                            index++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _listBuffer.AppendLine(
                            $"  [Error loading {category}: {ex.Message}]",
                            ThemeManager.Current.Error
                        );
                    }
                }
            }

            if (allEntities.Count == 0)
            {
                _listBuffer.AppendLine(
                    "  No entities found.",
                    ThemeManager.Current.TextDim
                );
                UpdateStatusBar();
                return;
            }

            // Display all entities in a flat list
            foreach (var (sourceName, index, entity, entityType, isCustomType) in allEntities)
            {
                string entityPath = $"{sourceName}[{index}]";
                bool entitySelected = _selectedEntityPath == entityPath;

                // Apply search filter to entity
                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    string entityStr = isCustomType
                        ? FormatCustomTypeDefinitionSummary((ICustomTypeDefinition)entity)
                        : FormatEntitySummary(entity, entityType);
                    if (!entityStr.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                // Display entity (track line number before appending)
                int entityLine = _listBuffer.TotalLines;
                string entitySelectionIcon = entitySelected ? NerdFontIcons.SelectedWithSpace : NerdFontIcons.UnselectedSpace;

                string entityKey;
                string typeName;

                if (isCustomType)
                {
                    var customDef = (ICustomTypeDefinition)entity;
                    entityKey = customDef.Id;
                    typeName = customDef.Category;
                }
                else
                {
                    entityKey = GetEntityKeyDisplay(entity, entityType);
                    typeName = CleanTypeName(entityType?.Name ?? entity.GetType().Name);
                }

                // Get type-based color, with selection override
                Color entityColor = entitySelected
                    ? ThemeManager.Current.Info
                    : GetEntityTypeColor(typeName);

                // Format: "◉ TypeName: key_value" or "◉ TypeName" if no key
                string displayText = string.IsNullOrEmpty(entityKey)
                    ? $"{entitySelectionIcon}{typeName}"
                    : $"{entitySelectionIcon}{typeName}: {entityKey}";

                _listBuffer.AppendLine(displayText, entityColor);
                // Track the line of the entity (line number is the line we just added)
                _lineToEntity[entityLine] = entityPath;
            }

            if (!string.IsNullOrEmpty(_searchFilter))
            {
                _listBuffer.AppendLine("", ThemeManager.Current.TextDim);
                _listBuffer.AppendLine(
                    $"  Search filter: '{_searchFilter}'",
                    ThemeManager.Current.Info
                );
            }

            // Display details in right pane
            UpdateDetailPane();

            // Restore scroll position (clamp to valid range)
            _listBuffer.SetScrollOffset(Math.Min(prevListScroll, Math.Max(0, _listBuffer.TotalLines - 1)));

            // Reset detail scroll only when viewing a DIFFERENT entity than before
            if (_selectedEntityPath != prevSelectedEntityPath)
            {
                _detailBuffer.SetScrollOffset(0);
            }
            else
            {
                // Preserve detail scroll if same entity
                _detailBuffer.SetScrollOffset(Math.Min(prevDetailScroll, Math.Max(0, _detailBuffer.TotalLines - 1)));
            }
        }
        catch (Exception ex)
        {
            _listBuffer.AppendLine(
                $"  Error accessing database: {ex.Message}",
                ThemeManager.Current.Error
            );
            // Still restore scroll position even on error
            _listBuffer.SetScrollOffset(Math.Min(prevListScroll, Math.Max(0, _listBuffer.TotalLines - 1)));
        }

        UpdateStatusBar();
    }

    /// <summary>
    ///     Gets the entity type from a DbSet type.
    /// </summary>
    private Type? GetEntityType(Type dbSetType)
    {
        if (dbSetType.IsGenericType)
        {
            Type[] genericArgs = dbSetType.GetGenericArguments();
            if (genericArgs.Length > 0)
            {
                return genericArgs[0];
            }
        }

        return null;
    }

    /// <summary>
    ///     Updates the detail pane with information about the selected item.
    /// </summary>
    private void UpdateDetailPane()
    {
        _detailBuffer.Clear();

        if (_selectedEntity != null && _selectedEntityPath != null)
        {
            // Check if this is a custom type definition
            bool isCustomType = _selectedEntity is ICustomTypeDefinition;
            Type entityType = _selectedEntity.GetType();
            string cleanTypeName;
            string headerIcon;

            if (isCustomType)
            {
                var customDef = (ICustomTypeDefinition)_selectedEntity;
                cleanTypeName = customDef.Category;
                headerIcon = "⚡"; // Custom type icon
            }
            else
            {
                cleanTypeName = CleanTypeName(entityType.Name);
                headerIcon = NerdFontIcons.Database;
            }

            // Get type color for header using hash-based color
            Color headerColor = GetEntityTypeColor(cleanTypeName);

            // Display entity header with type-specific color
            _detailBuffer.AppendLine(
                $"  {headerIcon} {cleanTypeName} Details",
                headerColor
            );
            _detailBuffer.AppendLine("", ThemeManager.Current.TextDim);

            PropertyInfo[] properties = entityType.GetProperties(
                BindingFlags.Public | BindingFlags.Instance
            );

            // Separate properties into categories for better organization
            var keyProps = new List<PropertyInfo>();
            var simpleProps = new List<PropertyInfo>();
            var collectionProps = new List<PropertyInfo>();
            var complexProps = new List<PropertyInfo>();
            PropertyInfo? extensionDataProp = null;
            PropertyInfo? rawDataProp = null;

            foreach (PropertyInfo prop in properties.OrderBy(p => p.Name))
            {
                // Special handling for ExtensionData - display as formatted JSON
                if (prop.Name == "ExtensionData" && prop.PropertyType == typeof(string))
                {
                    extensionDataProp = prop;
                }
                // Special handling for RawData in custom type definitions
                else if (prop.Name == "RawData" && isCustomType)
                {
                    rawDataProp = prop;
                }
                else if (prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.KeyAttribute>() != null ||
                    prop.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
                {
                    keyProps.Add(prop);
                }
                else if (IsCollectionType(prop.PropertyType))
                {
                    collectionProps.Add(prop);
                }
                else if (IsSimpleType(prop.PropertyType))
                {
                    simpleProps.Add(prop);
                }
                else
                {
                    complexProps.Add(prop);
                }
            }

            // Display key properties first (cyan)
            if (keyProps.Count > 0)
            {
                foreach (PropertyInfo prop in keyProps)
                {
                    FormatPropertyToBuffer(prop, "  ", new Color(80, 200, 220)); // Cyan for keys
                }
                _detailBuffer.AppendLine("", ThemeManager.Current.TextDim);
            }

            // Display simple properties (white/primary)
            foreach (PropertyInfo prop in simpleProps)
            {
                FormatPropertyToBuffer(prop, "  ", ThemeManager.Current.TextPrimary);
            }

            // Display complex properties (yellow)
            foreach (PropertyInfo prop in complexProps)
            {
                FormatPropertyToBuffer(prop, "  ", new Color(220, 200, 100)); // Yellow for complex
            }

            // Display collections with expanded view (green)
            if (collectionProps.Count > 0)
            {
                _detailBuffer.AppendLine("", ThemeManager.Current.TextDim);
                foreach (PropertyInfo prop in collectionProps)
                {
                    FormatCollectionPropertyToBuffer(prop, "  ");
                }
            }

            // Display extension data (mod custom properties) with special formatting (magenta)
            if (extensionDataProp != null)
            {
                FormatExtensionDataToBuffer(extensionDataProp, "  ");
            }

            // Display raw data from custom type definitions (magenta)
            if (rawDataProp != null)
            {
                FormatRawDataToBuffer(rawDataProp, "  ");
            }
        }
        else
        {
            _detailBuffer.AppendLine(
                "  Select an entity to view details.",
                ThemeManager.Current.TextDim
            );
            _detailBuffer.AppendLine("", ThemeManager.Current.TextDim);
            _detailBuffer.AppendLine(
                "  Use ↑↓ keys or click to select.",
                ThemeManager.Current.TextDim
            );
        }
    }

    /// <summary>
    ///     Formats a single property and appends it to the detail buffer.
    /// </summary>
    private void FormatPropertyToBuffer(PropertyInfo prop, string indent, Color color)
    {
        try
        {
            object? value = prop.GetValue(_selectedEntity);
            Type valueType = value?.GetType() ?? prop.PropertyType;
            string valueStr = FormatValue(value, valueType, 0);
            _detailBuffer.AppendLine($"{indent}{prop.Name}: {valueStr}", color);
        }
        catch (Exception ex)
        {
            _detailBuffer.AppendLine(
                $"{indent}{prop.Name}: [Error: {ex.Message}]",
                ThemeManager.Current.Error
            );
        }
    }

    /// <summary>
    ///     Formats a collection property with expanded items and appends to the detail buffer.
    /// </summary>
    private void FormatCollectionPropertyToBuffer(PropertyInfo prop, string indent)
    {
        try
        {
            object? value = prop.GetValue(_selectedEntity);
            if (value == null)
            {
                _detailBuffer.AppendLine(
                    $"{indent}{prop.Name}: [null]",
                    new Color(120, 200, 120) // Green for collections
                );
                return;
            }

            // Get collection as enumerable
            if (value is not System.Collections.IEnumerable enumerable)
            {
                _detailBuffer.AppendLine(
                    $"{indent}{prop.Name}: [not enumerable]",
                    ThemeManager.Current.Warning
                );
                return;
            }

            // Count items and collect first few
            var items = new List<object>();
            int totalCount = 0;
            const int maxItemsToShow = 10;

            foreach (object item in enumerable)
            {
                totalCount++;
                if (items.Count < maxItemsToShow)
                {
                    items.Add(item);
                }
            }

            // Display collection header with count
            string countStr = totalCount > maxItemsToShow
                ? $"{totalCount} items (showing first {maxItemsToShow})"
                : $"{totalCount} item{(totalCount != 1 ? "s" : "")}";

            _detailBuffer.AppendLine(
                $"{indent}{NerdFontIcons.List} {prop.Name} [{countStr}]",
                new Color(120, 200, 120) // Green for collections
            );

            // Display items with indentation
            string itemIndent = indent + "    ";
            for (int i = 0; i < items.Count; i++)
            {
                object item = items[i];
                Type itemType = item.GetType();

                // Check if item has meaningful properties to display
                if (IsSimpleType(itemType))
                {
                    _detailBuffer.AppendLine(
                        $"{itemIndent}[{i}] {FormatValue(item, itemType, 1)}",
                        ThemeManager.Current.TextSecondary
                    );
                }
                else
                {
                    // For complex items, show key properties inline
                    string itemSummary = FormatCollectionItemSummary(item, itemType);
                    _detailBuffer.AppendLine(
                        $"{itemIndent}[{i}] {itemSummary}",
                        ThemeManager.Current.TextSecondary
                    );
                }
            }

            if (totalCount > maxItemsToShow)
            {
                _detailBuffer.AppendLine(
                    $"{itemIndent}... and {totalCount - maxItemsToShow} more",
                    ThemeManager.Current.TextDim
                );
            }
        }
        catch (Exception ex)
        {
            _detailBuffer.AppendLine(
                $"{indent}{prop.Name}: [Error: {ex.Message}]",
                ThemeManager.Current.Error
            );
        }
    }

    /// <summary>
    ///     Formats ExtensionData (mod custom properties) with parsed JSON display.
    /// </summary>
    private void FormatExtensionDataToBuffer(PropertyInfo prop, string indent)
    {
        try
        {
            object? value = prop.GetValue(_selectedEntity);
            if (value == null || value is not string jsonString || string.IsNullOrWhiteSpace(jsonString))
            {
                return; // No extension data to display
            }

            // Parse JSON to display individual properties
            try
            {
                var extensionData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(jsonString);
                if (extensionData == null || extensionData.Count == 0)
                {
                    return;
                }

                _detailBuffer.AppendLine("", ThemeManager.Current.TextDim);
                _detailBuffer.AppendLine(
                    $"{indent}{NerdFontIcons.Component} Mod Extension Data [{extensionData.Count} properties]",
                    new Color(200, 120, 200) // Magenta for mod data
                );

                string itemIndent = indent + "    ";
                foreach (var kvp in extensionData.OrderBy(k => k.Key))
                {
                    string valueStr = FormatJsonElement(kvp.Value);
                    _detailBuffer.AppendLine(
                        $"{itemIndent}{kvp.Key}: {valueStr}",
                        new Color(180, 140, 200) // Lighter magenta for values
                    );
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // If JSON parsing fails, show raw string
                _detailBuffer.AppendLine("", ThemeManager.Current.TextDim);
                _detailBuffer.AppendLine(
                    $"{indent}{NerdFontIcons.Component} ExtensionData (raw):",
                    new Color(200, 120, 200)
                );
                _detailBuffer.AppendLine(
                    $"{indent}    {jsonString}",
                    ThemeManager.Current.TextSecondary
                );
            }
        }
        catch (Exception ex)
        {
            _detailBuffer.AppendLine(
                $"{indent}ExtensionData: [Error: {ex.Message}]",
                ThemeManager.Current.Error
            );
        }
    }

    /// <summary>
    ///     Formats RawData (custom type definition properties) with parsed display.
    /// </summary>
    private void FormatRawDataToBuffer(PropertyInfo prop, string indent)
    {
        try
        {
            object? value = prop.GetValue(_selectedEntity);
            if (value == null)
            {
                return; // No raw data to display
            }

            // Handle JsonElement type (from ICustomTypeDefinition.RawData)
            if (value is System.Text.Json.JsonElement jsonElement)
            {
                if (jsonElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                {
                    return; // Only display object types
                }

                // Count properties
                int propCount = 0;
                foreach (var _ in jsonElement.EnumerateObject())
                {
                    propCount++;
                }

                if (propCount == 0)
                {
                    return;
                }

                _detailBuffer.AppendLine("", ThemeManager.Current.TextDim);
                _detailBuffer.AppendLine(
                    $"{indent}⚡ Definition Data [{propCount} properties]",
                    new Color(200, 120, 200) // Magenta for mod data
                );

                string itemIndent = indent + "    ";
                foreach (System.Text.Json.JsonProperty jsonProp in jsonElement.EnumerateObject().OrderBy(p => p.Name))
                {
                    string valueStr = FormatJsonElement(jsonProp.Value);
                    _detailBuffer.AppendLine(
                        $"{itemIndent}{jsonProp.Name}: {valueStr}",
                        new Color(180, 140, 200) // Lighter magenta for values
                    );

                    // For nested objects/arrays, show expanded content
                    if (jsonProp.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        FormatNestedJsonObject(jsonProp.Value, itemIndent + "    ");
                    }
                    else if (jsonProp.Value.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        FormatNestedJsonArray(jsonProp.Value, itemIndent + "    ");
                    }
                }
            }
            // Legacy support for Dictionary<string, object?>
            else if (value is Dictionary<string, object?> rawData && rawData.Count > 0)
            {
                _detailBuffer.AppendLine("", ThemeManager.Current.TextDim);
                _detailBuffer.AppendLine(
                    $"{indent}⚡ Definition Data [{rawData.Count} properties]",
                    new Color(200, 120, 200) // Magenta for mod data
                );

                string itemIndent = indent + "    ";
                foreach (var kvp in rawData.OrderBy(k => k.Key))
                {
                    string valueStr = FormatRawDataValue(kvp.Value);
                    _detailBuffer.AppendLine(
                        $"{itemIndent}{kvp.Key}: {valueStr}",
                        new Color(180, 140, 200) // Lighter magenta for values
                    );
                }
            }
        }
        catch (Exception ex)
        {
            _detailBuffer.AppendLine(
                $"{indent}RawData: [Error: {ex.Message}]",
                ThemeManager.Current.Error
            );
        }
    }

    /// <summary>
    ///     Formats nested JSON object properties with indentation.
    /// </summary>
    private void FormatNestedJsonObject(System.Text.Json.JsonElement element, string indent, int depth = 0)
    {
        if (depth > 3) // Limit recursion depth
        {
            return;
        }

        foreach (System.Text.Json.JsonProperty prop in element.EnumerateObject().OrderBy(p => p.Name).Take(10))
        {
            string valueStr = FormatJsonElement(prop.Value);
            _detailBuffer.AppendLine(
                $"{indent}{prop.Name}: {valueStr}",
                new Color(160, 120, 180) // Even lighter magenta for nested
            );

            if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                FormatNestedJsonObject(prop.Value, indent + "    ", depth + 1);
            }
            else if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                FormatNestedJsonArray(prop.Value, indent + "    ", depth + 1);
            }
        }
    }

    /// <summary>
    ///     Formats nested JSON array elements with indentation.
    /// </summary>
    private void FormatNestedJsonArray(System.Text.Json.JsonElement element, string indent, int depth = 0)
    {
        if (depth > 3) // Limit recursion depth
        {
            return;
        }

        int index = 0;
        foreach (System.Text.Json.JsonElement item in element.EnumerateArray().Take(5))
        {
            string valueStr = FormatJsonElement(item);
            _detailBuffer.AppendLine(
                $"{indent}[{index}]: {valueStr}",
                new Color(160, 120, 180) // Even lighter magenta for nested
            );

            if (item.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                FormatNestedJsonObject(item, indent + "    ", depth + 1);
            }
            index++;
        }

        int totalCount = element.GetArrayLength();
        if (totalCount > 5)
        {
            _detailBuffer.AppendLine(
                $"{indent}... and {totalCount - 5} more items",
                ThemeManager.Current.TextDim
            );
        }
    }

    /// <summary>
    ///     Formats a raw data value for display.
    /// </summary>
    private string FormatRawDataValue(object? value)
    {
        if (value == null)
        {
            return "[null]";
        }

        return value switch
        {
            string s => $"\"{s}\"",
            bool b => b ? "true" : "false",
            System.Text.Json.JsonElement element => FormatJsonElement(element),
            System.Collections.IList list => $"[array: {list.Count} items]",
            System.Collections.IDictionary dict => $"[object: {dict.Count} properties]",
            _ => value.ToString() ?? "[null]"
        };
    }

    /// <summary>
    ///     Formats a JsonElement for display.
    /// </summary>
    private string FormatJsonElement(System.Text.Json.JsonElement element)
    {
        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => $"\"{element.GetString()}\"",
            System.Text.Json.JsonValueKind.Number => element.ToString(),
            System.Text.Json.JsonValueKind.True => "true",
            System.Text.Json.JsonValueKind.False => "false",
            System.Text.Json.JsonValueKind.Null => "[null]",
            System.Text.Json.JsonValueKind.Array => $"[array: {element.GetArrayLength()} items]",
            System.Text.Json.JsonValueKind.Object => "[object]",
            _ => element.ToString()
        };
    }

    /// <summary>
    ///     Formats a collection item for inline display.
    /// </summary>
    private string FormatCollectionItemSummary(object item, Type itemType)
    {
        PropertyInfo[] props = itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // For items with few properties, show them all inline
        var displayProps = props
            .Where(p => !IsCollectionType(p.PropertyType))
            .Take(5)
            .ToList();

        if (displayProps.Count == 0)
        {
            return item.ToString() ?? $"[{itemType.Name}]";
        }

        var parts = new List<string>();
        foreach (PropertyInfo prop in displayProps)
        {
            try
            {
                object? propValue = prop.GetValue(item);
                if (propValue != null)
                {
                    string valueStr = FormatValue(propValue, prop.PropertyType, 2);
                    // Truncate long values with bounds check
                    if (valueStr.Length > 30)
                    {
                        int substringLength = Math.Min(27, valueStr.Length);
                        valueStr = valueStr.Substring(0, substringLength) + "...";
                    }
                    parts.Add($"{prop.Name}={valueStr}");
                }
            }
            catch
            {
                // Skip properties that can't be read
            }
        }

        return parts.Count > 0 ? string.Join(", ", parts) : item.ToString() ?? $"[{itemType.Name}]";
    }


    /// <summary>
    ///     Gets DbSet properties from the context.
    /// </summary>
    private PropertyInfo[] GetDbSetProperties(GameDataContext context)
    {
        return context
            .GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .OrderBy(p => p.Name)
            .ToArray();
    }

    /// <summary>
    ///     Formats an entity summary for the list view - just the type name.
    /// </summary>
    private string FormatEntitySummary(object entity, Type? entityType)
    {
        Type type = entityType ?? entity.GetType();
        // Clean up type name (remove common suffixes/prefixes)
        return CleanTypeName(type.Name);
    }

    /// <summary>
    ///     Formats a custom type definition summary for the list view.
    /// </summary>
    private string FormatCustomTypeDefinitionSummary(ICustomTypeDefinition definition)
    {
        return $"{definition.Category}: {definition.Id}";
    }

    /// <summary>
    ///     Gets a display string for the entity's primary key.
    ///     Looks for properties ending in "Id" or marked with [Key] attribute.
    /// </summary>
    private string GetEntityKeyDisplay(object entity, Type? entityType)
    {
        Type type = entityType ?? entity.GetType();
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // First, look for a property with [Key] attribute
        PropertyInfo? keyProp = properties.FirstOrDefault(p =>
            p.GetCustomAttribute<System.ComponentModel.DataAnnotations.KeyAttribute>() != null);

        // If no [Key] attribute, look for common ID patterns
        if (keyProp == null)
        {
            // Try common patterns: AudioId, SpriteId, BehaviorId, etc.
            string cleanName = CleanTypeName(type.Name);
            keyProp = properties.FirstOrDefault(p =>
                p.Name.Equals($"{cleanName}Id", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Equals($"{type.Name}Id", StringComparison.OrdinalIgnoreCase));
        }

        // Fall back to any property ending in "Id" that's not a foreign key
        if (keyProp == null)
        {
            keyProp = properties.FirstOrDefault(p =>
                p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) &&
                !p.Name.Contains("Foreign") &&
                !p.Name.Contains("Parent"));
        }

        if (keyProp == null)
        {
            return string.Empty;
        }

        try
        {
            object? value = keyProp.GetValue(entity);
            if (value == null)
            {
                return string.Empty;
            }

            // For ID types with a Value property, extract the value
            PropertyInfo? valueProp = value.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
            if (valueProp != null)
            {
                object? innerValue = valueProp.GetValue(value);
                if (innerValue != null)
                {
                    string result = innerValue.ToString() ?? string.Empty;
                    // Truncate if too long with bounds check
                    if (result.Length > 60)
                    {
                        int substringLength = Math.Min(57, result.Length);
                        return result.Substring(0, substringLength) + "...";
                    }
                    return result;
                }
            }

            // Use ToString directly
            string str = value.ToString() ?? string.Empty;
            if (str.Length > 60)
            {
                int substringLength = Math.Min(57, str.Length);
                return str.Substring(0, substringLength) + "...";
            }
            return str;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    ///     Cleans up type name for display.
    /// </summary>
    private string CleanTypeName(string typeName)
    {
        // Remove common suffixes with bounds checking
        if (typeName.EndsWith("Entity") && typeName.Length > 6)
        {
            return typeName.Substring(0, typeName.Length - 6);
        }
        if (typeName.EndsWith("Model") && typeName.Length > 5)
        {
            return typeName.Substring(0, typeName.Length - 5);
        }
        if (typeName.EndsWith("Data") && typeName.Length > 4)
        {
            return typeName.Substring(0, typeName.Length - 4);
        }
        return typeName;
    }

    /// <summary>
    ///     Checks if a type is a collection.
    /// </summary>
    private bool IsCollectionType(Type type)
    {
        return type != typeof(string) && (
            type.IsArray
            || (type.IsGenericType && (
                type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                || type.GetGenericTypeDefinition() == typeof(ICollection<>)
                || type.GetGenericTypeDefinition() == typeof(IList<>)
                || type.GetGenericTypeDefinition() == typeof(List<>)
            ))
            || typeof(System.Collections.IEnumerable).IsAssignableFrom(type)
        );
    }

    /// <summary>
    ///     Checks if a type is a simple/primitive type suitable for display.
    /// </summary>
    private bool IsSimpleType(Type type)
    {
        return type.IsPrimitive
            || type == typeof(string)
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(TimeSpan)
            || type == typeof(Guid)
            || type.IsEnum
            || Nullable.GetUnderlyingType(type) != null && IsSimpleType(Nullable.GetUnderlyingType(type)!);
    }

    /// <summary>
    ///     Checks if a value is the default value for its type.
    /// </summary>
    private bool IsDefaultValue(object value, Type type)
    {
        if (value == null) return true;
        if (type.IsValueType)
        {
            return value.Equals(Activator.CreateInstance(type));
        }
        if (type == typeof(string))
        {
            return string.IsNullOrWhiteSpace((string)value);
        }
        return false;
    }

    /// <summary>
    ///     Formats a value for display, handling records, enums, and complex types properly.
    /// </summary>
    private string FormatValue(object? value, Type type, int depth)
    {
        if (value == null)
        {
            return "[null]";
        }

        // Use actual runtime type if available (handles polymorphism and nullable)
        Type actualType = value.GetType();
        if (actualType != type)
        {
            type = actualType;
        }

        // Handle nullable types
        Type underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        if (underlyingType != type)
        {
            return FormatValue(value, underlyingType, depth);
        }

        // Strings
        if (type == typeof(string))
        {
            string str = (string)value;
            if (str.Length > 50)
            {
                // Bounds check before substring
                int substringLength = Math.Min(47, str.Length);
                return $"\"{str.Substring(0, substringLength)}...\"";
            }
            return $"\"{str}\"";
        }

        // Primitives and common value types
        if (type.IsPrimitive || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || type == typeof(Guid))
        {
            return value.ToString() ?? "[null]";
        }

        // Enums
        if (type.IsEnum)
        {
            return value.ToString() ?? "[null]";
        }

        // First, check if it has a Value property (common for ID types like GameAudioId)
        // This should be checked BEFORE ToString() since many record types have meaningful ToString()
        // but we want to show the actual value
        PropertyInfo? valueProp = type.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
        if (valueProp != null)
        {
            try
            {
                object? propValue = valueProp.GetValue(value);
                if (propValue != null)
                {
                    return FormatValue(propValue, valueProp.PropertyType, depth);
                }
            }
            catch
            {
                // Fall through
            }
        }

        // Check if this is a record type (especially *Id types) that should be formatted nicely
        if (ShouldFormatAsRecord(type, value))
        {
            return FormatRecordType(value, type, depth);
        }

        // Try ToString() - many types override it meaningfully
        string? toStringResult = value.ToString();
        if (!string.IsNullOrEmpty(toStringResult))
        {
            // If ToString() just returns the type name, it's not helpful
            if (toStringResult != type.FullName && toStringResult != type.Name && !toStringResult.StartsWith(type.Namespace ?? ""))
            {
                // ToString() is meaningful, use it
                if (toStringResult.Length > 100)
                {
                    // Bounds check before substring
                    int substringLength = Math.Min(97, toStringResult.Length);
                    return toStringResult.Substring(0, substringLength) + "...";
                }
                return toStringResult;
            }
        }

        // Last resort: show type name
        return $"[{type.Name}]";
    }

    /// <summary>
    ///     Checks if a type should be formatted as a record.
    /// </summary>
    private bool ShouldFormatAsRecord(Type type, object value)
    {
        // Check if it's a record type (C# 9+ records have IsRecord property, but we can check for common patterns)
        // Records typically have properties and override ToString/Equals
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // If it has properties and ToString() wasn't helpful, format as record
        if (properties.Length > 0)
        {
            string? toString = value.ToString();
            if (string.IsNullOrEmpty(toString) || toString == type.FullName || toString == type.Name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Formats a record type for display.
    /// </summary>
    private string FormatRecordType(object value, Type type, int depth)
    {
        if (depth > 2) // Prevent infinite recursion
        {
            return $"[{type.Name}]";
        }

        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        if (properties.Length == 0)
        {
            return value.ToString() ?? $"[{type.Name}]";
        }

        // For simple records with just a Value property, show it inline
        if (properties.Length == 1 && properties[0].Name == "Value")
        {
            try
            {
                object? propValue = properties[0].GetValue(value);
                return FormatValue(propValue, properties[0].PropertyType, depth + 1);
            }
            catch
            {
                return value.ToString() ?? $"[{type.Name}]";
            }
        }

        // For records with multiple properties, show key properties inline (limited)
        var keyProps = properties
            .Where(p => !IsCollectionType(p.PropertyType))
            .Take(3) // Limit to 3 properties for inline display
            .ToList();

        if (keyProps.Count == 0)
        {
            return value.ToString() ?? $"[{type.Name}]";
        }

        var parts = new List<string>();
        foreach (var prop in keyProps)
        {
            try
            {
                object? propValue = prop.GetValue(value);
                if (propValue != null)
                {
                    string valueStr = FormatValue(propValue, prop.PropertyType, depth + 1);
                    // Truncate long values with bounds check
                    if (valueStr.Length > 20)
                    {
                        int substringLength = Math.Min(17, valueStr.Length);
                        valueStr = valueStr.Substring(0, substringLength) + "...";
                    }
                    parts.Add($"{prop.Name}={valueStr}");
                }
            }
            catch
            {
                // Skip properties that can't be read
            }
        }

        if (parts.Count > 0)
        {
            return string.Join(", ", parts);
        }

        return value.ToString() ?? $"[{type.Name}]";
    }

    protected override void OnRenderContainer(UIContext context)
    {
        base.OnRenderContainer(context);

        UIRenderer renderer = context.Renderer;

        // Calculate layout: StatusBar at bottom, filter bar at top, split panel fills remaining space
        float statusBarHeight = StatusBar.GetDesiredHeight(renderer);
        StatusBar.Constraint.Height = statusBarHeight;

        float paddingTop = Constraint.GetPaddingTop();
        float paddingBottom = Constraint.GetPaddingBottom();
        float availableHeight = Rect.Height - paddingTop - paddingBottom;

        // Filter bar at top - set explicit height
        float filterBarHeight = _filterBar.PreferredHeight;
        _filterBar.Constraint.Height = filterBarHeight;

        // Split panel fills remaining space, positioned below filter bar
        _splitPanel.Constraint.Height = availableHeight - statusBarHeight - filterBarHeight;
        _splitPanel.Constraint.OffsetY = filterBarHeight;

        // Handle keyboard navigation
        HandleKeyboardNavigation(context);

        // Update status bar content
        UpdateStatusBar();
    }

    /// <summary>
    ///     Handles keyboard navigation for the entity list.
    /// </summary>
    private void HandleKeyboardNavigation(UIContext context)
    {
        if (context.Input == null || _filterBar.HasExclusiveFocus)
        {
            return;
        }

        InputState input = context.Input;

        // Up arrow - move selection up
        if (input.IsKeyPressedWithRepeat(Keys.Up))
        {
            NavigateSelection(-1);
            input.ConsumeKey(Keys.Up);
        }
        // Down arrow - move selection down
        else if (input.IsKeyPressedWithRepeat(Keys.Down))
        {
            NavigateSelection(1);
            input.ConsumeKey(Keys.Down);
        }
        // Page Up - move selection up by page
        else if (input.IsKeyPressedWithRepeat(Keys.PageUp))
        {
            int pageSize = Math.Max(1, _listBuffer.VisibleLineCount - 2);
            NavigateSelection(-pageSize);
            input.ConsumeKey(Keys.PageUp);
        }
        // Page Down - move selection down by page
        else if (input.IsKeyPressedWithRepeat(Keys.PageDown))
        {
            int pageSize = Math.Max(1, _listBuffer.VisibleLineCount - 2);
            NavigateSelection(pageSize);
            input.ConsumeKey(Keys.PageDown);
        }
        // Home - go to first entity
        else if (input.IsKeyPressed(Keys.Home))
        {
            NavigateToFirst();
            input.ConsumeKey(Keys.Home);
        }
        // End - go to last entity
        else if (input.IsKeyPressed(Keys.End))
        {
            NavigateToLast();
            input.ConsumeKey(Keys.End);
        }
    }

    /// <summary>
    ///     Navigates the selection by the specified delta.
    /// </summary>
    private void NavigateSelection(int delta)
    {
        if (_lineToEntity.Count == 0)
        {
            return;
        }

        // Get sorted line numbers
        List<int> lines = _lineToEntity.Keys.OrderBy(l => l).ToList();

        // Find current selection index
        int currentIndex = -1;
        if (_selectedEntityPath != null)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (_lineToEntity[lines[i]] == _selectedEntityPath)
                {
                    currentIndex = i;
                    break;
                }
            }
        }

        // Calculate new index
        int newIndex;
        if (currentIndex < 0)
        {
            // Nothing selected, select first or last based on direction
            newIndex = delta > 0 ? 0 : lines.Count - 1;
        }
        else
        {
            newIndex = Math.Clamp(currentIndex + delta, 0, lines.Count - 1);
        }

        // Select the entity at the new index
        int newLine = lines[newIndex];
        if (_lineToEntity.TryGetValue(newLine, out string? entityPath))
        {
            SelectEntity(entityPath);

            // Scroll to keep selection visible
            int visibleLines = _listBuffer.VisibleLineCount;
            int scrollOffset = _listBuffer.ScrollOffset;

            if (newLine < scrollOffset)
            {
                _listBuffer.SetScrollOffset(newLine);
            }
            else if (newLine >= scrollOffset + visibleLines)
            {
                _listBuffer.SetScrollOffset(newLine - visibleLines + 1);
            }

            UpdateDisplay();
        }
    }

    /// <summary>
    ///     Navigates to the first entity.
    /// </summary>
    private void NavigateToFirst()
    {
        if (_lineToEntity.Count == 0)
        {
            return;
        }

        int firstLine = _lineToEntity.Keys.Min();
        if (_lineToEntity.TryGetValue(firstLine, out string? entityPath))
        {
            SelectEntity(entityPath);
            _listBuffer.SetScrollOffset(0);
            UpdateDisplay();
        }
    }

    /// <summary>
    ///     Navigates to the last entity.
    /// </summary>
    private void NavigateToLast()
    {
        if (_lineToEntity.Count == 0)
        {
            return;
        }

        int lastLine = _lineToEntity.Keys.Max();
        if (_lineToEntity.TryGetValue(lastLine, out string? entityPath))
        {
            SelectEntity(entityPath);
            // Scroll to show last entity
            int visibleLines = _listBuffer.VisibleLineCount;
            int maxScroll = Math.Max(0, _listBuffer.TotalLines - visibleLines);
            _listBuffer.SetScrollOffset(maxScroll);
            UpdateDisplay();
        }
    }

    /// <summary>
    ///     Renders children with proper input handling.
    /// </summary>
    protected override void OnRenderChildren(UIContext context)
    {
        // IMPORTANT: Process filter bar input FIRST so it can consume mouse clicks
        // before other components see them. This ensures dropdowns and search input
        // get exclusive input when focused.
        _filterBar.ProcessInput(context);

        // Suppress hover highlighting in entity list when filter bar has exclusive focus
        _listBuffer.SuppressHover = _filterBar.HasExclusiveFocus;

        // Handle entity list mouse input BEFORE rendering split panel
        // (TextBuffer consumes clicks during render, so we must handle ours first)
        HandleMouseInput(context);

        // Now render all components in visual order (back to front)
        _splitPanel.Render(context);
        _filterBar.Render(context);

        // Render status bar
        StatusBar.Render(context);

        // Render filter bar dropdown overlays absolutely last so they appear on top of everything
        _filterBar.RenderDropdownOverlays(context);
    }

    /// <summary>
    ///     Handles mouse clicks for selecting entities.
    /// </summary>
    private void HandleMouseInput(UIContext context)
    {
        if (context.Input == null)
        {
            return;
        }

        InputState input = context.Input;

        // Skip all mouse handling if filter bar has exclusive focus
        // (dropdowns or search input need exclusive mouse access)
        if (_filterBar.HasExclusiveFocus)
        {
            return;
        }

        // Skip if mouse is in the filter bar area - let filter bar handle its own input
        float filterBarBottom = Rect.Y + Constraint.GetPaddingTop() + _filterBar.PreferredHeight;
        if (input.MousePosition.Y < filterBarBottom)
        {
            return;
        }

        // Calculate the entity list pane bounds
        // (SplitPanel hasn't rendered yet, so we calculate it ourselves)
        LayoutRect listBounds = GetListBounds(context);
        if (!listBounds.Contains(input.MousePosition))
        {
            return;
        }

        // Don't consume clicks over the scrollbar - let TextBuffer handle those
        if (IsMouseOverScrollbar(input.MousePosition, listBounds))
        {
            return;
        }

        if (input.IsMouseButtonPressed(MouseButton.Left))
        {
            int clickedLine = GetLineAtMousePosition(input.MousePosition, listBounds);
            if (clickedLine >= 0 && _lineToEntity.TryGetValue(clickedLine, out string? entityPath))
            {
                SelectEntity(entityPath);
                UpdateDisplay();
                input.ConsumeMouseButton(MouseButton.Left);
            }
        }
    }

    /// <summary>
    ///     Checks if the mouse position is over the scrollbar area.
    /// </summary>
    private bool IsMouseOverScrollbar(Point mousePos, LayoutRect contentBounds)
    {
        // Check if TextBuffer has a scrollbar (more lines than visible)
        int totalLines = _listBuffer.TotalLines;
        int visibleLines = _listBuffer.VisibleLineCount;
        if (totalLines <= visibleLines)
        {
            return false; // No scrollbar needed
        }

        // Calculate scrollbar area (matches TextBuffer's scrollbar position)
        float scrollbarStartX = contentBounds.Right - _listBuffer.ScrollbarWidth;
        float scrollbarEndX = contentBounds.Right;
        float scrollbarStartY = contentBounds.Y + _listBuffer.LinePadding;
        float scrollbarEndY = contentBounds.Y + contentBounds.Height - _listBuffer.LinePadding;

        return mousePos.X >= scrollbarStartX
            && mousePos.X <= scrollbarEndX
            && mousePos.Y >= scrollbarStartY
            && mousePos.Y <= scrollbarEndY;
    }

    /// <summary>
    ///     Selects an entity and loads its data for the detail pane.
    /// </summary>
    private void SelectEntity(string entityPath)
    {
        _selectedEntityPath = entityPath;
        _selectedEntity = null;

        // Parse entity path: "SourceName[index]"
        int bracketIndex = entityPath.IndexOf('[');
        int closeBracketIndex = entityPath.IndexOf(']');

        // Validate format
        if (bracketIndex < 0 || closeBracketIndex < 0 || closeBracketIndex <= bracketIndex + 1)
        {
            return; // Invalid format
        }

        string sourceName = entityPath.Substring(0, bracketIndex);
        string indexStr = entityPath.Substring(bracketIndex + 1, closeBracketIndex - bracketIndex - 1);
        if (!int.TryParse(indexStr, out int index))
        {
            return;
        }

        // Check if this is a custom type (prefixed with ⚡)
        if (sourceName.StartsWith("⚡"))
        {
            if (_customTypesApi == null)
            {
                return;
            }

            // Remove ⚡ prefix with bounds check
            if (sourceName.Length <= 1)
            {
                return; // Invalid format
            }
            string category = sourceName.Substring(1);
            try
            {
                var definitions = _customTypesApi.GetAllDefinitions(category).ToList();
                if (index >= 0 && index < definitions.Count)
                {
                    _selectedEntity = definitions[index];
                }
            }
            catch
            {
                // Ignore errors
            }
        }
        else
        {
            // DbSet entity
            if (_contextFactory == null)
            {
                return;
            }

            try
            {
                using GameDataContext context = _contextFactory.CreateDbContext();
                PropertyInfo? prop = context.GetType().GetProperty(sourceName);
                if (prop == null)
                {
                    return;
                }

                object? dbSetValue = prop.GetValue(context);
                if (dbSetValue == null)
                {
                    return;
                }

                IEnumerable<object>? items = null;
                if (dbSetValue is IQueryable<object> queryable)
                {
                    items = queryable.Skip(index).Take(1).ToList();
                }
                else if (dbSetValue is IEnumerable<object> enumerable)
                {
                    items = enumerable.Skip(index).Take(1).ToList();
                }

                _selectedEntity = items?.FirstOrDefault();
            }
            catch
            {
                // Ignore errors
            }
        }
    }

    /// <summary>
    ///     Calculates the list pane bounds.
    /// </summary>
    private LayoutRect GetListBounds(UIContext context)
    {
        float paddingLeft = Constraint.GetPaddingLeft();
        float paddingTop = Constraint.GetPaddingTop();
        float paddingRight = Constraint.GetPaddingRight();
        float paddingBottom = Constraint.GetPaddingBottom();
        float filterBarHeight = _filterBar.PreferredHeight;
        float statusBarHeight = StatusBar.GetDesiredHeight(context.Renderer);

        float splitPanelX = Rect.X + paddingLeft;
        float splitPanelY = Rect.Y + paddingTop + filterBarHeight; // Below filter bar
        float splitPanelWidth = _splitPanel.Constraint.Width ?? (Rect.Width - paddingLeft - paddingRight);
        float splitPanelHeight = _splitPanel.Constraint.Height ?? (Rect.Height - paddingTop - paddingBottom - filterBarHeight - statusBarHeight);

        // Calculate first pane (left) width based on split ratio
        // Account for splitter size AND inner padding on both sides of splitter
        float splitterSize = _splitPanel.SplitterSize;
        float paneInnerPadding = _splitPanel.PaneInnerPadding;
        float totalGap = splitterSize + (paneInnerPadding * 2);
        float availableWidth = splitPanelWidth - totalGap;
        float firstPaneWidth = availableWidth * Math.Clamp(_splitPanel.SplitRatio, 0f, 1f);

        // Apply min constraints
        float maxFirst = availableWidth - _splitPanel.MinSecondPaneSize;
        firstPaneWidth = Math.Clamp(firstPaneWidth, _splitPanel.MinFirstPaneSize, maxFirst);

        return new LayoutRect(splitPanelX, splitPanelY, firstPaneWidth, splitPanelHeight);
    }

    /// <summary>
    ///     Gets the line number at the mouse position.
    /// </summary>
    private int GetLineAtMousePosition(Point mousePos, LayoutRect bounds)
    {
        float relativeY = mousePos.Y - bounds.Y - _listBuffer.LinePadding;
        if (relativeY < 0)
        {
            return -1;
        }

        int lineIndex = (int)(relativeY / _listBuffer.LineHeight);

        // Add scroll offset to get actual line position in buffer
        int clickedLine = lineIndex + _listBuffer.ScrollOffset;

        return clickedLine >= 0 && clickedLine < _listBuffer.TotalLines ? clickedLine : -1;
    }

    protected override UIComponent GetContentComponent()
    {
        return _splitPanel;
    }

    protected override void UpdateStatusBar()
    {
        if (_contextFactory == null)
        {
            SetStatusBar("No DbContext factory", "Set context factory to browse data");
            return;
        }

        try
        {
            using GameDataContext context = _contextFactory.CreateDbContext();
            PropertyInfo[] dbSetProperties = GetDbSetProperties(context);

            // Count total entities across all DbSets
            int totalEntities = 0;
            foreach (PropertyInfo prop in dbSetProperties)
            {
                try
                {
                    object? dbSetValue = prop.GetValue(context);
                    if (dbSetValue != null)
                    {
                        if (dbSetValue is IQueryable<object> queryable)
                        {
                            totalEntities += queryable.Count();
                        }
                        else if (dbSetValue is IEnumerable<object> enumerable)
                        {
                            totalEntities += enumerable.Count();
                        }
                    }
                }
                catch
                {
                    // Ignore errors
                }
            }

            string stats = $"Entities: {totalEntities} | DbSets: {dbSetProperties.Length}";

            if (_selectedEntityPath != null)
            {
                stats += $" | Selected: Entity";
            }

            if (!string.IsNullOrEmpty(_dbSetFilter))
            {
                stats += $" | DbSet: {_dbSetFilter}";
            }

            if (!string.IsNullOrEmpty(_searchFilter))
            {
                stats += $" | Search: '{_searchFilter}'";
            }

            string hints = "↑↓ Navigate | PgUp/PgDn Page | Home/End";
            if (!string.IsNullOrEmpty(_searchFilter))
            {
                hints = "Search active | " + hints;
            }

            SetStatusBar(stats, hints);
        }
        catch (Exception ex)
        {
            SetStatusBar($"Error: {ex.Message}", "Check database connection");
            SetStatusBarHealthColor(false);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Color Generation
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Gets a unique color for an entity type based on its name hash.
    ///     Each type gets a consistent, visually distinct color.
    /// </summary>
    private static Color GetEntityTypeColor(string typeName)
    {
        // Generate a hash from the type name
        int hash = typeName.GetHashCode();

        // Use hash to generate HSL values
        // Hue: 0-360 degrees (full color spectrum)
        float hue = Math.Abs(hash) % 360;

        // Saturation: 50-80% (vibrant but not oversaturated)
        float saturation = 0.5f + (Math.Abs(hash >> 8) % 30 / 100f);

        // Lightness: 55-75% (readable on dark background)
        float lightness = 0.55f + (Math.Abs(hash >> 16) % 20 / 100f);

        return HslToRgb(hue, saturation, lightness);
    }

    /// <summary>
    ///     Converts HSL color to RGB.
    /// </summary>
    private static Color HslToRgb(float h, float s, float l)
    {
        float c = (1 - Math.Abs((2 * l) - 1)) * s;
        float x = c * (1 - Math.Abs((h / 60 % 2) - 1));
        float m = l - (c / 2);

        float r, g, b;

        if (h < 60)
        {
            r = c; g = x; b = 0;
        }
        else if (h < 120)
        {
            r = x; g = c; b = 0;
        }
        else if (h < 180)
        {
            r = 0; g = c; b = x;
        }
        else if (h < 240)
        {
            r = 0; g = x; b = c;
        }
        else if (h < 300)
        {
            r = x; g = 0; b = c;
        }
        else
        {
            r = c; g = 0; b = x;
        }

        return new Color(
            (byte)((r + m) * 255),
            (byte)((g + m) * 255),
            (byte)((b + m) * 255)
        );
    }
}
