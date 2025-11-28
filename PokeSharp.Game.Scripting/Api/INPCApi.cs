using Arch.Core;
using Microsoft.Xna.Framework;
using PokeSharp.Game.Components.Movement;

namespace PokeSharp.Game.Scripting.Api;

/// <summary>
///     NPC management API for scripts.
///     Provides control over NPC movement, facing, and behavior.
/// </summary>
public interface INPCApi
{
    /// <summary>
    ///     Request an NPC to move in a direction.
    ///     Movement will be validated against collision before execution.
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    /// <param name="direction">Direction to move.</param>
    void MoveNPC(Entity npc, Direction direction);

    /// <summary>
    ///     Set an NPC's facing direction without moving.
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    /// <param name="direction">Direction to face.</param>
    void FaceDirection(Entity npc, Direction direction);

    /// <summary>
    ///     Make an NPC face another entity (e.g., face the player).
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    /// <param name="target">The entity to face toward.</param>
    void FaceEntity(Entity npc, Entity target);

    /// <summary>
    ///     Get an NPC's current grid position.
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    /// <returns>Current tile position.</returns>
    Point GetNPCPosition(Entity npc);

    /// <summary>
    ///     Set an NPC's patrol path with waypoints.
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    /// <param name="waypoints">Array of waypoint positions.</param>
    /// <param name="loop">Whether to loop back to start after reaching end.</param>
    void SetNPCPath(Entity npc, Point[] waypoints, bool loop);

    /// <summary>
    ///     Get an NPC's current patrol path.
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    /// <returns>Array of waypoint positions, or null if no path set.</returns>
    Point[]? GetNPCPath(Entity npc);

    /// <summary>
    ///     Clear an NPC's patrol path.
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    void ClearNPCPath(Entity npc);

    /// <summary>
    ///     Pause an NPC's patrol path (stops movement but keeps path).
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    void PauseNPCPath(Entity npc);

    /// <summary>
    ///     Resume an NPC's patrol path after pausing.
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    /// <param name="waitTime">Wait time at waypoints in seconds (default 0).</param>
    void ResumeNPCPath(Entity npc, float waitTime = 0f);

    /// <summary>
    ///     Check if an NPC is currently moving.
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    /// <returns>True if NPC is in motion.</returns>
    bool IsNPCMoving(Entity npc);

    /// <summary>
    ///     Stop an NPC's current movement immediately.
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    void StopNPC(Entity npc);
}
