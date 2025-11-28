using System.Globalization;
using System.Xml.Linq;
using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

namespace PokeSharp.Game.Data.MapLoading.Tiled.Core;

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
        {
            throw new FileNotFoundException($"TMX file not found: {tmxPath}");
        }

        var doc = XDocument.Load(tmxPath);
        XElement mapElement =
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
            ImageLayers = ParseImageLayers(mapElement),
        };

        return tmxDoc;
    }

    private static List<TmxTileset> ParseTilesets(XElement mapElement, string tmxPath)
    {
        var tilesets = new List<TmxTileset>();
        string mapDirectory = Path.GetDirectoryName(tmxPath) ?? string.Empty;

        foreach (XElement tilesetElement in mapElement.Elements("tileset"))
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
            XElement? imageElement = tilesetElement.Element("image");
            if (imageElement != null)
            {
                tileset.Image = new TmxImage
                {
                    Source = imageElement.Attribute("source")?.Value ?? string.Empty,
                    Width = ParseInt(imageElement, "width"),
                    Height = ParseInt(imageElement, "height"),
                };
            }

            // Handle external tileset reference (.tsx)
            if (!string.IsNullOrEmpty(tileset.Source))
            {
                string tsxPath = Path.Combine(mapDirectory, tileset.Source);
                if (File.Exists(tsxPath))
                {
                    LoadExternalTileset(tileset, tsxPath);
                }
            }

            tilesets.Add(tileset);
        }

        return tilesets;
    }

    private static void LoadExternalTileset(TmxTileset tileset, string tsxPath)
    {
        var doc = XDocument.Load(tsxPath);
        XElement tilesetElement = doc.Root!;

        tileset.Name = tilesetElement.Attribute("name")?.Value ?? tileset.Name;
        tileset.TileWidth = ParseInt(tilesetElement, "tilewidth", tileset.TileWidth);
        tileset.TileHeight = ParseInt(tilesetElement, "tileheight", tileset.TileHeight);
        tileset.TileCount = ParseInt(tilesetElement, "tilecount", tileset.TileCount);

        XElement? imageElement = tilesetElement.Element("image");
        if (imageElement != null)
        {
            tileset.Image = new TmxImage
            {
                Source = imageElement.Attribute("source")?.Value ?? string.Empty,
                Width = ParseInt(imageElement, "width"),
                Height = ParseInt(imageElement, "height"),
            };
        }
    }

    private static List<TmxLayer> ParseLayers(XElement mapElement)
    {
        var layers = new List<TmxLayer>();

        foreach (XElement layerElement in mapElement.Elements("layer"))
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
            XElement? dataElement = layerElement.Element("data");
            if (dataElement != null)
            {
                string? encoding = dataElement.Attribute("encoding")?.Value;

                if (encoding == "csv")
                {
                    layer.Data = ParseCsvData(dataElement.Value, layer.Width, layer.Height);
                }
                else if (encoding == null)
                // XML encoding (tile elements)
                {
                    layer.Data = ParseXmlData(dataElement, layer.Width, layer.Height);
                }
                else
                {
                    throw new NotSupportedException($"Unsupported tile data encoding: {encoding}");
                }
            }

            layers.Add(layer);
        }

        return layers;
    }

    private static uint[] ParseCsvData(string csv, int width, int height)
    {
        uint[] values = csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => uint.Parse(s.Trim(), CultureInfo.InvariantCulture))
            .ToArray();

        if (values.Length != width * height)
        {
            throw new InvalidOperationException(
                $"CSV data length mismatch. Expected {width * height}, got {values.Length}"
            );
        }

        return values; // Return flat array (row-major order)
    }

    private static uint[] ParseXmlData(XElement dataElement, int width, int height)
    {
        uint[] data = new uint[width * height];
        XElement[] tiles = dataElement.Elements("tile").ToArray();

        for (int i = 0; i < tiles.Length && i < data.Length; i++)
        {
            string gidAttr = tiles[i].Attribute("gid")?.Value ?? "0";
            if (
                !uint.TryParse(
                    gidAttr,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out uint gid
                )
            )
            {
                gid = 0;
            }

            data[i] = gid;
        }

        return data; // Return flat array (row-major order)
    }

    private static List<TmxObjectGroup> ParseObjectGroups(XElement mapElement)
    {
        var objectGroups = new List<TmxObjectGroup>();

        foreach (XElement groupElement in mapElement.Elements("objectgroup"))
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

        foreach (XElement objElement in groupElement.Elements("object"))
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
        XElement? propertiesElement = objectElement.Element("properties");

        if (propertiesElement == null)
        {
            return properties;
        }

        foreach (XElement propElement in propertiesElement.Elements("property"))
        {
            string? name = propElement.Attribute("name")?.Value;
            string type = propElement.Attribute("type")?.Value ?? "string";
            string value = propElement.Attribute("value")?.Value ?? string.Empty;

            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

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
        XAttribute? attr = element.Attribute(attributeName);
        return
            attr != null && int.TryParse(attr.Value, CultureInfo.InvariantCulture, out int result)
            ? result
            : defaultValue;
    }

    private static float ParseFloat(XElement element, string attributeName, float defaultValue = 0f)
    {
        XAttribute? attr = element.Attribute(attributeName);
        return
            attr != null
            && float.TryParse(attr.Value, CultureInfo.InvariantCulture, out float result)
            ? result
            : defaultValue;
    }

    private static List<TmxImageLayer> ParseImageLayers(XElement mapElement)
    {
        var imageLayers = new List<TmxImageLayer>();

        foreach (XElement layerElement in mapElement.Elements("imagelayer"))
        {
            var imageLayer = new TmxImageLayer
            {
                Id = ParseInt(layerElement, "id"),
                Name = layerElement.Attribute("name")?.Value ?? string.Empty,
                X = ParseFloat(layerElement, "offsetx"),
                Y = ParseFloat(layerElement, "offsety"),
                Visible = ParseInt(layerElement, "visible", 1) == 1,
                Opacity = ParseFloat(layerElement, "opacity", 1.0f),
            };

            // Parse image element
            XElement? imageElement = layerElement.Element("image");
            if (imageElement != null)
            {
                imageLayer.Image = new TmxImage
                {
                    Source = imageElement.Attribute("source")?.Value ?? string.Empty,
                    Width = ParseInt(imageElement, "width"),
                    Height = ParseInt(imageElement, "height"),
                };
            }

            imageLayers.Add(imageLayer);
        }

        return imageLayers;
    }
}
