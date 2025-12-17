using System.Text.Json;
using System.Text.Json.Serialization;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.TiledJson;

namespace MonoBallFramework.Game.GameData.MapLoading.Tiled.Utilities;

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
            NumberHandling = JsonNumberHandling.AllowReadingFromString
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
            WriteIndented = true
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

    // ==================== Source-Generated Context Methods (Optimized) ====================
    // These methods use the TiledJsonContext for faster deserialization and reduced memory allocations.

    /// <summary>
    ///     Deserializes a TiledJsonMap from a file using source-generated context (fastest).
    /// </summary>
    public static async Task<TiledJsonMap?> DeserializeMapFromFileAsync(
        string filePath,
        CancellationToken ct = default
    )
    {
        await using FileStream stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync(
            stream,
            TiledJsonContext.Default.TiledJsonMap,
            ct
        );
    }

    /// <summary>
    ///     Deserializes a TiledJsonMap from a string using source-generated context.
    /// </summary>
    public static TiledJsonMap? DeserializeMapFromString(string json)
    {
        return JsonSerializer.Deserialize(json, TiledJsonContext.Default.TiledJsonMap);
    }

    /// <summary>
    ///     Deserializes a TiledJsonTileset from a file using source-generated context.
    /// </summary>
    public static async Task<TiledJsonTileset?> DeserializeTilesetFromFileAsync(
        string filePath,
        CancellationToken ct = default
    )
    {
        await using FileStream stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync(
            stream,
            TiledJsonContext.Default.TiledJsonTileset,
            ct
        );
    }

    /// <summary>
    ///     Deserializes a TiledJsonTileset from a string using source-generated context.
    /// </summary>
    public static TiledJsonTileset? DeserializeTilesetFromString(string json)
    {
        return JsonSerializer.Deserialize(json, TiledJsonContext.Default.TiledJsonTileset);
    }
}
