using Arch.Core;
using MonoBallFramework.Engine.Core.Events;
using MonoBallFramework.Game.Scripting.Runtime;

/// <summary>
/// Template for creating custom event types and event-driven mod communication.
///
/// INSTRUCTIONS:
/// 1. Copy this file to your mod's Scripts folder
/// 2. Define your custom event types (see examples below)
/// 3. Create publishers (scripts that raise events)
/// 4. Create subscribers (scripts that listen to events)
/// 5. Use events for inter-mod communication
///
/// COMMON USE CASES:
/// - Quest systems (QuestCompletedEvent, QuestProgressEvent)
/// - Custom battles (CustomBattleStartEvent, CustomBattleEndEvent)
/// - Time/weather systems (DayNightChangeEvent, WeatherChangeEvent)
/// - Economy systems (ShopPurchaseEvent, MoneyChangedEvent)
/// - Custom mechanics (ComboChainEvent, AchievementUnlockedEvent)
/// - Mod-to-mod communication (CrossModEvent)
/// </summary>
// ============================================================================
// STEP 1: DEFINE CUSTOM EVENT TYPES
// ============================================================================
// Events must implement IGameEvent (basic) or ICancellableEvent (can be blocked)

/// <summary>
/// Example: Basic event that cannot be cancelled.
/// Use for notifications and post-action events.
/// </summary>
public sealed record CustomNotificationEvent : IGameEvent
{
    /// <summary>
    /// Unique identifier for this event instance (auto-generated).
    /// </summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// When the event was created (auto-generated).
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    // TODO: Add your custom properties
    /// <summary>
    /// Example: The entity that triggered this event.
    /// </summary>
    public required Entity SourceEntity { get; init; }

    /// <summary>
    /// Example: Custom message or data.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Example: Optional numeric data.
    /// </summary>
    public int Value { get; init; }
}

/// <summary>
/// Example: Cancellable event for validation and prevention.
/// Use for pre-action events where you want to allow blocking.
/// </summary>
public sealed record CustomActionEvent : ICancellableEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    // TODO: Add your custom properties
    /// <summary>
    /// Example: The entity performing the action.
    /// </summary>
    public required Entity ActorEntity { get; init; }

    /// <summary>
    /// Example: The target of the action.
    /// </summary>
    public Entity TargetEntity { get; init; }

    /// <summary>
    /// Example: The type of action being performed.
    /// </summary>
    public required string ActionType { get; init; }

    /// <summary>
    /// Example: Additional parameters for the action.
    /// </summary>
    public Dictionary<string, object>? Parameters { get; init; }

    // ICancellableEvent implementation
    /// <inheritdoc />
    public bool IsCancelled { get; private set; }

    /// <inheritdoc />
    public string? CancellationReason { get; private set; }

    /// <inheritdoc />
    public void PreventDefault(string? reason = null)
    {
        IsCancelled = true;
        CancellationReason = reason ?? "Action prevented";
    }
}

/// <summary>
/// Example: Entity-specific event (implements IEntityEvent).
/// Use for events that are filtered by entity.
/// </summary>
public sealed record CustomEntityEvent : IEntityEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <inheritdoc />
    public required Entity Entity { get; init; }

    // TODO: Add entity-specific properties
    /// <summary>
    /// Example: State change information.
    /// </summary>
    public required string StateChange { get; init; }
}

/// <summary>
/// Example: Tile-specific event (implements ITileEvent).
/// Use for events that occur at specific tile coordinates.
/// </summary>
public sealed record CustomTileEvent : ITileEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <inheritdoc />
    public required int TileX { get; init; }

    /// <inheritdoc />
    public required int TileY { get; init; }

    // TODO: Add tile-specific properties
    /// <summary>
    /// Example: The tile effect that occurred.
    /// </summary>
    public required string EffectType { get; init; }
}

// ============================================================================
// STEP 2: CREATE EVENT PUBLISHER SCRIPT
// ============================================================================
// This script publishes (raises) custom events

/// <summary>
/// Example publisher script that raises custom events based on game conditions.
/// </summary>
public class CustomEventPublisher : ScriptBase
{
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // TODO: Initialize publisher state
        // Set("event_cooldown", 0f);
        // Set("event_counter", 0);

