using Arch.Core;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Events.Tile;
using MonoBallFramework.Game.GameSystems.Events;
using MonoBallFramework.Game.Scripting.Api;

namespace MonoBallFramework.Game.Scripting.Runtime;

/// <summary>
///     Provides unified context for both entity-level and global scripts.
///     This is the primary interface for scripts to interact with the ECS world.
/// </summary>
/// <remarks>
///     <para>
///         ScriptContext serves as the bridge between scripts and the ECS architecture.
///         It provides type-safe component access, logging, entity management, and API services.
///     </para>
///     <example>
///         Entity script example:
///         <code>
/// // Create context (typically handled by ScriptService)
/// var apis = serviceProvider.GetRequiredService&lt;IScriptingApiProvider&gt;();
/// var ctx = new ScriptContext(world, entity, logger, apis);
///
/// public void Execute(ScriptContext ctx)
/// {
///     if (ctx.TryGetState&lt;Health&gt;(out var health))
///     {
///         ctx.Logger.LogInformation("Entity has {HP} HP", health.Current);
///     }
///
///     // Use API services (accessed via facade)
///     var playerMoney = ctx.Player.GetMoney();
///     ctx.Logger.LogInformation("Player has {Money} money", playerMoney);
/// }
/// </code>
///     </example>
///     <example>
///         Global script example:
///         <code>
/// public void Execute(ScriptContext ctx)
/// {
///     var query = ctx.World.Query(in new QueryDescription().WithAll&lt;Player&gt;());
///     foreach (var entity in query)
///     {
///         // Process all players
///     }
///
///     // Use domain APIs
///     ctx.Player.GiveMoney(100);
/// }
/// </code>
///     </example>
/// </remarks>
public sealed class ScriptContext
{
    private readonly IScriptingApiProvider _apis;
    private readonly Entity? _entity;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScriptContext" /> class.
    /// </summary>
    /// <param name="world">The ECS world instance.</param>
    /// <param name="entity">The target entity for entity-level scripts, or null for global scripts.</param>
    /// <param name="logger">Logger instance for this script's execution.</param>
    /// <param name="apis">The scripting API provider facade (provides access to all domain-specific APIs).</param>
    /// <param name="eventBus">The event bus for subscribing to and publishing game events.</param>
    /// <remarks>
    ///     <para>
    ///         This constructor uses the facade pattern to reduce parameter count.
    ///         The <paramref name="apis" /> provider supplies all domain-specific API services.
    ///     </para>
    ///     <para>
    ///         Typically, you won't construct this directly - ScriptService handles instantiation.
    ///     </para>
    /// </remarks>
    public ScriptContext(
        World world,
        Entity? entity,
        ILogger logger,
        IScriptingApiProvider apis,
        IEventBus eventBus
    )
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _entity = entity;
        _apis = apis ?? throw new ArgumentNullException(nameof(apis));
        Events = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    #region Core Properties

    /// <summary>
    ///     Gets the ECS world instance for direct world queries and operations.
    /// </summary>
    /// <remarks>
    ///     Use this for queries, bulk operations, or when you need direct world access.
    ///     For single-entity component access, prefer the type-safe helpers.
    /// </remarks>
    public World World { get; }

    /// <summary>
    ///     Gets the target entity for this script context, or null if this is a global script.
    /// </summary>
    /// <remarks>
    ///     Always check <see cref="IsEntityScript" /> or <see cref="IsGlobalScript" />
    ///     before accessing this property directly.
    /// </remarks>
    public Entity? Entity => _entity;

    /// <summary>
    ///     Gets the logger instance for this script's execution.
    /// </summary>
    /// <remarks>
    ///     Use this for debugging, error reporting, and tracking script execution.
    ///     Each script context has its own logger scope.
    /// </remarks>
    public ILogger Logger { get; }

    #endregion

    #region API Services

