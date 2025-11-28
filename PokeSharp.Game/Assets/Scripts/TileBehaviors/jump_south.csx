using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
///     Jump south behavior.
///     Allows jumping south but blocks north movement.
/// </summary>
public class JumpSouthBehavior : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(
        ScriptContext ctx,
        Direction fromDirection,
        Direction toDirection
    )
    {
        // Block movement from north (can't climb up)
        if (fromDirection == Direction.North)
            return true;

        return false;
    }

    public override bool IsBlockedTo(ScriptContext ctx, Direction toDirection)
    {
        // Block movement to north (can't enter from south)
        if (toDirection == Direction.North)
            return true;

        return false;
    }

    public override Direction GetJumpDirection(ScriptContext ctx, Direction fromDirection)
    {
        // Allow jumping south when coming from north (player moving south onto this tile)
        if (fromDirection == Direction.North)
            return Direction.South;

        return Direction.None;
    }
}

return new JumpSouthBehavior();
