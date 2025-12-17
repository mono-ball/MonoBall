using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Core.Events.Tile;
using MonoBallFramework.Game.GameSystems.Events;

namespace MonoBallFramework.Game.Scripting.Runtime;

/// <summary>
///     Base class for type behavior scripts using the ScriptContext pattern.
/// </summary>
/// <remarks>
///     <para>
///         All .csx behavior scripts should define a class that inherits from TypeScriptBase.
///         The class will be instantiated once and reused across all ticks.
///     </para>
///     <para>
///         <strong>IMPORTANT:</strong> Scripts are stateless! DO NOT use instance fields or properties.
///         Use <c>ctx.GetState&lt;T&gt;()</c> and <c>ctx.SetState&lt;T&gt;()</c> for persistent data.
///     </para>
///     <example>
///         <code>
/// public class MyScript : TypeScriptBase
/// {
///     // ❌ WRONG - instance state will break with multiple entities
///     private int counter;
/// 
///     // ✅ CORRECT - use ScriptContext for state
///     protected override void OnTick(ScriptContext ctx, float deltaTime)
///     {
///         var counter = ctx.GetState&lt;int&gt;("counter");
///         counter++;
///         ctx.SetState("counter", counter);
/// 
///         // Access ECS world, entity, logger via context
///         var position = ctx.World.Get&lt;Position&gt;(ctx.Entity);
///         ctx.Logger?.LogInformation("Position: {Pos}", position);
///     }
/// }
/// </code>
///     </example>
/// </remarks>
public abstract class TypeScriptBase
{
    // NO INSTANCE FIELDS OR PROPERTIES!
    // Scripts must be stateless - use ScriptContext.GetState<T>() for persistent data.

    // Track event subscriptions for automatic cleanup
    private readonly List<IDisposable> _eventSubscriptions = new();

    // ============================================================================
    // Lifecycle Hooks
    // ============================================================================

    /// <summary>
    ///     Called once when script is loaded.
    ///     Override to set up initial state or cache data.
    /// </summary>
    /// <param name="ctx">Script execution context providing access to World, Entity, Logger, and state.</param>
    /// <remarks>
    ///     Use <c>ctx.SetState&lt;T&gt;(key, value)</c> to initialize persistent data.
    /// </remarks>
    public virtual void OnInitialize(ScriptContext ctx) { }

    /// <summary>
    ///     Called after OnInitialize to register event handlers.
    ///     Override to subscribe to game events.
    /// </summary>
    /// <param name="ctx">Script execution context providing access to World, Entity, Logger, and events.</param>
    /// <remarks>
    ///     Use helper methods like On&lt;TEvent&gt;() to subscribe to events.
    ///     Event subscriptions are automatically tracked and cleaned up on script unload.
    /// </remarks>
    public virtual void RegisterEventHandlers(ScriptContext ctx) { }

    /// <summary>
    ///     Called when the type is activated on an entity or globally.
    ///     Override to handle activation logic.
    /// </summary>
    /// <param name="ctx">Script execution context providing access to World, Entity, Logger, and state.</param>
    public virtual void OnActivated(ScriptContext ctx) { }

    /// <summary>
    ///     Called every frame while the type is active.
    ///     Override to implement per-frame behavior logic.
    /// </summary>
    /// <param name="ctx">Script execution context providing access to World, Entity, Logger, and state.</param>
    /// <param name="deltaTime">Time elapsed since last frame (in seconds).</param>
    public virtual void OnTick(ScriptContext ctx, float deltaTime) { }

    /// <summary>
    ///     Called when the type is deactivated from an entity or globally.
    ///     Override to handle cleanup logic.
    /// </summary>
    /// <param name="ctx">Script execution context providing access to World, Entity, Logger, and state.</param>
    public virtual void OnDeactivated(ScriptContext ctx) { }

    /// <summary>
    ///     Called when script is being unloaded or reloaded.
    ///     Automatically disposes all tracked event subscriptions.
    /// </summary>
    public virtual void OnUnload()
    {
        // Dispose all event subscriptions
        foreach (var subscription in _eventSubscriptions)
        {
            subscription?.Dispose();
        }

        _eventSubscriptions.Clear();
    }

