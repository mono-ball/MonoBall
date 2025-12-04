namespace PokeSharp.Game.Engine.UI.Debug.Models;

/// <summary>
///     Information about an entity for debug display.
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

    /// <summary>Component data with their field values (ComponentName -> Fields)</summary>
    public Dictionary<string, Dictionary<string, string>> ComponentData { get; set; } = new();

    /// <summary>Whether this entity is currently active</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Optional tag/category for filtering</summary>
    public string? Tag { get; set; }

    /// <summary>Entity relationships organized by type</summary>
    public Dictionary<string, List<EntityRelationship>> Relationships { get; set; } = new();
}

/// <summary>
///     Represents a relationship between entities.
/// </summary>
public class EntityRelationship
{
    /// <summary>The related entity ID</summary>
    public int EntityId { get; set; }

    /// <summary>Optional display name for the related entity</summary>
    public string? EntityName { get; set; }

    /// <summary>Additional relationship metadata (e.g., "Type: Permanent", "EstablishedAt: ...")</summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>Whether the relationship is valid</summary>
    public bool IsValid { get; set; } = true;
}
