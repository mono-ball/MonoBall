using System.Globalization;
using System.Text.Json;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameData.MapLoading.Tiled.Utilities;

/// <summary>
///     Unified utility for parsing Tiled custom properties with fail-fast behavior.
///     Handles both JsonElement and Dictionary&lt;string, object&gt; formats.
///     Design principles (fail-fast):
///     - Required methods throw InvalidDataException if property is missing or invalid
///     - Optional methods return null if property is missing
///     - If a property is present but has an invalid format, ALWAYS throw (never silently ignore)
///     - All exceptions include context for debugging
/// </summary>
public static class TiledPropertyParser
{
    // ===================================================================
    // String Properties
    // ===================================================================

    /// <summary>
    ///     Gets a required string property. Throws if missing or empty.
    /// </summary>
    /// <exception cref="InvalidDataException">Property is missing or empty.</exception>
    public static string GetRequiredString(
        Dictionary<string, object> properties,
        string key,
        string context)
    {
        if (!properties.TryGetValue(key, out object? value))
        {
            throw new InvalidDataException(
                $"Required property '{key}' is missing. Context: {context}");
        }

        string? result = ExtractStringValue(value);
        if (string.IsNullOrWhiteSpace(result))
        {
            throw new InvalidDataException(
                $"Required property '{key}' is empty or whitespace. Context: {context}");
        }

        return result;
    }

    /// <summary>
    ///     Gets an optional string property. Returns null if missing or empty.
    /// </summary>
    public static string? GetOptionalString(
        Dictionary<string, object> properties,
        string key)
    {
        if (!properties.TryGetValue(key, out object? value))
        {
            return null;
        }

        string? result = ExtractStringValue(value);
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    private static string? ExtractStringValue(object? value)
    {
        if (value == null)
        {
            return null;
        }

        if (value is JsonElement je)
        {
            return je.ValueKind == JsonValueKind.String ? je.GetString() : je.ToString();
        }

        return value.ToString();
    }

    // ===================================================================
    // Integer Properties
    // ===================================================================

    /// <summary>
    ///     Gets a required integer property. Throws if missing or invalid.
    /// </summary>
    /// <exception cref="InvalidDataException">Property is missing or has invalid format.</exception>
    public static int GetRequiredInt(
        Dictionary<string, object> properties,
        string key,
        string context)
    {
        if (!properties.TryGetValue(key, out object? value))
        {
            throw new InvalidDataException(
                $"Required property '{key}' is missing. Context: {context}");
        }

        return ParseIntValue(value, key, context);
    }

    /// <summary>
    ///     Gets an optional integer property. Returns null if missing, throws if invalid format.
    /// </summary>
    /// <exception cref="InvalidDataException">Property is present but has invalid format.</exception>
    public static int? GetOptionalInt(
        Dictionary<string, object> properties,
        string key,
        string context)
    {
        if (!properties.TryGetValue(key, out object? value))
        {
            return null;
        }

        return ParseIntValue(value, key, context);
    }

    private static int ParseIntValue(object? value, string key, string context)
    {
        // Handle direct int
        if (value is int intValue)
        {
            return intValue;
        }

        // Handle JsonElement
        if (value is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Number)
            {
                return je.GetInt32();
            }

            // Try parsing string representation
            if (je.ValueKind == JsonValueKind.String && int.TryParse(je.GetString(), out int jeResult))
            {
                return jeResult;
            }
        }

        // Handle string conversion
        if (int.TryParse(value?.ToString(), out int parsedValue))
        {
            return parsedValue;
        }

        throw new InvalidDataException(
            $"Property '{key}' has invalid integer value '{value}'. Context: {context}");
    }

    // ===================================================================
    // Float Properties
    // ===================================================================

    /// <summary>
    ///     Gets a required float property. Throws if missing or invalid.
    /// </summary>
    /// <exception cref="InvalidDataException">Property is missing or has invalid format.</exception>
    public static float GetRequiredFloat(
        Dictionary<string, object> properties,
        string key,
        string context)
    {
        if (!properties.TryGetValue(key, out object? value))
        {
            throw new InvalidDataException(
                $"Required property '{key}' is missing. Context: {context}");
        }

        return ParseFloatValue(value, key, context);
    }

