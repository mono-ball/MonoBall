namespace PokeSharp.Core.Templates;

/// <summary>
///     Defines how to initialize a component when spawning an entity from a template.
///     Provides type-safe component type information and initialization data.
/// </summary>
public sealed class ComponentTemplate
{
    /// <summary>
    ///     The runtime type of the component to create.
    ///     Must be a struct that can be added to an Arch entity.
    /// </summary>
    public Type ComponentType { get; set; } = null!;

    /// <summary>
    ///     Initial data for the component, serialized as JSON or object.
    ///     Will be deserialized to ComponentType when spawning the entity.
    /// </summary>
    public object InitialData { get; set; } = null!;

    /// <summary>
    ///     Optional Roslyn script ID for dynamic behavior attachment.
    ///     Example: "Scripts/Behaviors/NpcPatrol.csx"
    /// </summary>
    public string? ScriptId { get; set; }

    /// <summary>
    ///     Optional tags for component categorization and filtering.
    ///     Example: ["runtime", "serializable", "network-synced"]
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>
    ///     Create a strongly-typed component template.
    /// </summary>
    /// <typeparam name="T">Component type (must be a struct)</typeparam>
    /// <param name="initialData">Initial component data</param>
    /// <param name="scriptId">Optional script ID</param>
    /// <returns>Configured component template</returns>
    public static ComponentTemplate Create<T>(T initialData, string? scriptId = null)
        where T : struct
    {
        return new ComponentTemplate
        {
            ComponentType = typeof(T),
            InitialData = initialData,
            ScriptId = scriptId,
        };
    }

    /// <summary>
    ///     Get the component type name for debugging.
    /// </summary>
    public string GetTypeName()
    {
        return ComponentType?.Name ?? "Unknown";
    }
}