    /// <summary>
    ///     Gets the Player API service for player-related operations.
    /// </summary>
    /// <remarks>
    ///     Use this to interact with the player entity, manage money, position, and movement.
    /// </remarks>
    /// <example>
    /// <code>
    /// var playerMoney = ctx.Player.GetMoney();
    /// ctx.Player.GiveMoney(100);
    /// var facing = ctx.Player.GetPlayerFacing();
    /// </code>
    /// </example>
    public IPlayerApi Player => _apis.Player;

    /// <summary>
    ///     Gets the NPC API service for NPC-related operations.
    /// </summary>
    /// <remarks>
    ///     Use this to control NPCs, move them, face directions, and manage paths.
    /// </remarks>
    /// <example>
    /// <code>
    /// ctx.Npc.FaceEntity(npcEntity, playerEntity);
    /// ctx.Npc.MoveNpc(npcEntity, Direction.North);
    /// </code>
    /// </example>
    public INpcApi Npc => _apis.Npc;

    /// <summary>
    ///     Gets the Map API service for map queries and transitions.
    /// </summary>
    /// <remarks>
    ///     Use this to check walkability, query entities at positions, and transition between maps.
    /// </remarks>
    /// <example>
    /// <code>
    /// var isWalkable = ctx.Map.IsPositionWalkable(mapId, x, y);
    /// var entities = ctx.Map.GetEntitiesAt(mapId, x, y);
    /// ctx.Map.TransitionToMap(2, 10, 10);
    /// </code>
    /// </example>
    public IMapApi Map => _apis.Map;

    /// <summary>
    ///     Gets the Game State API service for managing flags and variables.
    /// </summary>
    /// <remarks>
    ///     Use this to manage game state through flags (booleans) and variables (strings).
    /// </remarks>
    /// <example>
    /// <code>
    /// ctx.GameState.SetFlag("quest_completed", true);
    /// if (ctx.GameState.GetFlag("has_key"))
    /// {
    ///     ctx.GameState.SetVariable("door_state", "unlocked");
    /// }
    /// </code>
    /// </example>
    public IGameStateApi GameState => _apis.GameState;

    /// <summary>
    ///     Gets the Dialogue API service for displaying messages and text.
    /// </summary>
    /// <remarks>
    ///     Use this to show dialogue boxes, messages, and text to the player.
    /// </remarks>
    /// <example>
    /// <code>
    /// ctx.Dialogue.ShowMessage("Hello, traveler!");
    /// ctx.Dialogue.ShowDialogue(npcEntity, "Welcome to my shop.");
    /// </code>
    /// </example>
    public IDialogueApi Dialogue => _apis.Dialogue;

    /// <summary>
    ///     Gets the Entity API service for spawning and managing entities at runtime.
    /// </summary>
    /// <remarks>
    ///     Use this to spawn new entities dynamically, manage entity lifecycle, and work with entity definitions.
    ///     Note: This is different from the <see cref="Entity"/> property which returns the current script's target entity.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Spawn a new NPC entity
    /// var npcEntity = ctx.Entities.Spawn("npc_shopkeeper", mapId, x, y);
    /// </code>
    /// </example>
    public IEntityApi Entities => _apis.Entity;

    /// <summary>
    ///     Gets the Registry API service for querying game definitions and IDs.
    /// </summary>
    /// <remarks>
    ///     Use this to look up definitions by ID, validate IDs, and enumerate available content types.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Check if a behavior exists
    /// if (ctx.Registry.IsValidId("Behaviors", "wander"))
    /// {
    ///     var definition = ctx.Registry.GetDefinition("Behaviors", "wander");
    /// }
    /// </code>
    /// </example>
    public IRegistryApi Registry => _apis.Registry;

