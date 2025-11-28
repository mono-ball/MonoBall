namespace PokeSharp.Engine.UI.Debug.Interfaces;

/// <summary>
///     Provides operations for the variables panel.
///     Implemented by VariablesPanel.
/// </summary>
public interface IVariableOperations
{
    /// <summary>Gets variable statistics.</summary>
    (int Variables, int Globals, int Pinned, int Expanded) GetStatistics();

    /// <summary>Gets all variable names.</summary>
    IEnumerable<string> GetNames();

    /// <summary>Gets a variable's current value.</summary>
    object? GetValue(string name);

    /// <summary>Sets the search filter.</summary>
    void SetSearchFilter(string filter);

    /// <summary>Clears the search filter.</summary>
    void ClearSearchFilter();

    /// <summary>Expands a variable to show its properties.</summary>
    void Expand(string path);

    /// <summary>Collapses an expanded variable.</summary>
    void Collapse(string path);

    /// <summary>Expands all expandable variables.</summary>
    void ExpandAll();

    /// <summary>Collapses all expanded variables.</summary>
    void CollapseAll();

    /// <summary>Pins a variable to the top.</summary>
    void Pin(string name);

    /// <summary>Unpins a variable.</summary>
    void Unpin(string name);

    /// <summary>Clears all user-defined variables.</summary>
    void Clear();

    /// <summary>Sets a variable with a value getter.</summary>
    void SetVariable(string name, string typeName, Func<object?> valueGetter);
}
