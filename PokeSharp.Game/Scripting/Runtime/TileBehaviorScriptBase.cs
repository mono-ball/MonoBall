using Arch.Core;
using PokeSharp.Game.Components.Movement;

namespace PokeSharp.Game.Scripting.Runtime;

/// <summary>
///     Base class for tile behavior scripts.
///     Provides hooks for collision checking, forced movement, and interactions.
/// </summary>
/// <remarks>
///     <para>
///         All tile behavior .csx scripts should define a class that inherits from TileBehaviorScriptBase.
///         The class will be instantiated once and reused across all tiles with the same behavior type.
///     </para>
///     <para>
///         <strong>IMPORTANT:</strong> Scripts are stateless! DO NOT use instance fields or properties.
///         Use <c>ctx.GetState&lt;T&gt;()</c> and <c>ctx.SetState&lt;T&gt;()</c> for persistent data.
///     </para>
/// </remarks>
public abstract class TileBehaviorScriptBase : TypeScriptBase
{
    /// <summary>
    ///     Called when checking if movement is blocked from a direction.
    ///     Return true to block movement, false to allow.
    /// </summary>
    /// <param name="ctx">Script context</param>
    /// <param name="fromDirection">Direction entity is moving from</param>
    /// <param name="toDirection">Direction entity wants to move to</param>
    /// <returns>True if movement is blocked, false if allowed</returns>
    public virtual bool IsBlockedFrom(
        ScriptContext ctx,
        Direction fromDirection,
        Direction toDirection
    )
    {
        return false; // Default: allow movement
    }

    /// <summary>
    ///     Called when checking if movement is blocked to a direction.
    ///     Return true to block movement, false to allow.
    /// </summary>
    /// <param name="ctx">Script context</param>
    /// <param name="toDirection">Direction entity wants to move to</param>
    /// <returns>True if movement is blocked, false if allowed</returns>
    public virtual bool IsBlockedTo(ScriptContext ctx, Direction toDirection)
    {
        return false; // Default: allow movement
    }

    /// <summary>
    ///     Called to check if this tile forces movement.
    ///     Return the direction to force movement, or Direction.None for no forced movement.
    /// </summary>
    /// <param name="ctx">Script context</param>
    /// <param name="currentDirection">Current movement direction</param>
    /// <returns>Direction to force movement, or Direction.None</returns>
    public virtual Direction GetForcedMovement(ScriptContext ctx, Direction currentDirection)
    {
        return Direction.None; // Default: no forced movement
    }

    /// <summary>
    ///     Called when checking if this tile allows jumping (ledges).
    ///     Return the jump direction if allowed, or Direction.None.
    /// </summary>
    /// <param name="ctx">Script context</param>
    /// <param name="fromDirection">Direction entity is moving from</param>
    /// <returns>Jump direction if allowed, or Direction.None</returns>
    public virtual Direction GetJumpDirection(ScriptContext ctx, Direction fromDirection)
    {
        return Direction.None; // Default: no jumping
    }

    /// <summary>
    ///     Called when checking if this tile requires special movement mode (surf, dive).
    ///     Returns the required movement mode name, or null if no special mode required.
    /// </summary>
    /// <param name="ctx">Script context</param>
    /// <returns>Required movement mode name (e.g., "surf", "dive"), or null</returns>
    public virtual string? GetRequiredMovementMode(ScriptContext ctx)
    {
        return null; // Default: no special mode required
    }

    /// <summary>
    ///     Called when checking if running is allowed on this tile.
    /// </summary>
    /// <param name="ctx">Script context</param>
    /// <returns>True if running is allowed, false if disabled</returns>
    public virtual bool AllowsRunning(ScriptContext ctx)
    {
        return true; // Default: allow running
    }

    /// <summary>
    ///     Called when entity steps onto this tile.
    ///     Use for per-step effects (ice cracking, ash gathering, etc.).
    /// </summary>
    /// <param name="ctx">Script context</param>
    /// <param name="entity">Entity that stepped on tile</param>
    public virtual void OnStep(ScriptContext ctx, Entity entity) { }
}
