using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Core.Templates.Loading;

/// <summary>
///     Registry for component deserializers.
///     Maps component type names to deserialization functions.
/// </summary>
public class ComponentDeserializerRegistry
{
    private readonly Dictionary<string, ComponentDeserializerInfo> _deserializers = new();
    private readonly ILogger<ComponentDeserializerRegistry> _logger;

    public ComponentDeserializerRegistry(ILogger<ComponentDeserializerRegistry> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Register a component deserializer.
    /// </summary>
    /// <typeparam name="TComponent">Component type (must be a struct)</typeparam>
    /// <param name="typeName">Type name used in JSON (e.g., "TilePosition")</param>
    /// <param name="deserializer">Function to deserialize JsonElement to component instance</param>
    public void Register<TComponent>(string typeName, Func<JsonElement, TComponent> deserializer)
        where TComponent : struct
    {
        ArgumentNullException.ThrowIfNull(typeName);
        ArgumentNullException.ThrowIfNull(deserializer);

        _deserializers[typeName] = new ComponentDeserializerInfo
        {
            ComponentType = typeof(TComponent),
            Deserializer = jsonElement => deserializer(jsonElement),
        };

        _logger.LogDebug(
            "[steelblue1]WF[/] [green]✓[/] Registered deserializer for component type: [cyan]{TypeName}[/]",
            typeName
        );
    }

    /// <summary>
    ///     Register a component deserializer using the component type's name.
    /// </summary>
    /// <typeparam name="TComponent">Component type</typeparam>
    /// <param name="deserializer">Deserialization function</param>
    public void Register<TComponent>(Func<JsonElement, TComponent> deserializer)
        where TComponent : struct
    {
        Register(typeof(TComponent).Name, deserializer);
    }

    /// <summary>
    ///     Deserialize a component from a ComponentDto.
    /// </summary>
    /// <param name="dto">Component DTO</param>
    /// <returns>ComponentTemplate</returns>
    /// <exception cref="InvalidOperationException">If deserializer not found</exception>
    public ComponentTemplate DeserializeComponent(ComponentDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.Type))
        {
            throw new ArgumentException("Component type is required", nameof(dto));
        }

        if (!_deserializers.TryGetValue(dto.Type, out ComponentDeserializerInfo? info))
        {
            throw new InvalidOperationException(
                $"No deserializer registered for component type: {dto.Type}. "
                    + $"Available types: {string.Join(", ", _deserializers.Keys)}"
            );
        }

        try
        {
            JsonElement data =
                dto.Data
                ?? throw new ArgumentException(
                    $"Component data is required for type: {dto.Type}",
                    nameof(dto)
                );

            object componentData = info.Deserializer(data);

            // Create ComponentTemplate directly without using generic Create method
            return new ComponentTemplate
            {
                ComponentType = info.ComponentType,
                InitialData = componentData,
                ScriptId = dto.ScriptId,
                Tags = dto.Tags,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[steelblue1]WF[/] [red]✗[/] Failed to deserialize component [cyan]{ComponentType}[/]",
                dto.Type
            );
            throw;
        }
    }

    /// <summary>
    ///     Check if a deserializer is registered for a component type.
    /// </summary>
    /// <param name="typeName">Component type name</param>
    /// <returns>True if deserializer is registered</returns>
    public bool HasDeserializer(string typeName)
    {
        return !string.IsNullOrWhiteSpace(typeName) && _deserializers.ContainsKey(typeName);
    }

    /// <summary>
    ///     Get all registered component type names.
    /// </summary>
    /// <returns>Collection of type names</returns>
    public IEnumerable<string> GetRegisteredTypes()
    {
        return _deserializers.Keys;
    }
}

/// <summary>
///     Internal info about a component deserializer.
/// </summary>
internal record ComponentDeserializerInfo
{
    public required Type ComponentType { get; init; }
    public required Func<JsonElement, object> Deserializer { get; init; }
}

/// <summary>
///     DTO for deserializing ComponentTemplate from JSON.
/// </summary>
public record ComponentDto
{
    public string? Type { get; init; }
    public JsonElement? Data { get; init; }
    public string? ScriptId { get; init; }
    public List<string>? Tags { get; init; }
}