    /// <summary>
    ///     Gets the Custom Types API for accessing mod-defined custom content types.
    /// </summary>
    /// <remarks>
    ///     Use this to query custom definitions declared by mods (e.g., weather effects, quests).
    ///     Supports type-safe queries by category and ID.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get a specific weather effect
    /// var rain = ctx.CustomTypes.GetDefinition("WeatherEffects", "rain");
    /// if (rain != null)
    /// {
    ///     float intensity = rain.GetProperty&lt;float&gt;("intensity");
    ///     ctx.Logger.LogInformation("Rain intensity: {Intensity}", intensity);
    /// }
    ///
    /// // Get all weather effects
    /// foreach (var effect in ctx.CustomTypes.GetAllDefinitions("WeatherEffects"))
    /// {
    ///     ctx.Logger.LogInformation("Found weather effect: {Id}", effect.Id);
    /// }
    /// </code>
    /// </example>
    public ICustomTypesApi CustomTypes => _apis.CustomTypes;

    /// <summary>
    ///     Gets the Event Bus for subscribing to and publishing game events.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Use this to subscribe to gameplay events like movement, collisions, and tile interactions.
    ///         Scripts can react to events published by the ECS systems or publish custom events.
    ///     </para>
    ///     <para>
    ///         For convenience, use the helper methods like <see cref="OnMovementStarted" />
    ///         or the generic <see cref="On{TEvent}" /> method for typed event subscriptions.
    ///     </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Subscribe to movement events
    /// ctx.OnMovementStarted(evt =>
    /// {
    ///     ctx.Logger.LogInformation("Entity moving from ({FromX},{FromY}) to ({ToX},{ToY})",
    ///         evt.FromX, evt.FromY, evt.ToX, evt.ToY);
    /// });
    ///
    /// // Subscribe to custom events
    /// ctx.On&lt;MyCustomEvent&gt;(evt => HandleCustomEvent(evt), priority: 1000);
    /// </code>
    /// </example>
    public IEventBus Events { get; }

    #endregion

    #region Context Type Properties

    /// <summary>
    ///     Gets a value indicating whether this context represents an entity-level script.
    /// </summary>
    /// <remarks>
    ///     When true, the <see cref="Entity" /> property contains a valid entity,
    ///     and component access methods like <see cref="GetState{T}" /> can be used.
    /// </remarks>
    public bool IsEntityScript => _entity.HasValue;

    /// <summary>
    ///     Gets a value indicating whether this context represents a global script.
    /// </summary>
    /// <remarks>
    ///     When true, the <see cref="Entity" /> property is null,
    ///     and you must use world queries to access entities.
    ///     Component access methods will throw exceptions.
    /// </remarks>
    public bool IsGlobalScript => !_entity.HasValue;

    #endregion

    #region Type-Safe Component Access

    /// <summary>
    ///     Gets a reference to the specified component on the target entity.
    /// </summary>
    /// <typeparam name="T">The component type to retrieve.</typeparam>
    /// <returns>A reference to the component.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when called on a global script context (no entity).
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the entity doesn't have the specified component.
    /// </exception>
    /// <remarks>
    ///     This method throws on failure. Use <see cref="TryGetState{T}" /> for safe access.
    ///     Returns a reference for zero-allocation component modification.
    /// </remarks>
    /// <example>
    /// <code>
    /// ref var health = ref ctx.GetState&lt;Health&gt;();
    /// health.Current -= 10; // Modifies component directly
    /// </code>
    /// </example>
    public ref T GetState<T>()
        where T : struct
    {
        if (!_entity.HasValue)
        {
            throw new InvalidOperationException(
                $"Cannot get state of type '{typeof(T).Name}' for global script. "
                    + "Use TryGetState instead, or check IsEntityScript before calling."
            );
        }

        if (!World.Has<T>(_entity.Value))
        {
            throw new InvalidOperationException(
                $"Entity {_entity.Value.Id} does not have component '{typeof(T).Name}'. "
                    + "Use HasState or TryGetState to check existence first."
            );
        }

        return ref World.Get<T>(_entity.Value);
    }

