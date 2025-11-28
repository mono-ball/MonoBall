using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
///     Ice tile behavior.
///     Forces sliding movement in the current direction.
/// </summary>
public class IceBehavior : TileBehaviorScriptBase
{
    public override Direction GetForcedMovement(ScriptContext ctx, Direction currentDirection)
    {
        // Continue sliding in current direction
        if (currentDirection != Direction.None)
            return currentDirection;

        return Direction.None;
    }

    public override bool AllowsRunning(ScriptContext ctx)
    {
        // Can't run on ice
        return false;
    }
}

return new IceBehavior();
