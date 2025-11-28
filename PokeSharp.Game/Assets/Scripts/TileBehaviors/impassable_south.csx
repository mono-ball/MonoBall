using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
///     Impassable south behavior.
///     Blocks movement from south.
/// </summary>
public class ImpassableSouthBehavior : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(
        ScriptContext ctx,
        Direction fromDirection,
        Direction toDirection
    )
    {
        // Block if moving from south
        if (fromDirection == Direction.South)
            return true;

        return false;
    }

    public override bool IsBlockedTo(ScriptContext ctx, Direction toDirection)
    {
        // Block if trying to enter from south
        if (toDirection == Direction.South)
            return true;

        return false;
    }
}

return new ImpassableSouthBehavior();
