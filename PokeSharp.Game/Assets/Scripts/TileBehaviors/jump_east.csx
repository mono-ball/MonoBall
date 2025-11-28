using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
///     Jump east behavior.
///     Allows jumping east but blocks west movement.
/// </summary>
public class JumpEastBehavior : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(
        ScriptContext ctx,
        Direction fromDirection,
        Direction toDirection
    )
    {
        // Block movement from west (can't climb up)
        if (fromDirection == Direction.West)
            return true;

        return false;
    }

    public override bool IsBlockedTo(ScriptContext ctx, Direction toDirection)
    {
        // Block movement to west (can't enter from east)
        if (toDirection == Direction.West)
            return true;

        return false;
    }

    public override Direction GetJumpDirection(ScriptContext ctx, Direction fromDirection)
    {
        // Allow jumping east when coming from west (player moving east onto this tile)
        if (fromDirection == Direction.West)
            return Direction.East;

        return Direction.None;
    }
}

return new JumpEastBehavior();
