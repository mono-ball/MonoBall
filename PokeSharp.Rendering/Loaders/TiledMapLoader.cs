using System.Text.Json;

namespace PokeSharp.Rendering.Loaders;

/// <summary>
/// Loads Tiled maps in JSON format (Tiled 1.11.2).
/// Parses .json map files exported from Tiled editor.
/// </summary>
public static class TiledMapLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Loads a Tiled map from a JSON file.
    /// </summary>
    /// <param name="mapPath">Path to the .json map file.</param>
    /// <returns>Parsed Tiled map document.</returns>
    /// <exception cref="FileNotFoundException">Map file not found.</exception>
    /// <exception cref="JsonException">Invalid JSON format.</exception>
    public static TmxDocument Load(string mapPath)
    {
        if (!File.Exists(mapPath))
        {
            throw new FileNotFoundException($"Tiled map file not found: {mapPath}");
        }

        var json = File.ReadAllText(mapPath);
        var tiledMap = JsonSerializer.Deserialize<TiledJsonMap>(json, JsonOptions)
            ?? throw new JsonException($"Failed to deserialize Tiled map: {mapPath}");

        return ConvertToTmxDocument(tiledMap, mapPath);
    }

    private static TmxDocument ConvertToTmxDocument(TiledJsonMap tiledMap, string mapPath)
    {
        var doc = new TmxDocument
        {
            Version = tiledMap.Version,
            TiledVersion = tiledMap.TiledVersion,
            Width = tiledMap.Width,
            Height = tiledMap.Height,
            TileWidth = tiledMap.TileWidth,
            TileHeight = tiledMap.TileHeight,
            Tilesets = ConvertTilesets(tiledMap.Tilesets, mapPath),
            Layers = ConvertLayers(tiledMap.Layers, tiledMap.Width, tiledMap.Height),
            ObjectGroups = ConvertObjectGroups(tiledMap.Layers)
        };

        return doc;
    }

    private static List<TmxTileset> ConvertTilesets(List<TiledJsonTileset>? tilesets, string mapPath)
    {
        if (tilesets == null)
        {
            return new List<TmxTileset>();
        }

        var result = new List<TmxTileset>();
        var mapDirectory = Path.GetDirectoryName(mapPath) ?? string.Empty;

        foreach (var tiledTileset in tilesets)
        {
            var tileset = new TmxTileset
            {
                FirstGid = tiledTileset.FirstGid,
                Name = tiledTileset.Name ?? string.Empty,
                TileWidth = tiledTileset.TileWidth ?? 16,
                TileHeight = tiledTileset.TileHeight ?? 16,
                TileCount = tiledTileset.TileCount ?? 0
            };

            // Handle external tileset
            if (!string.IsNullOrEmpty(tiledTileset.Source))
            {
                tileset.Source = tiledTileset.Source;
                var tilesetPath = Path.Combine(mapDirectory, tiledTileset.Source);
                if (File.Exists(tilesetPath))
                {
                    LoadExternalTileset(tileset, tilesetPath);
                }
            }
            // Handle embedded tileset
            else if (!string.IsNullOrEmpty(tiledTileset.Image))
            {
                tileset.Image = new TmxImage
                {
                    Source = tiledTileset.Image,
                    Width = tiledTileset.ImageWidth ?? 0,
                    Height = tiledTileset.ImageHeight ?? 0
                };
            }

            // Parse tile animations
            if (tiledTileset.Tiles != null)
            {
                ParseTileAnimations(tileset, tiledTileset.Tiles);
            }

            result.Add(tileset);
        }

        return result;
    }

    private static void LoadExternalTileset(TmxTileset tileset, string tilesetPath)
    {
        try
        {
            var json = File.ReadAllText(tilesetPath);
            var tiledTileset = JsonSerializer.Deserialize<TiledJsonTileset>(json, JsonOptions);

            if (tiledTileset != null)
            {
                tileset.Name = tiledTileset.Name ?? tileset.Name;
                tileset.TileWidth = tiledTileset.TileWidth ?? tileset.TileWidth;
                tileset.TileHeight = tiledTileset.TileHeight ?? tileset.TileHeight;
                tileset.TileCount = tiledTileset.TileCount ?? tileset.TileCount;

                if (!string.IsNullOrEmpty(tiledTileset.Image))
                {
                    tileset.Image = new TmxImage
                    {
                        Source = tiledTileset.Image,
                        Width = tiledTileset.ImageWidth ?? 0,
                        Height = tiledTileset.ImageHeight ?? 0
                    };
                }

                // Parse tile animations from external tileset
                if (tiledTileset.Tiles != null)
                {
                    ParseTileAnimations(tileset, tiledTileset.Tiles);
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load external tileset: {tilesetPath}", ex);
        }
    }

    private static void ParseTileAnimations(TmxTileset tileset, List<TiledJsonTileDefinition> tiles)
    {
        foreach (var tile in tiles)
        {
            if (tile.Animation == null || tile.Animation.Count == 0)
            {
                continue;
            }

            var frameTileIds = new int[tile.Animation.Count];
            var frameDurations = new float[tile.Animation.Count];

            for (int i = 0; i < tile.Animation.Count; i++)
            {
                var frame = tile.Animation[i];
                frameTileIds[i] = frame.TileId;
                frameDurations[i] = frame.Duration / 1000f; // Convert milliseconds to seconds
            }

            tileset.Animations[tile.Id] = new TmxTileAnimation
            {
                FrameTileIds = frameTileIds,
                FrameDurations = frameDurations
            };
        }
    }

    private static List<TmxLayer> ConvertLayers(List<TiledJsonLayer>? layers, int mapWidth, int mapHeight)
    {
        if (layers == null)
        {
            return new List<TmxLayer>();
        }

        var result = new List<TmxLayer>();

        foreach (var tiledLayer in layers)
        {
            // Only process tile layers (not object groups)
            if (tiledLayer.Type != "tilelayer")
            {
                continue;
            }

            var layer = new TmxLayer
            {
                Id = tiledLayer.Id,
                Name = tiledLayer.Name,
                Width = tiledLayer.Width > 0 ? tiledLayer.Width : mapWidth,
                Height = tiledLayer.Height > 0 ? tiledLayer.Height : mapHeight,
                Visible = tiledLayer.Visible,
                Opacity = tiledLayer.Opacity
            };

            // Convert flat array to 2D array
            if (tiledLayer.Data != null)
            {
                layer.Data = ConvertFlatArrayTo2D(tiledLayer.Data, layer.Width, layer.Height);
            }

            result.Add(layer);
        }

        return result;
    }

    private static int[,] ConvertFlatArrayTo2D(int[] flatData, int width, int height)
    {
        var data = new int[height, width];

        for (int i = 0; i < flatData.Length && i < width * height; i++)
        {
            int y = i / width;
            int x = i % width;
            data[y, x] = flatData[i];
        }

        return data;
    }

    private static List<TmxObjectGroup> ConvertObjectGroups(List<TiledJsonLayer>? layers)
    {
        if (layers == null)
        {
            return new List<TmxObjectGroup>();
        }

        var result = new List<TmxObjectGroup>();

        foreach (var tiledLayer in layers)
        {
            // Only process object groups
            if (tiledLayer.Type != "objectgroup" || tiledLayer.Objects == null)
            {
                continue;
            }

            var group = new TmxObjectGroup
            {
                Id = tiledLayer.Id,
                Name = tiledLayer.Name,
                Objects = ConvertObjects(tiledLayer.Objects)
            };

            result.Add(group);
        }

        return result;
    }

    private static List<TmxObject> ConvertObjects(List<TiledJsonObject> tiledObjects)
    {
        var result = new List<TmxObject>();

        foreach (var tiledObj in tiledObjects)
        {
            var obj = new TmxObject
            {
                Id = tiledObj.Id,
                Name = tiledObj.Name,
                Type = tiledObj.Type,
                X = tiledObj.X,
                Y = tiledObj.Y,
                Width = tiledObj.Width,
                Height = tiledObj.Height,
                Properties = ConvertProperties(tiledObj.Properties)
            };

            result.Add(obj);
        }

        return result;
    }

    private static Dictionary<string, object> ConvertProperties(List<TiledJsonProperty>? properties)
    {
        var result = new Dictionary<string, object>();

        if (properties == null)
        {
            return result;
        }

        foreach (var prop in properties)
        {
            if (prop.Value != null)
            {
                result[prop.Name] = prop.Value;
            }
        }

        return result;
    }
}
