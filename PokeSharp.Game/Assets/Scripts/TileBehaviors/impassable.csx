using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
///     Impassable east behavior.
///     Blocks movement from east.
/// </summary>
public class ImpassableBehavior : TileBehaviorScriptBase
{
    public override bool IsBlockedFrom(
        ScriptContext ctx,
        Direction fromDirection,
        Direction toDirection
    )
    {
        return true;
    }

    public override bool IsBlockedTo(ScriptContext ctx, Direction toDirection)
    {
        return true;
    }
}

return new ImpassableBehavior();
