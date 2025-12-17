using System.Globalization;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Ecs.Components.Rendering;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameData.MapLoading.Tiled.Processors;

/// <summary>
///     Factory for creating ECS components from map object properties.
///     Centralizes component creation to eliminate duplication across spawners.
/// </summary>
public static class ComponentFactory
{
    /// <summary>
    ///     Creates a Sprite component from a GameSpriteId.
    /// </summary>
    public static Sprite CreateSprite(GameSpriteId spriteId, HashSet<GameSpriteId>? requiredSpriteIds = null)
    {
        requiredSpriteIds?.Add(spriteId);
        return new Sprite(spriteId);
    }

    /// <summary>
    ///     Parses direction from string value.
    /// </summary>
    public static Direction ParseDirection(object? directionProp, Direction defaultDirection = Direction.South)
    {
        string? dirStr = directionProp?.ToString()?.ToLowerInvariant();
        return dirStr switch
        {
            "north" or "up" => Direction.North,
            "south" or "down" => Direction.South,
            "west" or "left" => Direction.West,
            "east" or "right" => Direction.East,
            _ => defaultDirection
        };
    }

    /// <summary>
    ///     Gets animation name based on facing direction.
    /// </summary>
    public static string GetFacingAnimation(Direction direction)
    {
        return direction switch
        {
            Direction.North => "face_north",
            Direction.South => "face_south",
            Direction.East => "face_east",
            Direction.West => "face_west",
            _ => "face_south"
        };
    }

    /// <summary>
    ///     Safely parses elevation from property value.
    /// </summary>
    public static byte ParseElevation(object? elevationProp, byte defaultValue = 0)
    {
        return byte.TryParse(elevationProp?.ToString(), out byte elevation) ? elevation : defaultValue;
    }

    /// <summary>
    ///     Safely parses float with culture-invariant format.
    /// </summary>
    public static float ParseFloat(object? prop, float defaultValue = 0f)
    {
        return float.TryParse(prop?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : defaultValue;
    }

    /// <summary>
    ///     Safely parses integer value.
    /// </summary>
    public static int ParseInt(object? prop, int defaultValue = 0)
    {
        if (int.TryParse(prop?.ToString(), out int value))
        {
            return value;
        }

        return defaultValue;
    }

    /// <summary>
    ///     Parses waypoints from semicolon-separated string format.
    /// </summary>
    public static Point[]? ParseWaypoints(string? waypointsStr)
    {
        if (string.IsNullOrEmpty(waypointsStr))
        {
            return null;
        }

        var points = new List<Point>();
        string[] pairs = waypointsStr.Split(';');

        foreach (string pair in pairs)
        {
            string[] coords = pair.Split(',');
            if (coords.Length == 2 &&
                int.TryParse(coords[0].Trim(), out int x) &&
                int.TryParse(coords[1].Trim(), out int y))
            {
                points.Add(new Point(x, y));
            }
        }

        return points.Count > 0 ? points.ToArray() : null;
    }

    /// <summary>
    ///     Generates patrol waypoints for axis-based movement.
    /// </summary>
    public static Point[] GeneratePatrolWaypoints(int startX, int startY, string axis, int range)
    {
        range = Math.Max(1, range);

        if (axis == "horizontal")
        {
            return new[] { new Point(startX, startY), new Point(startX + range, startY) };
        }

        // vertical
        return new[] { new Point(startX, startY), new Point(startX, startY + range) };
    }
}
