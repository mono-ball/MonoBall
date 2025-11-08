namespace PokeSharp.Core.Systems;

/// <summary>
///     Defines system execution priorities.
///     Lower numbers execute first.
/// </summary>
public static class SystemPriority
{
    // Input and pre-processing
    public const int Input = 0;

    // Spatial indexing (must run early)
    public const int SpatialHash = 25;

    // AI and behaviors
    public const int AI = 50;
    public const int NpcBehavior = 75;
    public const int Pathfinding = 85;

    // Weather and environment
    public const int Weather = 90;

    // Movement and physics
    public const int Movement = 100;
    public const int Collision = 200;

    // Animation
    public const int Animation = 800;
    public const int CameraFollow = 825;
    public const int TileAnimation = 850;

    // Rendering
    public const int MapRender = 900;
    public const int Render = 1000;
}
