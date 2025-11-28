using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Core.Templates.Loading;

/// <summary>
///     Loads EntityTemplates from JSON files.
///     Supports component deserialization and template inheritance.
/// </summary>
public class JsonTemplateLoader
{
    private readonly ComponentDeserializerRegistry _componentRegistry;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<JsonTemplateLoader> _logger;

    public JsonTemplateLoader(
        ILogger<JsonTemplateLoader> logger,
        ComponentDeserializerRegistry componentRegistry
    )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _componentRegistry =
            componentRegistry ?? throw new ArgumentNullException(nameof(componentRegistry));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };
    }

    /// <summary>
    ///     Load a single template from a JSON file.
    /// </summary>
    /// <param name="filePath">Path to the JSON file</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Loaded EntityTemplate</returns>
    public async Task<EntityTemplate> LoadTemplateAsync(
        string filePath,
        CancellationToken ct = default
    )
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Template file not found: {filePath}");
        }

        try
        {
            string json = await File.ReadAllTextAsync(filePath, ct);
            TemplateDto? dto = JsonSerializer.Deserialize<TemplateDto>(json, _jsonOptions);

            if (dto == null)
            {
                throw new InvalidOperationException(
                    $"Failed to deserialize template from {filePath}"
                );
            }

            EntityTemplate template = ConvertDtoToTemplate(dto, filePath);

            _logger.LogDebug(
                "[steelblue1]WF[/] Loaded template [cyan]{TemplateId}[/] from [cyan]{FilePath}[/] with [yellow]{ComponentCount}[/] components",
                template.TemplateId,
                filePath,
                template.ComponentCount
            );

            return template;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[steelblue1]WF[/] [red]✗[/] Failed to load template from [cyan]{FilePath}[/]",
                filePath
            );
            throw;
        }
    }

    /// <summary>
    ///     Load multiple templates from a directory.
    /// </summary>
    /// <param name="directoryPath">Path to directory containing JSON files</param>
    /// <param name="recursive">Search subdirectories</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of loaded templates</returns>
    public async Task<List<EntityTemplate>> LoadTemplatesFromDirectoryAsync(
        string directoryPath,
        bool recursive = true,
        CancellationToken ct = default
    )
    {
        if (!Directory.Exists(directoryPath))
        {
            _logger.LogWarning(
                "[steelblue1]WF[/] [orange3]⚠[/] Template directory not found: [cyan]{DirectoryPath}[/]",
                directoryPath
            );
            return new List<EntityTemplate>();
        }

        SearchOption searchOption = recursive
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;
        string[] files = Directory.GetFiles(directoryPath, "*.json", searchOption);
        var templates = new List<EntityTemplate>();

        foreach (string file in files)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                EntityTemplate template = await LoadTemplateAsync(file, ct);
                templates.Add(template);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "[steelblue1]WF[/] [orange3]⚠[/] Skipping invalid template file: [cyan]{File}[/]",
                    file
                );
            }
        }

        _logger.LogInformation(
            "[steelblue1]WF[/] [green]✓[/] Loaded [yellow]{Count}[/] templates from [cyan]{DirectoryPath}[/]",
            templates.Count,
            directoryPath
        );

        return templates;
    }

    /// <summary>
    ///     Load template JSON files without deserializing (for patching).
    /// </summary>
    public async Task<TemplateJsonCache> LoadTemplateJsonAsync(
        string directoryPath,
        bool recursive = true,
        CancellationToken ct = default
    )
    {
        var cache = new TemplateJsonCache();

        if (!Directory.Exists(directoryPath))
        {
            _logger.LogWarning(
                "[steelblue1]WF[/] [orange3]⚠[/] Template directory not found: [cyan]{DirectoryPath}[/]",
                directoryPath
            );
            return cache;
        }

        SearchOption searchOption = recursive
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;
        string[] files = Directory.GetFiles(directoryPath, "*.json", searchOption);

        foreach (string file in files)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                string json = await File.ReadAllTextAsync(file, ct);
                var jsonNode = JsonNode.Parse(json);

                if (jsonNode != null)
                {
                    cache.Add(file, jsonNode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "[steelblue1]WF[/] [orange3]⚠[/] Failed to parse JSON from: [cyan]{File}[/]",
                    file
                );
            }
        }

        _logger.LogDebug(
            "[steelblue1]WF[/] Loaded [yellow]{Count}[/] template JSON files from [cyan]{DirectoryPath}[/]",
            cache.Count,
            directoryPath
        );
        return cache;
    }

    /// <summary>
    ///     Deserialize a template from a JSON node (after patching).
    /// </summary>
    public EntityTemplate DeserializeTemplate(JsonNode templateJson, string sourcePath)
    {
        TemplateDto? dto = JsonSerializer.Deserialize<TemplateDto>(
            templateJson.ToJsonString(),
            _jsonOptions
        );

        if (dto == null)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize template from {sourcePath}"
            );
        }

        return ConvertDtoToTemplate(dto, sourcePath);
    }

    /// <summary>
    ///     Convert a TemplateDto to an EntityTemplate.
    /// </summary>
    private EntityTemplate ConvertDtoToTemplate(TemplateDto dto, string sourcePath)
    {
        var template = new EntityTemplate
        {
            TemplateId =
                dto.TemplateId ?? throw new InvalidOperationException("TemplateId is required"),
            Name = dto.Name ?? dto.TemplateId,
            Tag = dto.Tag ?? "entity",
            BaseTemplateId = dto.BaseTemplateId,
            CustomProperties = dto.CustomProperties,
            Metadata = new EntityTemplateMetadata
            {
                Version = dto.Version ?? "1.0.0",
                CompiledAt = DateTime.UtcNow,
                SourcePath = sourcePath,
            },
        };

        // Deserialize components
        if (dto.Components != null)
        {
            foreach (ComponentDto componentDto in dto.Components)
            {
                try
                {
                    ComponentTemplate componentTemplate = _componentRegistry.DeserializeComponent(
                        componentDto
                    );
                    template.AddComponent(componentTemplate);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "[steelblue1]WF[/] [red]✗[/] Failed to deserialize component [cyan]{ComponentType}[/] in template [cyan]{TemplateId}[/]",
                        componentDto.Type,
                        dto.TemplateId
                    );
                    throw;
                }
            }
        }

        return template;
    }
}

/// <summary>
///     DTO for deserializing EntityTemplate from JSON.
/// </summary>
internal record TemplateDto
{
    public string? TemplateId { get; init; }
    public string? Name { get; init; }
    public string? Tag { get; init; }
    public string? BaseTemplateId { get; init; }
    public string? Version { get; init; }
    public List<ComponentDto>? Components { get; init; }
    public Dictionary<string, object>? CustomProperties { get; init; }
}
