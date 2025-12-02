using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Core.Events;

namespace PokeSharp.Game.Scripting.Runtime;

/// <summary>
///     Unified base class for modular, event-driven scripts supporting composition and custom events.
///     This is the modern alternative to <see cref="TypeScriptBase" />, designed for Phase 3+ features.
/// </summary>
/// <remarks>
///     <para>
///         ScriptBase is the foundation for the Phase 3 modding platform, enabling:
///         - Event-driven architecture with custom event publishing
///         - Entity and tile-filtered event subscriptions
///         - Composition of multiple scripts on a single entity (Phase 3.2+)
///         - State management via ECS components
///         - Automatic subscription cleanup on unload
///     </para>
///     <para>
///         <strong>Key Differences from TypeScriptBase:</strong>
///         - Context is initialized once, not passed to every method
///         - Supports custom event publishing via <see cref="Publish{TEvent}" />
///         - Supports entity/tile-filtered subscriptions via <see cref="OnEntity{TEvent}" /> and
///         <see cref="OnTile{TEvent}" />
///         - Designed for composition (multiple scripts per entity)
///     </para>
///     <para>
///         <strong>Lifecycle:</strong>
///         1. <see cref="Initialize" /> - Set up context and initialize state
///         2. <see cref="RegisterEventHandlers" /> - Subscribe to game events
///         3. (Script runs, handlers execute as events fire)
///         4. <see cref="OnUnload" /> - Clean up subscriptions and resources
///     </para>
/// </remarks>
/// <example>
///     Simple event-driven script:
///     <code>
/// public class TallGrassScript : ScriptBase
/// {
///     public override void RegisterEventHandlers(ScriptContext ctx)
///     {
///         // Subscribe to tile step events
///         On&lt;TileSteppedOnEvent&gt;(evt =>
///         {
///             if (evt.TileType == "tall_grass")
///             {
///                 TriggerRandomEncounter();
///             }
///         });
///     }
///
///     private void TriggerRandomEncounter()
///     {
///         var encounterRate = Get&lt;float&gt;("encounter_rate", 0.1f);
///         if (Random.Shared.NextDouble() &lt; encounterRate)
///         {
///             Context.Logger.LogInformation("Wild Pokemon appeared!");
///             Publish(new WildEncounterEvent { Entity = Context.Entity.Value });
///         }
///     }
/// }
/// </code>
/// </example>
/// <example>
///     Entity-filtered event subscription:
///     <code>
/// public class PlayerTrackerScript : ScriptBase
/// {
///     public override void RegisterEventHandlers(ScriptContext ctx)
///     {
///         var playerEntity = ctx.Player.GetPlayerEntity();
///
///         // Only receive movement events for the player
///         OnEntity&lt;MovementCompletedEvent&gt;(playerEntity, evt =>
///         {
///             Context.Logger.LogInformation("Player moved to ({X}, {Y})",
///                 evt.CurrentX, evt.CurrentY);
///         });
///     }
/// }
/// </code>
/// </example>
public abstract class ScriptBase
{
    // Track event subscriptions for automatic cleanup
    private readonly List<IDisposable> subscriptions = new();

    /// <summary>
    ///     Gets the script execution context providing access to World, Entity, Logger, Events, and APIs.
    /// </summary>
    /// <remarks>
    ///     This property is set during <see cref="Initialize" /> and should not be accessed before then.
    ///     The context provides access to all game systems and APIs needed by scripts.
    /// </remarks>
    protected ScriptContext Context { get; private set; } = null!;

    // ============================================================================
    // Lifecycle Methods
    // ============================================================================

    /// <summary>
    ///     Called once when the script is loaded and attached to an entity or registered globally.
    ///     Override to initialize the script's context and set up initial state.
    /// </summary>
    /// <param name="ctx">
    ///     The script execution context providing access to World, Entity, Logger, Events, and APIs.
    /// </param>
    /// <remarks>
    ///     <para>
    ///         This is the first lifecycle method called. The context is stored internally and
    ///         made available via the <see cref="Context" /> property.
    ///     </para>
    ///     <para>
    ///         Use this method to:
    ///         - Initialize state via <see cref="Set{T}" />
    ///         - Cache entity references
    ///         - Validate context (e.g., check if required components exist)
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    /// public override void Initialize(ScriptContext ctx)
    /// {
    ///     base.Initialize(ctx);
    ///
    ///     // Initialize state
    ///     Set("counter", 0);
    ///
    ///     // Log initialization
    ///     Context.Logger.LogInformation("Script initialized for entity {EntityId}",
    ///         ctx.Entity?.Id ?? 0);
    /// }
    /// </code>
    /// </example>
    public virtual void Initialize(ScriptContext ctx)
    {
        Context = ctx ?? throw new ArgumentNullException(nameof(ctx));
    }

