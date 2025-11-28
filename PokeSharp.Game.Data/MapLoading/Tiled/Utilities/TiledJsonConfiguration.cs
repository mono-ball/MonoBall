using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeSharp.Game.Data.MapLoading.Tiled.Utilities;

/// <summary>
///     Centralized JSON serialization configuration for Tiled map loading.
///     Eliminates duplicated JsonSerializerOptions across loaders.
/// </summary>
public static class TiledJsonConfiguration
{
    /// <summary>
    ///     Standard options for reading Tiled JSON files.
    ///     Case-insensitive, allows comments and trailing commas.
    /// </summary>
    public static JsonSerializerOptions ReadOptions { get; } =
        new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
        };

    /// <summary>
    ///     Options for writing JSON with formatting.
    ///     Extends ReadOptions with WriteIndented = true.
    /// </summary>
    public static JsonSerializerOptions WriteOptions { get; } =
        new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            WriteIndented = true,
        };

    /// <summary>
    ///     Deserializes JSON from a file path.
    /// </summary>
    public static async Task<T?> DeserializeFromFileAsync<T>(
        string filePath,
        CancellationToken ct = default
    )
    {
        await using FileStream stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<T>(stream, ReadOptions, ct);
    }

    /// <summary>
    ///     Deserializes JSON from a string.
    /// </summary>
    public static T? DeserializeFromString<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, ReadOptions);
    }
}
