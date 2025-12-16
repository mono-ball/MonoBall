using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace MonoBallFramework.Game.GameData.Entities.Base;

/// <summary>
///     Base class for entities that support mod extensibility.
///     Provides ExtensionData storage and typed property access.
/// </summary>
public abstract class ExtensibleEntity : BaseEntity
{
    /// <summary>
    ///     Human-readable name.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Optional description for documentation and UI display.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    ///     JSON-serialized extension data from mods.
    ///     Contains arbitrary custom properties added by mods.
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? ExtensionData { get; set; }

    /// <summary>
    ///     Gets the extension data as a parsed dictionary.
    ///     Returns null if no extension data is present or parsing fails.
    /// </summary>
    [NotMapped]
    public Dictionary<string, JsonElement>? ParsedExtensionData
    {
        get
        {
            if (string.IsNullOrEmpty(ExtensionData))
                return null;
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(ExtensionData);
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    ///     Gets a custom property value from extension data.
    /// </summary>
    /// <typeparam name="T">The expected type of the property.</typeparam>
    /// <param name="propertyName">The name of the property to retrieve.</param>
    /// <returns>The property value, or default if not found or wrong type.</returns>
    public T? GetExtensionProperty<T>(string propertyName)
    {
        var data = ParsedExtensionData;
        if (data == null || !data.TryGetValue(propertyName, out var element))
            return default;
        try
        {
            return JsonSerializer.Deserialize<T>(element.GetRawText());
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    ///     Sets a custom property value in extension data.
    /// </summary>
    /// <typeparam name="T">The type of the property.</typeparam>
    /// <param name="propertyName">The name of the property to set.</param>
    /// <param name="value">The value to set.</param>
    public void SetExtensionProperty<T>(string propertyName, T value)
    {
        var data = ParsedExtensionData ?? new Dictionary<string, JsonElement>();
        var json = JsonSerializer.Serialize(value);
        data[propertyName] = JsonSerializer.Deserialize<JsonElement>(json);
        ExtensionData = JsonSerializer.Serialize(data);
    }
}