    /// <summary>
    ///     Called after <see cref="Initialize" /> to register event handlers.
    ///     Override to subscribe to game events using <see cref="On{TEvent}" />,
    ///     <see cref="OnEntity{TEvent}" />, or <see cref="OnTile{TEvent}" />.
    /// </summary>
    /// <param name="ctx">
    ///     The script execution context (same as <see cref="Context" /> property).
    /// </param>
    /// <remarks>
    ///     <para>
    ///         This method is called after Initialize but before any events are published.
    ///         All event subscriptions should be registered here to ensure they're active
    ///         when the script starts receiving events.
    ///     </para>
    ///     <para>
    ///         Event subscriptions are automatically tracked and cleaned up when
    ///         <see cref="OnUnload" /> is called.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    /// public override void RegisterEventHandlers(ScriptContext ctx)
    /// {
    ///     // Subscribe to movement events with high priority
    ///     On&lt;MovementStartedEvent&gt;(evt =>
    ///     {
    ///         if (ShouldBlockMovement(evt))
    ///         {
    ///             evt.PreventDefault("Movement blocked by script");
    ///         }
    ///     }, priority: 1000);
    ///
    ///     // Subscribe to tile events at specific position
    ///     OnTile&lt;TileSteppedOnEvent&gt;(new Vector2(10, 15), evt =>
    ///     {
    ///         Context.Logger.LogInformation("Player stepped on special tile!");
    ///     });
    /// }
    /// </code>
    /// </example>
    public virtual void RegisterEventHandlers(ScriptContext ctx)
    {
        // Default: no event handlers
        // Override to add event subscriptions
    }

    /// <summary>
    ///     Called when the script is being unloaded or reloaded.
    ///     Automatically disposes all tracked event subscriptions.
    ///     Override to add custom cleanup logic.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method is called when:
    ///         - The script is hot-reloaded during development
    ///         - The entity the script is attached to is destroyed
    ///         - The script is manually unloaded
    ///     </para>
    ///     <para>
    ///         All event subscriptions registered via <see cref="On{TEvent}" />,
    ///         <see cref="OnEntity{TEvent}" />, or <see cref="OnTile{TEvent}" />
    ///         are automatically disposed. You only need to override this if you have
    ///         additional resources to clean up.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    /// public override void OnUnload()
    /// {
    ///     // Clean up custom resources
    ///     _customResource?.Dispose();
    ///
    ///     // Base class handles event subscription cleanup
    ///     base.OnUnload();
    /// }
    /// </code>
    /// </example>
    public virtual void OnUnload()
    {
        // Dispose all tracked event subscriptions
        foreach (var subscription in subscriptions)
        {
            subscription?.Dispose();
        }

        subscriptions.Clear();
    }

    // ============================================================================
    // Event Subscription Methods
    // ============================================================================

    /// <summary>
    ///     Subscribes to a game event with optional priority.
    ///     The subscription is automatically tracked and cleaned up on <see cref="OnUnload" />.
    /// </summary>
    /// <typeparam name="TEvent">The event type to subscribe to (must implement <see cref="IGameEvent" />).</typeparam>
    /// <param name="handler">The handler to invoke when the event is published.</param>
    /// <param name="priority">
    ///     The priority of this handler (higher numbers execute first).
    ///     Default is 500. Use higher values for critical handlers (e.g., validation),
    ///     lower values for logging/analytics.
    /// </param>
    /// <remarks>
    ///     <para>
    ///         Priority determines handler execution order:
    ///         - 1000+: Validation and anti-cheat systems
    ///         - 500: Normal game logic (default)
    ///         - 0: Post-processing and effects
    ///         - -1000: Logging and analytics
    ///     </para>
    ///     <para>
    ///         <strong>NOTE:</strong> Priority is currently accepted but not fully implemented
    ///         in EventBus. All handlers execute in registration order until EventBus is upgraded.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    /// // Subscribe to movement events
    /// On&lt;MovementStartedEvent&gt;(evt =>
    /// {
    ///     Context.Logger.LogInformation("Entity {Id} moving to ({X}, {Y})",
    ///         evt.Entity.Id, evt.ToX, evt.ToY);
    /// });
    ///
    /// // Subscribe with high priority to validate movement
    /// On&lt;MovementStartedEvent&gt;(evt =>
    /// {
    ///     if (IsInvalidMove(evt))
    ///     {
    ///         evt.PreventDefault("Invalid movement detected");
    ///     }
    /// }, priority: 1000);
    /// </code>
    /// </example>
    protected void On<TEvent>(Action<TEvent> handler, int priority = 500)
        where TEvent : class, IGameEvent
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        if (Context?.Events == null)
        {
            Context?.Logger?.LogWarning(
                "Cannot subscribe to {EventType}: Events system not available in ScriptContext",
                typeof(TEvent).Name
            );
            return;
        }

