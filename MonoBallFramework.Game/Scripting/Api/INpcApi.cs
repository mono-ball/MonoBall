using Arch.Core;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.Scripting.Api;

/// <summary>
///     NPC management API for scripts.
///     Provides control over NPC movement, facing, behavior, and appearance.
/// </summary>
public interface INpcApi
{
    #region Fluent API

    /// <summary>
    ///     Gets a fluent context for chaining operations on a single NPC entity.
    /// </summary>
    /// <param name="npc">The NPC entity to operate on.</param>
    /// <returns>A fluent context for chaining NPC operations.</returns>
    /// <example>
    ///     <code>
    /// // Chain multiple operations fluently
    /// Npc.For(guard)
    ///    .Move(Direction.North)
    ///    .Face(Direction.South)
    ///    .SetVisible(true)
    ///    .SetBehavior(patrolBehavior);
    ///     </code>
    /// </example>
    INpcContext For(Entity npc);

    #endregion

    #region Movement

    /// <summary>
    ///     Request an NPC to move in a direction.
    ///     Movement will be validated against collision before execution.
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    /// <param name="direction">Direction to move.</param>
    void MoveNpc(Entity npc, Direction direction);

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
    Point GetNpcPosition(Entity npc);

    /// <summary>
    ///     Check if an NPC is currently moving.
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    /// <returns>True if NPC is in motion.</returns>
    bool IsNpcMoving(Entity npc);

    /// <summary>
    ///     Stop an NPC's current movement immediately.
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    void StopNpc(Entity npc);

    #endregion

    #region Path/Patrol

    /// <summary>
    ///     Set an NPC's patrol path with waypoints.
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    /// <param name="waypoints">Array of waypoint positions.</param>
    /// <param name="loop">Whether to loop back to start after reaching end.</param>
    void SetNpcPath(Entity npc, Point[] waypoints, bool loop);

    /// <summary>
    ///     Get an NPC's current patrol path.
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    /// <returns>Array of waypoint positions, or null if no path set.</returns>
    Point[]? GetNpcPath(Entity npc);

    /// <summary>
    ///     Clear an NPC's patrol path.
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    void ClearNpcPath(Entity npc);

    /// <summary>
    ///     Pause an NPC's patrol path (stops movement but keeps path).
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    void PauseNpcPath(Entity npc);

    /// <summary>
    ///     Resume an NPC's patrol path after pausing.
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    /// <param name="waitTime">Wait time at waypoints in seconds (default 0).</param>
    void ResumeNpcPath(Entity npc, float waitTime = 0f);

    #endregion

    #region Sprite Management

    /// <summary>
    ///     Changes an NPC's sprite at runtime.
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    /// <param name="spriteId">The new sprite ID.</param>
    void SetNpcSprite(Entity npc, GameSpriteId spriteId);

    /// <summary>
    ///     Gets the current sprite ID for an NPC.
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    /// <returns>The sprite ID, or null if no sprite is set.</returns>
    GameSpriteId? GetNpcSprite(Entity npc);

    #endregion

    #region Behavior Management

    /// <summary>
    ///     Changes an NPC's behavior at runtime.
    ///     This will re-initialize the behavior script.
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    /// <param name="behaviorId">The new behavior ID.</param>
    void SetNpcBehavior(Entity npc, GameBehaviorId behaviorId);

    /// <summary>
    ///     Gets the current behavior ID for an NPC.
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    /// <returns>The behavior ID, or null if no behavior is set.</returns>
    GameBehaviorId? GetNpcBehavior(Entity npc);

    /// <summary>
    ///     Activates an NPC's behavior (starts executing OnTick).
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    void ActivateBehavior(Entity npc);

    /// <summary>
    ///     Deactivates an NPC's behavior (pauses OnTick execution).
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    void DeactivateBehavior(Entity npc);

    /// <summary>
    ///     Checks if an NPC's behavior is currently active.
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    /// <returns>True if behavior is active.</returns>
    bool IsBehaviorActive(Entity npc);

    #endregion

    #region Visibility

    /// <summary>
    ///     Shows or hides an NPC.
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    /// <param name="visible">True to show, false to hide.</param>
    void SetNpcVisible(Entity npc, bool visible);

    /// <summary>
    ///     Checks if an NPC is currently visible.
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    /// <returns>True if visible.</returns>
    bool IsNpcVisible(Entity npc);

    #endregion

    #region Identity

    /// <summary>
    ///     Gets the NPC's definition ID if it was spawned from a definition.
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    /// <returns>The NPC ID, or null if entity was not spawned from a definition.</returns>
    GameNpcId? GetNpcId(Entity npc);

    /// <summary>
    ///     Gets the NPC's display name.
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    /// <returns>The display name, or null if not set.</returns>
    string? GetNpcDisplayName(Entity npc);

    /// <summary>
    ///     Sets the NPC's display name.
    /// </summary>
    /// <param name="npc">The NPC entity.</param>
    /// <param name="displayName">The new display name.</param>
    void SetNpcDisplayName(Entity npc, string displayName);

    #endregion
}
