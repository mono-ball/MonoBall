using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
///     Impassable east behavior.
///     Blocks movement from east.
/// </summary>
public class ImpassableEastBehavior : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(
        ScriptContext ctx,
        Direction fromDirection,
        Direction toDirection
    )
    {
        // Block if moving from east
        if (fromDirection == Direction.East)
            return true;

        return false;
    }

    public override bool IsBlockedTo(ScriptContext ctx, Direction toDirection)
    {
        // Block if trying to enter from east
        if (toDirection == Direction.East)
            return true;

        return false;
    }
}

return new ImpassableEastBehavior();
