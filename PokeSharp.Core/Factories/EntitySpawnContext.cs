namespace PokeSharp.Core.Factories;

/// <summary>
///     Context information for spawning an entity from a template.
///     Provides position, tag overrides, and component initialization data.
/// </summary>
public sealed class EntitySpawnContext
{
    /// <summary>
    ///     Optional tag override (replaces template's default tag).
    ///     Useful for spawning variants of the same template.
    /// </summary>
    public string? Tag { get; set; }

    /// <summary>
    ///     Component-specific overrides for initialization data.
    ///     Key: Component type name (e.g., "Health", "Position")
    ///     Value: Override data (will be merged with template data)
    /// </summary>
    public Dictionary<string, object> Overrides { get; set; } = new();

    /// <summary>
    ///     Optional parent entity for hierarchical relationships.
    ///     Used with Arch.Relationships for entity parent-child links.
    /// </summary>
    public int? ParentEntityId { get; set; }

    /// <summary>
    ///     Custom metadata to attach to spawned entity.
    ///     Useful for tracking spawn source, modding data, etc.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    ///     Create an empty spawn context (use template defaults).
    /// </summary>
    public static EntitySpawnContext Default => new();
}