    /// <summary>
    ///     Attempts to get the specified component from the target entity.
    /// </summary>
    /// <typeparam name="T">The component type to retrieve.</typeparam>
    /// <param name="state">When this method returns, contains the component if found; otherwise, the default value.</param>
    /// <returns>true if the component exists; otherwise, false.</returns>
    /// <remarks>
    ///     This is the safe way to access components. Always prefer this over <see cref="GetState{T}" />
    ///     unless you're certain the component exists and you're in an entity script.
    ///     Returns false for global scripts or missing components without throwing.
    /// </remarks>
    /// <example>
    /// <code>
    /// if (ctx.TryGetState&lt;Health&gt;(out var health))
    /// {
    ///     ctx.Logger.LogInformation("HP: {Current}/{Max}", health.Current, health.Max);
    /// }
    /// </code>
    /// </example>
    public bool TryGetState<T>(out T state)
        where T : struct
    {
        state = default;

        if (!_entity.HasValue)
        {
            return false;
        }

        if (!World.Has<T>(_entity.Value))
        {
            return false;
        }

        state = World.Get<T>(_entity.Value);
        return true;
    }

    /// <summary>
    ///     Gets the specified component if it exists, or adds it with default values if it doesn't.
    /// </summary>
    /// <typeparam name="T">The component type to retrieve or add.</typeparam>
    /// <returns>A reference to the component (existing or newly added).</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when called on a global script context (no entity).
    /// </exception>
    /// <remarks>
    ///     This is useful for lazy initialization of optional components.
    ///     The component is added with default struct values if it doesn't exist.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Ensure entity has a timer, create if needed
    /// ref var timer = ref ctx.GetOrAddState&lt;ScriptTimer&gt;();
    /// timer.ElapsedSeconds += deltaTime;
    /// </code>
    /// </example>
    public ref T GetOrAddState<T>()
        where T : struct
    {
        if (!_entity.HasValue)
        {
            throw new InvalidOperationException(
                $"Cannot get or add state of type '{typeof(T).Name}' for global script. "
                    + "Use TryGetState or check IsEntityScript before calling."
            );
        }

        Entity entity = _entity.Value;

        if (!World.Has<T>(entity))
        {
            World.Add(entity, default(T));
            Logger.LogDebug(
                "Added component {ComponentType} to entity {EntityId}",
                typeof(T).Name,
                entity.Id
            );
        }

        return ref World.Get<T>(entity);
    }

    /// <summary>
    ///     Checks whether the target entity has the specified component.
    /// </summary>
    /// <typeparam name="T">The component type to check.</typeparam>
    /// <returns>true if the entity has the component; false if it doesn't or this is a global script.</returns>
    /// <remarks>
    ///     Safe to call on both entity and global scripts. Returns false for global scripts.
    ///     Use this before calling <see cref="GetState{T}" /> if you're unsure whether the component exists.
    /// </remarks>
    /// <example>
    /// <code>
    /// if (ctx.HasState&lt;Inventory&gt;())
    /// {
    ///     ref var inventory = ref ctx.GetState&lt;Inventory&gt;();
    ///     // Work with inventory
    /// }
    /// </code>
    /// </example>
    public bool HasState<T>()
        where T : struct
    {
        return _entity.HasValue && World.Has<T>(_entity.Value);
    }

    /// <summary>
    ///     Removes the specified component from the target entity.
    /// </summary>
    /// <typeparam name="T">The component type to remove.</typeparam>
    /// <returns>true if the component was removed; false if it didn't exist or this is a global script.</returns>
    /// <remarks>
    ///     Safe to call even if the component doesn't exist. Returns false for global scripts.
    ///     Use this to clean up temporary or conditional components.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Remove temporary status effect
    /// if (ctx.RemoveState&lt;PoisonEffect&gt;())
    /// {
    ///     ctx.Logger.LogInformation("Poison effect removed");
    /// }
    /// </code>
    /// </example>
    public bool RemoveState<T>()
        where T : struct
    {
        if (!_entity.HasValue || !World.Has<T>(_entity.Value))
        {
            return false;
        }

        World.Remove<T>(_entity.Value);
        Logger.LogDebug(
            "Removed component {ComponentType} from entity {EntityId}",
            typeof(T).Name,
            _entity.Value.Id
        );
        return true;
    }

