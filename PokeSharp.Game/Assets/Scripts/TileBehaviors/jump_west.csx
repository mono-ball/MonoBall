using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
///     Jump west behavior.
///     Allows jumping west but blocks east movement.
/// </summary>
public class JumpWestBehavior : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(
        ScriptContext ctx,
        Direction fromDirection,
        Direction toDirection
    )
    {
        // Block movement from east (can't climb up)
        if (fromDirection == Direction.East)
            return true;

        return false;
    }

    public override bool IsBlockedTo(ScriptContext ctx, Direction toDirection)
    {
        // Block movement to east (can't enter from west)
        if (toDirection == Direction.East)
            return true;

        return false;
    }

    public override Direction GetJumpDirection(ScriptContext ctx, Direction fromDirection)
    {
        // Allow jumping west when coming from east (player moving west onto this tile)
        if (fromDirection == Direction.East)
            return Direction.West;

        return Direction.None;
    }
}

return new JumpWestBehavior();
