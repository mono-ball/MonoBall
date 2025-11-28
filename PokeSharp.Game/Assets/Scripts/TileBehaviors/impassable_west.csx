using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
///     Impassable west behavior.
///     Blocks movement from west.
/// </summary>
public class ImpassableWestBehavior : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(
        ScriptContext ctx,
        Direction fromDirection,
        Direction toDirection
    )
    {
        // Block if moving from west
        if (fromDirection == Direction.West)
            return true;

        return false;
    }

    public override bool IsBlockedTo(ScriptContext ctx, Direction toDirection)
    {
        // Block if trying to enter from west
        if (toDirection == Direction.West)
            return true;

        return false;
    }
}

return new ImpassableWestBehavior();
