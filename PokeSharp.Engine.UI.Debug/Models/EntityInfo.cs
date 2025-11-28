namespace PokeSharp.Engine.UI.Debug.Models;

/// <summary>
/// Information about an entity for debug display.
/// </summary>
public class EntityInfo
{
    /// <summary>Entity ID</summary>
    public int Id { get; set; }

    /// <summary>Entity name/type</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Key-value properties to display</summary>
    public Dictionary<string, string> Properties { get; set; } = new();

    /// <summary>List of component types attached</summary>
    public List<string> Components { get; set; } = new();

    /// <summary>Whether this entity is currently active</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Optional tag/category for filtering</summary>
    public string? Tag { get; set; }
}




