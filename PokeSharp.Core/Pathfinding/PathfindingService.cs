using Microsoft.Xna.Framework;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Systems;

namespace PokeSharp.Core.Pathfinding;

/// <summary>
///     Service providing A* pathfinding algorithms for grid-based navigation.
///     Supports obstacle detection, path smoothing, and dynamic recalculation.
/// </summary>
public class PathfindingService
{
    private readonly Dictionary<Point, PathNode> _allNodes = new();
    private readonly HashSet<Point> _closedSet = new();
    private readonly PriorityQueue<PathNode, float> _openSet = new();

    /// <summary>
    ///     Finds the shortest path from start to goal using A* algorithm.
    /// </summary>
    /// <param name="start">Starting tile position</param>
    /// <param name="goal">Goal tile position</param>
    /// <param name="mapId">Map identifier</param>
    /// <param name="spatialHashSystem">Spatial hash system for collision detection</param>
    /// <param name="maxSearchNodes">Maximum nodes to search before giving up (default 1000)</param>
    /// <returns>Queue of waypoints from start to goal, or null if no path found</returns>
    public Queue<Point>? FindPath(
        Point start,
        Point goal,
        int mapId,
        SpatialHashSystem spatialHashSystem,
        int maxSearchNodes = 1000
    )
    {
        if (start == goal)
        {
            var singlePoint = new Queue<Point>();
            singlePoint.Enqueue(goal);
            return singlePoint;
        }

        // Clear previous search state
        _openSet.Clear();
        _allNodes.Clear();
        _closedSet.Clear();

        // Initialize start node
        var startNode = new PathNode(start, null, 0f, Heuristic(start, goal));
        _allNodes[start] = startNode;
        _openSet.Enqueue(startNode, startNode.F);

        var nodesSearched = 0;

        while (_openSet.Count > 0 && nodesSearched < maxSearchNodes)
        {
            nodesSearched++;

            // Get node with lowest F score
            var current = _openSet.Dequeue();

            // Goal reached
            if (current.Position == goal)
                return ReconstructPath(current);

            _closedSet.Add(current.Position);

            // Explore neighbors
            foreach (var neighborPos in GetNeighbors(current.Position))
            {
                // Skip if already evaluated
                if (_closedSet.Contains(neighborPos))
                    continue;

                // Skip if not walkable (check collision)
                if (!IsWalkable(neighborPos, mapId, spatialHashSystem))
                    continue;

                var tentativeG = current.G + 1f; // Each step costs 1

                // Check if we found a better path to this neighbor
                if (_allNodes.TryGetValue(neighborPos, out var existingNeighbor))
                {
                    if (tentativeG >= existingNeighbor.G)
                        continue; // Not a better path

                    // Update existing node with better path
                    existingNeighbor.Parent = current;
                    existingNeighbor.G = tentativeG;
                    existingNeighbor.F = tentativeG + existingNeighbor.H;
                }
                else
                {
                    // Create new node
                    var h = Heuristic(neighborPos, goal);
                    var neighbor = new PathNode(neighborPos, current, tentativeG, h);
                    _allNodes[neighborPos] = neighbor;
                    _openSet.Enqueue(neighbor, neighbor.F);
                }
            }
        }

        return null; // No path found
    }

    /// <summary>
    ///     Checks if a path is still valid (no obstacles blocking it).
    /// </summary>
    /// <param name="path">Path to validate</param>
    /// <param name="mapId">Map identifier</param>
    /// <param name="spatialHashSystem">Spatial hash system for collision detection</param>
    /// <returns>True if path is clear, false if blocked</returns>
    public bool IsPathValid(Queue<Point> path, int mapId, SpatialHashSystem spatialHashSystem)
    {
        foreach (var point in path)
            if (!IsWalkable(point, mapId, spatialHashSystem))
                return false;

        return true;
    }

