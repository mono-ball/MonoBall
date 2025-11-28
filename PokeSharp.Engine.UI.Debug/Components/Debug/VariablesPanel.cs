using System.Collections;
using System.Reflection;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Interfaces;
using PokeSharp.Engine.UI.Debug.Layout;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
///     Panel for viewing, inspecting, and editing script variables.
///     Supports object expansion, inline editing, search, and pinning.
///     Implements <see cref="IVariableOperations" /> for command access.
/// </summary>
public class VariablesPanel : DebugPanelBase, IVariableOperations
{
    private const int MaxExpansionDepth = 5;
    private const int MaxCollectionItems = 20;

    private readonly List<DisplayRow> _displayRows = new();

    // Inspection state - tracks which paths are expanded
    private readonly HashSet<string> _expandedPaths = new();
    private readonly List<GlobalInfo> _globals = new();
    private readonly HashSet<string> _pinnedVariables = new();
    private readonly Dictionary<string, VariableInfo> _variables = new();
    private readonly TextBuffer _variablesBuffer;

    // Edit callback (for future edit functionality)
    private Action<string, object?>? _onVariableEdited;
    private string _searchFilter = "";

    /// <summary>
    ///     Creates a VariablesPanel with the specified components.
    ///     Use <see cref="VariablesPanelBuilder" /> to construct instances.
    /// </summary>
    internal VariablesPanel(TextBuffer variablesBuffer, StatusBar statusBar)
        : base(statusBar)
    {
        _variablesBuffer = variablesBuffer;

        Id = "variables_panel";

        // TextBuffer fills space above StatusBar
        _variablesBuffer.Constraint.Anchor = Anchor.StretchTop;

        AddChild(_variablesBuffer);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // IVariableOperations Explicit Interface Implementation
    // ═══════════════════════════════════════════════════════════════════════════

    (int Variables, int Globals, int Pinned, int Expanded) IVariableOperations.GetStatistics()
    {
        return GetStatistics();
    }

    IEnumerable<string> IVariableOperations.GetNames()
    {
        return GetVariableNames();
    }

    object? IVariableOperations.GetValue(string name)
    {
        return GetVariableValue(name);
    }

    void IVariableOperations.SetSearchFilter(string filter)
    {
        SetSearchFilter(filter);
    }

    void IVariableOperations.ClearSearchFilter()
    {
        ClearSearchFilter();
    }

    void IVariableOperations.Expand(string path)
    {
        ExpandVariable(path);
    }

    void IVariableOperations.Collapse(string path)
    {
        CollapseVariable(path);
    }

    void IVariableOperations.ExpandAll()
    {
        ExpandAll();
    }

    void IVariableOperations.CollapseAll()
    {
        CollapseAll();
    }

    void IVariableOperations.Pin(string name)
    {
        PinVariable(name);
    }

    void IVariableOperations.Unpin(string name)
    {
        UnpinVariable(name);
    }

    void IVariableOperations.Clear()
    {
        ClearVariables();
    }

    void IVariableOperations.SetVariable(string name, string typeName, Func<object?> valueGetter)
    {
        SetVariable(name, typeName, valueGetter);
    }

    protected override UIComponent GetContentComponent()
    {
        return _variablesBuffer;
    }

    /// <summary>
    ///     Sets the callback for when a variable is edited.
    /// </summary>
    public void SetEditCallback(Action<string, object?> callback)
    {
        _onVariableEdited = callback;
    }

    /// <summary>
    ///     Adds or updates a script variable.
    /// </summary>
    public void SetVariable(
        string name,
        string typeName,
        Func<object?> valueGetter,
        Action<object?>? valueSetter = null
    )
    {
        _variables[name] = new VariableInfo
        {
            Name = name,
            TypeName = typeName,
            ValueGetter = valueGetter,
            ValueSetter = valueSetter,
        };

        UpdateVariableDisplay();
    }

    /// <summary>
    ///     Removes a script variable.
    /// </summary>
    public void RemoveVariable(string name)
    {
        if (_variables.Remove(name))
        {
            _pinnedVariables.Remove(name);
            UpdateVariableDisplay();
        }
    }

    /// <summary>
    ///     Clears all script variables.
    /// </summary>
    public void ClearVariables()
    {
        _variables.Clear();
        _pinnedVariables.Clear();
        _expandedPaths.Clear();
        UpdateVariableDisplay();
    }

    /// <summary>
    ///     Sets the list of global variables available in the script environment.
    /// </summary>
    public void SetGlobals(IEnumerable<GlobalInfo> globals)
    {
        _globals.Clear();
        _globals.AddRange(globals);
        UpdateVariableDisplay();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Search & Filter
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Sets the search filter.
    /// </summary>
    public void SetSearchFilter(string filter)
    {
        _searchFilter = filter ?? "";
        UpdateVariableDisplay();
    }

    /// <summary>
    ///     Clears the search filter.
    /// </summary>
    public void ClearSearchFilter()
    {
        _searchFilter = "";
        UpdateVariableDisplay();
    }

    /// <summary>
    ///     Gets the current search filter.
    /// </summary>
    public string GetSearchFilter()
    {
        return _searchFilter;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Pinning
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Pins a variable to the top of the list.
    /// </summary>
    public void PinVariable(string name)
    {
        if (_variables.ContainsKey(name) || _globals.Any(g => g.Name == name))
        {
            _pinnedVariables.Add(name);
            UpdateVariableDisplay();
        }
    }

    /// <summary>
    ///     Unpins a variable.
    /// </summary>
    public void UnpinVariable(string name)
    {
        if (_pinnedVariables.Remove(name))
        {
            UpdateVariableDisplay();
        }
    }

    /// <summary>
    ///     Toggles the pinned state of a variable.
    /// </summary>
    public bool TogglePin(string name)
    {
        if (_pinnedVariables.Contains(name))
        {
            _pinnedVariables.Remove(name);
            UpdateVariableDisplay();
            return false;
        }

        PinVariable(name);
        return true;
    }

    /// <summary>
    ///     Gets the list of pinned variables.
    /// </summary>
    public IEnumerable<string> GetPinnedVariables()
    {
        return _pinnedVariables;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Expansion/Inspection
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Expands a variable to show its members.
    /// </summary>
    public void ExpandVariable(string path)
    {
        _expandedPaths.Add(path);
        UpdateVariableDisplay();
    }

    /// <summary>
    ///     Collapses a variable.
    /// </summary>
    public void CollapseVariable(string path)
    {
        // Remove this path and all children
        _expandedPaths.RemoveWhere(p =>
            p == path || p.StartsWith(path + ".") || p.StartsWith(path + "[")
        );
        UpdateVariableDisplay();
    }

    /// <summary>
    ///     Toggles expansion of a variable.
    /// </summary>
    public bool ToggleExpansion(string path)
    {
        if (_expandedPaths.Contains(path))
        {
            CollapseVariable(path);
            return false;
        }

        ExpandVariable(path);
        return true;
    }

    /// <summary>
    ///     Expands all variables.
    /// </summary>
    public void ExpandAll()
    {
        foreach (DisplayRow row in _displayRows.Where(r => r.IsExpandable))
        {
            _expandedPaths.Add(row.Path);
        }

        UpdateVariableDisplay();
    }

    /// <summary>
    ///     Collapses all variables.
    /// </summary>
    public void CollapseAll()
    {
        _expandedPaths.Clear();
        UpdateVariableDisplay();
    }

    /// <summary>
    ///     Forces an immediate update of the variable display.
    /// </summary>
    public void UpdateVariableDisplay()
    {
        _variablesBuffer.Clear();
        _displayRows.Clear();

        // Build display rows
        BuildDisplayRows();

        // Apply search filter
        List<DisplayRow> filteredRows = _displayRows;
        if (!string.IsNullOrEmpty(_searchFilter))
        {
            filteredRows = _displayRows
                .Where(r =>
                    r.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)
                    || r.TypeName.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)
                    || r.ValueStr.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)
                )
                .ToList();
        }

        // Count stats
        int pinnedCount = _pinnedVariables.Count;
        int expandedCount = _expandedPaths.Count;

        // Display pinned variables first
        var pinnedRows = filteredRows.Where(r => r.Depth == 0 && r.IsPinned).ToList();
        if (pinnedRows.Count > 0)
        {
            _variablesBuffer.AppendLine(
                $"  {NerdFontIcons.Pinned} PINNED",
                ThemeManager.Current.Warning
            );
            foreach (DisplayRow row in pinnedRows)
            {
                RenderRow(row);
            }

            _variablesBuffer.AppendLine("", ThemeManager.Current.TextDim);
        }

        // Display user-defined variables
        var userRows = filteredRows
            .Where(r => _variables.ContainsKey(GetRootName(r.Path)) && !r.IsPinned)
            .ToList();

        if (userRows.Count == 0 && pinnedRows.Count == 0 && _variables.Count == 0)
        {
            _variablesBuffer.AppendLine("  No variables defined.", ThemeManager.Current.TextDim);
        }
        else if (userRows.Count > 0)
        {
            foreach (DisplayRow row in userRows)
            {
                RenderRow(row);
            }
        }

        // Display globals section
        var globalRows = filteredRows
            .Where(r => _globals.Any(g => g.Name == GetRootName(r.Path)))
            .ToList();

        if (globalRows.Count > 0)
        {
            _variablesBuffer.AppendLine("", ThemeManager.Current.TextDim);
            _variablesBuffer.AppendLine("  [G] GLOBALS", ThemeManager.Current.Info);

            foreach (DisplayRow row in globalRows)
            {
                RenderRow(row);
            }
        }

        // Update status bar
        UpdateStatusBar();
    }

    /// <summary>
    ///     Updates the status bar with current variable stats.
    /// </summary>
    protected override void UpdateStatusBar()
    {
        // Build stats text
        string stats = $"Variables: {_variables.Count}";
        if (_globals.Count > 0)
        {
            stats += $" | Globals: {_globals.Count}";
        }

        if (_pinnedVariables.Count > 0)
        {
            stats += $" | Pinned: {_pinnedVariables.Count}";
        }

        if (_expandedPaths.Count > 0)
        {
            stats += $" | Expanded: {_expandedPaths.Count}";
        }

        // Build hints text
        string hints = !string.IsNullOrEmpty(_searchFilter)
            ? "Search active"
            : "'variables' to manage";

        SetStatusBar(stats, hints);
        // StatsColor uses theme fallback (Success) - don't set explicitly for dynamic theme support
    }

    /// <summary>
    ///     Builds the display rows from variables and globals.
    /// </summary>
    private void BuildDisplayRows()
    {
        // Add user variables
        foreach (
            KeyValuePair<string, VariableInfo> kvp in _variables
                .OrderBy(v => !_pinnedVariables.Contains(v.Key))
                .ThenBy(v => v.Key)
        )
        {
            VariableInfo variable = kvp.Value;
            try
            {
                object? value = variable.ValueGetter();
                AddDisplayRow(
                    variable.Name,
                    variable.Name,
                    variable.TypeName,
                    value,
                    0,
                    !variable.IsReadOnly,
                    _pinnedVariables.Contains(variable.Name)
                );
            }
            catch (Exception ex)
            {
                _displayRows.Add(
                    new DisplayRow
                    {
                        Path = variable.Name,
                        Name = variable.Name,
                        TypeName = variable.TypeName,
                        ValueStr = $"[Error: {ex.Message}]",
                        ValueColor = ThemeManager.Current.Error,
                        Depth = 0,
                        IsPinned = _pinnedVariables.Contains(variable.Name),
                    }
                );
            }
        }

        // Add globals with their instances for inspection
        foreach (
            GlobalInfo global in _globals
                .OrderBy(g => !_pinnedVariables.Contains(g.Name))
                .ThenBy(g => g.Name)
        )
        {
            AddDisplayRow(
                global.Name,
                global.Name,
                global.TypeName,
                global.Instance,
                0,
                false,
                _pinnedVariables.Contains(global.Name)
            );
        }
    }

    /// <summary>
    ///     Adds a display row and its children if expanded.
    /// </summary>
    private void AddDisplayRow(
        string path,
        string name,
        string typeName,
        object? value,
        int depth,
        bool isEditable,
        bool isPinned
    )
    {
        bool isExpandable = IsExpandableValue(value);
        bool isExpanded = _expandedPaths.Contains(path);

        var row = new DisplayRow
        {
            Path = path,
            Name = name,
            TypeName = GetDisplayTypeName(typeName, value),
            ValueStr = FormatValue(value, isExpanded),
            Value = value,
            Depth = depth,
            IsExpandable = isExpandable,
            IsExpanded = isExpanded,
            IsPinned = isPinned && depth == 0,
            IsEditable = isEditable && IsEditableType(value),
            ValueColor = GetValueColor(value),
        };

        _displayRows.Add(row);

        // Add children if expanded
        if (isExpanded && value != null && depth < MaxExpansionDepth)
        {
            AddChildRows(path, value, depth + 1);
        }
    }

    /// <summary>
    ///     Adds child rows for an expanded object.
    /// </summary>
    private void AddChildRows(string parentPath, object value, int depth)
    {
        Type type = value.GetType();

        // Handle collections
        if (value is IDictionary dict)
        {
            int index = 0;
            foreach (DictionaryEntry entry in dict)
            {
                if (index >= MaxCollectionItems)
                {
                    _displayRows.Add(
                        new DisplayRow
                        {
                            Path = $"{parentPath}[...]",
                            Name = $"... ({dict.Count - MaxCollectionItems} more)",
                            TypeName = "",
                            ValueStr = "",
                            Depth = depth,
                            ValueColor = ThemeManager.Current.TextDim,
                        }
                    );
                    break;
                }

                string keyStr = FormatValue(entry.Key);
                string childPath = $"{parentPath}[{keyStr}]";
                AddDisplayRow(
                    childPath,
                    $"[{keyStr}]",
                    entry.Value?.GetType()?.Name ?? "null",
                    entry.Value,
                    depth,
                    false,
                    false
                );
                index++;
            }
        }
        else if (value is IList list)
        {
            for (int i = 0; i < Math.Min(list.Count, MaxCollectionItems); i++)
            {
                string childPath = $"{parentPath}[{i}]";
                object? item = list[i];
                AddDisplayRow(
                    childPath,
                    $"[{i}]",
                    item?.GetType()?.Name ?? "null",
                    item,
                    depth,
                    false,
                    false
                );
            }

            if (list.Count > MaxCollectionItems)
            {
                _displayRows.Add(
                    new DisplayRow
                    {
                        Path = $"{parentPath}[...]",
                        Name = $"... ({list.Count - MaxCollectionItems} more)",
                        TypeName = "",
                        ValueStr = "",
                        Depth = depth,
                        ValueColor = ThemeManager.Current.TextDim,
                    }
                );
            }
        }
        else if (value is IEnumerable enumerable && !(value is string))
        {
            int index = 0;
            foreach (object? item in enumerable)
            {
                if (index >= MaxCollectionItems)
                {
                    _displayRows.Add(
                        new DisplayRow
                        {
                            Path = $"{parentPath}[...]",
                            Name = "... (more items)",
                            TypeName = "",
                            ValueStr = "",
                            Depth = depth,
                            ValueColor = ThemeManager.Current.TextDim,
                        }
                    );
                    break;
                }

                string childPath = $"{parentPath}[{index}]";
                AddDisplayRow(
                    childPath,
                    $"[{index}]",
                    item?.GetType()?.Name ?? "null",
                    item,
                    depth,
                    false,
                    false
                );
                index++;
            }
        }
        else
        {
            // Add properties
            IEnumerable<PropertyInfo> properties = type.GetProperties(
                    BindingFlags.Public | BindingFlags.Instance
                )
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .OrderBy(p => p.Name)
                .Take(50); // Limit to prevent huge lists

            foreach (PropertyInfo prop in properties)
            {
                try
                {
                    object? propValue = prop.GetValue(value);
                    string childPath = $"{parentPath}.{prop.Name}";
                    AddDisplayRow(
                        childPath,
                        prop.Name,
                        prop.PropertyType.Name,
                        propValue,
                        depth,
                        prop.CanWrite,
                        false
                    );
                }
                catch
                {
                    string childPath = $"{parentPath}.{prop.Name}";
                    _displayRows.Add(
                        new DisplayRow
                        {
                            Path = childPath,
                            Name = prop.Name,
                            TypeName = prop.PropertyType.Name,
                            ValueStr = "[Error reading]",
                            Depth = depth,
                            ValueColor = ThemeManager.Current.Error,
                        }
                    );
                }
            }

            // Add public fields
            IEnumerable<FieldInfo> fields = type.GetFields(
                    BindingFlags.Public | BindingFlags.Instance
                )
                .OrderBy(f => f.Name)
                .Take(50);

            foreach (FieldInfo field in fields)
            {
                try
                {
                    object? fieldValue = field.GetValue(value);
                    string childPath = $"{parentPath}.{field.Name}";
                    AddDisplayRow(
                        childPath,
                        field.Name,
                        field.FieldType.Name,
                        fieldValue,
                        depth,
                        !field.IsInitOnly,
                        false
                    );
                }
                catch
                {
                    string childPath = $"{parentPath}.{field.Name}";
                    _displayRows.Add(
                        new DisplayRow
                        {
                            Path = childPath,
                            Name = field.Name,
                            TypeName = field.FieldType.Name,
                            ValueStr = "[Error reading]",
                            Depth = depth,
                            ValueColor = ThemeManager.Current.Error,
                        }
                    );
                }
            }
        }
    }

    /// <summary>
    ///     Renders a single display row to the buffer.
    /// </summary>
    private void RenderRow(DisplayRow row)
    {
        string indent = new(' ', 2 + (row.Depth * 2));

        // Expansion indicator
        string expandIndicator;
        if (row.IsExpandable)
        {
            expandIndicator = row.IsExpanded
                ? NerdFontIcons.ExpandedWithSpace
                : NerdFontIcons.CollapsedWithSpace;
        }
        else
        {
            expandIndicator = NerdFontIcons.UnselectedSpace;
        }

        // Build the line
        int nameWidth = Math.Max(1, 25 - (row.Depth * 2));
        string name =
            row.Name.Length > nameWidth
                ? row.Name.Substring(0, nameWidth - 1) + "…"
                : row.Name.PadRight(nameWidth);
        string typeStr =
            row.TypeName.Length > 15
                ? row.TypeName.Substring(0, 14) + "…"
                : row.TypeName.PadRight(15);

        // Build the full line - use value color for the whole line for simplicity
        string line = $"{indent}{expandIndicator}{name} {typeStr} {row.ValueStr}";
        _variablesBuffer.AppendLine(line, row.ValueColor);
    }

    /// <summary>
    ///     Gets the root variable name from a path.
    /// </summary>
    private static string GetRootName(string path)
    {
        int dotIndex = path.IndexOf('.');
        int bracketIndex = path.IndexOf('[');

        if (dotIndex < 0 && bracketIndex < 0)
        {
            return path;
        }

        if (dotIndex < 0)
        {
            return path.Substring(0, bracketIndex);
        }

        if (bracketIndex < 0)
        {
            return path.Substring(0, dotIndex);
        }

        return path.Substring(0, Math.Min(dotIndex, bracketIndex));
    }

    /// <summary>
    ///     Formats a value for display.
    /// </summary>
    private string FormatValue(object? value, bool isExpanded = false)
    {
        if (value == null)
        {
            return "null";
        }

        Type type = value.GetType();

        // Handle primitives
        if (type.IsPrimitive || type == typeof(string))
        {
            if (value is bool b)
            {
                return b ? "true" : "false";
            }

            if (value is float f)
            {
                return f.ToString("F2");
            }

            if (value is double d)
            {
                return d.ToString("F2");
            }

            if (value is string s)
            {
                if (s.Length > 50)
                {
                    return $"\"{s.Substring(0, 47)}...\"";
                }

                return $"\"{s}\"";
            }

            return value.ToString() ?? "null";
        }

        // Handle Vector2
        if (value is Vector2 v2)
        {
            return $"({v2.X:F2}, {v2.Y:F2})";
        }

        // Handle Vector3
        if (value is Vector3 v3)
        {
            return $"({v3.X:F2}, {v3.Y:F2}, {v3.Z:F2})";
        }

        // Handle Color
        if (value is Color color)
        {
            return $"RGBA({color.R}, {color.G}, {color.B}, {color.A})";
        }

        // Handle Point
        if (value is Point pt)
        {
            return $"({pt.X}, {pt.Y})";
        }

        // Handle Rectangle
        if (value is Rectangle rect)
        {
            return $"({rect.X}, {rect.Y}, {rect.Width}x{rect.Height})";
        }

        // Handle collections
        if (value is IDictionary dict)
        {
            return isExpanded ? $"Dictionary ({dict.Count} items)" : $"{{ {dict.Count} items }}";
        }

        if (value is ICollection collection)
        {
            return isExpanded
                ? $"Collection ({collection.Count} items)"
                : $"[{collection.Count} items]";
        }

        if (value is IEnumerable && !(value is string))
        {
            return isExpanded ? "Enumerable" : "[...]";
        }

        // Handle DateTime
        if (value is DateTime dt)
        {
            return dt.ToString("yyyy-MM-dd HH:mm:ss");
        }

        // Handle TimeSpan
        if (value is TimeSpan ts)
        {
            return ts.ToString(@"hh\:mm\:ss\.fff");
        }

        // Handle Guid
        if (value is Guid guid)
        {
            return guid.ToString("D");
        }

        // Handle enums
        if (type.IsEnum)
        {
            return value.ToString() ?? type.Name;
        }

        // Complex object
        if (isExpanded)
        {
            return $"{type.Name}";
        }

        return $"{{ {type.Name} }}";
    }

    /// <summary>
    ///     Gets a display-friendly type name.
    /// </summary>
    private static string GetDisplayTypeName(string typeName, object? value)
    {
        if (value == null)
        {
            return typeName;
        }

        Type type = value.GetType();

        // Handle generic types
        if (type.IsGenericType)
        {
            string baseName = type.Name;
            int tickIndex = baseName.IndexOf('`');
            if (tickIndex > 0)
            {
                baseName = baseName.Substring(0, tickIndex);
            }

            Type[] args = type.GetGenericArguments();
            string argNames = string.Join(", ", args.Select(a => a.Name));
            return $"{baseName}<{argNames}>";
        }

        // Handle arrays
        if (type.IsArray)
        {
            Type? elementType = type.GetElementType();
            return $"{elementType?.Name ?? "?"}[]";
        }

        return type.Name;
    }

    /// <summary>
    ///     Determines if a value can be expanded to show children.
    /// </summary>
    private static bool IsExpandableValue(object? value)
    {
        if (value == null)
        {
            return false;
        }

        Type type = value.GetType();

        // Primitives and strings are not expandable
        if (type.IsPrimitive || type == typeof(string))
        {
            return false;
        }

        // Known simple types
        if (type == typeof(DateTime) || type == typeof(TimeSpan) || type == typeof(Guid))
        {
            return false;
        }

        if (type == typeof(Vector2) || type == typeof(Vector3))
        {
            return false;
        }

        if (type == typeof(Color) || type == typeof(Point) || type == typeof(Rectangle))
        {
            return false;
        }

        // Enums are not expandable
        if (type.IsEnum)
        {
            return false;
        }

        // Collections are expandable
        if (value is IEnumerable)
        {
            return true;
        }

        // Objects with properties/fields are expandable
        bool hasProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(p => p.CanRead && p.GetIndexParameters().Length == 0);
        bool hasFields = type.GetFields(BindingFlags.Public | BindingFlags.Instance).Any();

        return hasProperties || hasFields;
    }

    /// <summary>
    ///     Determines if a value type can be edited inline.
    /// </summary>
    private static bool IsEditableType(object? value)
    {
        if (value == null)
        {
            return false;
        }

        Type type = value.GetType();

        // Only allow editing of simple types
        return type.IsPrimitive
            || type == typeof(string)
            || type == typeof(DateTime)
            || type.IsEnum;
    }

    /// <summary>
    ///     Gets the display color for a value based on its type.
    /// </summary>
    private static Color GetValueColor(object? value)
    {
        if (value == null)
        {
            return ThemeManager.Current.TextDim;
        }

        Type type = value.GetType();

        if (value is bool)
        {
            return ThemeManager.Current.Info; // Blue for booleans
        }

        if (type.IsPrimitive)
        {
            return ThemeManager.Current.Success; // Green for numbers
        }

        if (value is string)
        {
            return ThemeManager.Current.SyntaxString; // Theme string color
        }

        if (type.IsEnum)
        {
            return ThemeManager.Current.Warning; // Yellow for enums
        }

        if (value is ICollection)
        {
            return ThemeManager.Current.TextSecondary;
        }

        return ThemeManager.Current.TextPrimary;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Public API
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Gets the count of user-defined variables.
    /// </summary>
    public int GetVariableCount()
    {
        return _variables.Count;
    }

    /// <summary>
    ///     Gets the count of global variables.
    /// </summary>
    public int GetGlobalCount()
    {
        return _globals.Count;
    }

    /// <summary>
    ///     Gets all variable names.
    /// </summary>
    public IEnumerable<string> GetVariableNames()
    {
        return _variables.Keys;
    }

    /// <summary>
    ///     Gets a variable's current value.
    /// </summary>
    public object? GetVariableValue(string name)
    {
        if (_variables.TryGetValue(name, out VariableInfo? info))
        {
            try
            {
                return info.ValueGetter();
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    ///     Checks if a variable exists.
    /// </summary>
    public bool HasVariable(string name)
    {
        return _variables.ContainsKey(name);
    }

    /// <summary>
    ///     Gets statistics about the variables panel.
    /// </summary>
    public (int Variables, int Globals, int Pinned, int Expanded) GetStatistics()
    {
        return (_variables.Count, _globals.Count, _pinnedVariables.Count, _expandedPaths.Count);
    }

    public class VariableInfo
    {
        public string Name { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public Func<object?> ValueGetter { get; set; } = () => null;
        public Action<object?>? ValueSetter { get; set; } // For editing
        public bool IsReadOnly => ValueSetter == null;
    }

    public class GlobalInfo
    {
        public string Name { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public object? Instance { get; set; } = null; // For inspection
    }

    // Represents a displayable row in the variables view
    private class DisplayRow
    {
        public string Path { get; set; } = "";
        public string Name { get; set; } = "";
        public string TypeName { get; set; } = "";
        public string ValueStr { get; set; } = "";
        public int Depth { get; set; }
        public bool IsExpandable { get; set; }
        public bool IsExpanded { get; set; }
        public bool IsPinned { get; set; }
        public bool IsEditable { get; set; }
        public object? Value { get; set; }
        public Color ValueColor { get; set; } // Uses theme at render time
    }
}