        ctx.Logger.LogInformation("CustomEventPublisher initialized");
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Example: Publish event in response to game events
        On<TickEvent>(evt =>
        {
            // TODO: Add your event publishing logic

            // Example 1: Publish notification event every 10 seconds
            /*
            var timeSinceLastEvent = Get<float>("time_since_event", 0f);
            timeSinceLastEvent += evt.DeltaTime;

            if (timeSinceLastEvent >= 10.0f)
            {
                var notificationEvent = new CustomNotificationEvent
                {
                    SourceEntity = Context.Entity.Value,
                    Message = "10 seconds have passed!",
                    Value = Get<int>("event_counter", 0)
                };

                Publish(notificationEvent);

                Set("time_since_event", 0f);
                Set("event_counter", Get<int>("event_counter", 0) + 1);

                Context.Logger.LogInformation("Published CustomNotificationEvent #{Counter}",
                    Get<int>("event_counter", 0));
            }
            else
            {
                Set("time_since_event", timeSinceLastEvent);
            }
            */

            // Example 2: Publish cancellable event before action
            /*
            if (ShouldPerformAction())
            {
                var actionEvent = new CustomActionEvent
                {
                    ActorEntity = Context.Entity.Value,
                    TargetEntity = GetTargetEntity(),
                    ActionType = "custom_action",
                    Parameters = new Dictionary<string, object>
                    {
                        { "power", 100 },
                        { "accuracy", 0.95f }
                    }
                };

                Publish(actionEvent);

                // Check if any subscriber cancelled the event
                if (!actionEvent.IsCancelled)
                {
                    PerformAction();
                    Context.Logger.LogInformation("Action performed successfully");
                }
                else
                {
                    Context.Logger.LogInformation("Action cancelled: {Reason}",
                        actionEvent.CancellationReason);
                }
            }
            */

            // Example 3: Publish entity-specific event
            /*
            if (EntityStateChanged())
            {
                var entityEvent = new CustomEntityEvent
                {
                    Entity = Context.Entity.Value,
                    StateChange = "new_state"
                };

                Publish(entityEvent);

                Context.Logger.LogInformation("Entity state changed");
            }
            */

            // Example 4: Publish tile-specific event
            /*
            ref var position = ref Context.Position;
            if (TileEffectOccurred())
            {
                var tileEvent = new CustomTileEvent
                {
                    TileX = position.X,
                    TileY = position.Y,
                    EffectType = "custom_effect"
                };

                Publish(tileEvent);

                Context.Logger.LogInformation("Tile effect at ({X}, {Y})", position.X, position.Y);
            }
            */
        });
    }

    // TODO: Helper methods for publishing logic
    private bool ShouldPerformAction()
    {
        // TODO: Implement action condition logic
        return false;
    }

    private Entity GetTargetEntity()
    {
        // TODO: Get target entity
        return Entity.Null;
    }

    private void PerformAction()
    {
        // TODO: Implement the action
    }

    private bool EntityStateChanged()
    {
        // TODO: Check if entity state changed
        return false;
    }

    private bool TileEffectOccurred()
    {
        // TODO: Check if tile effect occurred
        return false;
    }
}

// ============================================================================
// STEP 3: CREATE EVENT SUBSCRIBER SCRIPT
// ============================================================================
// This script listens to and responds to custom events

/// <summary>
/// Example subscriber script that listens to custom events and reacts.
/// </summary>
public class CustomEventSubscriber : ScriptBase
{
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // TODO: Initialize subscriber state
        // Set("events_received", 0);

        ctx.Logger.LogInformation("CustomEventSubscriber initialized");
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // ====================================================================
        // SUBSCRIBE TO BASIC EVENTS
        // ====================================================================

        // Example 1: Subscribe to notification event
        On<CustomNotificationEvent>(evt =>
        {
            Context.Logger.LogInformation(
                "Received notification from entity {Entity}: {Message} (Value: {Value})",
                evt.SourceEntity.Id,
                evt.Message,
                evt.Value
            );

            // TODO: React to the notification
            // Example: Update UI, play sound, trigger animation, etc.

            var eventsReceived = Get<int>("events_received", 0);
            Set("events_received", eventsReceived + 1);
        });

        // Example 2: Subscribe to cancellable event (can prevent action)
        On<CustomActionEvent>(
            evt =>
            {
                Context.Logger.LogInformation(
                    "Entity {Actor} attempting action: {Action} on entity {Target}",
                    evt.ActorEntity.Id,
                    evt.ActionType,
                    evt.TargetEntity.Id
                );

                // TODO: Add validation logic
                // Example: Prevent action under certain conditions
                /*
                if (ShouldBlockAction(evt))
                {
                    evt.PreventDefault("Action blocked by custom logic");
                    Context.Logger.LogInformation("Blocked action: {Action}", evt.ActionType);
                    return;
                }
                */

                // If not blocked, react to the action
                // TODO: Update state, trigger effects, etc.
            },
            priority: 1000
        ); // High priority for validation

        // ====================================================================
        // SUBSCRIBE TO FILTERED EVENTS
        // ====================================================================

        // Example 3: Subscribe to entity-specific events
        // Only receive events for this specific entity
        /*
        if (Context.Entity.HasValue)
        {
            OnEntity<CustomEntityEvent>(Context.Entity.Value, evt =>
            {
                Context.Logger.LogInformation(
                    "Our entity changed state: {State}",
                    evt.StateChange
                );

                // TODO: React to entity state change
            });
        }
        */