    #endregion

    #region Convenience Properties

    /// <summary>
    ///     Gets a reference to the entity's Position component.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when called on a global script or when the entity doesn't have a Position component.
    /// </exception>
    /// <remarks>
    ///     This is a convenience shortcut for <c>GetState&lt;Position&gt;()</c>.
    ///     Use <see cref="HasPosition" /> to check existence first if you're unsure.
    /// </remarks>
    /// <example>
    /// <code>
    /// if (ctx.HasPosition)
    /// {
    ///     ref var pos = ref ctx.Position;
    ///     pos.X += 10;
    /// }
    /// </code>
    /// </example>
    public ref Position Position => ref GetState<Position>();

    /// <summary>
    ///     Gets a value indicating whether the target entity has a Position component.
    /// </summary>
    /// <remarks>
    ///     This is a convenience shortcut for <c>HasState&lt;Position&gt;()</c>.
    ///     Returns false for global scripts or entities without positions.
    /// </remarks>
    public bool HasPosition => HasState<Position>();

    #endregion

    #region Event Subscription Helpers

    /// <summary>
    ///     Subscribes to a game event with optional priority.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to subscribe to (must implement IGameEvent).</typeparam>
    /// <param name="handler">The handler to invoke when the event is published.</param>
    /// <param name="priority">
    ///     The priority of this handler (higher numbers execute first).
    ///     Default is 500. Use higher values for critical handlers, lower for logging/analytics.
    /// </param>
    /// <returns>A disposable subscription that can be used to unsubscribe.</returns>
    /// <remarks>
    ///     <para>
    ///         This is the primary method for subscribing to events from scripts.
    ///         The subscription will remain active until disposed or the script context is destroyed.
    ///     </para>
    ///     <para>
    ///         Priority determines handler execution order. Higher priority handlers run first:
    ///         - 1000+: Validation and anti-cheat systems
    ///         - 500: Normal game logic (default)
    ///         - 0: Post-processing and effects
    ///         - -1000: Logging and analytics
    ///     </para>
    ///     <para>
    ///         NOTE: Priority parameter is currently accepted but not fully implemented in EventBus.
    ///         All handlers execute in registration order until EventBus is upgraded to support priority-based dispatch.
    ///     </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Subscribe to any event type with custom priority
    /// var subscription = ctx.On&lt;MovementStartedEvent&gt;(evt =>
    /// {
    ///     if (evt.ToX == 10 &amp;&amp; evt.ToY == 10)
    ///     {
    ///         ctx.Logger.LogInformation("Player reached special tile!");
    ///     }
    /// }, priority: 1000);
    ///
    /// // Later: unsubscribe when no longer needed
    /// subscription.Dispose();
    /// </code>
    /// </example>
    public IDisposable On<TEvent>(Action<TEvent> handler, int priority = 500)
        where TEvent : class
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        // NOTE: Priority is accepted but not yet used by EventBus.Subscribe()
        // This maintains API compatibility for when EventBus is upgraded to support priority.
        // For now, all handlers execute in registration order.
        Logger.LogDebug(
            "Subscribing to {EventType} with priority {Priority} (priority not yet implemented)",
            typeof(TEvent).Name,
            priority
        );