    /// <summary>
    ///     Gets an optional float property. Returns null if missing, throws if invalid format.
    /// </summary>
    /// <exception cref="InvalidDataException">Property is present but has invalid format.</exception>
    public static float? GetOptionalFloat(
        Dictionary<string, object> properties,
        string key,
        string context)
    {
        if (!properties.TryGetValue(key, out object? value))
        {
            return null;
        }

        return ParseFloatValue(value, key, context);
    }

    private static float ParseFloatValue(object? value, string key, string context)
    {
        // Handle direct float/double
        if (value is float floatValue)
        {
            return floatValue;
        }

        if (value is double doubleValue)
        {
            return (float)doubleValue;
        }

        // Handle JsonElement
        if (value is JsonElement je && je.ValueKind == JsonValueKind.Number)
        {
            return (float)je.GetDouble();
        }

        // Handle string conversion with InvariantCulture
        if (float.TryParse(
                value?.ToString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float parsedValue))
        {
            return parsedValue;
        }

        throw new InvalidDataException(
            $"Property '{key}' has invalid float value '{value}'. Context: {context}");
    }

    // ===================================================================
    // Byte Properties (for elevation, etc.)
    // ===================================================================

    /// <summary>
    ///     Gets a required byte property. Throws if missing or invalid.
    /// </summary>
    /// <exception cref="InvalidDataException">Property is missing or has invalid format.</exception>
    public static byte GetRequiredByte(
        Dictionary<string, object> properties,
        string key,
        string context)
    {
        if (!properties.TryGetValue(key, out object? value))
        {
            throw new InvalidDataException(
                $"Required property '{key}' is missing. Context: {context}");
        }

        return ParseByteValue(value, key, context);
    }

    /// <summary>
    ///     Gets an optional byte property. Returns null if missing, throws if invalid format.
    /// </summary>
    /// <exception cref="InvalidDataException">Property is present but has invalid format.</exception>
    public static byte? GetOptionalByte(
        Dictionary<string, object> properties,
        string key,
        string context)
    {
        if (!properties.TryGetValue(key, out object? value))
        {
            return null;
        }

        return ParseByteValue(value, key, context);
    }

    private static byte ParseByteValue(object? value, string key, string context)
    {
        // Handle direct byte/int
        if (value is byte byteValue)
        {
            return byteValue;
        }

        if (value is int intValue)
        {
            if (intValue < 0 || intValue > 255)
            {
                throw new InvalidDataException(
                    $"Property '{key}' value {intValue} is out of byte range (0-255). Context: {context}");
            }

            return (byte)intValue;
        }

        // Handle JsonElement
        if (value is JsonElement je && je.ValueKind == JsonValueKind.Number)
        {
            int jeValue = je.GetInt32();
            if (jeValue < 0 || jeValue > 255)
            {
                throw new InvalidDataException(
                    $"Property '{key}' value {jeValue} is out of byte range (0-255). Context: {context}");
            }

            return (byte)jeValue;
        }

        // Handle string conversion
        if (byte.TryParse(value?.ToString(), out byte parsedValue))
        {
            return parsedValue;
        }

        throw new InvalidDataException(
            $"Property '{key}' has invalid byte value '{value}'. Context: {context}");
    }

    // ===================================================================
    // Boolean Properties
    // ===================================================================

    /// <summary>
    ///     Gets a required boolean property. Throws if missing or invalid.
    /// </summary>
    /// <exception cref="InvalidDataException">Property is missing or has invalid format.</exception>
    public static bool GetRequiredBool(
        Dictionary<string, object> properties,
        string key,
        string context)
    {
        if (!properties.TryGetValue(key, out object? value))
        {
            throw new InvalidDataException(
                $"Required property '{key}' is missing. Context: {context}");
        }

        return ParseBoolValue(value, key, context);
    }

    /// <summary>
    ///     Gets an optional boolean property. Returns null if missing, throws if invalid format.
    /// </summary>
    /// <exception cref="InvalidDataException">Property is present but has invalid format.</exception>
    public static bool? GetOptionalBool(
        Dictionary<string, object> properties,
        string key,
        string context)
    {
        if (!properties.TryGetValue(key, out object? value))
        {
            return null;
        }

        return ParseBoolValue(value, key, context);
    }

