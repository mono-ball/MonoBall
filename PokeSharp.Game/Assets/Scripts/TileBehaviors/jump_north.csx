using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
///     Jump north behavior.
///     Allows jumping north but blocks south movement.
/// </summary>
public class JumpNorthBehavior : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(
        ScriptContext ctx,
        Direction fromDirection,
        Direction toDirection
    )
    {
        // Block movement from south (can't climb up)
        if (fromDirection == Direction.South)
            return true;

        return false;
    }

    public override bool IsBlockedTo(ScriptContext ctx, Direction toDirection)
    {
        // Block movement to south (can't enter from north)
        if (toDirection == Direction.South)
            return true;

        return false;
    }

    public override Direction GetJumpDirection(ScriptContext ctx, Direction fromDirection)
    {
        // Allow jumping north when coming from south (player moving north onto this tile)
        if (fromDirection == Direction.South)
            return Direction.North;

        return Direction.None;
    }
}

return new JumpNorthBehavior();