        return Events.Subscribe(handler);
    }

    /// <summary>
    ///     Subscribes to MovementStartedEvent with default priority.
    /// </summary>
    /// <param name="handler">The handler to invoke when movement starts.</param>
    /// <returns>A disposable subscription.</returns>
    /// <remarks>
    ///     Convenience method for subscribing to movement start events.
    ///     Equivalent to calling <c>On&lt;MovementStartedEvent&gt;(handler, priority: 500)</c>.
    /// </remarks>
    /// <example>
    /// <code>
    /// ctx.OnMovementStarted(evt =>
    /// {
    ///     ctx.Logger.LogInformation("Entity {EntityId} moving to ({ToX},{ToY})",
    ///         evt.Entity.Id, evt.ToX, evt.ToY);
    ///
    ///     // Can cancel movement by calling evt.PreventDefault()
    ///     if (IsBlockedByScript(evt.ToX, evt.ToY))
    ///     {
    ///         evt.PreventDefault("Script blocked movement");
    ///     }
    /// });
    /// </code>
    /// </example>
    public IDisposable OnMovementStarted(Action<MovementStartedEvent> handler)
    {
        return On(handler, 500);
    }

    /// <summary>
    ///     Subscribes to MovementCompletedEvent with default priority.
    /// </summary>
    /// <param name="handler">The handler to invoke when movement completes.</param>
    /// <returns>A disposable subscription.</returns>
    /// <remarks>
    ///     Convenience method for subscribing to movement completion events.
    ///     Equivalent to calling <c>On&lt;MovementCompletedEvent&gt;(handler, priority: 500)</c>.
    /// </remarks>
    /// <example>
    /// <code>
    /// ctx.OnMovementCompleted(evt =>
    /// {
    ///     ctx.Logger.LogInformation("Entity {EntityId} reached ({CurrentX},{CurrentY})",
    ///         evt.Entity.Id, evt.CurrentX, evt.CurrentY);
    ///
    ///     // Trigger follow-up actions after movement
    ///     CheckForRandomEncounter(evt.CurrentX, evt.CurrentY);
    /// });
    /// </code>
    /// </example>
    public IDisposable OnMovementCompleted(Action<MovementCompletedEvent> handler)
    {
        return On(handler, 500);
    }

    /// <summary>
    ///     Subscribes to CollisionDetectedEvent with default priority.
    /// </summary>
    /// <param name="handler">The handler to invoke when a collision occurs.</param>
    /// <returns>A disposable subscription.</returns>
    /// <remarks>
    ///     Convenience method for subscribing to collision events.
    ///     Equivalent to calling <c>On&lt;CollisionDetectedEvent&gt;(handler, priority: 500)</c>.
    /// </remarks>
    /// <example>
    /// <code>
    /// ctx.OnCollisionDetected(evt =>
    /// {
    ///     ctx.Logger.LogInformation("Collision: Entity {A} hit Entity {B} at ({X},{Y})",
    ///         evt.EntityA.Id, evt.EntityB.Id, evt.ContactX, evt.ContactY);
    ///
    ///     // Handle collision based on type
    ///     if (evt.CollisionType == CollisionType.PlayerNPC)
    ///     {
    ///         StartNPCInteraction(evt.EntityB);
    ///     }
    /// });
    /// </code>
    /// </example>
    public IDisposable OnCollisionDetected(Action<CollisionDetectedEvent> handler)
    {
        return On(handler, 500);
    }

    /// <summary>
    ///     Subscribes to TileSteppedOnEvent with default priority.
    /// </summary>
    /// <param name="handler">The handler to invoke when an entity steps on a tile.</param>
    /// <returns>A disposable subscription.</returns>
    /// <remarks>
    ///     Convenience method for subscribing to tile step events.
    ///     Equivalent to calling <c>On&lt;TileSteppedOnEvent&gt;(handler, priority: 500)</c>.
    /// </remarks>
    /// <example>
    /// <code>
    /// ctx.OnTileSteppedOn(evt =>
    /// {
    ///     ctx.Logger.LogInformation("Entity {EntityId} stepped on {TileType} at ({X},{Y})",
    ///         evt.Entity.Id, evt.TileType, evt.TileX, evt.TileY);
    ///
    ///     // Can cancel tile entry by calling evt.PreventDefault()
    ///     if (evt.TileType == "lava" &amp;&amp; !HasFireResistance(evt.Entity))
    ///     {
    ///         evt.PreventDefault("Cannot walk on lava without protection");
    ///     }
    /// });
    /// </code>
    /// </example>
    public IDisposable OnTileSteppedOn(Action<TileSteppedOnEvent> handler)
    {
        return On(handler, 500);
    }

    #endregion
}
