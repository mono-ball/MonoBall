using System.Text.Json.Nodes;

namespace PokeSharp.Engine.Core.Templates.Loading;

/// <summary>
///     Caches template JSON data before deserialization.
///     Allows patches to be applied to JSON before converting to EntityTemplate.
/// </summary>
public sealed class TemplateJsonCache
{
    private readonly Dictionary<string, string> _pathByTemplateId = new();
    private readonly Dictionary<string, JsonNode> _templateJsonByPath = new();

    public int Count => _templateJsonByPath.Count;

    /// <summary>
    ///     Stores JSON for a template file
    /// </summary>
    public void Add(string filePath, JsonNode templateJson)
    {
        _templateJsonByPath[filePath] = templateJson;

        // Extract templateId from JSON
        if (
            templateJson is JsonObject obj
            && obj.TryGetPropertyValue("templateId", out JsonNode? idNode)
        )
        {
            string? templateId = idNode?.ToString().Trim('"');
            if (!string.IsNullOrEmpty(templateId))
            {
                _pathByTemplateId[templateId] = filePath;
            }
        }
    }

    /// <summary>
    ///     Gets JSON by file path
    /// </summary>
    public JsonNode? GetByPath(string filePath)
    {
        return _templateJsonByPath.TryGetValue(filePath, out JsonNode? json) ? json : null;
    }

    /// <summary>
    ///     Gets JSON by template ID
    /// </summary>
    public JsonNode? GetByTemplateId(string templateId)
    {
        if (_pathByTemplateId.TryGetValue(templateId, out string? path))
        {
            return GetByPath(path);
        }

        return null;
    }

    /// <summary>
    ///     Gets the file path for a template ID
    /// </summary>
    public string? GetPathByTemplateId(string templateId)
    {
        return _pathByTemplateId.TryGetValue(templateId, out string? path) ? path : null;
    }

    /// <summary>
    ///     Updates the JSON for a template (after patching)
    /// </summary>
    public void Update(string templateId, JsonNode patchedJson)
    {
        if (_pathByTemplateId.TryGetValue(templateId, out string? path))
        {
            _templateJsonByPath[path] = patchedJson;
        }
    }

    /// <summary>
    ///     Gets all template JSON nodes
    /// </summary>
    public IEnumerable<(string Path, JsonNode Json)> GetAll()
    {
        return _templateJsonByPath.Select(kvp => (kvp.Key, kvp.Value));
    }
}