        // Subscribe and track for cleanup
        var subscription = Context.Events.Subscribe(handler);
        subscriptions.Add(subscription);

        Context.Logger?.LogDebug(
            "Subscribed to {EventType} with priority {Priority}",
            typeof(TEvent).Name,
            priority
        );
    }

    /// <summary>
    ///     Subscribes to a game event filtered by entity.
    ///     Only events where <c>evt.Entity == entity</c> will be passed to the handler.
    /// </summary>
    /// <typeparam name="TEvent">
    ///     The event type to subscribe to (must implement <see cref="IEntityEvent" />).
    /// </typeparam>
    /// <param name="entity">The entity to filter events by.</param>
    /// <param name="handler">The handler to invoke when the event is published for this entity.</param>
    /// <param name="priority">
    ///     The priority of this handler (higher numbers execute first). Default is 500.
    /// </param>
    /// <remarks>
    ///     <para>
    ///         This method wraps the handler with an entity ID check, ensuring the handler
    ///         only executes for events associated with the specified entity.
    ///     </para>
    ///     <para>
    ///         <strong>NOTE:</strong> The event type must implement <see cref="IEntityEvent" />.
    ///         Existing events (MovementStartedEvent, etc.) don't implement this interface yet.
    ///         Future phases will retrofit these interfaces to existing events.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    /// public override void RegisterEventHandlers(ScriptContext ctx)
    /// {
    ///     var playerEntity = ctx.Player.GetPlayerEntity();
    ///
    ///     // Only receive movement events for the player
    ///     OnEntity&lt;MovementCompletedEvent&gt;(playerEntity, evt =>
    ///     {
    ///         Context.Logger.LogInformation("Player reached ({X}, {Y})",
    ///             evt.CurrentX, evt.CurrentY);
    ///     });
    /// }
    /// </code>
    /// </example>
    protected void OnEntity<TEvent>(Entity entity, Action<TEvent> handler, int priority = 500)
        where TEvent : class, IEntityEvent
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        // Wrap handler with entity filter
        On<TEvent>(
            evt =>
            {
                if (evt.Entity == entity)
                {
                    handler(evt);
                }
            },
            priority
        );

        Context?.Logger?.LogDebug(
            "Subscribed to {EventType} for entity {EntityId}",
            typeof(TEvent).Name,
            entity.Id
        );
    }

    /// <summary>
    ///     Subscribes to a game event filtered by tile position.
    ///     Only events where <c>evt.TileX == tilePos.X &amp;&amp; evt.TileY == tilePos.Y</c>
    ///     will be passed to the handler.
    /// </summary>
    /// <typeparam name="TEvent">
    ///     The event type to subscribe to (must implement <see cref="ITileEvent" />).
    /// </typeparam>
    /// <param name="tilePos">The tile position to filter events by.</param>
    /// <param name="handler">The handler to invoke when the event is published at this tile.</param>
    /// <param name="priority">
    ///     The priority of this handler (higher numbers execute first). Default is 500.
    /// </param>
    /// <remarks>
    ///     <para>
    ///         This method wraps the handler with a tile position check, ensuring the handler
    ///         only executes for events at the specified tile coordinates.
    ///     </para>
    ///     <para>
    ///         <strong>NOTE:</strong> The event type must implement <see cref="ITileEvent" />.
    ///         Existing events (TileSteppedOnEvent, etc.) don't implement this interface yet.
    ///         Future phases will retrofit these interfaces to existing events.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    /// public override void RegisterEventHandlers(ScriptContext ctx)
    /// {
    ///     var warpTilePos = new Vector2(10, 15);
    ///
    ///     // Only receive step events at this specific tile
    ///     OnTile&lt;TileSteppedOnEvent&gt;(warpTilePos, evt =>
    ///     {
    ///         Context.Logger.LogInformation("Warping player to new map!");
    ///         Context.Map.TransitionToMap(2, 5, 5);
    ///     });
    /// }
    /// </code>
    /// </example>
    protected void OnTile<TEvent>(Vector2 tilePos, Action<TEvent> handler, int priority = 500)
        where TEvent : class, ITileEvent
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        // Wrap handler with tile position filter
        On<TEvent>(
            evt =>
            {
                if (evt.TileX == (int)tilePos.X && evt.TileY == (int)tilePos.Y)
                {
                    handler(evt);
                }
            },
            priority
        );

        Context?.Logger?.LogDebug(
            "Subscribed to {EventType} at tile ({X}, {Y})",
            typeof(TEvent).Name,
            (int)tilePos.X,
            (int)tilePos.Y
        );
    }

    // ============================================================================
    // State Management Methods
    // ============================================================================

    /// <summary>
    ///     Gets a component value from the entity's state, or returns a default value if not found.
    /// </summary>
    /// <typeparam name="T">The component type to retrieve (must be a struct).</typeparam>
    /// <param name="key">
    ///     The key parameter is currently unused (reserved for future key-based state).
    ///     State is retrieved by component type.
    /// </param>
    /// <param name="defaultValue">The value to return if the component doesn't exist.</param>
    /// <returns>The component value if it exists, otherwise <paramref name="defaultValue" />.</returns>
    /// <remarks>
    ///     <para>
    ///         <strong>CURRENT LIMITATION:</strong> This method retrieves state by component type,
    ///         not by string key. The <paramref name="key" /> parameter is accepted for API consistency
    ///         but is currently ignored. Future phases will add true key-based state storage.
    ///     </para>
    ///     <para>
    ///         This method delegates to <see cref="ScriptContext.TryGetState{T}" />.
    ///         Only works for entity scripts (not global scripts).
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    /// // Get movement speed, default to 1.0 if not set
    /// var speed = Get&lt;MovementSpeed&gt;("speed", new MovementSpeed { Value = 1.0f });
    ///
    /// // Get counter, default to 0
    /// var counter = Get&lt;int&gt;("counter", 0);
    /// </code>
    /// </example>
    protected T Get<T>(string key, T defaultValue = default)
        where T : struct
    {
        if (Context?.TryGetState<T>(out var value) == true)
        {
            return value;
        }

        return defaultValue;
    }

    /// <summary>
    ///     Sets a component value in the entity's state.
    /// </summary>
    /// <typeparam name="T">The component type to set (must be a struct).</typeparam>
    /// <param name="key">
    ///     The key parameter is currently unused (reserved for future key-based state).
    ///     State is stored by component type.
    /// </param>
    /// <param name="value">The component value to set.</param>
    /// <remarks>
    ///     <para>
    ///         <strong>CURRENT LIMITATION:</strong> This method stores state by component type,
    ///         not by string key. The <paramref name="key" /> parameter is accepted for API consistency
    ///         but is currently ignored. Future phases will add true key-based state storage.
    ///     </para>
    ///     <para>
    ///         This method uses <c>World.Set&lt;T&gt;()</c> to update the entity's component.
    ///         Only works for entity scripts (not global scripts).
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    /// // Set movement speed
    /// Set("speed", new MovementSpeed { Value = 2.0f });
    ///
    /// // Increment counter
    /// var counter = Get&lt;int&gt;("counter", 0);
    /// Set("counter", counter + 1);
    /// </code>
    /// </example>
    protected void Set<T>(string key, T value)
        where T : struct
    {
        if (Context?.Entity.HasValue == true)
        {
            Context.World.Set(Context.Entity.Value, value);

            Context.Logger?.LogDebug(
                "Set component {ComponentType} on entity {EntityId}",
                typeof(T).Name,
                Context.Entity.Value.Id
            );
        }
        else
        {
            Context?.Logger?.LogWarning(
                "Cannot set state {Key} on global script context (no entity)",
                key
            );
        }
    }

    // ============================================================================
    // Event Publishing
    // ============================================================================

    /// <summary>
    ///     Publishes a custom event to the event bus.
    ///     All subscribed handlers will receive the event.
    /// </summary>
    /// <typeparam name="TEvent">The event type to publish (must implement <see cref="IGameEvent" />).</typeparam>
    /// <param name="evt">The event instance to publish.</param>
    /// <remarks>
    ///     <para>
    ///         This method allows scripts to publish custom events that other scripts
    ///         or systems can subscribe to. This enables script-to-script communication
    ///         and custom event-driven workflows.
    ///     </para>
    ///     <para>
    ///         The event will be delivered to all handlers subscribed via <see cref="On{TEvent}" />,
    ///         <see cref="OnEntity{TEvent}" />, or <see cref="OnTile{TEvent}" />.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    /// // Define custom event
    /// public sealed record WildEncounterEvent : IGameEvent
    /// {
    ///     public Guid EventId { get; init; } = Guid.NewGuid();
    ///     public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    ///     public required Entity Entity { get; init; }
    ///     public string PokemonSpecies { get; init; }
    ///     public int Level { get; init; }
    /// }
    ///
    /// // Publish custom event
    /// Publish(new WildEncounterEvent
    /// {
    ///     Entity = Context.Entity.Value,
    ///     PokemonSpecies = "Pikachu",
    ///     Level = 5
    /// });
    /// </code>
    /// </example>
    protected void Publish<TEvent>(TEvent evt)
        where TEvent : class, IGameEvent
    {
        if (evt == null)
        {
            throw new ArgumentNullException(nameof(evt));
        }

        Context?.Events?.Publish(evt);

        Context?.Logger?.LogDebug(
            "Published event {EventType} with ID {EventId}",
            typeof(TEvent).Name,
            evt.EventId
        );
    }
}
