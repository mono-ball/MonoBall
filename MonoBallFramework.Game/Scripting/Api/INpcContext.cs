using Arch.Core;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.Scripting.Api;

/// <summary>
///     Fluent context for chaining NPC operations on a single entity.
///     Obtained via <see cref="INpcApi.For(Entity)" />.
/// </summary>
/// <example>
///     <code>
/// // Chain multiple NPC operations fluently
/// Npc.For(guard)
///    .Move(Direction.North)
///    .Face(Direction.South)
///    .SetVisible(true)
///    .SetBehavior(patrolBehavior);
///     </code>
/// </example>
public interface INpcContext
{
    /// <summary>
    ///     Gets the entity this context operates on.
    /// </summary>
    Entity Entity { get; }

    #region Identity

    /// <summary>
    ///     Set the NPC's display name.
    /// </summary>
    INpcContext SetDisplayName(string displayName);

    #endregion

    #region Movement

    /// <summary>
    ///     Request the NPC to move in a direction.
    /// </summary>
    INpcContext Move(Direction direction);

    /// <summary>
    ///     Set the NPC's facing direction without moving.
    /// </summary>
    INpcContext Face(Direction direction);

    /// <summary>
    ///     Make the NPC face another entity.
    /// </summary>
    INpcContext FaceEntity(Entity target);

    /// <summary>
    ///     Stop the NPC's current movement immediately.
    /// </summary>
    INpcContext Stop();

    #endregion

    #region Path/Patrol

    /// <summary>
    ///     Set a patrol path with waypoints.
    /// </summary>
    INpcContext SetPath(Point[] waypoints, bool loop = false);

    /// <summary>
    ///     Clear the current patrol path.
    /// </summary>
    INpcContext ClearPath();

    /// <summary>
    ///     Pause the patrol path (stops movement but keeps path).
    /// </summary>
    INpcContext PausePath();

    /// <summary>
    ///     Resume the patrol path after pausing.
    /// </summary>
    INpcContext ResumePath(float waitTime = 0f);

    #endregion

    #region Appearance

    /// <summary>
    ///     Change the NPC's sprite.
    /// </summary>
    INpcContext SetSprite(GameSpriteId spriteId);

    /// <summary>
    ///     Show or hide the NPC.
    /// </summary>
    INpcContext SetVisible(bool visible);

    /// <summary>
    ///     Show the NPC (shorthand for SetVisible(true)).
    /// </summary>
    INpcContext Show();

    /// <summary>
    ///     Hide the NPC (shorthand for SetVisible(false)).
    /// </summary>
    INpcContext Hide();

    #endregion

    #region Behavior

    /// <summary>
    ///     Change the NPC's behavior script.
    /// </summary>
    INpcContext SetBehavior(GameBehaviorId behaviorId);

    /// <summary>
    ///     Activate the NPC's behavior (starts executing OnTick).
    /// </summary>
    INpcContext ActivateBehavior();

    /// <summary>
    ///     Deactivate the NPC's behavior (pauses OnTick execution).
    /// </summary>
    INpcContext DeactivateBehavior();

    #endregion
}