    /// <summary>
    ///     Checks if a boolean property exists and is true.
    ///     Returns false if missing. Throws if present but invalid.
    /// </summary>
    /// <exception cref="InvalidDataException">Property is present but has invalid format.</exception>
    public static bool GetBoolOrFalse(
        Dictionary<string, object> properties,
        string key,
        string context)
    {
        if (!properties.TryGetValue(key, out object? value))
        {
            return false;
        }

        return ParseBoolValue(value, key, context);
    }

    private static bool ParseBoolValue(object? value, string key, string context)
    {
        // Handle direct bool
        if (value is bool boolValue)
        {
            return boolValue;
        }

        // Handle JsonElement
        if (value is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (je.ValueKind == JsonValueKind.False)
            {
                return false;
            }
        }

        // Handle string conversion
        if (bool.TryParse(value?.ToString(), out bool parsedValue))
        {
            return parsedValue;
        }

        throw new InvalidDataException(
            $"Property '{key}' has invalid boolean value '{value}'. Context: {context}");
    }

    // ===================================================================
    // Direction Parsing
    // ===================================================================

    /// <summary>
    ///     Gets a required Direction property. Throws if missing or invalid.
    ///     Supports: "north"/"up", "south"/"down", "east"/"right", "west"/"left"
    /// </summary>
    /// <exception cref="InvalidDataException">Property is missing or has invalid value.</exception>
    public static Direction GetRequiredDirection(
        Dictionary<string, object> properties,
        string key,
        string context)
    {
        string dirStr = GetRequiredString(properties, key, context).ToLowerInvariant();
        return ParseDirectionValue(dirStr, key, context);
    }

    /// <summary>
    ///     Gets an optional Direction property. Returns null if missing, throws if invalid.
    /// </summary>
    /// <exception cref="InvalidDataException">Property is present but has invalid value.</exception>
    public static Direction? GetOptionalDirection(
        Dictionary<string, object> properties,
        string key,
        string context)
    {
        string? dirStr = GetOptionalString(properties, key)?.ToLowerInvariant();
        if (dirStr == null)
        {
            return null;
        }

        return ParseDirectionValue(dirStr, key, context);
    }

    private static Direction ParseDirectionValue(string dirStr, string key, string context)
    {
        return dirStr switch
        {
            "north" or "up" => Direction.North,
            "south" or "down" => Direction.South,
            "west" or "left" => Direction.West,
            "east" or "right" => Direction.East,
            _ => throw new InvalidDataException(
                $"Property '{key}' has invalid direction value '{dirStr}'. " +
                $"Expected: north/up, south/down, east/right, west/left. Context: {context}")
        };
    }

    // ===================================================================
    // Connection Parsing (nested objects)
    // ===================================================================

    /// <summary>
    ///     Gets an optional connection property. Returns (null, 0) if missing.
    ///     Throws if present but has invalid format.
    ///     Expected structure: { "map": "mapId", "offset": 0 }
    /// </summary>
    /// <exception cref="InvalidDataException">Property is present but has invalid format.</exception>
    public static (GameMapId? MapId, int Offset) GetOptionalConnection(
        Dictionary<string, object> properties,
        string key,
        string context)
    {
        if (!properties.TryGetValue(key, out object? value) || value == null)
        {
            return (null, 0);
        }

        return ParseConnectionValue(value, key, context);
    }

    private static (GameMapId? MapId, int Offset) ParseConnectionValue(
        object value, string key, string context)
    {
        string? mapIdStr = null;
        int offset = 0;

