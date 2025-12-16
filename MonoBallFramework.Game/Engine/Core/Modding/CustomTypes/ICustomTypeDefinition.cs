using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.Engine.Core.Modding.CustomTypes;

/// <summary>
/// Base interface for all custom type definitions loaded from mods.
/// Custom types are JSON-based definitions that mods can declare and use.
/// </summary>
public interface ICustomTypeDefinition : ITypeDefinition
{
    /// <summary>
    /// The content type category this definition belongs to (e.g., "WeatherEffects").
    /// </summary>
    string Category { get; }

    /// <summary>
    /// The mod that provided this definition.
    /// </summary>
    string? SourceMod { get; }

    /// <summary>
    /// Version of this definition.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Raw JSON data for accessing custom properties.
    /// </summary>
    System.Text.Json.JsonElement RawData { get; }
}