        // Example 4: Subscribe to tile-specific events
        // Only receive events at specific coordinates
        /*
        var watchedTilePosition = new Vector2(10, 15);
        OnTile<CustomTileEvent>(watchedTilePosition, evt =>
        {
            Context.Logger.LogInformation(
                "Tile effect occurred at watched position ({X}, {Y}): {Effect}",
                evt.TileX,
                evt.TileY,
                evt.EffectType
            );

            // TODO: React to tile effect at this position
        });
        */

        // ====================================================================
        // INTER-MOD COMMUNICATION
        // ====================================================================

        // Example 5: Listen for events from other mods
        // Events are global, so any mod can publish and any mod can subscribe
        On<CustomNotificationEvent>(evt =>
        {
            // Check if this event is from another mod
            // You can use message prefixes or custom properties to identify source mod
            if (evt.Message.StartsWith("ModA:"))
            {
                Context.Logger.LogInformation("Received event from ModA: {Message}", evt.Message);

                // TODO: Respond to other mod's event
                // Example: Update shared state, trigger coordinated actions
            }
        });
    }

    // TODO: Helper methods for subscriber logic
    private bool ShouldBlockAction(CustomActionEvent evt)
    {
        // TODO: Implement blocking logic
        // Examples:
        // - Check permissions
        // - Validate target
        // - Check cooldowns
        // - Check resource availability

        return false; // Placeholder
    }
}

// ============================================================================
// STEP 4: INTER-MOD COMMUNICATION PATTERN
// ============================================================================

/// <summary>
/// Example: Mod API event for cross-mod communication.
/// Other mods can subscribe to these events to integrate with your mod.
/// </summary>
public sealed record ModIntegrationEvent : IGameEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// The mod that published this event.
    /// </summary>
    public required string SourceModId { get; init; }

    /// <summary>
    /// The type of integration event.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Data payload for the event (use for mod-specific information).
    /// </summary>
    public Dictionary<string, object>? Data { get; init; }
}

/// <summary>
/// Example: Script that provides an API for other mods via events.
/// </summary>
public class ModApiProvider : ScriptBase
{
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);
        ctx.Logger.LogInformation("ModApiProvider initialized");
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Publish API events that other mods can subscribe to
        On<TickEvent>(evt =>
        {
            // TODO: Publish API events based on your mod's state

            // Example: Publish mod state updates
            /*
            var stateChangeEvent = new ModIntegrationEvent
            {
                SourceModId = "your_mod_id",
                EventType = "state_update",
                Data = new Dictionary<string, object>
                {
                    { "current_state", "active" },
                    { "data_version", 1 }
                }
            };

            Publish(stateChangeEvent);
            */
        });
    }
}

/// <summary>
/// Example: Script that consumes API events from other mods.
/// </summary>
public class ModApiConsumer : ScriptBase
{
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);
        ctx.Logger.LogInformation("ModApiConsumer initialized");
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Subscribe to API events from other mods
        On<ModIntegrationEvent>(evt =>
        {
            Context.Logger.LogInformation(
                "Received integration event from {ModId}: {EventType}",
                evt.SourceModId,
                evt.EventType
            );

            // TODO: React to other mod's API events
            // Example: Update state, trigger actions, coordinate features

            if (evt.EventType == "state_update" && evt.Data != null)
            {
                // Process the data from the other mod
                var currentState = evt.Data.GetValueOrDefault("current_state") as string;
                Context.Logger.LogInformation("Other mod state: {State}", currentState);
            }
        });
    }
}

// ============================================================================
// USAGE NOTES
// ============================================================================
/*
 * BEST PRACTICES:
 *
 * 1. Event Naming:
 *    - Use descriptive names ending in "Event"
 *    - Use past tense for completed actions (PlayerMovedEvent)
 *    - Use present tense for ongoing actions (MovementStartedEvent)
 *
 * 2. Event Properties:
 *    - Use 'required' for essential properties
 *    - Use optional properties for additional context
 *    - Include Entity references when applicable
 *    - Add timestamp and EventId (auto-generated)
 *
 * 3. Cancellable Events:
 *    - Use for pre-action validation
 *    - Always check IsCancelled after publishing
 *    - Provide clear cancellation reasons
 *    - Use high priority for validation handlers
 *
 * 4. Performance:
 *    - Don't publish events every frame unless necessary
 *    - Use filtered subscriptions (OnEntity, OnTile) when possible
 *    - Keep event handlers fast and simple
 *    - Use state components instead of events for continuous data
 *
 * 5. Inter-Mod Communication:
 *    - Use a consistent mod ID prefix in events
 *    - Document your mod's events for others to use
 *    - Handle missing events gracefully (other mod not loaded)
 *    - Version your event data structures
 */

// IMPORTANT: Return an instance of your script class
// Uncomment ONE of these based on what you're creating:
// return new CustomEventPublisher();
// return new CustomEventSubscriber();
return new ModApiProvider();
