using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Engine.Core.Events.Movement;
using EnhancedLedges.Events;
using Arch.Core;

/// <summary>
///     Jump Boost Item behavior - consumable that temporarily increases jump height.
///     Allows jumping 2 tiles instead of 1 when activated.
///     Demonstrates item interaction patterns and temporary effect management.
/// </summary>
/// <remarks>
///     Configuration via ItemData properties:
///     - "boost_multiplier": Jump height multiplier (default: 2.0 = double jump)
///     - "duration_seconds": Effect duration (default: 30.0 seconds)
///     - "consumable": Whether item is consumed on use (default: true)
/// </remarks>
public class JumpBoostItemBehavior : ScriptBase
{
    private const string STATE_ACTIVE_BOOSTS = "active_boosts";
    private const string STATE_BOOST_EXPIRES = "boost_expires";

    private float _boostMultiplier;
    private float _durationSeconds;
    private bool _isConsumable;

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Read configuration from ItemData properties
        _boostMultiplier = ctx.ItemData?.GetFloatProperty("boost_multiplier", 2.0f) ?? 2.0f;
        _durationSeconds = ctx.ItemData?.GetFloatProperty("duration_seconds", 30.0f) ?? 30.0f;
        _isConsumable = ctx.ItemData?.GetBoolProperty("consumable", true) ?? true;

        Context.Logger.LogInformation("Jump boost item initialized: multiplier={Multiplier}x, duration={Duration}s", _boostMultiplier, _durationSeconds);
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Handle item use/consumption
        On<ItemUsedEvent>((evt) =>
        {
            if (evt.ItemId != Context.ItemData?.ItemId)
            {
                return; // Not this item
            }

            ActivateBoost(evt.Entity);

            // Consume item if configured
            if (_isConsumable)
            {
                Context.Logger.LogDebug("Jump boost item consumed");
                // Inventory system would remove the item
            }
        });

        // Modify jump behavior when boost is active
        On<MovementStartedEvent>((evt) =>
        {
            if (!IsBoostActive(evt.Entity))
            {
                return;
            }

            // Check if this is a jump movement (onto a ledge tile)
            // Would need to query destination tile type
            var isJumping = IsJumpMovement(evt.ToX, evt.ToY);

            if (isJumping)
            {
                Context.Logger.LogDebug("Jump boost active: applying {Multiplier}x multiplier", _boostMultiplier);

                // Modify movement to jump 2 tiles instead of 1
                var extendedX = evt.ToX + GetDirectionDeltaX(evt.Direction);
                var extendedY = evt.ToY + GetDirectionDeltaY(evt.Direction);

                Context.Logger.LogInformation("Extended jump: ({FromX},{FromY}) -> ({ToX},{ToY})", evt.ToX, evt.ToY, extendedX, extendedY);

                // Note: Would need to update evt.ToX/ToY if events were mutable
                // Alternatively, publish a separate JumpExtendedEvent for systems to handle
            }
        });

    }

    private void ActivateBoost(Entity entity)
    {
        var expiresAt = DateTime.UtcNow.AddSeconds(_durationSeconds);

        // Store boost data in global state (would need entity-specific state in real implementation)
        Context.State.SetFloat($"boost_expires_{entity.Id}", (float)expiresAt.Ticks);

        Context.Logger.LogInformation("Jump boost activated for entity {EntityId}, expires at {ExpiresAt}", entity.Id, expiresAt);

        // Publish boost activation event
        Context.Events.Publish(new JumpBoostActivatedEvent
        {
            Entity = entity,
            BoostMultiplier = _boostMultiplier,
            DurationSeconds = _durationSeconds,
            BoostSource = "JumpBoostItem",
            ExpiresAt = expiresAt
        });
    }

    private bool IsBoostActive(Entity entity)
    {
        var expiresKey = $"boost_expires_{entity.Id}";
        if (!Context.State.HasKey(expiresKey))
        {
            return false;
        }

        var expiresTicks = Context.State.GetFloat(expiresKey);
        var expiresAt = new DateTime((long)expiresTicks);

        return DateTime.UtcNow < expiresAt;
    }

    private void CleanupExpiredBoosts()
    {
        // In real implementation, would iterate through all entity boost states
        // and remove expired ones to prevent memory leaks
        Context.Logger.LogDebug("Cleaning up expired boosts");
    }

    private bool IsJumpMovement(int toX, int toY)
    {
        // Would query tile at (toX, toY) to check if it's a ledge tile
        // For demonstration, assume any movement could be a jump
        return true;
    }

    private int GetDirectionDeltaX(int direction)
    {
        // Direction values: 0=South, 1=West, 2=East, 3=North
        return direction switch
        {
            1 => -1, // West
            2 => 1,  // East
            _ => 0
        };
    }

    private int GetDirectionDeltaY(int direction)
    {
        // Direction values: 0=South, 1=West, 2=East, 3=North
        return direction switch
        {
            0 => 1,  // South
            3 => -1, // North
            _ => 0
        };
    }
}

return new JumpBoostItemBehavior();
