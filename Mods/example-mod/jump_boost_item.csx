/*
 * Example mod script: Jump Boost Item
 * Adds an item that temporarily allows jumping over multiple tiles.
 */

using MonoBallFramework.Engine.Core.Events;
using MonoBallFramework.Game.Scripting.Runtime;

public class JumpBoostItemScript : ScriptBase
{
    private bool isJumpBoostActive = false;
    private const int BoostDurationSeconds = 10;

    public override void RegisterEventHandlers(ScriptContext context)
    {
        // Subscribe to item use events
        context.EventBus.Subscribe<ItemUsedEvent>(OnItemUsed);
        context.Logger.LogInformation("🚀 Jump Boost Item mod loaded");
    }

    private void OnItemUsed(ItemUsedEvent evt)
    {
        if (evt.ItemId == "jump-boost")
        {
            ActivateJumpBoost();
        }
    }

    private void ActivateJumpBoost()
    {
        if (isJumpBoostActive)
        {
            Context.Logger.LogInformation("⚠️  Jump Boost already active");
            return;
        }

        isJumpBoostActive = true;
        Context.Logger.LogInformation(
            $"🚀 Jump Boost activated for {BoostDurationSeconds} seconds!"
        );

        // Play activation sound
        Context.Api.Audio.PlaySound("power_up");

        // Start timer to deactivate
        _ = Task.Run(async () =>
        {
            await Task.Delay(BoostDurationSeconds * 1000);
            DeactivateJumpBoost();
        });
    }

    private void DeactivateJumpBoost()
    {
        isJumpBoostActive = false;
        Context.Logger.LogInformation("🔵 Jump Boost deactivated");
        Context.Api.Audio.PlaySound("power_down");
    }

    public override void OnUnload()
    {
        Context?.Logger.LogInformation("🚀 Jump Boost Item mod unloaded");
    }
}

// Event placeholder (would be defined in core engine)
public record ItemUsedEvent(string ItemId) : IEvent;

return new JumpBoostItemScript();
