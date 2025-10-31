namespace PokeSharp.Core.Systems;

/// <summary>
/// Defines standard priority values for system execution order.
/// Systems execute in ascending priority order (lower values first).
/// </summary>
public static class SystemPriority
{
    /// <summary>
    /// Input systems that read player/AI input (Priority: 0).
    /// </summary>
    public const int Input = 0;

    /// <summary>
    /// AI and decision-making systems (Priority: 50).
    /// </summary>
    public const int AI = 50;

    /// <summary>
    /// Movement and physics systems (Priority: 100).
    /// </summary>
    public const int Movement = 100;

    /// <summary>
    /// Collision detection systems (Priority: 200).
    /// </summary>
    public const int Collision = 200;

    /// <summary>
    /// Game logic systems (Priority: 300).
    /// </summary>
    public const int Logic = 300;

    /// <summary>
    /// Animation systems (Priority: 800).
    /// </summary>
    public const int Animation = 800;

    /// <summary>
    /// Rendering systems that draw to screen (Priority: 1000).
    /// </summary>
    public const int Render = 1000;

    /// <summary>
    /// UI rendering systems (Priority: 1100).
    /// </summary>
    public const int UI = 1100;
}
