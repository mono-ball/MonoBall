using System.Globalization;
using System.Xml.Linq;
using PokeSharp.Rendering.Loaders.Tmx;

namespace PokeSharp.Rendering.Loaders;

/// <summary>
///     Parses Tiled map files (TMX format) using System.Xml.Linq.
///     Supports Tiled 1.11.2 format without external dependencies.
/// </summary>
public static class TmxParser
{
    /// <summary>
    ///     Loads and parses a TMX file.
    /// </summary>
    /// <param name="tmxPath">Path to the TMX file.</param>
    /// <returns>Parsed TMX document.</returns>
    /// <exception cref="FileNotFoundException">TMX file not found.</exception>
    /// <exception cref="InvalidOperationException">Invalid TMX format.</exception>
    public static TmxDocument Load(string tmxPath)
    {
        if (!File.Exists(tmxPath))
            throw new FileNotFoundException($"TMX file not found: {tmxPath}");

        var doc = XDocument.Load(tmxPath);
        var mapElement =
            doc.Root
            ?? throw new InvalidOperationException("Invalid TMX file: missing root element");

        var tmxDoc = new TmxDocument
        {
            Version = mapElement.Attribute("version")?.Value ?? "1.0",
            TiledVersion = mapElement.Attribute("tiledversion")?.Value,
            Width = ParseInt(mapElement, "width"),
            Height = ParseInt(mapElement, "height"),
            TileWidth = ParseInt(mapElement, "tilewidth"),
            TileHeight = ParseInt(mapElement, "tileheight"),
            Tilesets = ParseTilesets(mapElement, tmxPath),
            Layers = ParseLayers(mapElement),
            ObjectGroups = ParseObjectGroups(mapElement),
        };

        return tmxDoc;
    }

    private static List<TmxTileset> ParseTilesets(XElement mapElement, string tmxPath)
    {
        var tilesets = new List<TmxTileset>();
        var mapDirectory = Path.GetDirectoryName(tmxPath) ?? string.Empty;

        foreach (var tilesetElement in mapElement.Elements("tileset"))
        {
            var tileset = new TmxTileset
            {
                FirstGid = ParseInt(tilesetElement, "firstgid"),
                Name = tilesetElement.Attribute("name")?.Value ?? string.Empty,
                Source = tilesetElement.Attribute("source")?.Value,
                TileWidth = ParseInt(tilesetElement, "tilewidth", 16),
                TileHeight = ParseInt(tilesetElement, "tileheight", 16),
                TileCount = ParseInt(tilesetElement, "tilecount"),
            };

            // Parse embedded image
            var imageElement = tilesetElement.Element("image");
            if (imageElement != null)
                tileset.Image = new TmxImage
                {
                    Source = imageElement.Attribute("source")?.Value ?? string.Empty,
                    Width = ParseInt(imageElement, "width"),
                    Height = ParseInt(imageElement, "height"),
                };

            // Handle external tileset reference (.tsx)
            if (!string.IsNullOrEmpty(tileset.Source))
            {
                var tsxPath = Path.Combine(mapDirectory, tileset.Source);
                if (File.Exists(tsxPath))
                    LoadExternalTileset(tileset, tsxPath);
            }

            tilesets.Add(tileset);
        }

        return tilesets;
    }

    private static void LoadExternalTileset(TmxTileset tileset, string tsxPath)
    {
        var doc = XDocument.Load(tsxPath);
        var tilesetElement = doc.Root!;

        tileset.Name = tilesetElement.Attribute("name")?.Value ?? tileset.Name;
        tileset.TileWidth = ParseInt(tilesetElement, "tilewidth", tileset.TileWidth);
        tileset.TileHeight = ParseInt(tilesetElement, "tileheight", tileset.TileHeight);
        tileset.TileCount = ParseInt(tilesetElement, "tilecount", tileset.TileCount);

        var imageElement = tilesetElement.Element("image");
        if (imageElement != null)
            tileset.Image = new TmxImage
            {
                Source = imageElement.Attribute("source")?.Value ?? string.Empty,
                Width = ParseInt(imageElement, "width"),
                Height = ParseInt(imageElement, "height"),
            };
    }

