namespace PokeSharp.Core.Factories;

/// <summary>
///     Fluent builder for configuring entity spawning with component overrides.
///     Provides a clean API for inline entity configuration during spawning.
/// </summary>
/// <example>
///     <code>
/// var entity = await factory.SpawnFromTemplateAsync("pokemon/bulbasaur", world, builder =>
/// {
///     builder.WithPosition(new Position(100, 200))
///            .WithTag("wild_pokemon")
///            .OverrideComponent(new Health { CurrentHP = 50, MaxHP = 100 });
/// });
/// </code>
/// </example>
public sealed class EntityBuilder
{
    private readonly Dictionary<Type, object> _componentOverrides = new();
    private readonly Dictionary<string, object> _customProperties = new();

    /// <summary>
    ///     Get the entity tag if set.
    /// </summary>
    internal string? Tag { get; private set; }

    /// <summary>
    ///     Get all component overrides.
    /// </summary>
    internal IReadOnlyDictionary<Type, object> ComponentOverrides => _componentOverrides;

    /// <summary>
    ///     Get all custom properties.
    /// </summary>
    internal IReadOnlyDictionary<string, object> CustomProperties => _customProperties;

    /// <summary>
    ///     Set the entity tag for querying.
    /// </summary>
    /// <param name="tag">Entity tag (e.g., "player", "npc", "pokemon")</param>
    /// <returns>This builder for chaining</returns>
    public EntityBuilder WithTag(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag, nameof(tag));
        Tag = tag;
        return this;
    }

    /// <summary>
    ///     Override a component's initial data from the template.
    ///     If the template doesn't have this component, it will be added.
    /// </summary>
    /// <typeparam name="T">Component type</typeparam>
    /// <param name="component">Component data to override</param>
    /// <returns>This builder for chaining</returns>
    public EntityBuilder OverrideComponent<T>(T component)
        where T : struct
    {
        _componentOverrides[typeof(T)] = component;
        return this;
    }

    /// <summary>
    ///     Add a custom property that can be used by systems or scripts.
    /// </summary>
    /// <param name="key">Property key</param>
    /// <param name="value">Property value</param>
    /// <returns>This builder for chaining</returns>
    public EntityBuilder WithProperty(string key, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(key));
        ArgumentNullException.ThrowIfNull(value);
        _customProperties[key] = value;
        return this;
    }

    /// <summary>
    ///     Get component override for a specific type.
    /// </summary>
    /// <typeparam name="T">Component type</typeparam>
    /// <returns>Component data or null if not overridden</returns>
    internal T? GetComponentOverride<T>()
        where T : struct
    {
        return _componentOverrides.TryGetValue(typeof(T), out var component) ? (T)component : null;
    }

    /// <summary>
    ///     Check if a component type has an override.
    /// </summary>
    /// <param name="componentType">Component type to check</param>
    /// <returns>True if override exists</returns>
    internal bool HasComponentOverride(Type componentType)
    {
        return _componentOverrides.ContainsKey(componentType);
    }

    /// <summary>
    ///     Get component override by type.
    /// </summary>
    /// <param name="componentType">Component type</param>
    /// <returns>Component data or null if not overridden</returns>
    internal object? GetComponentOverride(Type componentType)
    {
        return _componentOverrides.TryGetValue(componentType, out var component) ? component : null;
    }

    /// <summary>
    ///     Clear all overrides and properties (for reuse).
    /// </summary>
    internal void Clear()
    {
        _componentOverrides.Clear();
        _customProperties.Clear();
        Tag = null;
    }
}
