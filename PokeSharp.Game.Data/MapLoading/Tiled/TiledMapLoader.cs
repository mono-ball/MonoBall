using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Configuration;
using PokeSharp.Game.Data.MapLoading.Tiled.TiledJson;
using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;
using PokeSharp.Game.Data.Validation;
using PokeSharp.Game.Data.Configuration;
using ZstdSharp;

namespace PokeSharp.Game.Data.MapLoading.Tiled;

/// <summary>
///     Loads Tiled maps in JSON format (Tiled 1.11.2).
///     Parses .json map files exported from Tiled editor.
/// </summary>
public static class TiledMapLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static IMapValidator? _validator;
    private static MapLoaderOptions? _options;
    private static ILogger? _logger;

    /// <summary>
    ///     Configures the map loader with validation options.
    /// </summary>
    public static void Configure(MapLoaderOptions options, ILogger? logger = null)
    {
        _options = options;
        _logger = logger;

        if (options.ValidateMaps && logger != null)
        {
            _validator = new TmxDocumentValidator(
                logger as ILogger<TmxDocumentValidator> ??
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<TmxDocumentValidator>.Instance,
                options.ValidateFileReferences);
        }
    }

    /// <summary>
    ///     Loads a Tiled map from a JSON file.
    /// </summary>
    /// <param name="mapPath">Path to the .json map file.</param>
    /// <returns>Parsed Tiled map document.</returns>
    /// <exception cref="FileNotFoundException">Map file not found.</exception>
    /// <exception cref="JsonException">Invalid JSON format.</exception>
    /// <exception cref="MapValidationException">Map validation failed (if validation is enabled).</exception>
    public static TmxDocument Load(string mapPath)
    {
        if (!File.Exists(mapPath))
            throw new FileNotFoundException($"Tiled map file not found: {mapPath}");

        var json = File.ReadAllText(mapPath);
        var tiledMap =
            JsonSerializer.Deserialize<TiledJsonMap>(json, JsonOptions)
            ?? throw new JsonException($"Failed to deserialize Tiled map: {mapPath}");

        var tmxDoc = ConvertToTmxDocument(tiledMap, mapPath);

        // Validate map if validator is configured
        if (_validator != null)
        {
            var validationResult = _validator.Validate(tmxDoc, mapPath);

            // Log warnings
            if (validationResult.Warnings.Count > 0 && _options?.LogValidationWarnings == true)
            {
                _logger?.LogWarning(validationResult.GetWarningMessage());
            }

            // Handle validation errors
            if (!validationResult.IsValid)
            {
                if (_options?.ThrowOnValidationError == true)
                {
                    throw new MapValidationException(validationResult);
                }
                else
                {
                    _logger?.LogError(validationResult.GetErrorMessage());
                }
            }
        }

        return tmxDoc;
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
            ObjectGroups = ConvertObjectGroups(tiledMap.Layers),
            ImageLayers = ConvertImageLayers(tiledMap.Layers),
        };

        return doc;
    }

    private static List<TmxTileset> ConvertTilesets(
        List<TiledJsonTileset>? tilesets,
        string mapPath
    )
    {
        if (tilesets == null)
            return new List<TmxTileset>();

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
                TileCount = tiledTileset.TileCount ?? 0,
                Spacing = tiledTileset.Spacing ?? 0,
                Margin = tiledTileset.Margin ?? 0,
            };

            // Handle external tileset
            if (!string.IsNullOrEmpty(tiledTileset.Source))
            {
                tileset.Source = tiledTileset.Source;
                var tilesetPath = Path.Combine(mapDirectory, tiledTileset.Source);
                if (File.Exists(tilesetPath))
                    LoadExternalTileset(tileset, tilesetPath);
            }
            // Handle embedded tileset
            else if (!string.IsNullOrEmpty(tiledTileset.Image))
            {
                tileset.Image = new TmxImage
                {
                    Source = tiledTileset.Image,
                    Width = tiledTileset.ImageWidth ?? 0,
                    Height = tiledTileset.ImageHeight ?? 0,
                };
            }

            // Parse tile animations
            if (tiledTileset.Tiles != null)
                ParseTileAnimations(tileset, tiledTileset.Tiles);

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
                tileset.Spacing = tiledTileset.Spacing ?? tileset.Spacing;
                tileset.Margin = tiledTileset.Margin ?? tileset.Margin;

                if (!string.IsNullOrEmpty(tiledTileset.Image))
                    tileset.Image = new TmxImage
                    {
                        Source = tiledTileset.Image,
                        Width = tiledTileset.ImageWidth ?? 0,
                        Height = tiledTileset.ImageHeight ?? 0,
                    };

                // Parse tile animations and properties from external tileset
                if (tiledTileset.Tiles != null)
                    ParseTileAnimations(tileset, tiledTileset.Tiles);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to load external tileset: {tilesetPath}",
                ex
            );
        }
    }

    private static void ParseTileAnimations(TmxTileset tileset, List<TiledJsonTileDefinition> tiles)
    {
        foreach (var tile in tiles)
        {
            // Parse animation frames
            if (tile.Animation != null && tile.Animation.Count > 0)
            {
                var frameTileIds = new int[tile.Animation.Count];
                var frameDurations = new float[tile.Animation.Count];

                for (var i = 0; i < tile.Animation.Count; i++)
                {
                    var frame = tile.Animation[i];
                    frameTileIds[i] = frame.TileId;
                    frameDurations[i] = frame.Duration / 1000f; // Convert milliseconds to seconds
                }

                tileset.Animations[tile.Id] = new TmxTileAnimation
                {
                    FrameTileIds = frameTileIds,
                    FrameDurations = frameDurations,
                };
            }

            // Parse custom properties (data-driven!)
            if (tile.Properties != null && tile.Properties.Count > 0)
            {
                var props = ConvertProperties(tile.Properties);

                // Add tile type if specified (optional, from Tiled object types)
                if (!string.IsNullOrEmpty(tile.Type))
                    props["tile_type"] = tile.Type;

                tileset.TileProperties[tile.Id] = props;
            }
        }
    }

    private static List<TmxLayer> ConvertLayers(
        List<TiledJsonLayer>? layers,
        int mapWidth,
        int mapHeight
    )
    {
        if (layers == null)
            return new List<TmxLayer>();

        var result = new List<TmxLayer>();

        foreach (var tiledLayer in layers)
        {
            // Only process tile layers (not object groups)
            if (tiledLayer.Type != "tilelayer")
                continue;

            var layer = new TmxLayer
            {
                Id = tiledLayer.Id,
                Name = tiledLayer.Name,
                Width = tiledLayer.Width > 0 ? tiledLayer.Width : mapWidth,
                Height = tiledLayer.Height > 0 ? tiledLayer.Height : mapHeight,
                Visible = tiledLayer.Visible,
                Opacity = tiledLayer.Opacity,
                OffsetX = tiledLayer.OffsetX,
                OffsetY = tiledLayer.OffsetY,
            };

            // Decode layer data (handles plain arrays, base64, and compression)
            var flatData = DecodeLayerData(tiledLayer);
            if (flatData.Length > 0)
                layer.Data = flatData; // Store as flat array (row-major order)

            result.Add(layer);
        }

        return result;
    }

    /// <summary>
    ///     Decodes layer data from various formats (plain array, base64, compressed).
    /// </summary>
    private static int[] DecodeLayerData(TiledJsonLayer layer)
    {
        if (layer.Data == null)
            return Array.Empty<int>();

        var dataElement = layer.Data.Value;

        // Check if data is an array (uncompressed)
        if (dataElement.ValueKind == JsonValueKind.Array)
        {
            var dataList = new List<int>();
            foreach (var element in dataElement.EnumerateArray())
                dataList.Add(element.GetInt32());
            return dataList.ToArray();
        }

        // Data is a string (compressed or base64)
        if (dataElement.ValueKind == JsonValueKind.String)
        {
            var base64Data = dataElement.GetString();
            if (string.IsNullOrEmpty(base64Data))
                return Array.Empty<int>();

            var bytes = Convert.FromBase64String(base64Data);

            // Handle compression
            if (!string.IsNullOrEmpty(layer.Compression))
                bytes = DecompressBytes(bytes, layer.Compression);

            return ConvertBytesToInts(bytes);
        }

        // Unknown data type - return empty array (production code should not use Console.WriteLine)
        // TODO: Add proper logging via ILogger when available
        return Array.Empty<int>();
    }

    /// <summary>
    ///     Decompresses byte array using the specified compression algorithm.
    /// </summary>
    private static byte[] DecompressBytes(byte[] compressed, string compression)
    {
        return compression.ToLower() switch
        {
            "gzip" => DecompressGzip(compressed),
            "zlib" => DecompressZlib(compressed),
            "zstd" => DecompressZstd(compressed),
            _ => throw new NotSupportedException(
                $"Compression '{compression}' not supported. Supported formats: gzip, zlib, zstd"
            ),
        };
    }

    private static byte[] DecompressGzip(byte[] compressed)
    {
        using var compressedStream = new MemoryStream(compressed);
        using var decompressor = new GZipStream(compressedStream, CompressionMode.Decompress);
        using var decompressed = new MemoryStream();
        decompressor.CopyTo(decompressed);
        return decompressed.ToArray();
    }

    private static byte[] DecompressZlib(byte[] compressed)
    {
        using var compressedStream = new MemoryStream(compressed);
        using var decompressor = new ZLibStream(compressedStream, CompressionMode.Decompress);
        using var decompressed = new MemoryStream();
        decompressor.CopyTo(decompressed);
        return decompressed.ToArray();
    }

    private static byte[] DecompressZstd(byte[] compressed)
    {
        using var decompressor = new Decompressor();
        return decompressor.Unwrap(compressed).ToArray();
    }

    /// <summary>
    ///     Converts byte array to int array (little-endian format).
    /// </summary>
    private static int[] ConvertBytesToInts(byte[] bytes)
    {
        if (bytes.Length % 4 != 0)
            throw new InvalidDataException(
                $"Byte array length must be multiple of 4, got {bytes.Length}"
            );

        var ints = new int[bytes.Length / 4];
        for (var i = 0; i < ints.Length; i++)
            ints[i] = BitConverter.ToInt32(bytes, i * 4);

        return ints;
    }

    private static int[,] ConvertFlatArrayTo2D(int[] flatData, int width, int height)
    {
        var data = new int[height, width];

        for (var i = 0; i < flatData.Length && i < width * height; i++)
        {
            var y = i / width;
            var x = i % width;
            data[y, x] = flatData[i];
        }

        return data;
    }

    private static List<TmxObjectGroup> ConvertObjectGroups(List<TiledJsonLayer>? layers)
    {
        if (layers == null)
            return new List<TmxObjectGroup>();

        var result = new List<TmxObjectGroup>();

        foreach (var tiledLayer in layers)
        {
            // Only process object groups
            if (tiledLayer.Type != "objectgroup" || tiledLayer.Objects == null)
                continue;

            var group = new TmxObjectGroup
            {
                Id = tiledLayer.Id,
                Name = tiledLayer.Name,
                Objects = ConvertObjects(tiledLayer.Objects),
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
                Properties = ConvertProperties(tiledObj.Properties),
            };

            result.Add(obj);
        }

        return result;
    }

    private static Dictionary<string, object> ConvertProperties(List<TiledJsonProperty>? properties)
    {
        var result = new Dictionary<string, object>();

        if (properties == null)
            return result;

        foreach (var prop in properties)
            if (prop.Value != null)
                result[prop.Name] = prop.Value;

        return result;
    }

    /// <summary>
    ///     Converts image layers from Tiled JSON format to TMX format.
    /// </summary>
    private static List<TmxImageLayer> ConvertImageLayers(List<TiledJsonLayer>? layers)
    {
        if (layers == null)
            return new List<TmxImageLayer>();

        var result = new List<TmxImageLayer>();

        foreach (var tiledLayer in layers)
        {
            // Only process image layers
            if (tiledLayer.Type != "imagelayer")
                continue;

            var imageLayer = new TmxImageLayer
            {
                Id = tiledLayer.Id,
                Name = tiledLayer.Name,
                Visible = tiledLayer.Visible,
                Opacity = tiledLayer.Opacity,
                X = tiledLayer.X,
                Y = tiledLayer.Y,
                OffsetX = tiledLayer.OffsetX,
                OffsetY = tiledLayer.OffsetY,
            };

            // Parse image source if present
            if (!string.IsNullOrEmpty(tiledLayer.Image))
            {
                imageLayer.Image = new TmxImage
                {
                    Source = tiledLayer.Image,
                    Width = 0, // Image dimensions will be determined when texture is loaded
                    Height = 0,
                };
            }

            result.Add(imageLayer);
        }

        return result;
    }
}
