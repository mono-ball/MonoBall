namespace PokeSharp.Engine.Core.Templates;

/// <summary>
///     Template for creating Arch ECS entities with predefined components.
///     Acts as a bridge between static data (EF Core) and runtime entities (Arch ECS).
///     Templates are compiled from data layer entities and cached for fast spawning.
/// </summary>
public sealed class EntityTemplate
{
    /// <summary>
    ///     Unique identifier for this template.
    ///     Format: "category/name" (e.g., "pokemon/bulbasaur", "npc/professor_oak")
    /// </summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>
    ///     Human-readable display name.
    ///     Example: "Bulbasaur", "Professor Oak"
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Entity category/archetype tag for querying.
    ///     Example: "pokemon", "npc", "item", "trigger"
    ///     Used for ECS queries and filtering.
    /// </summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    ///     List of components to attach when spawning entity.
    ///     Components are applied in order during entity creation.
    /// </summary>
    public List<ComponentTemplate> Components { get; set; } = new();

    /// <summary>
    ///     Metadata about template origin and compilation.
    ///     Used for hot-reload and cache invalidation.
    /// </summary>
    public EntityTemplateMetadata Metadata { get; set; } = new();

    /// <summary>
    ///     Optional base template ID for inheritance.
    ///     Child templates inherit components from base and can override them.
    ///     Example: "pokemon/base" → "pokemon/bulbasaur"
    /// </summary>
    public string? BaseTemplateId { get; set; }

    /// <summary>
    ///     Optional custom properties for template-specific data.
    ///     Useful for modding and custom behaviors.
    /// </summary>
    public Dictionary<string, object>? CustomProperties { get; set; }

    /// <summary>
    ///     Get component count for this template.
    /// </summary>
    public int ComponentCount => Components.Count;

    /// <summary>
    ///     Add a component template to this entity template.
    /// </summary>
    /// <param name="componentTemplate">Component template to add</param>
    public void AddComponent(ComponentTemplate componentTemplate)
    {
        ArgumentNullException.ThrowIfNull(componentTemplate);
        Components.Add(componentTemplate);
    }

    /// <summary>
    ///     Add a component template with fluent API.
    /// </summary>
    /// <typeparam name="T">Component type</typeparam>
    /// <param name="initialData">Initial component data</param>
    /// <param name="scriptId">Optional script ID</param>
    /// <returns>This template for chaining</returns>
    public EntityTemplate WithComponent<T>(T initialData, string? scriptId = null)
        where T : struct
    {
        AddComponent(ComponentTemplate.Create(initialData, scriptId));
        return this;
    }

    /// <summary>
    ///     Check if template has a component of the specified type.
    /// </summary>
    /// <typeparam name="T">Component type to check</typeparam>
    /// <returns>True if component exists in template</returns>
    public bool HasComponent<T>()
        where T : struct
    {
        return Components.Any(c => c.ComponentType == typeof(T));
    }

    /// <summary>
    ///     Get a component template by type.
    /// </summary>
    /// <typeparam name="T">Component type</typeparam>
    /// <returns>Component template or null if not found</returns>
    public ComponentTemplate? GetComponent<T>()
        where T : struct
    {
        return Components.FirstOrDefault(c => c.ComponentType == typeof(T));
    }

    /// <summary>
    ///     Validate this template for completeness and correctness.
    /// </summary>
    /// <returns>True if template is valid</returns>
    public bool Validate(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(TemplateId))
        {
            errors.Add("TemplateId is required");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            errors.Add("Name is required");
        }

        if (string.IsNullOrWhiteSpace(Tag))
        {
            errors.Add("Tag is required");
        }

        // Allow empty components if template inherits from another template
        if (Components.Count == 0 && string.IsNullOrWhiteSpace(BaseTemplateId))
        {
            errors.Add("Template must have at least one component or inherit from a base template");
        }

        // Validate component types
        foreach (ComponentTemplate component in Components)
        {
            if (component.ComponentType == null)
            {
                errors.Add("Component has null ComponentType");
            }
            else if (!component.ComponentType.IsValueType)
            {
                errors.Add($"Component type {component.ComponentType.Name} must be a struct");
            }
        }

        // Check for duplicate component types
        var duplicates = Components
            .GroupBy(c => c.ComponentType)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key?.Name ?? "Unknown")
            .ToList();

        if (duplicates.Any())
        {
            errors.Add($"Duplicate component types: {string.Join(", ", duplicates)}");
        }

        return errors.Count == 0;
    }
}
