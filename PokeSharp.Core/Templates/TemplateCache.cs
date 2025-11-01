using System.Collections.Concurrent;

namespace PokeSharp.Core.Templates;

/// <summary>
/// Thread-safe in-memory cache for entity templates with O(1) lookup performance.
/// Supports hot-reload and invalidation for development workflows.
/// Uses ConcurrentDictionary for lock-free operations.
/// </summary>
public sealed class TemplateCache
{
    private readonly ConcurrentDictionary<string, EntityTemplate> _templates = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastModified = new();
    private readonly object _invalidationLock = new();

    /// <summary>
    /// Register a template in the cache.
    /// If a template with the same ID exists, it will be replaced.
    /// </summary>
    /// <param name="template">Template to register</param>
    /// <exception cref="ArgumentNullException">Template is null</exception>
    /// <exception cref="ArgumentException">Template has invalid data</exception>
    public void Register(EntityTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        if (string.IsNullOrWhiteSpace(template.TemplateId))
            throw new ArgumentException("Template must have a valid TemplateId", nameof(template));

        // Validate template before caching
        if (!template.Validate(out var errors))
            throw new ArgumentException($"Template validation failed: {string.Join(", ", errors)}", nameof(template));

        _templates[template.TemplateId] = template;
        _lastModified[template.TemplateId] = DateTime.UtcNow;
    }

    /// <summary>
    /// Get a template by ID with O(1) lookup performance.
    /// </summary>
    /// <param name="templateId">Unique template identifier</param>
    /// <returns>Template if found, null otherwise</returns>
    public EntityTemplate? Get(string templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId))
            return null;

        return _templates.TryGetValue(templateId, out var template) ? template : null;
    }

    /// <summary>
    /// Try to get a template by ID.
    /// </summary>
    /// <param name="templateId">Template identifier</param>
    /// <param name="template">Output template if found</param>
    /// <returns>True if template exists</returns>
    public bool TryGet(string templateId, out EntityTemplate? template)
    {
        template = Get(templateId);
        return template != null;
    }

    /// <summary>
    /// Check if a template exists in the cache.
    /// </summary>
    /// <param name="templateId">Template identifier</param>
    /// <returns>True if template is cached</returns>
    public bool Contains(string templateId)
    {
        return !string.IsNullOrWhiteSpace(templateId) && _templates.ContainsKey(templateId);
    }

    /// <summary>
    /// Remove a template from the cache (invalidation).
    /// </summary>
    /// <param name="templateId">Template identifier to remove</param>
    /// <returns>True if template was removed</returns>
    public bool Invalidate(string templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId))
            return false;

        var removed = _templates.TryRemove(templateId, out _);
        if (removed)
            _lastModified.TryRemove(templateId, out _);

        return removed;
    }

    /// <summary>
    /// Invalidate all templates matching a predicate.
    /// Useful for bulk invalidation (e.g., all pokemon templates).
    /// </summary>
    /// <param name="predicate">Predicate to test template IDs</param>
    /// <returns>Number of templates invalidated</returns>
    public int InvalidateWhere(Func<string, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        lock (_invalidationLock)
        {
            var toRemove = _templates.Keys.Where(predicate).ToList();
            var count = 0;

            foreach (var templateId in toRemove)
            {
                if (Invalidate(templateId))
                    count++;
            }

            return count;
        }
    }

    /// <summary>
    /// Clear all templates from the cache.
    /// Use with caution - typically only for testing or full reload.
    /// </summary>
    public void Clear()
    {
        _templates.Clear();
        _lastModified.Clear();
    }

    /// <summary>
    /// Get all template IDs currently in the cache.
    /// </summary>
    /// <returns>Enumerable of template IDs</returns>
    public IEnumerable<string> GetAllTemplateIds()
    {
        return _templates.Keys.ToList(); // Snapshot to avoid enumeration issues
    }

    /// <summary>
    /// Get all templates currently in the cache.
    /// Returns a snapshot to avoid concurrent modification issues.
    /// </summary>
    /// <returns>Enumerable of templates</returns>
    public IEnumerable<EntityTemplate> GetAllTemplates()
    {
        return _templates.Values.ToList(); // Snapshot
    }

    /// <summary>
    /// Get templates by tag (category/archetype).
    /// Example: GetByTag("pokemon") returns all Pokemon templates.
    /// </summary>
    /// <param name="tag">Entity tag to filter by</param>
    /// <returns>Templates matching the tag</returns>
    public IEnumerable<EntityTemplate> GetByTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return Enumerable.Empty<EntityTemplate>();

        return _templates.Values
            .Where(t => t.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase))
            .ToList(); // Snapshot
    }

    /// <summary>
    /// Get the timestamp when a template was last modified/registered.
    /// </summary>
    /// <param name="templateId">Template identifier</param>
    /// <returns>Last modified timestamp or null if not found</returns>
    public DateTime? GetLastModified(string templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId))
            return null;

        return _lastModified.TryGetValue(templateId, out var timestamp) ? timestamp : null;
    }

    /// <summary>
    /// Get cache statistics for monitoring.
    /// </summary>
    /// <returns>Cache statistics</returns>
    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics
        {
            TotalTemplates = _templates.Count,
            TemplatesByTag = _templates.Values
                .GroupBy(t => t.Tag)
                .ToDictionary(g => g.Key, g => g.Count()),
            OldestTemplate = _lastModified.Values.Any() ? _lastModified.Values.Min() : (DateTime?)null,
            NewestTemplate = _lastModified.Values.Any() ? _lastModified.Values.Max() : (DateTime?)null
        };
    }

    /// <summary>
    /// Batch register multiple templates efficiently.
    /// </summary>
    /// <param name="templates">Templates to register</param>
    /// <returns>Number of templates successfully registered</returns>
    public int RegisterBatch(IEnumerable<EntityTemplate> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);

        var count = 0;
        foreach (var template in templates)
        {
            try
            {
                Register(template);
                count++;
            }
            catch
            {
                // Log error but continue with other templates
                // In production, use ILogger here
                continue;
            }
        }

        return count;
    }
}

/// <summary>
/// Statistics about the template cache state.
/// </summary>
public sealed class CacheStatistics
{
    public int TotalTemplates { get; init; }
    public Dictionary<string, int> TemplatesByTag { get; init; } = new();
    public DateTime? OldestTemplate { get; init; }
    public DateTime? NewestTemplate { get; init; }

    public override string ToString()
    {
        return $"Templates: {TotalTemplates}, Tags: {TemplatesByTag.Count}, " +
               $"Oldest: {OldestTemplate?.ToString() ?? "N/A"}, " +
               $"Newest: {NewestTemplate?.ToString() ?? "N/A"}";
    }
}