    // ============================================================================
    // Event Subscription Helpers
    // ============================================================================

    /// <summary>
    ///     Track an event subscription for automatic cleanup.
    ///     Used internally by event helper methods.
    /// </summary>
    /// <param name="subscription">The subscription to track.</param>
    protected void TrackSubscription(IDisposable subscription)
    {
        if (subscription != null)
        {
            _eventSubscriptions.Add(subscription);
        }
    }

    /// <summary>
    ///     Subscribe to a game event with automatic cleanup tracking.
    ///     The subscription will be automatically disposed when the script is unloaded.
    /// </summary>
    /// <typeparam name="TEvent">The event type to subscribe to (must implement IGameEvent or be a class).</typeparam>
    /// <param name="ctx">Script execution context providing access to the event bus.</param>
    /// <param name="handler">The handler to invoke when the event is published.</param>
    /// <remarks>
    ///     Event subscriptions are automatically tracked and cleaned up on OnUnload().
    /// </remarks>
    /// <example>
    ///     <code>
    /// public override void RegisterEventHandlers(ScriptContext ctx)
    /// {
    ///     On&lt;MovementStartedEvent&gt;(ctx, evt =&gt;
    ///     {
    ///         ctx.Logger.LogInformation("Movement started to {Target}", evt.TargetPosition);
    ///     });
    /// }
    /// </code>
    /// </example>
    protected void On<TEvent>(ScriptContext ctx, Action<TEvent> handler)
        where TEvent : class
    {
        if (ctx?.Events == null)
        {
            ctx?.Logger?.LogWarning(
                "Cannot subscribe to {EventType}: Events system not available in ScriptContext",
                typeof(TEvent).Name
            );
            return;
        }

        IDisposable subscription = ctx.Events.Subscribe(handler);
        TrackSubscription(subscription);
    }

    /// <summary>
    ///     Subscribe to MovementStartedEvent with automatic cleanup.
    /// </summary>
    /// <param name="ctx">Script execution context.</param>
    /// <param name="handler">Handler to invoke when movement starts.</param>
    /// <remarks>
    ///     Convenience method for subscribing to movement start events.
    ///     Requires Phase 2.1 (ctx.Events) to be implemented.
    /// </remarks>
    protected void OnMovementStarted(ScriptContext ctx, Action<MovementStartedEvent> handler)
    {
        On(ctx, handler);
    }

    /// <summary>
    ///     Subscribe to MovementCompletedEvent with automatic cleanup.
    /// </summary>
    /// <param name="ctx">Script execution context.</param>
    /// <param name="handler">Handler to invoke when movement completes.</param>
    /// <remarks>
    ///     Convenience method for subscribing to movement completion events.
    ///     Requires Phase 2.1 (ctx.Events) to be implemented.
    /// </remarks>
    protected void OnMovementCompleted(ScriptContext ctx, Action<MovementCompletedEvent> handler)
    {
        On(ctx, handler);
    }

    /// <summary>
    ///     Subscribe to CollisionDetectedEvent with automatic cleanup.
    /// </summary>
    /// <param name="ctx">Script execution context.</param>
    /// <param name="handler">Handler to invoke when collision is detected.</param>
    /// <remarks>
    ///     Convenience method for subscribing to collision events.
    ///     Requires Phase 2.1 (ctx.Events) to be implemented.
    /// </remarks>
    protected void OnCollisionDetected(ScriptContext ctx, Action<CollisionDetectedEvent> handler)
    {
        On(ctx, handler);
    }

    /// <summary>
    ///     Subscribe to TileSteppedOnEvent with automatic cleanup.
    /// </summary>
    /// <param name="ctx">Script execution context.</param>
    /// <param name="handler">Handler to invoke when tile is stepped on.</param>
    /// <remarks>
    ///     Convenience method for subscribing to tile step events.
    ///     Requires Phase 2.1 (ctx.Events) to be implemented.
    /// </remarks>
    protected void OnTileSteppedOn(ScriptContext ctx, Action<TileSteppedOnEvent> handler)
    {
        On(ctx, handler);
    }
}