    private static List<TmxLayer> ParseLayers(XElement mapElement)
    {
        var layers = new List<TmxLayer>();

        foreach (var layerElement in mapElement.Elements("layer"))
        {
            var layer = new TmxLayer
            {
                Id = ParseInt(layerElement, "id"),
                Name = layerElement.Attribute("name")?.Value ?? string.Empty,
                Width = ParseInt(layerElement, "width"),
                Height = ParseInt(layerElement, "height"),
                Visible = ParseInt(layerElement, "visible", 1) == 1,
                Opacity = ParseFloat(layerElement, "opacity", 1.0f),
            };

            // Parse tile data
            var dataElement = layerElement.Element("data");
            if (dataElement != null)
            {
                var encoding = dataElement.Attribute("encoding")?.Value;

                if (encoding == "csv")
                    layer.Data = ParseCsvData(dataElement.Value, layer.Width, layer.Height);
                else if (encoding == null)
                    // XML encoding (tile elements)
                    layer.Data = ParseXmlData(dataElement, layer.Width, layer.Height);
                else
                    throw new NotSupportedException($"Unsupported tile data encoding: {encoding}");
            }

            layers.Add(layer);
        }

        return layers;
    }

    private static int[,] ParseCsvData(string csv, int width, int height)
    {
        var data = new int[height, width];
        var values = csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.Parse(s.Trim(), CultureInfo.InvariantCulture))
            .ToArray();

        if (values.Length != width * height)
            throw new InvalidOperationException(
                $"CSV data length mismatch. Expected {width * height}, got {values.Length}"
            );

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            data[y, x] = values[y * width + x];

        return data;
    }

    private static int[,] ParseXmlData(XElement dataElement, int width, int height)
    {
        var data = new int[height, width];
        var tiles = dataElement.Elements("tile").ToArray();

        for (var i = 0; i < tiles.Length && i < width * height; i++)
        {
            var y = i / width;
            var x = i % width;
            data[y, x] = ParseInt(tiles[i], "gid");
        }

        return data;
    }

    private static List<TmxObjectGroup> ParseObjectGroups(XElement mapElement)
    {
        var objectGroups = new List<TmxObjectGroup>();

        foreach (var groupElement in mapElement.Elements("objectgroup"))
        {
            var group = new TmxObjectGroup
            {
                Id = ParseInt(groupElement, "id"),
                Name = groupElement.Attribute("name")?.Value ?? string.Empty,
                Objects = ParseObjects(groupElement),
            };

            objectGroups.Add(group);
        }

        return objectGroups;
    }

    private static List<TmxObject> ParseObjects(XElement groupElement)
    {
        var objects = new List<TmxObject>();

        foreach (var objElement in groupElement.Elements("object"))
        {
            var obj = new TmxObject
            {
                Id = ParseInt(objElement, "id"),
                X = ParseFloat(objElement, "x"),
                Y = ParseFloat(objElement, "y"),
                Width = ParseFloat(objElement, "width"),
                Height = ParseFloat(objElement, "height"),
                Type = objElement.Attribute("type")?.Value,
                Name = objElement.Attribute("name")?.Value,
                Properties = ParseProperties(objElement),
            };

            objects.Add(obj);
        }

        return objects;
    }

    private static Dictionary<string, object> ParseProperties(XElement objectElement)
    {
        var properties = new Dictionary<string, object>();
        var propertiesElement = objectElement.Element("properties");

        if (propertiesElement == null)
            return properties;

        foreach (var propElement in propertiesElement.Elements("property"))
        {
            var name = propElement.Attribute("name")?.Value;
            var type = propElement.Attribute("type")?.Value ?? "string";
            var value = propElement.Attribute("value")?.Value ?? string.Empty;

            if (string.IsNullOrEmpty(name))
                continue;

            properties[name] = type switch
            {
                "bool" => bool.Parse(value),
                "int" => int.Parse(value, CultureInfo.InvariantCulture),
                "float" => float.Parse(value, CultureInfo.InvariantCulture),
                "string" => value,
                _ => value,
            };
        }

        return properties;
    }

    private static int ParseInt(XElement element, string attributeName, int defaultValue = 0)
    {
        var attr = element.Attribute(attributeName);
        return
            attr != null && int.TryParse(attr.Value, CultureInfo.InvariantCulture, out var result)
            ? result
            : defaultValue;
    }

    private static float ParseFloat(XElement element, string attributeName, float defaultValue = 0f)
    {
        var attr = element.Attribute(attributeName);
        return
            attr != null && float.TryParse(attr.Value, CultureInfo.InvariantCulture, out var result)
            ? result
            : defaultValue;
    }
}
