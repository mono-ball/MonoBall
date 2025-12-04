/*
 * Example mod script: Crumbling Ledges
 * Makes ledges crumble after being jumped over multiple times.
 */

using MonoBallFramework.Engine.Core.Events;
using MonoBallFramework.Game.Scripting.Runtime;

public class CrumblingLedgeScript : ScriptBase
{
    private Dictionary<(int x, int y), int> ledgeJumpCounts = new();
    private const int MaxJumps = 3;

    public override void RegisterEventHandlers(ScriptContext context)
    {
        // Subscribe to ledge jump events
        context.EventBus.Subscribe<LedgeJumpedEvent>(OnLedgeJumped);
        context.Logger.LogInformation("🪨 Crumbling Ledge mod loaded");
    }

    private void OnLedgeJumped(LedgeJumpedEvent evt)
    {
        var position = (evt.X, evt.Y);

        if (!ledgeJumpCounts.ContainsKey(position))
        {
            ledgeJumpCounts[position] = 0;
        }

        ledgeJumpCounts[position]++;
        int jumps = ledgeJumpCounts[position];

        Context.Logger.LogInformation(
            $"Ledge at ({evt.X}, {evt.Y}) jumped {jumps}/{MaxJumps} times"
        );

        if (jumps >= MaxJumps)
        {
            // Play crumble effect
            Context.Api.Audio.PlaySound("crumble");
            Context.Logger.LogInformation($"💥 Ledge at ({evt.X}, {evt.Y}) crumbled!");

            // Remove the ledge tile
            // Context.Api.World.SetTile(evt.X, evt.Y, TileType.Ground);

            ledgeJumpCounts.Remove(position);
        }
    }

    public override void OnUnload()
    {
        Context?.Logger.LogInformation("🪨 Crumbling Ledge mod unloaded");
    }
}

// Event placeholder (would be defined in core engine)
public record LedgeJumpedEvent(int X, int Y) : IEvent;

return new CrumblingLedgeScript();
