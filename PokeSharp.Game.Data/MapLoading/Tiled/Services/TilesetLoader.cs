using System.Text.Json;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Rendering.Assets;
using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

namespace PokeSharp.Game.Data.MapLoading.Tiled.Services;

/// <summary>
///     Handles loading and management of tilesets from Tiled maps.
///     Responsible for loading tileset textures, external tileset files, and parsing animations.
/// </summary>
public class TilesetLoader
{
    private readonly IAssetProvider _assetManager;
    private readonly ILogger<TilesetLoader>? _logger;

    public TilesetLoader(IAssetProvider assetManager, ILogger<TilesetLoader>? logger = null)
    {
        _assetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));
        _logger = logger;
    }

    /// <summary>
    ///     Loads all tilesets from a TmxDocument.
    /// </summary>
    public List<LoadedTileset> LoadTilesets(TmxDocument tmxDoc, string mapPath)
    {
        if (tmxDoc.Tilesets.Count == 0)
        {
            return new List<LoadedTileset>();
        }

        var loadedTilesets = new List<LoadedTileset>(tmxDoc.Tilesets.Count);
        foreach (TmxTileset tileset in tmxDoc.Tilesets)
        {
            string tilesetId = ExtractTilesetId(tileset, mapPath);
            tileset.Name = tilesetId;

            if (tileset.Image != null && !string.IsNullOrEmpty(tileset.Image.Source))
            {
                if (!_assetManager.HasTexture(tilesetId))
                {
                    LoadTilesetTexture(tileset, mapPath, tilesetId);
                }
            }

            loadedTilesets.Add(new LoadedTileset(tileset, tilesetId));
        }

        loadedTilesets.Sort((a, b) => a.Tileset.FirstGid.CompareTo(b.Tileset.FirstGid));
        return loadedTilesets;
    }

    /// <summary>
    ///     Loads external tileset files referenced in the map JSON.
    ///     Tiled JSON format can reference external tileset files via "source" field.
    /// </summary>
    public void LoadExternalTilesets(TmxDocument tmxDoc, string mapBasePath)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        foreach (TmxTileset tileset in tmxDoc.Tilesets)
        // Check if this is an external tileset reference (has "Source" but no tile data)
        {
            if (!string.IsNullOrEmpty(tileset.Source) && tileset.TileWidth == 0)
            {
                // Resolve tileset path relative to map
                string tilesetPath = Path.Combine(mapBasePath, tileset.Source);

                if (File.Exists(tilesetPath))
                {
                    try
                    {
                        string tilesetJson = File.ReadAllText(tilesetPath);
                        // Use dynamic object since tileset JSON format differs from map JSON
                        using var jsonDoc = JsonDocument.Parse(tilesetJson);
                        JsonElement root = jsonDoc.RootElement;

                        // Extract tileset properties from JSON (flat structure)
                        int originalFirstGid = tileset.FirstGid;
                        tileset.Name = root.TryGetProperty("name", out JsonElement name)
                            ? name.GetString() ?? ""
                            : "";
                        tileset.TileWidth = root.TryGetProperty("tilewidth", out JsonElement tw)
                            ? tw.GetInt32()
                            : 0;
                        tileset.TileHeight = root.TryGetProperty("tileheight", out JsonElement th)
                            ? th.GetInt32()
                            : 0;
                        tileset.TileCount = root.TryGetProperty("tilecount", out JsonElement tc)
                            ? tc.GetInt32()
                            : 0;
                        tileset.Margin = root.TryGetProperty("margin", out JsonElement mg)
                            ? mg.GetInt32()
                            : 0;
                        tileset.Spacing = root.TryGetProperty("spacing", out JsonElement sp)
                            ? sp.GetInt32()
                            : 0;

                        // Image data is at top level in tileset JSON
                        if (
                            root.TryGetProperty("image", out JsonElement img)
                            && root.TryGetProperty("imagewidth", out JsonElement iw)
                            && root.TryGetProperty("imageheight", out JsonElement ih)
                        )
                        {
                            string imageValue = img.GetString() ?? "";
                            string tilesetDir = Path.GetDirectoryName(tilesetPath) ?? string.Empty;
                            string imageAbsolute = Path.GetFullPath(
                                Path.Combine(tilesetDir, imageValue)
                            );

                            tileset.Image = new TmxImage
                            {
                                Source = imageAbsolute,
                                Width = iw.GetInt32(),
                                Height = ih.GetInt32(),
                            };
                        }

                        tileset.FirstGid = originalFirstGid; // Preserve from map reference

                        // Parse tile animations from "tiles" array
                        if (root.TryGetProperty("tiles", out JsonElement tilesArray))
                        {
                            ParseTilesetAnimations(tilesArray, tileset);
                        }

                        _logger?.LogDebug(
                            "Loaded external tileset: {Name} ({Width}x{Height}) with {AnimCount} animations from {Path}",
                            tileset.Name,
                            tileset.TileWidth,
                            tileset.TileHeight,
                            tileset.Animations.Count,
                            tileset.Source
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(
                            ex,
                            "Failed to load external tileset from {Path}",
                            tilesetPath
                        );
                        throw;
                    }
                }
                else
                {
                    throw new FileNotFoundException($"External tileset not found: {tilesetPath}");
                }
            }
        }
    }

    /// <summary>
    ///     Parses tile animations and properties from Tiled tileset JSON "tiles" array.
    ///     Tiled format: tiles: [{ "id": 0, "animation": [...], "properties": [...] }]
    /// </summary>
    private void ParseTilesetAnimations(JsonElement tilesArray, TmxTileset tileset)
    {
        foreach (JsonElement tileElement in tilesArray.EnumerateArray())
        {
            if (!tileElement.TryGetProperty("id", out JsonElement tileIdProp))
            {
                continue;
            }

            int tileId = tileIdProp.GetInt32();

            // Parse animation data
            if (tileElement.TryGetProperty("animation", out JsonElement animArray))
            {
                var frameTileIds = new List<int>();
                var frameDurations = new List<float>();

                foreach (JsonElement frameElement in animArray.EnumerateArray())
                {
                    if (
                        frameElement.TryGetProperty("tileid", out JsonElement frameTileId)
                        && frameElement.TryGetProperty("duration", out JsonElement frameDuration)
                    )
                    {
                        frameTileIds.Add(frameTileId.GetInt32());
                        // Convert milliseconds to seconds
                        frameDurations.Add(frameDuration.GetInt32() / 1000f);
                    }
                }

                if (frameTileIds.Count > 0)
                {
                    tileset.Animations[tileId] = new TmxTileAnimation
                    {
                        FrameTileIds = frameTileIds.ToArray(),
                        FrameDurations = frameDurations.ToArray(),
                    };
                }
            }

            // Parse tile properties (collision, ledge, etc.)
            if (tileElement.TryGetProperty("properties", out JsonElement propsArray))
            {
                var properties = new Dictionary<string, object>();

                foreach (JsonElement propElement in propsArray.EnumerateArray())
                {
                    if (
                        propElement.TryGetProperty("name", out JsonElement propName)
                        && propElement.TryGetProperty("value", out JsonElement propValue)
                    )
                    {
                        string? key = propName.GetString();
                        if (!string.IsNullOrEmpty(key))
                        {
                            // Get value based on type
                            object value = propValue.ValueKind switch
                            {
                                JsonValueKind.String => propValue.GetString() ?? "",
                                JsonValueKind.Number => propValue.TryGetInt32(out int i)
                                    ? i
                                    : propValue.GetDouble(),
                                JsonValueKind.True => true,
                                JsonValueKind.False => false,
                                _ => propValue.ToString(),
                            };
                            properties[key] = value;
                        }
                    }
                }

                if (properties.Count > 0)
                {
                    tileset.TileProperties[tileId] = properties;
                }
            }
        }
    }

    /// <summary>
    ///     Loads a tileset texture into the asset manager.
    /// </summary>
    private void LoadTilesetTexture(TmxTileset tileset, string mapPath, string tilesetId)
    {
        if (tileset.Image == null || string.IsNullOrEmpty(tileset.Image.Source))
        {
            throw new InvalidOperationException("Tileset has no image source");
        }

        string mapDirectory = Path.GetDirectoryName(mapPath) ?? string.Empty;

        string tilesetImageAbsolutePath = Path.IsPathRooted(tileset.Image.Source)
            ? tileset.Image.Source
            : Path.GetFullPath(Path.Combine(mapDirectory, tileset.Image.Source));

        // If using AssetManager, make path relative to Assets root
        // Otherwise (e.g., in tests with stub), use the path directly
        string pathForLoader;
        if (_assetManager is AssetManager assetManager)
        {
            pathForLoader = Path.GetRelativePath(assetManager.AssetRoot, tilesetImageAbsolutePath);
        }
        else
        {
            pathForLoader = tilesetImageAbsolutePath;
        }

        try
        {
            _assetManager.LoadTexture(tilesetId, pathForLoader);
            _logger?.LogInformation(
                "[TilesetDebug] Loaded tileset texture: TilesetId='{TilesetId}', Path='{PathForLoader}', "
                    + "ImageSource='{ImageSource}'",
                tilesetId,
                pathForLoader,
                tileset.Image?.Source ?? "null"
            );
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "Failed to load tileset texture: {TilesetId} from {PathForLoader}",
                tilesetId,
                pathForLoader
            );
            throw;
        }
    }

    /// <summary>
    ///     Extracts a unique tileset ID from a tileset.
    /// </summary>
    private static string ExtractTilesetId(TmxTileset tileset, string mapPath)
    {
        // If tileset has an image, use the image filename as ID
        if (tileset.Image != null && !string.IsNullOrEmpty(tileset.Image.Source))
        {
            string id = Path.GetFileNameWithoutExtension(tileset.Image.Source);
            if (!string.IsNullOrEmpty(id))
            {
                return id;
            }
        }

        // Fallback to tileset name (handle both null and empty string)
        if (!string.IsNullOrEmpty(tileset.Name))
        {
            return tileset.Name;
        }

        // Last resort: generate ID from map path
        string mapName = Path.GetFileNameWithoutExtension(mapPath);
        return !string.IsNullOrEmpty(mapName) ? mapName : "default-tileset";
    }
}

/// <summary>
///     Represents a loaded tileset with its ID.
///     Shared between TilesetLoader and MapLoader.
/// </summary>
public sealed class LoadedTileset
{
    public LoadedTileset(TmxTileset tileset, string tilesetId)
    {
        Tileset = tileset ?? throw new ArgumentNullException(nameof(tileset));
        TilesetId = tilesetId ?? throw new ArgumentNullException(nameof(tilesetId));
    }

    public TmxTileset Tileset { get; }
    public string TilesetId { get; }
}