    /// <summary>
    ///     Smooths a path by removing unnecessary waypoints.
    ///     Uses line-of-sight optimization to reduce zigzagging.
    /// </summary>
    /// <param name="path">Original path</param>
    /// <param name="mapId">Map identifier</param>
    /// <param name="spatialHashSystem">Spatial hash system for collision detection</param>
    /// <returns>Smoothed path with fewer waypoints</returns>
    public Queue<Point> SmoothPath(
        Queue<Point> path,
        int mapId,
        SpatialHashSystem spatialHashSystem
    )
    {
        if (path.Count <= 2)
            return path; // Can't smooth a path with 2 or fewer points

        var smoothed = new Queue<Point>();
        var pathArray = path.ToArray();

        smoothed.Enqueue(pathArray[0]); // Always keep start

        var currentIndex = 0;
        while (currentIndex < pathArray.Length - 1)
        {
            var farthestVisible = currentIndex + 1;

            // Find the farthest point we can see from current
            for (var i = currentIndex + 2; i < pathArray.Length; i++)
                if (HasLineOfSight(pathArray[currentIndex], pathArray[i], mapId, spatialHashSystem))
                    farthestVisible = i;
                else
                    break; // Can't see beyond this point

            currentIndex = farthestVisible;
            smoothed.Enqueue(pathArray[currentIndex]);
        }

        return smoothed;
    }

    /// <summary>
    ///     Checks if there's a clear line of sight between two points.
    /// </summary>
    private bool HasLineOfSight(
        Point from,
        Point to,
        int mapId,
        SpatialHashSystem spatialHashSystem
    )
    {
        // Use Bresenham's line algorithm to check all tiles in the line
        var points = GetLinePoints(from, to);

        foreach (var point in points)
            if (!IsWalkable(point, mapId, spatialHashSystem))
                return false;

        return true;
    }

    /// <summary>
    ///     Gets all points along a line using Bresenham's algorithm.
    /// </summary>
    private IEnumerable<Point> GetLinePoints(Point from, Point to)
    {
        var points = new List<Point>();

        int x0 = from.X,
            y0 = from.Y;
        int x1 = to.X,
            y1 = to.Y;

        var dx = Math.Abs(x1 - x0);
        var dy = Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;

        while (true)
        {
            points.Add(new Point(x0, y0));

            if (x0 == x1 && y0 == y1)
                break;

            var e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }

        return points;
    }

    /// <summary>
    ///     Reconstructs the path from goal to start by following parent pointers.
    /// </summary>
    private Queue<Point> ReconstructPath(PathNode goalNode)
    {
        var path = new Stack<Point>();
        var current = goalNode;

        while (current != null)
        {
            path.Push(current.Position);
            current = current.Parent;
        }

        // Convert stack to queue (reverses the path)
        return new Queue<Point>(path);
    }

    /// <summary>
    ///     Gets the four cardinal neighbors of a position (up, down, left, right).
    /// </summary>
    private IEnumerable<Point> GetNeighbors(Point position)
    {
        yield return new Point(position.X, position.Y - 1); // Up
        yield return new Point(position.X, position.Y + 1); // Down
        yield return new Point(position.X - 1, position.Y); // Left
        yield return new Point(position.X + 1, position.Y); // Right
    }

    /// <summary>
    ///     Checks if a position is walkable (no collision).
    /// </summary>
    private bool IsWalkable(Point position, int mapId, SpatialHashSystem spatialHashSystem)
    {
        // Use the same collision detection as MovementSystem
        // Check all four directions to be thorough
        return CollisionSystem.IsPositionWalkable(
            spatialHashSystem,
            mapId,
            position.X,
            position.Y,
            Direction.None
        );
    }

    /// <summary>
    ///     Manhattan distance heuristic for A* algorithm.
    ///     Admissible (never overestimates) for 4-directional grid movement.
    /// </summary>
    private float Heuristic(Point from, Point to)
    {
        return Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y);
    }

    /// <summary>
    ///     Represents a node in the A* pathfinding graph.
    /// </summary>
    private class PathNode
    {
        public PathNode(Point position, PathNode? parent, float g, float h)
        {
            Position = position;
            Parent = parent;
            G = g;
            H = h;
            F = g + h;
        }

        public Point Position { get; }
        public PathNode? Parent { get; set; }
        public float G { get; set; } // Cost from start
        public float H { get; } // Heuristic cost to goal
        public float F { get; set; } // Total cost (G + H)
    }
}
