using Arch.Core;

namespace PokeSharp.Engine.Core.Events;

/// <summary>
///     Marker interface for events that are associated with a specific entity.
///     Enables entity-filtered event subscriptions via ScriptBase.OnEntity&lt;TEvent&gt;().
/// </summary>
/// <remarks>
///     <para>
///         Events that implement this interface can be filtered by entity ID,
///         allowing scripts to subscribe only to events for specific entities.
///     </para>
///     <para>
///         Example: Subscribe only to movement events for the player entity,
///         ignoring movement events from NPCs and other entities.
///     </para>
///     <para>
///         <strong>NOTE:</strong> This is a marker interface introduced in Phase 3.1.
///         Existing events (MovementStartedEvent, etc.) have Entity properties but
///         don't implement this interface yet. Future phases will retrofit these interfaces.
///     </para>
/// </remarks>
/// <example>
///     <code>
/// // Event implementation
/// public sealed record CustomEntityEvent : IEntityEvent
/// {
///     public Guid EventId { get; init; } = Guid.NewGuid();
///     public DateTime Timestamp { get; init; } = DateTime.UtcNow;
///     public required Entity Entity { get; init; }
///     public string CustomData { get; init; }
/// }
///
/// // Usage in script
/// public class MyScript : ScriptBase
/// {
///     public override void RegisterEventHandlers(ScriptContext ctx)
///     {
///         var playerEntity = ctx.Player.GetPlayerEntity();
///
///         // Only receive events for player entity
///         OnEntity&lt;CustomEntityEvent&gt;(playerEntity, evt =>
///         {
///             ctx.Logger.LogInformation("Player event: {Data}", evt.CustomData);
///         });
///     }
/// }
/// </code>
/// </example>
public interface IEntityEvent : IGameEvent
{
    /// <summary>
    ///     Gets the entity associated with this event.
    ///     Used for entity-filtered event subscriptions.
    /// </summary>
    Entity Entity { get; }
}