        // Handle JsonElement case
        if (value is JsonElement je)
        {
            if (je.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException(
                    $"Property '{key}' must be an object with 'map' and 'offset' fields. Context: {context}");
            }

            if (je.TryGetProperty("map", out JsonElement mapProp))
            {
                mapIdStr = mapProp.GetString();
            }

            if (je.TryGetProperty("offset", out JsonElement offsetProp))
            {
                if (offsetProp.ValueKind != JsonValueKind.Number)
                {
                    throw new InvalidDataException(
                        $"Property '{key}.offset' must be a number. Context: {context}");
                }

                offset = offsetProp.GetInt32();
            }
        }
        // Handle Dictionary case
        else if (value is Dictionary<string, object> dict)
        {
            if (dict.TryGetValue("map", out object? mapValue))
            {
                mapIdStr = mapValue?.ToString();
            }

            if (dict.TryGetValue("offset", out object? offsetValue))
            {
                if (offsetValue is int intOffset)
                {
                    offset = intOffset;
                }
                else if (offsetValue is JsonElement je2 && je2.ValueKind == JsonValueKind.Number)
                {
                    offset = je2.GetInt32();
                }
                else if (!int.TryParse(offsetValue?.ToString(), out offset))
                {
                    throw new InvalidDataException(
                        $"Property '{key}.offset' has invalid value '{offsetValue}'. Context: {context}");
                }
            }
        }
        else
        {
            throw new InvalidDataException(
                $"Property '{key}' must be an object with 'map' and 'offset' fields, " +
                $"got {value.GetType().Name}. Context: {context}");
        }

        GameMapId? mapId = null;
        if (!string.IsNullOrEmpty(mapIdStr))
        {
            mapId = GameMapId.TryCreate(mapIdStr);
            if (mapId == null)
            {
                throw new InvalidDataException(
                    $"Property '{key}.map' has invalid map ID format '{mapIdStr}'. Context: {context}");
            }
        }

        return (mapId, offset);
    }

    // ===================================================================
    // Waypoint Parsing
    // ===================================================================

    /// <summary>
    ///     Gets an optional waypoints property. Returns empty array if missing.
    ///     Throws if present but has invalid format.
    ///     Format: "x1,y1;x2,y2;x3,y3"
    /// </summary>
    /// <exception cref="InvalidDataException">Property has invalid waypoint format.</exception>
    public static Point[] GetOptionalWaypoints(
        Dictionary<string, object> properties,
        string key,
        string context)
    {
        string? waypointsStr = GetOptionalString(properties, key);
        if (string.IsNullOrEmpty(waypointsStr))
        {
            return Array.Empty<Point>();
        }

        var points = new List<Point>();
        string[] pairs = waypointsStr.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (string pair in pairs)
        {
            string[] coords = pair.Split(',');
            if (coords.Length != 2)
            {
                throw new InvalidDataException(
                    $"Property '{key}' has invalid waypoint format '{pair}'. " +
                    $"Expected 'x,y' format. Context: {context}");
            }

            if (!int.TryParse(coords[0].Trim(), out int x))
            {
                throw new InvalidDataException(
                    $"Property '{key}' has invalid X coordinate '{coords[0]}' in waypoint '{pair}'. Context: {context}");
            }

            if (!int.TryParse(coords[1].Trim(), out int y))
            {
                throw new InvalidDataException(
                    $"Property '{key}' has invalid Y coordinate '{coords[1]}' in waypoint '{pair}'. Context: {context}");
            }

            points.Add(new Point(x, y));
        }

        return points.ToArray();
    }

    /// <summary>
    ///     Gets required waypoints property. Throws if missing or empty.
    /// </summary>
    /// <exception cref="InvalidDataException">Property is missing, empty, or has invalid format.</exception>
    public static Point[] GetRequiredWaypoints(
        Dictionary<string, object> properties,
        string key,
        string context)
    {
        Point[] waypoints = GetOptionalWaypoints(properties, key, context);
        if (waypoints.Length == 0)
        {
            throw new InvalidDataException(
                $"Required property '{key}' is missing or has no waypoints. Context: {context}");
        }

        return waypoints;
    }

    // ===================================================================
    // Nested Object Extraction
    // ===================================================================

