// Ice Tile Script - Example Test Mod
// Triggers forced sliding movement when player steps on ice tiles

using MonoBallFramework.Engine.Core.Events.Tile;
using MonoBallFramework.Game.Scripting.Runtime;

public class IceScript : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Subscribe to tile step events
        On<TileSteppedOnEvent>(
            evt =>
            {
                if (evt.TileType == "ice")
                {
                    Context.Logger?.LogInformation(
                        "[IceScript] Player stepped on ice at ({X}, {Y})",
                        evt.TileX,
                        evt.TileY
                    );

                    // Simulate forced sliding
                    var slideDirection = DetermineSlideDirection(evt.FromDirection);
                    Context.Logger?.LogDebug("[IceScript] Sliding {Direction}", slideDirection);

                    // In real implementation, would trigger MovementStartedEvent here
                }
            },
            priority: 500
        );
    }

    private string DetermineSlideDirection(int fromDirection)
    {
        return fromDirection switch
        {
            0 => "North", // Came from South, slide North
            1 => "East", // Came from West, slide East
            2 => "West", // Came from East, slide West
            3 => "South", // Came from North, slide South
            _ => "North",
        };
    }
}
