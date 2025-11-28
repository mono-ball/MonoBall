using System.Collections.Concurrent;

namespace PokeSharp.Engine.Core.Templates;

/// <summary>
///     Thread-safe in-memory cache for entity templates.
///     Provides fast O(1) lookups by template ID.
/// </summary>
public sealed class TemplateCache
{
    private readonly ConcurrentDictionary<string, EntityTemplate> _templates = new();

    /// <summary>
    ///     Gets the number of templates in the cache.
    /// </summary>
    public int Count => _templates.Count;

    /// <summary>
    ///     Registers or updates a template in the cache.
    ///     If a template with the same ID exists, it will be replaced.
    /// </summary>
    public void Register(EntityTemplate template)
    {
        if (string.IsNullOrWhiteSpace(template.TemplateId))
        {
            throw new ArgumentException("Template ID cannot be null or empty", nameof(template));
        }

        if (!template.Validate(out List<string> errors))
        {
            throw new InvalidOperationException(
                $"Template validation failed for '{template.TemplateId}': {string.Join(", ", errors)}"
            );
        }

        _templates[template.TemplateId] = template;
    }

    /// <summary>
    ///     Retrieves a template by ID.
    ///     Returns null if not found.
    /// </summary>
    public EntityTemplate? Get(string templateId)
    {
        return _templates.TryGetValue(templateId, out EntityTemplate? template) ? template : null;
    }

    /// <summary>
    ///     Checks if a template exists in the cache.
    /// </summary>
    public bool Contains(string templateId)
    {
        return _templates.ContainsKey(templateId);
    }

    /// <summary>
    ///     Gets all templates in the cache.
    /// </summary>
    public IEnumerable<EntityTemplate> GetAll()
    {
        return _templates.Values;
    }

    /// <summary>
    ///     Clears all templates from the cache.
    /// </summary>
    public void Clear()
    {
        _templates.Clear();
    }

    /// <summary>
    ///     Gets all templates with a specific tag.
    /// </summary>
    public IEnumerable<EntityTemplate> GetByTag(string tag)
    {
        return _templates.Values.Where(t => t.Tag == tag);
    }
}
