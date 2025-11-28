using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
///     Impassable north behavior.
///     Blocks movement from north.
/// </summary>
public class ImpassableNorthBehavior : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(
        ScriptContext ctx,
        Direction fromDirection,
        Direction toDirection
    )
    {
        // Block if moving from north
        if (fromDirection == Direction.North)
            return true;

        return false;
    }

    public override bool IsBlockedTo(ScriptContext ctx, Direction toDirection)
    {
        // Block if trying to enter from north
        if (toDirection == Direction.North)
            return true;

        return false;
    }
}

return new ImpassableNorthBehavior();
