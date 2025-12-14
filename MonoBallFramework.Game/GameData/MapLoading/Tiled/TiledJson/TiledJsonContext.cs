using System.Text.Json;
using System.Text.Json.Serialization;

namespace MonoBallFramework.Game.GameData.MapLoading.Tiled.TiledJson;

/// <summary>
///     Source-generated JSON serialization context for Tiled JSON types.
///     Provides 2-3x faster deserialization by eliminating reflection.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(TiledJsonMap))]
[JsonSerializable(typeof(TiledJsonTileset))]
[JsonSerializable(typeof(TiledJsonLayer))]
[JsonSerializable(typeof(TiledJsonObject))]
[JsonSerializable(typeof(TiledJsonProperty))]
[JsonSerializable(typeof(TiledJsonTileDefinition))]
[JsonSerializable(typeof(TiledJsonAnimationFrame))]
[JsonSerializable(typeof(List<TiledJsonLayer>))]
[JsonSerializable(typeof(List<TiledJsonTileset>))]
[JsonSerializable(typeof(List<TiledJsonObject>))]
[JsonSerializable(typeof(List<TiledJsonProperty>))]
[JsonSerializable(typeof(List<TiledJsonTileDefinition>))]
[JsonSerializable(typeof(List<TiledJsonAnimationFrame>))]
[JsonSerializable(typeof(JsonElement))]
public partial class TiledJsonContext : JsonSerializerContext
{
}