    /// <summary>
    ///     Gets an optional nested object property. Returns null if missing.
    ///     Throws if present but not an object.
    /// </summary>
    /// <exception cref="InvalidDataException">Property is present but not an object.</exception>
    public static Dictionary<string, object>? GetOptionalNestedObject(
        Dictionary<string, object> properties,
        string key,
        string context)
    {
        if (!properties.TryGetValue(key, out object? value) || value == null)
        {
            return null;
        }

        // Handle Dictionary case (already converted)
        if (value is Dictionary<string, object> dict)
        {
            return dict;
        }

        // Handle JsonElement case
        if (value is JsonElement je)
        {
            if (je.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException(
                    $"Property '{key}' must be an object, got {je.ValueKind}. Context: {context}");
            }

            var result = new Dictionary<string, object>();
            foreach (JsonProperty prop in je.EnumerateObject())
            {
                result[prop.Name] = prop.Value;
            }

            return result;
        }

        throw new InvalidDataException(
            $"Property '{key}' must be an object, got {value.GetType().Name}. Context: {context}");
    }

    /// <summary>
    ///     Gets a required nested object property. Throws if missing.
    /// </summary>
    /// <exception cref="InvalidDataException">Property is missing or not an object.</exception>
    public static Dictionary<string, object> GetRequiredNestedObject(
        Dictionary<string, object> properties,
        string key,
        string context)
    {
        Dictionary<string, object>? nested = GetOptionalNestedObject(properties, key, context);
        if (nested == null)
        {
            throw new InvalidDataException(
                $"Required property '{key}' is missing. Context: {context}");
        }

        return nested;
    }

    // ===================================================================
    // Patrol Waypoint Generation
    // ===================================================================

    /// <summary>
    ///     Gets patrol waypoints from Tiled properties.
    ///     Supports explicit waypoints string OR axis-based generation.
    ///     Returns null if no patrol configuration is found.
    /// </summary>
    /// <param name="properties">Tiled object properties.</param>
    /// <param name="behaviorId">Behavior ID to check if it's a patrol behavior.</param>
    /// <param name="originX">Origin tile X for generated waypoints.</param>
    /// <param name="originY">Origin tile Y for generated waypoints.</param>
    /// <param name="context">Error context string.</param>
    /// <returns>Array of waypoints, or null if not a patrol behavior or no waypoints configured.</returns>
    public static Point[]? GetPatrolWaypoints(
        Dictionary<string, object> properties,
        string? behaviorId,
        int originX,
        int originY,
        string context)
    {
        // Only generate waypoints for patrol behaviors
        if (string.IsNullOrEmpty(behaviorId) || !behaviorId.Contains("patrol"))
        {
            return null;
        }

        // Check for explicit waypoints first
        if (HasProperty(properties, "waypoints"))
        {
            Point[] waypoints = GetOptionalWaypoints(properties, "waypoints", context);
            return waypoints.Length > 0 ? waypoints : null;
        }

        // Generate waypoints from axis + range
        string? axis = GetOptionalString(properties, "axis")?.ToLowerInvariant();
        if (string.IsNullOrEmpty(axis))
        {
            return null;
        }

        // Get range - check axis-specific range first, then general range
        int range;
        if (axis == "horizontal" && HasProperty(properties, "rangeX"))
        {
            range = GetRequiredInt(properties, "rangeX", context);
        }
        else if (axis == "vertical" && HasProperty(properties, "rangeY"))
        {
            range = GetRequiredInt(properties, "rangeY", context);
        }
        else
        {
            range = GetOptionalInt(properties, "range", context) ?? 2;
        }

        range = Math.Max(1, range);

        return axis == "horizontal"
            ? [new Point(originX, originY), new Point(originX + range, originY)]
            : [new Point(originX, originY), new Point(originX, originY + range)];
    }

    // ===================================================================
    // Utility Methods
    // ===================================================================

    /// <summary>
    ///     Checks if a property exists in the dictionary.
    /// </summary>
    public static bool HasProperty(Dictionary<string, object> properties, string key)
    {
        return properties.ContainsKey(key);
    }

    /// <summary>
    ///     Creates a context string for error messages.
    /// </summary>
    public static string CreateContext(string objectType, string? objectName, int? x = null, int? y = null)
    {
        string ctx = $"{objectType}";
        if (!string.IsNullOrEmpty(objectName))
        {
            ctx += $" '{objectName}'";
        }

        if (x.HasValue && y.HasValue)
        {
            ctx += $" at ({x}, {y})";
        }

        return ctx;
    }
}
